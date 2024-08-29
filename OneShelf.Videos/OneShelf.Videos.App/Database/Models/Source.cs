namespace OneShelf.Videos.App.Database.Models;

public class Source
{
    public int Id { get; set; }

    public required string Title { get; set; }

    public ICollection<SourceTopic> Topics { get; set; } = null!;
}