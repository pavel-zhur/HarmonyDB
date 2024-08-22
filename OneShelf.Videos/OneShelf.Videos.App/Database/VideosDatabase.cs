using System.Text.Json;
using CasCap.Models;
using Microsoft.EntityFrameworkCore;
using OneShelf.Videos.App.Database.Models;

namespace OneShelf.Videos.App.Database;

public class VideosDatabase : DbContext
{
    public VideosDatabase(DbContextOptions<VideosDatabase> options)
        : base(options)
    {
    }

    public required DbSet<UploadedItem> UploadedItems { get; set; }

    public void AddItems(IEnumerable<(long chatId, int messageId, string path, DateTime publishedOn, NewMediaItemResult result)> items)
    {
        UploadedItems.AddRange(items.Select(i => new UploadedItem
        {
            CreatedOn = DateTime.Now,
            ChatId = i.chatId,
            MessageId = i.messageId,
            TelegramPublishedOn = i.publishedOn,
            Status = i.result.status.status,
            StatusCode = i.result.status.code,
            StatusMessage = i.result.status.message,
            MediaItemId = i.result.mediaItem.id,
            MediaItemIsPhoto = i.result.mediaItem.isPhoto,
            MediaItemIsVideo = i.result.mediaItem.isVideo,
            MediaItemMimeType = i.result.mediaItem.mimeType,
            MediaItemSyncDate = i.result.mediaItem.syncDate,
            MediaItemMetadataCreationType = i.result.mediaItem.mediaMetadata.creationTime,
            Json = JsonSerializer.Serialize(i.result.mediaItem),
        }));
    }
}