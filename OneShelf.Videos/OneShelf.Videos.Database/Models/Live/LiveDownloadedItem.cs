using Microsoft.EntityFrameworkCore;

namespace OneShelf.Videos.Database.Models.Live;

[Index(nameof(LiveMediaMediaId), IsUnique = true)]
[Index(nameof(FileName), IsUnique = true)]
public class LiveDownloadedItem
{
    public int Id { get; set; }
    public required long LiveMediaMediaId { get; set; }
    public string? ThumbnailFileName { get; set; }
    public required string FileName { get; set; }
}