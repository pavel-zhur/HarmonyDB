using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common.OpenAi.Models;
using OneShelf.Common.OpenAi.Models.Memory;
using OneShelf.Common.OpenAi.Services;
using OneShelf.OneDog.Database;
using OneShelf.OneDog.Database.Model.Enums;
using OneShelf.Telegram.Options;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI.GettingUpdates;
using DateTime = System.DateTime;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace OneShelf.OneDog.Processor.Services.PipelineHandlers;

public class OwnChatterHandler : ChatterHandler
{
    private readonly TelegramOptions _telegramOptions;
    private readonly DialogRunner _dialogRunner;
    private readonly DogContext _dogContext;

    public OwnChatterHandler(
        ILogger<OwnChatterHandler> logger,
        IOptions<TelegramOptions> telegramOptions,
        DogDatabase dogDatabase,
        DialogRunner dialogRunner, 
        IScopedAbstractions scopedAbstractions,
        DogContext dogContext)
        : base(dogDatabase, scopedAbstractions, logger)
    {
        _telegramOptions = telegramOptions.Value;
        _dialogRunner = dialogRunner;
        _dogContext = dogContext;
    }

    protected override async Task<bool> HandleSync(Update update)
    {
        if (!CheckTopicId(update, _dogContext.Domain.TopicId, _dogContext.Domain.ChatId)) return false;

        await Log(update, _dogContext.DomainId, InteractionType.OwnChatterMessage);

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

        var interactions = await DogDatabase.Interactions
            .Where(x => x.InteractionType == InteractionType.OwnChatterMessage ||
                        x.InteractionType == InteractionType.OwnChatterMemoryPoint ||
                        x.InteractionType == InteractionType.OwnChatterResetDialog)
            .Where(x => x.ShortInfoSerialized!.Length > 0 || x.InteractionType == InteractionType.OwnChatterResetDialog)
            .Where(x => x.CreatedOn > since)
            .OrderByDescending(x => x.CreatedOn)
            .Take(20)
            .ToListAsync();

        DateTime? imagesUnavailableUntil = null;
        if (_dogContext.Domain.ImagesLimit != null)
        {
            var imagesSince = now.Add(-_dogContext.Domain.ImagesLimit.Window);
            var images = (await DogDatabase.Interactions
                .Where(x => x.InteractionType == InteractionType.ImagesSuccess)
                .Where(x => x.CreatedOn >= imagesSince)
                .ToListAsync())
                .Select(x => (x.CreatedOn, count: int.Parse(x.Serialized)))
                .ToList();

            if (images.Sum(x => x.count) >= _dogContext.Domain.ImagesLimit.Limit)
            {
                imagesUnavailableUntil = images.Min(x => x.CreatedOn).Add(_dogContext.Domain.ImagesLimit.Window);
            }
        }

        interactions = interactions.AsEnumerable().Reverse().ToList();

        var reset = interactions.FindLastIndex(x => x.InteractionType == InteractionType.OwnChatterResetDialog);
        if (reset > -1)
        {
            interactions = interactions.Skip(reset + 1).ToList();
        }

        var system = _dogContext.Domain.SystemMessage;
        var version = _dogContext.Domain.GptVersion;
        var frequencyPenalty = _dogContext.Domain.FrequencyPenalty;
        var presencePenalty = _dogContext.Domain.PresencePenalty;
        var imagesVersion = _dogContext.Domain.DalleVersion;

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

            (result, newMessagePoint) = await _dialogRunner.Execute(existingMemory, new()
            {
                Version = version ?? throw new("The version is required."),
                SystemMessage = system ?? throw new("The system message is required."),
                FrequencyPenalty = frequencyPenalty,
                PresencePenalty = presencePenalty,
                ImagesVersion = imagesVersion,
                UserId = update.Message?.From?.Id,
                UseCase = "own chatter",
                AdditionalBillingInfo = "one dog",
                DomainId = _dogContext.DomainId,
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
            Logger.LogError(e, "Error requesting the data.");
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

        DogDatabase.Interactions.Add(new()
        {
            CreatedOn = now,
            InteractionType = InteractionType.OwnChatterMemoryPoint,
            Serialized = JsonSerializer.Serialize(newMessagePoint),
            ShortInfoSerialized = JsonSerializer.Serialize(result),
            UserId = _telegramOptions.AdminId,
            DomainId = _dogContext.DomainId,
        });

        if (result.Images.Any())
        {
            DogDatabase.Interactions.Add(new()
            {
                CreatedOn = now,
                InteractionType = imagesUnavailableUntil.HasValue ? InteractionType.ImagesLimit : InteractionType.ImagesSuccess,
                DomainId = _dogContext.DomainId,
                Serialized = result.Images.Count.ToString(),
                UserId = _telegramOptions.AdminId,
            });
        }
        
        await DogDatabase.SaveChangesAsync(cancellationToken: default);

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
                Logger.LogError(e, "Error sending the image.");
            }
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            await SendMessage(update, text, false);
        }
    }
}