namespace OneShelf.Videos.Database.Models;

public class AlbumConstraint
{
    public int Id { get; set; }

    public int AlbumId { get; set; }

    public Album Album { get; set; } = null!;

    public bool Include { get; set; }

    public int? TopicId { get; set; }

    public Topic? Topic { get; set; }

    public MediaType? StaticMessageSelectedType { get; set; }

    public bool IsSquare { get; set; }

    public DateTime? Before { get; set; }

    public DateTime? After { get; set; }
}