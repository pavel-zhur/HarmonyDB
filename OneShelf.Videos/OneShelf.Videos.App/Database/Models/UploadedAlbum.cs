namespace OneShelf.Videos.App.Database.Models;

public class UploadedAlbum
{
    public int Id { get; set; }

    public int AlbumId { get; set; }

    public Album Album { get; set; } = null!;

    public required string GoogleAlbumId { get; set; }
}