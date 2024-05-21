namespace OneShelf.Common.Database.Songs.Model;

public class SameSongGroup
{
    public int Id { get; set; }

    public ICollection<Song> Songs { get; set; } = null!;
}