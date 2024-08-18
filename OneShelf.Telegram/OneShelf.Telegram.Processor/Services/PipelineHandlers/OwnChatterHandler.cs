using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Common.OpenAi.Models;
using OneShelf.Common.OpenAi.Models.Memory;
using OneShelf.Common.OpenAi.Services;
using OneShelf.Telegram.PipelineHandlers;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Services.Base;
using System.Text;
using OneShelf.Telegram.Ai.PipelineHandlers;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;
using DateTime = System.DateTime;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace OneShelf.Telegram.Processor.Services.PipelineHandlers;

public class OwnChatterHandler : ChatterHandlerBase
{
    private readonly DialogRunner _dialogRunner;
    private readonly SongsDatabase _songsDatabase;
    private readonly TelegramOptions _telegramOptions;

    public OwnChatterHandler(
        ILogger<OwnChatterHandler> logger,
        IOptions<TelegramOptions> telegramOptions,
        SongsDatabase songsDatabase,
        DialogRunner dialogRunner, 
        IScopedAbstractions scopedAbstractions)
        : base(scopedAbstractions, logger)
    {
        _songsDatabase = songsDatabase;
        _dialogRunner = dialogRunner;
        _telegramOptions = telegramOptions.Value;
    }
    
    private bool CheckTopicId(Update update, int topicId)
    {
        if (update.Message?.Chat.Username != _telegramOptions.PublicChatId.Substring(1)) return false;
        if (update.Message.MessageThreadId != topicId) return false;
        if (update.Message.From == null) return false;

        return true;
    }

    private async Task Log(Update update, InteractionType interactionType)
    {
        _songsDatabase.Interactions.Add(new()
        {
            CreatedOn = DateTime.Now,
            InteractionType = interactionType,
            UserId = update.Message!.From!.Id,
            ShortInfoSerialized = update.Message.Text,
            Serialized = JsonSerializer.Serialize(update)
        });
        await _songsDatabase.SaveChangesAsyncX();
    }

    private async Task CheckNoUpdates(CancellationTokenSource cancellationTokenSource, CancellationToken cancellationToken, int lastUpdateId)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var last = await _songsDatabase.Interactions.Where(x => x.InteractionType == InteractionType.OwnChatterMessage).OrderBy(x => x.Id).LastAsync(cancellationToken);
                if (last.Id != lastUpdateId)
                {
                    await cancellationTokenSource.CancelAsync();
                    return;
                }

