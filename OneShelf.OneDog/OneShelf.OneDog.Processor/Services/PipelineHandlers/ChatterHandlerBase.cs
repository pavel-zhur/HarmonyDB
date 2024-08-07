﻿using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OneShelf.Common;
using OneShelf.OneDog.Database;
using OneShelf.OneDog.Database.Model.Enums;
using OneShelf.OneDog.Processor.Model;
using OneShelf.OneDog.Processor.Services.PipelineHandlers.Base;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.OneDog.Processor.Services.PipelineHandlers;

public abstract class ChatterHandlerBase : PipelineHandler
{
    protected ChatterHandlerBase(
        IOptions<TelegramOptions> telegramOptions,
        DogDatabase dogDatabase,
        ScopeAwareness scopeAwareness)
        : base(telegramOptions, dogDatabase, scopeAwareness)
    {
    }

    protected bool CheckTopicId(Update update, int topicId, long publicChatId)
    {
        if (update.Message?.Chat.Id != publicChatId) return false;
        if (update.Message.MessageThreadId != topicId) return false;
        if (update.Message.From == null) return false;

        return true;
    }

    protected async Task Log(Update update, int domainId, InteractionType interactionType)
    {
        DogDatabase.Interactions.Add(new()
        {
            CreatedOn = DateTime.Now,
            InteractionType = interactionType,
            UserId = update.Message!.From!.Id,
            ShortInfoSerialized = update.Message.Text,
            Serialized = JsonConvert.SerializeObject(update),
            DomainId = domainId,
        });
        await DogDatabase.SaveChangesAsync();
    }

    protected async Task SendMessage(Update respondTo, IReadOnlyList<string> images, bool reply)
    {
        await (await CreateApi()).SendMediaGroupAsync(
            new(respondTo.Message!.Chat.Id, images.Select(x => new InputMediaPhoto(x.ToString())))
            {
                MessageThreadId = respondTo.Message!.MessageThreadId,
                ReplyToMessageId = reply ? respondTo.Message.MessageId : null,
                AllowSendingWithoutReply = false,
                DisableNotification = true,
            });
    }

    private async Task<BotClient> CreateApi()
    {
        return new(ScopeAwareness.Domain.BotToken);
    }

    protected async Task SendMessage(Update respondTo, string text, IReadOnlyList<string> images, bool reply)
    {
        var (messageEntities, result) = GetMessageEntities(text);

        try
        {
            await (await CreateApi()).SendMediaGroupAsync(new(respondTo.Message!.Chat.Id, images.WithIndices().Select(x => new InputMediaPhoto(x.x.ToString())
            {
                Caption = x.i == 0 ? result : null,
                CaptionEntities = x.i == 0 ? messageEntities : null,
            }))
            {
                MessageThreadId = respondTo.Message!.MessageThreadId,
                ReplyToMessageId = reply ? respondTo.Message.MessageId : null,
                AllowSendingWithoutReply = false,
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

        await (await CreateApi()).SendMessageAsync(new(respondTo.Message!.Chat.Id, result)
        {
            MessageThreadId = respondTo.Message!.MessageThreadId,
            ReplyToMessageId = reply ? respondTo.Message.MessageId : null,
            AllowSendingWithoutReply = false,
            DisableNotification = true,
            DisableWebPagePreview = true,
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
        await (await CreateApi()).SendChatActionAsync(update.Message!.Chat.Id, "typing", update.Message.MessageThreadId);
    }
}