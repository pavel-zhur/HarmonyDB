using Microsoft.EntityFrameworkCore;

namespace OneShelf.Videos.Database.Models;

[Index(nameof(LiveMediaId), IsUnique = true)]
[Index(nameof(FileName), IsUnique = true)]
public class DownloadedItem
{
    public int Id { get; set; }
    public required long LiveMediaId { get; set; }
    public string? ThumbnailFileName { get; set; }
    public required string FileName { get; set; }
}