using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using OneShelf.Common.OpenAi.Models;
using OneShelf.Common.OpenAi.Models.Memory;
using OneShelf.Common.OpenAi.Services;
using OneShelf.Telegram.Ai.Model;
using OneShelf.Telegram.Options;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.Telegram.Ai.PipelineHandlers;

public abstract class AiDialogHandlerBase<TInteractionType> : PipelineHandler
{
    private TelegramBotClient? _api;
    
    protected readonly ILogger<AiDialogHandlerBase<TInteractionType>> _logger;
    protected readonly IInteractionsRepository<TInteractionType> _repository;
    protected readonly DialogRunner _dialogRunner;

    protected AiDialogHandlerBase(IScopedAbstractions scopedAbstractions, ILogger<AiDialogHandlerBase<TInteractionType>> logger, IInteractionsRepository<TInteractionType> repository, DialogRunner dialogRunner) 
        : base(scopedAbstractions)
    {
        _logger = logger;
        _repository = repository;
        _dialogRunner = dialogRunner;
    }

    protected async Task Log(Update update, TInteractionType interactionType)
    {
        var interaction = CreateInteraction(update);
        interaction.CreatedOn = DateTime.Now;
        interaction.InteractionType = interactionType;
        interaction.UserId = update.Message!.From!.Id;
        interaction.ShortInfoSerialized = update.Message.Text;
        interaction.Serialized = JsonSerializer.Serialize(update);
        await _repository.Add(interaction.Once().ToList());
    }

    protected abstract IInteraction<TInteractionType> CreateInteraction(Update update);

    protected async Task CheckNoUpdates(CancellationTokenSource cancellationTokenSource, CancellationToken cancellationToken, int lastUpdateId)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var last = (await _repository.Get(q => q
                        .Where(x => Equals(x.InteractionType, _repository.OwnChatterMessage))
                        .OrderByDescending(x => x.Id)
                        .Take(1)))
                    .Single();

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

    protected TelegramBotClient GetApi()
    {
        return _api ??= new(ScopedAbstractions.GetBotToken());
    }

    protected async Task SendMessage(Update respondTo, IReadOnlyList<string> images, bool reply)
    {
        await GetApi().SendMediaGroupAsync(
            new(respondTo.Message!.Chat.Id, images.Select(x => new InputMediaPhoto(x.ToString())))
            {
                MessageThreadId = respondTo.Message!.MessageThreadId,
                ReplyParameters = !reply ? null : new()
                {
                    MessageId = respondTo.Message.MessageId,
                    AllowSendingWithoutReply = false,
                },
                DisableNotification = true,
            });
    }

    protected async Task SendMessage(Update respondTo, string text, IReadOnlyList<string> images, bool reply)
    {
        var (messageEntities, result) = GetMessageEntities(text);

        try
        {
            await GetApi().SendMediaGroupAsync(new(respondTo.Message!.Chat.Id, images.WithIndices().Select(x => new InputMediaPhoto(x.x.ToString())
            {
                Caption = x.i == 0 ? result : null,
                CaptionEntities = x.i == 0 ? messageEntities : null,
            }))
            {
                MessageThreadId = respondTo.Message!.MessageThreadId,
                ReplyParameters = !reply ? null : new()
                {
                    MessageId = respondTo.Message.MessageId,
                    AllowSendingWithoutReply = false,
                },
                DisableNotification = true,
            });
        }
        catch (BotRequestException e) when (e.Message.Contains("message caption is too long"))
        {
            await SendMessage(respondTo, images, reply);

            await SendMessage(respondTo, text, reply);
        }
    }

    protected async Task SendMessage(Update respondTo, string text, bool reply)
    {
        var (messageEntities, result) = GetMessageEntities(text);

        await GetApi().SendMessageAsync(new(respondTo.Message!.Chat.Id, result)
        {
            MessageThreadId = respondTo.Message!.MessageThreadId,
            ReplyParameters = !reply ? null : new()
            {
                MessageId = respondTo.Message.MessageId,
                AllowSendingWithoutReply = false,
            },
            DisableNotification = true,
            LinkPreviewOptions = new()
            {
                IsDisabled = true,
            },
            Entities = messageEntities
        });
    }

    protected static (List<MessageEntity> messageEntities, string result) GetMessageEntities(string text)
    {
        var split = text.Split("**");
        var messageEntities = new List<MessageEntity>();

        StringBuilder builder = new StringBuilder();

        var isBold = false;
        foreach (var part in split)
        {
            if (isBold)
            {
                if (part.Length > 0)
                {
                    messageEntities.Add(new()
                    {
                        Type = "bold",
                        Offset = builder.Length,
                        Length = part.Length
                    });
                }
            }

            builder.Append(part);

            isBold = !isBold;
        }

        var result = builder.ToString();
        return (messageEntities, result);
    }

    protected async Task Typing(Update update)
    {
        await GetApi().SendChatActionAsync(update.Message!.Chat.Id, ChatActions.Typing, messageThreadId: update.Message.MessageThreadId);
    }

