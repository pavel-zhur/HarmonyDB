﻿using System.Text;
using System.Text.Json;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using OneShelf.Common;
using OneShelf.Common.OpenAi.Models;
using OneShelf.Common.OpenAi.Models.Memory;
using OneShelf.Common.OpenAi.Services;
using OneShelf.Telegram.Ai.Model;
using OneShelf.Telegram.Helpers;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;
using Telegram.BotAPI.UpdatingMessages;

namespace OneShelf.Telegram.Ai.PipelineHandlers;

public abstract class AiDialogHandlerBase<TInteractionType> : PipelineHandler
{
    private TelegramBotClient? _api;
    private readonly Dictionary<long, AsyncLock> _databaseLocks = new();

    private const string Numerals = "①②③④⑤⑥⑦⑧⑨⑩";
    private const string ShowImageActionsCallbackCommand = "imagegroup";
    private const string ImageCloseCallbackCommand = "imageclose";
    private const string ImageCallbackCommand = "image";
    private const string AudioApplyCallbackCommand = "audio";
    private const string CallbackDataSeparator = ", ";

    protected readonly ILogger<AiDialogHandlerBase<TInteractionType>> _logger;
    protected readonly IInteractionsRepository<TInteractionType> _repository;
    protected readonly DialogRunner _dialogRunner;
    protected readonly IHttpClientFactory _httpClientFactory;
    protected readonly Transcriber _transcriber;

    protected AiDialogHandlerBase(IScopedAbstractions scopedAbstractions, ILogger<AiDialogHandlerBase<TInteractionType>> logger, IInteractionsRepository<TInteractionType> repository, DialogRunner dialogRunner, IHttpClientFactory httpClientFactory, Transcriber transcriber) 
        : base(scopedAbstractions)
    {
        _logger = logger;
        _repository = repository;
        _dialogRunner = dialogRunner;
        _httpClientFactory = httpClientFactory;
        _transcriber = transcriber;
    }

    private async Task<bool> Log(Update update)
    {
        var text = update.Message?.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            text = update.Message?.Caption;
        }

        if (string.IsNullOrWhiteSpace(text) && update.Message?.Photo == null)
            return false;

        if (!string.IsNullOrWhiteSpace(text))
        {
            var interaction = CreateInteraction(update);
            interaction.CreatedOn = DateTime.Now;
            interaction.UserId = update.Message!.From!.Id;
            interaction.Serialized = JsonSerializer.Serialize(update);
            interaction.InteractionType = _repository.OwnChatterMessage;
            interaction.ShortInfoSerialized = text;
            await _repository.Add(interaction.Once().ToList());
        }

        if (update.Message?.Photo != null)
        {
            var bytes = await Download(update.Message.Photo.OrderByDescending(x => x.Height).First().FileId);

            var interaction = CreateInteraction(update);
            interaction.CreatedOn = DateTime.Now;
            interaction.UserId = update.Message!.From!.Id;
            interaction.Serialized = JsonSerializer.Serialize(update);
            interaction.InteractionType = _repository.OwnChatterImageMessage;
            interaction.ShortInfoSerialized = Convert.ToBase64String(bytes);
            await _repository.Add(interaction.Once().ToList());
        }

