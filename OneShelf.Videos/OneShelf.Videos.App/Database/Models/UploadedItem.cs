namespace OneShelf.Videos.App.Database.Models;

public class UploadedItem
{
    public int Id { get; set; }
    public required DateTime CreatedOn { get; init; }
    public required long ChatId { get; init; }
    public required int MessageId { get; init; }
    public required DateTime TelegramPublishedOn { get; init; }
    public required string? Status { get; init; }
    public required int StatusCode { get; init; }
    public required string? StatusMessage { get; init; }
    public required string? MediaItemId { get; init; }
    public required bool? MediaItemIsPhoto { get; init; }
    public required bool? MediaItemIsVideo { get; init; }
    public required string? MediaItemMimeType { get; init; }
    public required DateTime? MediaItemSyncDate { get; init; }
    public required DateTime? MediaItemMetadataCreationTime { get; init; }
    public required string Json { get; init; }
    public required DateTime? FileNameTimestamp { get; init; }
}