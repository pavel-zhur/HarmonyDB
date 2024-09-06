namespace OneShelf.Videos.App.Database.Models;

public class Album
{
    public int Id { get; set; }

    public string Title { get; set; }

    public ICollection<AlbumConstraint> Constraints { get; set; }

    public UploadedAlbum? UploadedAlbum { get; set; }
}