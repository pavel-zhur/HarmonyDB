using OneShelf.Videos.Database.Models.Static;

namespace OneShelf.Videos.Database.Models;

public class AlbumConstraint
{
    public int Id { get; set; }

    public int AlbumId { get; set; }

    public Album Album { get; set; } = null!;

    public bool Include { get; set; }

    public int? StaticTopicId { get; set; }

    public StaticTopic? StaticTopic { get; set; }

    public StaticMessageSelectedType? StaticMessageSelectedType { get; set; }

    public bool IsSquare { get; set; }

    public DateTime? Before { get; set; }

    public DateTime? After { get; set; }
}