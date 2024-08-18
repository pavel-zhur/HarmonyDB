using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.Common;
using OneShelf.OneDog.Database;
using OneShelf.OneDog.Database.Model.Enums;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.OneDog.Processor.Services.PipelineHandlers;

public abstract class ChatterHandler : PipelineHandler
{
    protected ChatterHandler(
        DogDatabase dogDatabase,
        IScopedAbstractions scopedAbstractions,
        ILogger<ChatterHandler> logger)
        : base(scopedAbstractions)
    {
        DogDatabase = dogDatabase;
        Logger = logger;
    }

    protected DogDatabase DogDatabase { get; }
    protected ILogger<ChatterHandler> Logger { get; }

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
            Serialized = JsonSerializer.Serialize(update),
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
                ReplyParameters = !reply ? null : new()
                {
                    MessageId = respondTo.Message.MessageId,
                    AllowSendingWithoutReply = false,
                },
                DisableNotification = true,
            });
    }

    private async Task<TelegramBotClient> CreateApi()
    {
        return new(ScopedAbstractions.GetBotToken());
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

        await (await CreateApi()).SendMessageAsync(new(respondTo.Message!.Chat.Id, result)
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
        await (await CreateApi()).SendChatActionAsync(update.Message!.Chat.Id, "typing", messageThreadId: update.Message.MessageThreadId);
    }

    protected async Task CheckNoUpdates(CancellationTokenSource cancellationTokenSource, CancellationToken cancellationToken, int lastUpdateId)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var last = await DogDatabase.Interactions.Where(x => x.InteractionType == InteractionType.OwnChatterMessage).OrderBy(x => x.Id).LastAsync(cancellationToken);
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
            Logger.LogError(e, "Error checking for updates.");
        }
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
            Logger.LogError(e, "Error sending typing events.");
        }
    }
}