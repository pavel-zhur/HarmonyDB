namespace OneShelf.Videos.Database.Models;

public class InventoryItem
{
    public required string Id { get; init; }
    public required DateTime CreatedOn { get; init; }
    public string BaseUrl { get; set; }
    public string? Description { get; set; }
    public string FileName { get; set; }
    public bool IsPhoto { get; set; }
    public bool IsVideo { get; set; }
    public string ProductUrl { get; set; }
    public DateTime SyncDate { get; set; }
    public string MimeType { get; set; }
    public string? ContributorInfoDisplayName { get; set; }
    public string? ContributorInfoProfilePictureBaseUrl { get; set; }
    public string? MediaMetadataHeight { get; set; }
    public string? MediaMetadataWidth { get; set; }
    public DateTime MediaMetadataCreationTime { get; set; }
    public float? MediaMetadataPhotoApertureFNumber { get; set; }
    public string? MediaMetadataPhotoExposureTime { get; set; }
    public float? MediaMetadataPhotoFocalLength { get; set; }
    public int? MediaMetadataPhotoIsoEquivalent { get; set; }
    public string? MediaMetadataPhotoCameraMake { get; set; }
    public string? MediaMetadataPhotoCameraModel { get; set; }
    public string? MediaMetadataVideoStatus { get; set; }
    public double? MediaMetadataVideoFps { get; set; }
    public string? MediaMetadataVideoCameraMake { get; set; }
    public string? MediaMetadataVideoCameraModel { get; set; }
}