    protected async void LongTyping(Update update, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Typing(update);
                await Task.Delay(3000, cancellationToken);
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error sending typing events.");
        }
    }

    protected override async Task<bool> HandleSync(Update update)
    {
        if (update.Message?.From == null) return false;

        if (!CheckRelevant(update)) return false;

        OnInitializing(update);

        var chatUnavailableUntil = await GetChatUnavailableUntil();
        if (chatUnavailableUntil.HasValue)
        {
            Queued(SendMessage(update, string.Format(UnavailableUntilTemplate, chatUnavailableUntil.Value.ToString("f")), false));
            return true;
        }

        await Log(update, _repository.OwnChatterMessage);

        if (update.Message?.Text?.Length > 0)
        {
            Queued(Respond(update));
            return true;
        }

        return false;
    }

    protected abstract string UnavailableUntilTemplate { get; }

    protected virtual void OnInitializing(Update update)
    {
    }

    protected async Task Respond(Update update)
    {
        var now = DateTime.Now;
        var since = now.AddDays(-1);

        var interactions = await _repository.Get(q => q
            .Where(x => Equals(x.InteractionType, _repository.OwnChatterMessage) ||
                        Equals(x.InteractionType, _repository.OwnChatterMemoryPoint) ||
                        Equals(x.InteractionType, _repository.OwnChatterResetDialog))
            .Where(x => x.ShortInfoSerialized!.Length > 0 || Equals(x.InteractionType, _repository.OwnChatterResetDialog))
            .Where(x => x.CreatedOn > since)
            .OrderByDescending(x => x.CreatedOn)
            .Take(20));

        var imagesUnavailableUntil = await GetImagesUnavailableUntil(now);

        interactions = interactions.AsEnumerable().Reverse().ToList();

        var reset = interactions.FindLastIndex(x => Equals(x.InteractionType, _repository.OwnChatterResetDialog));
        if (reset > -1)
        {
            interactions = interactions.Skip(reset + 1).ToList();
        }

        var (system, version, frequencyPenalty, presencePenalty, imagesVersion) = await GetAiParameters();

        using var callingApis = new CancellationTokenSource();
        using var checkingIsStillLast = new CancellationTokenSource();
        LongTyping(update, callingApis.Token);
        var checking = CheckNoUpdates(checkingIsStillLast, callingApis.Token, interactions.Last(x => Equals(x.InteractionType, _repository.OwnChatterMessage)).Id);

        ChatBotMemoryPointWithTraces newMessagePoint;
        DialogResult result;

        try
        {
            var existingMemory = interactions.Select(i =>
                Equals(i.InteractionType, _repository.OwnChatterMessage)
                    ?
                    (MemoryPoint)new UserMessageMemoryPoint(i.ShortInfoSerialized!)
                    : Equals(i.InteractionType, _repository.OwnChatterMemoryPoint)
                        ? JsonSerializer.Deserialize<ChatBotMemoryPoint>(i.Serialized)!
                        : throw new ArgumentOutOfRangeException(nameof(i))).ToList();

            if (checkingIsStillLast.IsCancellationRequested)
            {
                return;
            }

            var (additionalBillingInfo, domainId) = GetDialogConfigurationParameters();
            (result, newMessagePoint) = await _dialogRunner.Execute(existingMemory, new()
            {
                Version = version ?? throw new("The version is required."),
                SystemMessage = system ?? throw new("The system message is required."),
                FrequencyPenalty = frequencyPenalty,
                PresencePenalty = presencePenalty,
                ImagesVersion = imagesVersion,
                UserId = update.Message!.From!.Id,
                UseCase = "own chatter",
                AdditionalBillingInfo = additionalBillingInfo,
                DomainId = domainId,
                ChatId = update.Message!.Chat.Id,
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
            await SendMessage(update, ResponseError, true);
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

        var interaction = CreateInteraction(update);
        interaction.CreatedOn = DateTime.Now;
        interaction.InteractionType = _repository.OwnChatterMemoryPoint;
        interaction.Serialized = JsonSerializer.Serialize(newMessagePoint);
        interaction.ShortInfoSerialized = JsonSerializer.Serialize(result);
        interaction.UserId = update.Message.From.Id;
        await _repository.Add(interaction.Once().ToList());

        if (result.Images.Any())
        {
            interaction = CreateInteraction(update);
            interaction.CreatedOn = DateTime.Now;
            interaction.InteractionType = imagesUnavailableUntil.HasValue ? _repository.ImagesLimit : _repository.ImagesSuccess;
            interaction.Serialized = result.Images.Count.ToString();
            interaction.UserId = update.Message.From.Id;
            await _repository.Add(interaction.Once().ToList());
        }

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

    protected abstract bool CheckRelevant(Update update);

    protected abstract Task<DateTime?> GetImagesUnavailableUntil(DateTime now);
    
    protected abstract Task<DateTime?> GetChatUnavailableUntil();

    protected abstract Task<(string? system, string? version, float? frequencyPenalty, float? presencePenalty, int? imagesVersion)> GetAiParameters();

    protected abstract (string? additionalBillingInfo, int? domainId) GetDialogConfigurationParameters();

    protected abstract string ResponseError { get; }
}