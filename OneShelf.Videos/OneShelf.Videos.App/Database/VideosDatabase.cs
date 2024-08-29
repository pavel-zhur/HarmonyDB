using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using CasCap.Models;
using Microsoft.EntityFrameworkCore;
using OneShelf.Videos.App.Database.Models;
using OneShelf.Videos.App.Database.Models.Json;

namespace OneShelf.Videos.App.Database;

public class VideosDatabase : DbContext
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    public VideosDatabase(DbContextOptions<VideosDatabase> options)
        : base(options)
    {
    }

    public required DbSet<UploadedItem> UploadedItems { get; set; }
    public required DbSet<ChatFolder> ChatFolders { get; set; }
    public required DbSet<Chat> Chats { get; set; }
    public required DbSet<Message> Messages { get; set; }
    public required DbSet<InventoryItem> InventoryItems { get; set; }
    public required DbSet<Source> Sources { get; set; }
    public required DbSet<SourceTopic> SourceTopics { get; set; }

    public void AddItems(IEnumerable<(long chatId, int messageId, string path, DateTime publishedOn, NewMediaItemResult result, DateTime? fileNameTimestamp)> items)
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
            MediaItemId = i.result.mediaItem?.id,
            MediaItemIsPhoto = i.result.mediaItem?.isPhoto,
            MediaItemIsVideo = i.result.mediaItem?.isVideo,
            MediaItemMimeType = i.result.mediaItem?.mimeType,
            MediaItemSyncDate = i.result.mediaItem?.syncDate,
            MediaItemMetadataCreationTime = i.result.mediaItem?.mediaMetadata?.creationTime,
            Json = JsonSerializer.Serialize(i.result.mediaItem),
            FileNameTimestamp = i.fileNameTimestamp,
        }));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Message>()
            .Property(x => x.ContactInformation)
            .HasConversion<string?>(
                e => JsonSerializer.Serialize(e, _jsonSerializerOptions),
                x => JsonSerializer.Deserialize<JsonElement>(x, _jsonSerializerOptions));

        modelBuilder.Entity<Message>()
            .Property(x => x.InlineBotButtons)
            .HasConversion<string?>(
                e => JsonSerializer.Serialize(e, _jsonSerializerOptions),
                x => JsonSerializer.Deserialize<JsonElement>(x, _jsonSerializerOptions));

        modelBuilder.Entity<Message>()
            .Property(x => x.LocationInformation)
            .HasConversion<string?>(
                e => JsonSerializer.Serialize(e, _jsonSerializerOptions),
                x => JsonSerializer.Deserialize<JsonElement>(x, _jsonSerializerOptions));

        modelBuilder.Entity<Message>()
            .Property(x => x.Poll)
            .HasConversion<string?>(
                e => JsonSerializer.Serialize(e, _jsonSerializerOptions),
                x => JsonSerializer.Deserialize<JsonElement>(x, _jsonSerializerOptions));

        modelBuilder.Entity<Message>()
            .Property(x => x.Text)
            .HasConversion<string>(
                e => JsonSerializer.Serialize(e, _jsonSerializerOptions),
                x => JsonSerializer.Deserialize<JsonElement>(x, _jsonSerializerOptions));

        modelBuilder.Entity<Message>()
            .Property(x => x.TextEntities)
            .HasConversion<string>(
                e => JsonSerializer.Serialize(e, _jsonSerializerOptions),
                x => JsonSerializer.Deserialize<JsonElement>(x, _jsonSerializerOptions));
    }
}