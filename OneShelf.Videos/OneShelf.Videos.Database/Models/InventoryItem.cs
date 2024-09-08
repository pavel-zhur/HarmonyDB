using System.ComponentModel.DataAnnotations;

namespace OneShelf.Videos.Database.Models;

public class InventoryItem
{
    [Key]
    public int DatabaseInventoryItemId { get; set; }

    public required string Id { get; init; }
    public required string BaseUrl { get; set; }
    public required string? Description { get; set; }
    public required string FileName { get; set; }
    public required bool IsPhoto { get; set; }
    public required bool IsVideo { get; set; }
    public required string ProductUrl { get; set; }
    public required DateTime SyncDate { get; set; }
    public required string MimeType { get; set; }
    public required string? ContributorInfoDisplayName { get; set; }
    public required string? ContributorInfoProfilePictureBaseUrl { get; set; }
    public required string? MediaMetadataHeight { get; set; }
    public required string? MediaMetadataWidth { get; set; }
    public required DateTime MediaMetadataCreationTime { get; set; }
    public required float? MediaMetadataPhotoApertureFNumber { get; set; }
    public required string? MediaMetadataPhotoExposureTime { get; set; }
    public required float? MediaMetadataPhotoFocalLength { get; set; }
    public required int? MediaMetadataPhotoIsoEquivalent { get; set; }
    public required string? MediaMetadataPhotoCameraMake { get; set; }
    public required string? MediaMetadataPhotoCameraModel { get; set; }
    public required string? MediaMetadataVideoStatus { get; set; }
    public required double? MediaMetadataVideoFps { get; set; }
    public required string? MediaMetadataVideoCameraMake { get; set; }
    public required string? MediaMetadataVideoCameraModel { get; set; }
}