                await Task.Delay(500, cancellationToken);
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error checking for updates.");
        }
    }

    protected override async Task<bool> HandleSync(Update update)
    {
        if (!CheckTopicId(update, _telegramOptions.OwnChatterTopicId)) return false;

        await Log(update, InteractionType.OwnChatterMessage);

        if (update.Message?.Text?.Length > 2)
        {
            Queued(Respond(update));
            return true;
        }

        return false;
    }

    private async Task Respond(Update update)
    {
        var now = DateTime.Now;
        var since = now.AddDays(-1);

        var interactions = await _songsDatabase.Interactions
            .Where(x => x.InteractionType == InteractionType.OwnChatterMessage ||
                        x.InteractionType == InteractionType.OwnChatterMemoryPoint ||
                        x.InteractionType == InteractionType.OwnChatterResetDialog)
            .Where(x => x.ShortInfoSerialized!.Length > 0 || x.InteractionType == InteractionType.OwnChatterResetDialog)
            .Where(x => x.CreatedOn > since)
            .OrderByDescending(x => x.CreatedOn)
            .Take(20)
            .ToListAsync();
        
        var imagesUnavailableUntil = await GetImagesUnavailableUntil(now);

        interactions = interactions.AsEnumerable().Reverse().ToList();

        var reset = interactions.FindLastIndex(x => x.InteractionType == InteractionType.OwnChatterResetDialog);
        if (reset > -1)
        {
            interactions = interactions.Skip(reset + 1).ToList();
        }

        var (system, version, frequencyPenalty, presencePenalty, imagesVersion) = await GetAiParameters();

        using var callingApis = new CancellationTokenSource();
        using var checkingIsStillLast = new CancellationTokenSource();
        LongTyping(update, callingApis.Token);
        var checking = CheckNoUpdates(checkingIsStillLast, callingApis.Token, interactions.Last(x => x.InteractionType == InteractionType.OwnChatterMessage).Id);

        ChatBotMemoryPointWithTraces newMessagePoint;
        DialogResult result;

        try
        {
            // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
            var existingMemory = interactions.Select(i => i.InteractionType switch
            {
                InteractionType.OwnChatterMessage => (MemoryPoint)new UserMessageMemoryPoint(i.ShortInfoSerialized!),
                InteractionType.OwnChatterMemoryPoint => JsonSerializer.Deserialize<ChatBotMemoryPoint>(i.Serialized)!,
                _ => throw new ArgumentOutOfRangeException(nameof(i))
            }).ToList();

            if (checkingIsStillLast.IsCancellationRequested)
            {
                return;
            }

            var (additionalBillingInfo, dogContextDomainId) = GetDialogConfigurationParameters();
            (result, newMessagePoint) = await _dialogRunner.Execute(existingMemory, new()
            {
                Version = version ?? throw new("The version is required."),
                SystemMessage = system ?? throw new("The system message is required."),
                FrequencyPenalty = frequencyPenalty,
                PresencePenalty = presencePenalty,
                ImagesVersion = imagesVersion,
                UserId = update.Message?.From?.Id,
                UseCase = "own chatter",
                AdditionalBillingInfo = additionalBillingInfo,
                DomainId = dogContextDomainId,
            }, checkingIsStillLast.Token, imagesUnavailableUntil);

            if (checkingIsStillLast.IsCancellationRequested)
            {
                return;
            }
        }
        catch (TaskCanceledException)
        {
            return;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error requesting the data.");
            await SendMessage(update, "Случилась ошибка. :(", true);
            return;
        }
        finally
        {
            await callingApis.CancelAsync();
        }

        await checking;
        
        if (checkingIsStillLast.IsCancellationRequested)
        {
            return;
        }

        await SavePoint(now, newMessagePoint, result, imagesUnavailableUntil);
        
        var text = result.Text;
        if (result.IsTopicChangeDetected)
        {
            text = $"⟳ {text}";
        }

        if (result.Images.Any() && !imagesUnavailableUntil.HasValue)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    await SendMessage(update, text, result.Images, false);
                }
                else
                {
                    await SendMessage(update, result.Images, false);
                }

                return;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error sending the image.");
            }
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            await SendMessage(update, text, false);
        }
    }

    private async Task SavePoint(DateTime now, ChatBotMemoryPointWithTraces newMessagePoint, DialogResult result,
        DateTime? imagesUnavailableUntil)
    {
        _songsDatabase.Interactions.Add(new()
        {
            CreatedOn = now,
            InteractionType = InteractionType.OwnChatterMemoryPoint,
            Serialized = JsonSerializer.Serialize(newMessagePoint),
            ShortInfoSerialized = JsonSerializer.Serialize(result),
            UserId = _telegramOptions.BotId,
        });

        await _songsDatabase.SaveChangesAsyncX(cancellationToken: default);
    }

    private (string? additionalBillingInfo, int? domainId) GetDialogConfigurationParameters() => default;

    private async Task<DateTime?> GetImagesUnavailableUntil(DateTime now) => null;

    private async Task<(string? system, string? version, float? frequencyPenalty, float? presencePenalty, int? imagesVersion)> GetAiParameters()
    {
        var parameters = await _songsDatabase.Interactions
            .Where(x => x.InteractionType == InteractionType.OwnChatterSystemMessage
                        || x.InteractionType == InteractionType.OwnChatterVersion
                        || x.InteractionType == InteractionType.OwnChatterImagesVersion
                        || x.InteractionType == InteractionType.OwnChatterFrequencyPenalty
                        || x.InteractionType == InteractionType.OwnChatterPresencePenalty)
            .GroupBy(x => x.InteractionType)
            .Select(x => x.OrderByDescending(x => x.CreatedOn).FirstOrDefault())
            .ToListAsync();

        var system = parameters.SingleOrDefault(x => x.InteractionType == InteractionType.OwnChatterSystemMessage)?.Serialized;
        var version = parameters.SingleOrDefault(x => x.InteractionType == InteractionType.OwnChatterVersion)?.Serialized;
        var frequencyPenalty = parameters.SingleOrDefault(x => x.InteractionType == InteractionType.OwnChatterFrequencyPenalty)?.Serialized?.SelectSingle(x => float.TryParse(x, out var value) ? (float?)value : null);
        var presencePenalty = parameters.SingleOrDefault(x => x.InteractionType == InteractionType.OwnChatterPresencePenalty)?.Serialized?.SelectSingle(x => float.TryParse(x, out var value) ? (float?)value : null);
        var imagesVersion = parameters.SingleOrDefault(x => x.InteractionType == InteractionType.OwnChatterImagesVersion)?.Serialized?.SelectSingle(x => int.TryParse(x, out var value) ? (int?)value : null);
        return (system, version, frequencyPenalty, presencePenalty, imagesVersion);
    }
}