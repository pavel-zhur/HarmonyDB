using Microsoft.Extensions.Options;
using OneShelf.Telegram.Helpers;
using OneShelf.Telegram.Services.Base;
using OneShelf.Videos.Database;
using OneShelf.Videos.Database.Models;
using OneShelf.Videos.Database.Models.Enums;
using OneShelf.Videos.Telegram.Processor.Model;
using OneShelf.Videos.Telegram.Processor.Services;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.Videos.Telegram.Processor.PipelineHandlers;

public class VideosCollector : PipelineHandler
{
    private readonly VideosDatabase _videosDatabase;
    private readonly IOptions<TelegramOptions> _options;
    private readonly Scope _scope;

    public VideosCollector(IScopedAbstractions scopedAbstractions, VideosDatabase videosDatabase, IOptions<TelegramOptions> options, Scope scope) 
        : base(scopedAbstractions)
    {
        _videosDatabase = videosDatabase;
        _options = options;
        _scope = scope;
    }

    protected override async Task<bool> HandleSync(Update update)
    {
        if (update.Message?.From?.Id != _options.Value.AdminId) return false;
        if (!IsPrivate(update.Message?.Chat)) return false;

        TelegramMediaType type;
        PhotoSize? thumbnail;
        string fileId;
        string? fileName;
        long? fileSize;
        string? mimeType;
        int? width;
        int? height;
        int? duration;
        string fileUniqueId;

        if (update.Message!.Video != null)
        {
            type = TelegramMediaType.Video;

            var item = update.Message.Video;

            mimeType = item.MimeType;
            width = item.Width;
            height = item.Height;
            duration = item.Duration;

            thumbnail = item.Thumbnail;

            fileId = item.FileId;
            fileName = item.FileName;
            fileSize = item.FileSize;
            fileUniqueId = item.FileUniqueId;
        }
        else if (update.Message.Photo?.Any() == true)
        {
            type = TelegramMediaType.Photo;

            var item = update.Message.Photo.MaxBy(x => x.Height);
            width = item!.Width;
            height = item.Height;
            duration = null;
            mimeType = null;

            thumbnail = update.Message.Photo.MinBy(x => x.Height);

            fileId = item.FileId;
            fileSize = item.FileSize;
            fileUniqueId = item.FileUniqueId;
            fileName = null;
        }
        else if (update.Message.VideoNote != null)
        {
            type = TelegramMediaType.VideoNote;

            var item = update.Message.VideoNote;

            width = height = item.Length;
            duration = item.Duration;
            mimeType = null;

            thumbnail = item.Thumbnail;

            fileId = item.FileId;
            fileSize = item.FileSize;
            fileUniqueId = item.FileUniqueId;
            fileName = null;
        }
        else if (update.Message.Document != null)
        {
            type = TelegramMediaType.Document;

            var item = update.Message.Document;

            mimeType = item.MimeType;
            width = height = duration = null;

            thumbnail = item.Thumbnail;

            fileId = item.FileId;
            fileName = item.FileName;
            fileSize = item.FileSize;
            fileUniqueId = item.FileUniqueId;
        }
        else
        {
            return false;
        }

        var telegramMedia = new TelegramMedia
        {
            TelegramUpdateId = _scope.UpdateId,
            
            Type = type,

            CreatedOn = DateTime.UtcNow,
            TelegramPublishedOn = DateTime.UnixEpoch.AddSeconds(update.Message!.ForwardOrigin?.Date ?? update.Message.Date),
 
            ChatId = update.Message.Chat.Id,
            MessageId = update.Message.MessageId,
            MediaGroupId = update.Message.MediaGroupId,

            ForwardOriginTitle = update.Message.ForwardOrigin.GetTitle(),
            FileId = fileId,
            FileName = fileName,
            FileSize = fileSize,
            FileUniqueId = fileUniqueId,
            MimeType = mimeType,
            Width = width,
            Height = height,
            Duration = duration,
            ThumbnailFileId = thumbnail?.FileId,
            ThumbnailWidth = thumbnail?.Width,
            ThumbnailHeight = thumbnail?.Height,
        };

        _videosDatabase.TelegramMedia.Add(telegramMedia);

        await _videosDatabase.SaveChangesAsync();

        QueueApi(null, api => Respond(api, update, telegramMedia.Id));

        return true;
    }

    private async Task Respond(TelegramBotClient client, Update update, int telegramMediaId)
    {
        await client.SendMessageAsync(
            update.Message!.Chat.Id,
            $"Got {telegramMediaId}.",
            replyParameters: new()
            {
                ChatId = update.Message.Chat.Id,
                MessageId = update.Message.MessageId,
                AllowSendingWithoutReply = true,
            },
            replyMarkup: new InlineKeyboardMarkup([[new("button") { CallbackData = telegramMediaId.ToString(), }]]));
    }
}