        return true;
    }

    private async Task<byte[]> Download(string fileId)
    {
        var path = (await GetApi().GetFileAsync(fileId)).FilePath;
        using var client = _httpClientFactory.CreateClient();
        return await client.GetByteArrayAsync($"https://api.telegram.org/file/bot{ScopedAbstractions.GetBotToken()}/{path}");
    }

    protected abstract IInteraction<TInteractionType> CreateInteraction(Update update);

    protected async Task CheckNoUpdates(CancellationTokenSource cancellationTokenSource, CancellationToken cancellationToken, int lastUpdateId)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var last = (await _repository.Get(q => q
                        .Where(x => Equals(x.InteractionType, _repository.OwnChatterMessage) || Equals(x.InteractionType, _repository.OwnChatterImageMessage))
                        .OrderByDescending(x => x.Id)
                        .Take(1)))
                    .Single();

                if (last.Id != lastUpdateId)
                {
                    await cancellationTokenSource.CancelAsync();
                    return;
                }

                await Task.Delay(100, cancellationToken);
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

    protected async Task Typing(long chatId, int? messageThreadId, ValueHolder<MediaAction>? mediaAction = null)
    {
        var action = mediaAction?.Value switch
        {
            MediaAction.UploadPhoto => ChatActions.UploadPhoto,
            MediaAction.RecordVideo => ChatActions.RecordVideo,
            MediaAction.RecordVoice => ChatActions.UploadVoice,
            _ => ChatActions.Typing
        };
        await GetApi().SendChatActionAsync(chatId, action, messageThreadId: messageThreadId);
    }

    protected async void LongTyping(long chatId, int? messageThreadId, CancellationToken cancellationToken, ValueHolder<MediaAction>? mediaAction = null)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Typing(chatId, messageThreadId, mediaAction);
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
        if (update.CallbackQuery is { Data: not null, Message: not null })
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

        var anyContent = await Log(update);
        if (anyContent)
        {
            Queued(Respond(update));
            return true;
        }

        if (TranscribeAudio(update))
        {
            Transcribe(update);
            return true;
        }

        return false;
    }

    private void Transcribe(Update update)
    {
        QueueApi(update.Message!.From!.Id.ToString(), async client =>
        {
            var interaction = CreateInteraction(update);
            interaction.InteractionType = _repository.Audio;
            interaction.UserId = update.Message.From.Id;
            interaction.CreatedOn = DateTime.Now;
            interaction.Serialized = JsonSerializer.Serialize(update);
            await _repository.Add([interaction]);

            var autoRespond = update.Message.ForwardOrigin == null;
            var replyMarkup = autoRespond ? null : new InlineKeyboardMarkup([[new("✅")
            {
                CallbackData = GetCallbackData(AudioApplyCallbackCommand, interaction.Id),
            }]]);

            var message = await client.SendMessageAsync(update.Message.Chat.Id, "Transcribing...", null,
                update.Message.MessageThreadId, replyParameters: new()
                {
                    MessageId = update.Message.MessageId,
                }, replyMarkup: replyMarkup);

            try
            {
                var bytes = await Download(update.Message.Voice!.FileId);
                var (additionalBillingInfo, domainId) = GetDialogConfigurationParameters();
                var text = await _transcriber.TranscribeAudio(bytes, new()
                {
                    SystemMessage = null!,
                    Version = null!,
                    UserId = update.Message.From.Id,
                    UseCase = "audio",
                    AdditionalBillingInfo = additionalBillingInfo,
                    DomainId = domainId,
                    ChatId = update.Message.Chat.Id,
                });

                interaction.ShortInfoSerialized = text;
                await _repository.Update(interaction);

                await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, string.Join("\n", text.Replace("\r\n", "\n").Split('\n').Select(x => x.ToMarkdown())).SelectSingle(x => $"`{x}`"), replyMarkup: replyMarkup, parseMode: Constants.MarkdownV2);

                if (autoRespond)
                {
                    update.Message.Text = text;
                    if (await Log(update))
                    {
                        await Respond(update, message.MessageId);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error transcribing the audio.");
                await client.EditMessageTextAsync(message.Chat.Id, message.MessageId, "Случилась ошибка.");
            }
        });
    }

    private async Task<bool> HandleCallback(Update update)
    {
        var callbackQuery = update.CallbackQuery!;
        var data = callbackQuery.Data!.Split(CallbackDataSeparator);

        if (data is [AudioApplyCallbackCommand, { } audioInteractionIdValue]
            && int.TryParse(audioInteractionIdValue, out var audioInteractionId))
        {
            QueueApi(
                callbackQuery.From.Id.ToString(),
                async api =>
                {
                    var interaction = (await _repository.Get(x => x.Where(x => x.Id == audioInteractionId))).Single();
                    while (interaction.ShortInfoSerialized == null
                           && DateTime.Now - interaction.CreatedOn < TimeSpan.FromSeconds(10))
                    {
                        await Task.Delay(500);
                        interaction = (await _repository.Get(x => x.Where(x => x.Id == audioInteractionId))).Single();
                    }

                    if (interaction.ShortInfoSerialized == null)
                    {
                        return;
                    }

                    var text = interaction.ShortInfoSerialized;
                    var update2 = JsonSerializer.Deserialize<Update>(interaction.Serialized);
                    update2.Message.Text = text;

                    if (callbackQuery.Message != null)
                    {
                        await api.EditMessageReplyMarkupAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, replyMarkup: null);
                    }

                    if (await Log(update2))
                    {
                        await Respond(update2, callbackQuery.Message?.MessageId);
                    }
                });

            return true;
        }

        if (data is [ShowImageActionsCallbackCommand, { } imageGroupInteractionIdValue, { } imageGroupImageIndexValue, { } imageGroupImagesCountValue]
            && int.TryParse(imageGroupInteractionIdValue, out var interactionId)
            && int.TryParse(imageGroupImageIndexValue, out var imageIndex)
            && int.TryParse(imageGroupImagesCountValue, out var imagesCount))
        {
            QueueApi(
                callbackQuery.From.Id.ToString(),
                async api => await api.EditMessageReplyMarkupAsync(
                    callbackQuery.Message!.Chat.Id,
                    callbackQuery.Message.MessageId,
                    replyMarkup: new InlineKeyboardMarkup(
                        [
                            GetMainMarkup(imagesCount, interactionId).InlineKeyboard.Single(),
                            [
                                new($"✖️ {GetImageNumber(imageIndex)}") { CallbackData = GetCallbackData(ImageCloseCallbackCommand, interactionId, imagesCount) },
                                new("👀") { CallbackData = GetCallbackData(ImageCallbackCommand, interactionId, imageIndex, 0) },
                                new("👀↵") { CallbackData = GetCallbackData(ImageCallbackCommand, interactionId, imageIndex, -1) },
                            ],
                            Enumerable.Range(1, 5).Select(x => new InlineKeyboardButton($"+{x}") { CallbackData = GetCallbackData(ImageCallbackCommand, interactionId, imageIndex, x) })
                        ])));

            return true;
        }

        if (data is [ImageCloseCallbackCommand, { } imageBackInteractionIdValue, { } imageBackImagesCountValue]
            && int.TryParse(imageBackInteractionIdValue, out interactionId)
            && int.TryParse(imageBackImagesCountValue, out imagesCount))
        {
            QueueApi(
                callbackQuery.From.Id.ToString(),
                async api => await api.EditMessageReplyMarkupAsync(
                    callbackQuery.Message!.Chat.Id,
                    callbackQuery.Message.MessageId,
                    replyMarkup: GetMainMarkup(imagesCount, interactionId)));

            return true;
        }

        if (data is [ImageCallbackCommand, { } interactionIdValue, { } imageIndexValue, { } timesValue]
            && int.TryParse(interactionIdValue, out interactionId)
            && int.TryParse(imageIndexValue, out imageIndex)
            && int.TryParse(timesValue, out var times))
        {
            var interaction = (await _repository.Get(x => x.Where(x => x.Id == interactionId))).Single();
            DateTime? imagesUnavailableUntil = null;
            if (times > 0)
            {
                var @lock = _databaseLocks.GetValueOrDefault(callbackQuery.From.Id) ??
                            (_databaseLocks[callbackQuery.From.Id] = new());
                using var _ = await @lock.LockAsync();
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

            QueueApi(callbackQuery.From.Id.ToString(),
                api => ImageCallback(api, callbackQuery, interaction, imageIndex, times, imagesUnavailableUntil));
            return true;
        }

        return false;
    }

    private async Task ImageCallback(TelegramBotClient api, CallbackQuery callbackQuery, IInteraction<TInteractionType> interaction, int imageIndex, int times, DateTime? imagesUnavailableUntil)
    {
        var prompt = JsonSerializer.Deserialize<ChatBotMemoryPointWithDeserializableTraces>(interaction.Serialized)!.ImageTraces[imageIndex];
        var text = times switch
        {
            -1 => null,
            0 => prompt,
            _ when imagesUnavailableUntil.HasValue => string.Format(RegenerationUnavailableUntilTemplate, imagesUnavailableUntil.Value.ToString("f")), 
            _ => RegenerationTemplate
        };

        try
        {
            await api.AnswerCallbackQueryAsync(new AnswerCallbackQueryArgs(callbackQuery.Id)
            {
                ShowAlert = text != null,
                Text = text,
            });
        }
        catch (BotRequestException e) when (e.Message.Contains("query is too old and response timeout expired or query ID is invalid"))
        {
            _logger.LogWarning(e, "Couldn't interactively respond to the image callback query. {chat}, {user}.", callbackQuery.Message!.Chat, callbackQuery.From.Id);
        }

        if (times == -1)
        {
            await SendMessage(callbackQuery.Message!.Chat.Id, null, callbackQuery.Message!.MessageId, $"{GetImageNumber(imageIndex)}: {prompt}", true);
            return;
        }

        if (!imagesUnavailableUntil.HasValue && times > 0)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            try
            {
                var mediaAction = new ValueHolder<MediaAction>(MediaAction.UploadPhoto);
                LongTyping(callbackQuery.Message!.Chat.Id, null, cancellationTokenSource.Token, mediaAction);
                var aiParameters = await GetAiParameters();
                var images = await _dialogRunner.GenerateImages(
                    Enumerable.Repeat(prompt, times).ToList(),
                    new()
                    {
                        ImagesVersion = aiParameters.imagesVersion,
                        UserId = callbackQuery.From.Id,
                        DomainId = -1,
                        Version = aiParameters.version!,
                        ChatId = callbackQuery.Message.Chat.Id,
                        UseCase = "direct regeneration",
                        AdditionalBillingInfo = "images regeneration",
                        SystemMessage = "no message",
                    },
                    default);

                images = images.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                if (images.Any())
                {
                    await SendMessage(callbackQuery.Message!.Chat.Id, null, callbackQuery.Message!.MessageId, images!, true);
                }
                else
                {
                    await SendMessage(callbackQuery.Message!.Chat.Id, null, callbackQuery.Message!.MessageId, "Не получилось нарисовать новые изображения.", true);
                }
            }
            finally
            {
                await cancellationTokenSource.CancelAsync();
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

    protected virtual bool TranscribeAudio(Update update)
    {
        if (update.Message?.Voice == null) return false;
        if (update.Message.Voice.Duration > 120) return false;

        return true;
    }

    private async Task Respond(Update update, int? replyToMessageId = null) 
    {
        var now = DateTime.Now;
        var since = now.AddDays(-1);

        var interactions = await _repository.Get(q => q
            .Where(x => Equals(x.InteractionType, _repository.OwnChatterMessage) ||
                        Equals(x.InteractionType, _repository.OwnChatterImageMessage) ||
                        Equals(x.InteractionType, _repository.OwnChatterMemoryPoint) ||
                        Equals(x.InteractionType, _repository.OwnChatterResetDialog))
            .Where(x => x.ShortInfoSerialized!.Length > 0 || Equals(x.InteractionType, _repository.OwnChatterResetDialog))
            .Where(x => x.CreatedOn > since)
            .OrderByDescending(x => x.CreatedOn)
            .Take(20));

        var imagesUnavailableUntil = await GetImagesUnavailableUntil(now);
        var videosUnavailableUntil = await GetVideosUnavailableUntil(now);
        var musicUnavailableUntil = await GetMusicUnavailableUntil(now);

        interactions = interactions.AsEnumerable().Reverse().ToList();

        var reset = interactions.FindLastIndex(x => Equals(x.InteractionType, _repository.OwnChatterResetDialog));
        if (reset > -1)
        {
            interactions = interactions.Skip(reset + 1).ToList();
        }

        var (system, version, frequencyPenalty, presencePenalty, imagesVersion, videoModel, musicModel) = await GetAiParameters();

        using var callingApis = new CancellationTokenSource();
        using var checkingIsStillLast = new CancellationTokenSource();
        ValueHolder<MediaAction> mediaAction = new(MediaAction.Typing);
        LongTyping(update.Message!.Chat.Id, update.Message.MessageThreadId, callingApis.Token, mediaAction);
        var checking = CheckNoUpdates(checkingIsStillLast, callingApis.Token, interactions.Last(x => Equals(x.InteractionType, _repository.OwnChatterMessage) || Equals(x.InteractionType, _repository.OwnChatterImageMessage)).Id);

        ChatBotMemoryPointWithTraces newMessagePoint;
        DialogResult result;

        try
        {
            await Task.Delay(update.Message.MediaGroupId != null || update.Message.Photo != null ? 500 : 220, checkingIsStillLast.Token);

            var existingMemory = interactions.Select(i =>
                Equals(i.InteractionType, _repository.OwnChatterImageMessage)
                    ? (MemoryPoint)new UserImageMessageMemoryPoint(i.ShortInfoSerialized!)
                    : Equals(i.InteractionType, _repository.OwnChatterMessage)
                        ? new UserMessageMemoryPoint(i.ShortInfoSerialized!)
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
                VideoModel = videoModel,
                MusicModel = musicModel,
                UserId = update.Message.From!.Id,
                UseCase = "own chatter",
                AdditionalBillingInfo = additionalBillingInfo,
                DomainId = domainId,
                ChatId = update.Message.Chat.Id,
            }, checkingIsStillLast.Token, imagesUnavailableUntil, videosUnavailableUntil, musicUnavailableUntil, mediaAction);

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
        interaction.ShortInfoSerialized = result.ToString();
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

        // Process video results
        foreach (var video in result.Videos)
        {
            interaction = CreateInteraction(update);
            interaction.CreatedOn = DateTime.Now;
            interaction.InteractionType = _repository.VideosSuccess;
            interaction.Serialized = video.Duration.ToString();
            interaction.ShortInfoSerialized = video.Prompt;
            interaction.UserId = update.Message.From.Id;
            await _repository.Add(interaction.Once().ToList());
            
            await SendVideo(update.Message!.Chat.Id, update.Message.MessageThreadId, replyToMessageId ?? update.Message.MessageId, video.Data, video.Prompt, replyToMessageId.HasValue);
        }

        // Process video limits
        foreach (var videoLimit in result.VideoLimits)
        {
            interaction = CreateInteraction(update);
            interaction.CreatedOn = DateTime.Now;
            interaction.InteractionType = _repository.VideosLimit;
            interaction.Serialized = "limit";
            interaction.ShortInfoSerialized = videoLimit.Prompt;
            interaction.UserId = update.Message.From.Id;
            await _repository.Add(interaction.Once().ToList());
        }

        // Process music results
        foreach (var music in result.Music)
        {
            interaction = CreateInteraction(update);
            interaction.CreatedOn = DateTime.Now;
            interaction.InteractionType = _repository.SongsSuccess;
            interaction.Serialized = "1";
            interaction.ShortInfoSerialized = music.Prompt;
            interaction.UserId = update.Message.From.Id;
            await _repository.Add(interaction.Once().ToList());
            
            await SendMusic(update.Message!.Chat.Id, update.Message.MessageThreadId, replyToMessageId ?? update.Message.MessageId, music.Data, music.Prompt, music.Title, replyToMessageId.HasValue);
        }

        // Process music limits
        foreach (var musicLimit in result.MusicLimits)
        {
            interaction = CreateInteraction(update);
            interaction.CreatedOn = DateTime.Now;
            interaction.InteractionType = _repository.SongsLimit;
            interaction.Serialized = "limit";
            interaction.ShortInfoSerialized = musicLimit.Prompt;
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

                    var imagesCount = result.Images.Count;
                    replyMarkup = GetMainMarkup(imagesCount, memoryPointInteractionId);
                }

                if (!string.IsNullOrWhiteSpace(text))
                {
                    await SendMessage(update.Message!.Chat.Id, update.Message.MessageThreadId, replyToMessageId ?? update.Message.MessageId, text, result.Images, replyToMessageId.HasValue, replyMarkup);
                }
                else
                {
                    await SendMessage(update.Message!.Chat.Id, update.Message!.MessageThreadId, replyToMessageId ?? update.Message.MessageId, result.Images, replyToMessageId.HasValue);
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
            await SendMessage(update.Message!.Chat.Id, update.Message.MessageThreadId, replyToMessageId ?? update.Message.MessageId, text, replyToMessageId.HasValue);
        }
    }

    protected async Task SendVideo(long chatId, int? messageThreadId, int messageId, byte[] videoData, string prompt, bool reply)
    {
        try
        {
            using var videoStream = new MemoryStream(videoData);
            var videoFile = new InputFile(videoStream, "video.mp4");
            await GetApi().SendVideoAsync(new(chatId, videoFile)
            {
                MessageThreadId = messageThreadId,
                ReplyParameters = reply ? new()
                {
                    MessageId = messageId,
                    AllowSendingWithoutReply = false,
                } : null,
                Caption = $"🎬 {prompt} (от Sora 🐶)"
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error sending video. Prompt: {prompt}", prompt);
        }
    }

    private static string ValidateAndFixMusicFilename(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "music.wav";
        }

        // Remove invalid filename characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleanTitle = title;
        foreach (var invalidChar in invalidChars)
        {
            cleanTitle = cleanTitle.Replace(invalidChar, '_');
        }

        // Ensure .wav extension
        if (!cleanTitle.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
        {
            // Remove any existing extension and add .wav
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(cleanTitle);
            cleanTitle = $"{nameWithoutExtension}.wav";
        }

        return cleanTitle;
    }

    protected async Task SendMusic(long chatId, int? messageThreadId, int messageId, byte[] musicData, string prompt, string? title, bool reply)
    {
        try
        {
            var filename = ValidateAndFixMusicFilename(title);
            using var audioStream = new MemoryStream(musicData);
            var audioFile = new InputFile(audioStream, filename);
            await GetApi().SendAudioAsync(new(chatId, audioFile)
            {
                MessageThreadId = messageThreadId,
                ReplyParameters = reply ? new()
                {
                    MessageId = messageId,
                    AllowSendingWithoutReply = false,
                } : null,
                Caption = $"🎵 {prompt} (от Lyria 🐶)"
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error sending music. Prompt: {prompt}", prompt);
        }
    }

    private static InlineKeyboardMarkup GetMainMarkup(int imagesCount, int memoryPointInteractionId)
    {
        return new([
            Enumerable.Range(0, imagesCount).Select(i => new InlineKeyboardButton(GetImageNumber(i)) { CallbackData = GetCallbackData(ShowImageActionsCallbackCommand, memoryPointInteractionId, i, imagesCount) })
        ]);
    }

    private static string GetCallbackData(params object[] args) => string.Join(CallbackDataSeparator, args);

    private static string GetImageNumber(int i)
    {
        return i < Numerals.Length ? Numerals[i].ToString() : (i + 1).ToString();
    }

    protected abstract bool CheckRelevant(Update update);

    protected abstract Task<DateTime?> GetImagesUnavailableUntil(DateTime now);

    protected abstract Task<DateTime?> GetVideosUnavailableUntil(DateTime now);

    protected abstract Task<DateTime?> GetMusicUnavailableUntil(DateTime now);
    
    protected abstract Task<DateTime?> GetChatUnavailableUntil();

    protected abstract Task<(string? system, string? version, float? frequencyPenalty, float? presencePenalty, int? imagesVersion, string? videoModel, string? musicModel)> GetAiParameters();

    protected abstract (string? additionalBillingInfo, int? domainId) GetDialogConfigurationParameters();

    protected abstract string ResponseError { get; }
}