using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.Telegram.Processor.Services.PipelineHandlers;

public abstract class ChatterHandlerBase : PipelineHandler
{
    private readonly TelegramBotClient _api;

    protected ChatterHandlerBase(
        IOptions<TelegramOptions> telegramOptions,
        SongsDatabase songsDatabase,
        IScopedAbstractions scopedAbstractions)
        : base(scopedAbstractions)
    {
        TelegramOptions = telegramOptions.Value;
        SongsDatabase = songsDatabase;
        _api = new(TelegramOptions.Token);
    }

    protected SongsDatabase SongsDatabase { get; }

    protected TelegramOptions TelegramOptions { get; }

    protected bool CheckTopicId(Update update, int topicId)
    {
        if (update.Message?.Chat.Username != TelegramOptions.PublicChatId.Substring(1)) return false;
        if (update.Message.MessageThreadId != topicId) return false;
        if (update.Message.From == null) return false;

        return true;
    }

    protected bool CheckOurChat(Update update)
    {
        if (update.Message?.Chat.Username != TelegramOptions.PublicChatId.Substring(1)) return false;
        if (update.Message.From == null) return false;

        return true;
    }

    protected async Task Log(Update update, InteractionType interactionType)
    {
        SongsDatabase.Interactions.Add(new()
        {
            CreatedOn = DateTime.Now,
            InteractionType = interactionType,
            UserId = update.Message!.From!.Id,
            ShortInfoSerialized = update.Message.Text,
            Serialized = JsonSerializer.Serialize(update)
        });
        await SongsDatabase.SaveChangesAsyncX();
    }

    protected async Task SendMessage(Update respondTo, IReadOnlyList<string> images, bool reply)
    {
        await _api.SendMediaGroupAsync(
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
            await _api.SendMediaGroupAsync(new(respondTo.Message!.Chat.Id, images.WithIndices().Select(x => new InputMediaPhoto(x.x.ToString())
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

        await _api.SendMessageAsync(new(respondTo.Message!.Chat.Id, result)
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

    private static (List<MessageEntity> messageEntities, string result) GetMessageEntities(string text)
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
        await _api.SendChatActionAsync(update.Message!.Chat.Id, "typing", messageThreadId: update.Message.MessageThreadId);
    }
}