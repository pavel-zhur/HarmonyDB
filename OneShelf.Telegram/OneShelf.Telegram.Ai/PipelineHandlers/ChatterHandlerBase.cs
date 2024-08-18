using System.Text;
using Microsoft.Extensions.Logging;
using OneShelf.Common;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.Telegram.Ai.PipelineHandlers;

public abstract class ChatterHandlerBase : PipelineHandler
{
    private TelegramBotClient? _api;
    
    protected readonly ILogger<ChatterHandlerBase> _logger;

    protected ChatterHandlerBase(IScopedAbstractions scopedAbstractions, ILogger<ChatterHandlerBase> logger) 
        : base(scopedAbstractions)
    {
        _logger = logger;
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
        await GetApi().SendChatActionAsync(update.Message!.Chat.Id, "typing", messageThreadId: update.Message.MessageThreadId);
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
}