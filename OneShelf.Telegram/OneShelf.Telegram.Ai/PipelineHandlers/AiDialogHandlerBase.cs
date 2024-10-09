using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OneShelf.Common;
using OneShelf.Common.OpenAi.Models;
using OneShelf.Common.OpenAi.Models.Memory;
using OneShelf.Common.OpenAi.Services;
using OneShelf.Telegram.Ai.Model;
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

    protected async Task SendMessage(long chatId, int? messageThreadId, int messageId, IReadOnlyList<string> images, bool reply)
    {
        await GetApi().SendMediaGroupAsync(
            new(chatId, images.Select(x => new InputMediaPhoto(x.ToString())))
            {
                MessageThreadId = messageThreadId,
                ReplyParameters = !reply ? null : new()
                {
                    MessageId = messageId,
                    AllowSendingWithoutReply = false,
                },
                DisableNotification = true,
            });
    }

    protected async Task SendMessage(long chatId, int? messageThreadId, int messageId, string text, IReadOnlyList<string> images, bool reply, ReplyMarkup? replyMarkup = null)
    {
        if (replyMarkup != null)
        {
            await SendSeparately(chatId, messageThreadId, messageId, text, images, reply, replyMarkup);
            return;
        }

        try
        {
            var (messageEntities, result) = GetMessageEntities(text);
            await GetApi().SendMediaGroupAsync(new(chatId, images.WithIndices().Select(x => new InputMediaPhoto(x.x.ToString())
            {
                Caption = x.i == 0 ? result : null,
                CaptionEntities = x.i == 0 ? messageEntities : null,
            }))
            {
                MessageThreadId = messageThreadId,
                ReplyParameters = !reply ? null : new()
                {
                    MessageId = messageId,
                    AllowSendingWithoutReply = false,
                },
                DisableNotification = true,
            });
        }
        catch (BotRequestException e) when (e.Message.Contains("message caption is too long"))
        {
            await SendSeparately(chatId, messageThreadId, messageId, text, images, reply);
        }
    }

    private async Task SendSeparately(long chatId, int? messageThreadId, int messageId, string text, IReadOnlyList<string> images, bool reply, ReplyMarkup? replyMarkup = null)
    {
        await SendMessage(chatId, messageThreadId, messageId, images, reply);
        await SendMessage(chatId, messageThreadId, messageId, text, reply, replyMarkup);
    }

    protected async Task SendMessage(long chatId, int? messageThreadId, int messageId, string text, bool reply, ReplyMarkup? replyMarkup = null)
    {
        var (messageEntities, result) = GetMessageEntities(text);

        await GetApi().SendMessageAsync(new(chatId, result)
        {
            MessageThreadId = messageThreadId,
            ReplyParameters = !reply ? null : new()
            {
                MessageId = messageId,
                AllowSendingWithoutReply = false,
            },
            DisableNotification = true,
            LinkPreviewOptions = new()
            {
                IsDisabled = true,
            },
            Entities = messageEntities,
            ReplyMarkup = replyMarkup,
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
        if (update.CallbackQuery?.Data?.StartsWith("image, ") == true && update.CallbackQuery.Message != null)
        {
            OnInitializing(update.CallbackQuery.From.Id, update.CallbackQuery.Message!.Chat.Id);
            return await HandleCallback(update);
        }

        if (update.Message?.From == null) return false;

        if (!CheckRelevant(update)) return false;

        OnInitializing(update.Message!.From!.Id, update.Message.Chat.Id);

        var chatUnavailableUntil = await GetChatUnavailableUntil();
        if (chatUnavailableUntil.HasValue)
        {
            Queued(SendMessage(update.Message!.Chat.Id, update.Message.MessageThreadId, update.Message.MessageId, string.Format(UnavailableUntilTemplate, chatUnavailableUntil.Value.ToString("f")), false));
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

    private async Task<bool> HandleCallback(Update update)
    {
        var callbackQuery = update.CallbackQuery!;

        var data = callbackQuery.Data!.Split(", ");
        
        if (data is not ["image", { } interactionIdValue, { } imageIndexValue, { } timesValue]
            || !int.TryParse(interactionIdValue, out var interactionId)
            || !int.TryParse(imageIndexValue, out var imageIndex)
            || !int.TryParse(timesValue, out var times))
        {
            return false;
        }

        var interaction = (await _repository.Get(x => x.Where(x => x.Id == interactionId))).Single();
        DateTime? imagesUnavailableUntil = null;
        if (times != 0)
        {
            imagesUnavailableUntil = await GetImagesUnavailableUntil(DateTime.Now);
            if (!imagesUnavailableUntil.HasValue)
            {
                var interaction2 = CreateInteraction(update);
                interaction2.CreatedOn = DateTime.Now;
                interaction2.InteractionType = _repository.ImagesSuccess;
                interaction2.Serialized = timesValue;
                interaction2.ShortInfoSerialized = callbackQuery.Data;
                interaction2.UserId = callbackQuery.From.Id;
                await _repository.Add(interaction2.Once().ToList());
            }
        }

        QueueApi(callbackQuery.From.Id.ToString(), api => React(api, callbackQuery, interaction, imageIndex, times, imagesUnavailableUntil));
        return true;
    }

    private async Task React(TelegramBotClient api, CallbackQuery callbackQuery, IInteraction<TInteractionType> interaction, int imageIndex, int times, DateTime? imagesUnavailableUntil)
    {
        var prompt = JsonSerializer.Deserialize<ChatBotMemoryPointWithDeserializableTraces>(interaction.Serialized)!.ImageTraces[imageIndex];
        var text = times == 0 
            ? prompt 
            : imagesUnavailableUntil.HasValue 
                ? string.Format(RegenerationUnavailableUntilTemplate, imagesUnavailableUntil!.Value.ToString("f"))
                : RegenerationTemplate;

        try
        {
            await api.AnswerCallbackQueryAsync(new AnswerCallbackQueryArgs(callbackQuery.Id)
            {
                ShowAlert = true,
                Text = text,
            });
        }
        catch (BotRequestException e) when (e.Message.Contains("query is too old and response timeout expired or query ID is invalid"))
        {
            _logger.LogWarning(e, "Couldn't interactively respond to the image callback query. {chat}, {user}.", callbackQuery.Message!.Chat, callbackQuery.From.Id);
        }

        if (!imagesUnavailableUntil.HasValue && times > 0)
        {
            var aiParameters = await GetAiParameters();
            var images = await _dialogRunner.GenerateImages(
                Enumerable.Repeat(prompt, times).ToList(),
                new()
                {
                    ImagesVersion = aiParameters.imagesVersion,
                    UserId = callbackQuery.From.Id,
                    DomainId = -1,
                    Version = aiParameters.version!,
                    ChatId = callbackQuery.Message!.Chat.Id,
                    UseCase = "direct regeneration",
                    AdditionalBillingInfo = "images regeneration",
                    SystemMessage = "no message",
                });

            images = images.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            if (images.Any())
            {
                await SendSeparately(callbackQuery.Message!.Chat.Id, null, callbackQuery.Message!.MessageId, $"⟳ {imageIndex + 1} × {times}", images!, true);
            }
            else
            {
                await SendMessage(callbackQuery.Message!.Chat.Id, null, callbackQuery.Message!.MessageId, "Не получилось нарисовать новые изображения.", true);
            }
        }
    }

    protected abstract string UnavailableUntilTemplate { get; }
    protected virtual string RegenerationUnavailableUntilTemplate => "Я пока отдыхаю, а ты возвращайся {0} UTC.";
    protected virtual string RegenerationTemplate => "Минутку, процесс идёт...";

    protected virtual void OnInitializing(long userId, long chatId)
    {
    }

    protected virtual bool TraceImages => false;

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
            await SendMessage(update.Message!.Chat.Id, update.Message.MessageThreadId, update.Message.MessageId, ResponseError, true);
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
        var memoryPointInteractionId = interaction.Id;

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
                InlineKeyboardMarkup? replyMarkup = null;
                if (TraceImages && IsPrivate(update.Message.Chat))
                {
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        text = "⚙ управление";
                    }

                    replyMarkup = new(Enumerable.Range(0, result.Images.Count).Select(i => (InlineKeyboardButton[])[
                        new($"👀 {i + 1}") { CallbackData = $"image, {memoryPointInteractionId}, {i}, 0" },
                        new($"⟳ {i + 1}") { CallbackData = $"image, {memoryPointInteractionId}, {i}, 1" },
                        new($"⟳ {i + 1} × 2") { CallbackData = $"image, {memoryPointInteractionId}, {i}, 2" },
                        new($"⟳ {i + 1} × 3") { CallbackData = $"image, {memoryPointInteractionId}, {i}, 3" },
                        new($"⟳ {i + 1} × 4") { CallbackData = $"image, {memoryPointInteractionId}, {i}, 4" },
                    ]));
                }

                if (!string.IsNullOrWhiteSpace(text))
                {
                    await SendMessage(update.Message!.Chat.Id, update.Message.MessageThreadId, update.Message.MessageId, text, result.Images, false, replyMarkup);
                }
                else
                {
                    await SendMessage(update.Message!.Chat.Id, update.Message!.MessageThreadId, update.Message!.MessageId, result.Images, false);
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
            await SendMessage(update.Message!.Chat.Id, update.Message.MessageThreadId, update.Message.MessageId, text, false);
        }
    }

    protected abstract bool CheckRelevant(Update update);

    protected abstract Task<DateTime?> GetImagesUnavailableUntil(DateTime now);
    
    protected abstract Task<DateTime?> GetChatUnavailableUntil();

    protected abstract Task<(string? system, string? version, float? frequencyPenalty, float? presencePenalty, int? imagesVersion)> GetAiParameters();

    protected abstract (string? additionalBillingInfo, int? domainId) GetDialogConfigurationParameters();

    protected abstract string ResponseError { get; }
}