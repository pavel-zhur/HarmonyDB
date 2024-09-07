using Microsoft.EntityFrameworkCore;
using OneShelf.Videos.Database.Models.Enums;

namespace OneShelf.Videos.Database.Models;

[Index(nameof(MediaGroupId))]
public class TelegramMedia
{
    public int Id { get; set; }

    public required int TelegramUpdateId { get; set; }
    public TelegramUpdate TelegramUpdate { get; set; } = null!;

    public required TelegramMediaType Type { get; set; }

    public int? HandlerMessageId { get; set; }

    public required DateTime CreatedOn { get; set; }
    public required DateTime TelegramPublishedOn { get; set; }

    public required long ChatId { get; set; }
    public required int MessageId { get; set; }
    public required string? MediaGroupId { get; set; }

    public required string? ForwardOriginTitle { get; set; }
    
    public required string FileId { get; set; }
    public required string? FileName { get; set; }
    public required long? FileSize { get; set; }
    public required string FileUniqueId { get; set; }
    public required string? MimeType { get; set; }
    public required int? Width { get; set; }
    public required int? Height { get; set; }
    public required int? Duration { get; set; }

    public required string? ThumbnailFileId { get; set; }
    public int? ThumbnailWidth { get; set; }
    public int? ThumbnailHeight { get; set; }
}