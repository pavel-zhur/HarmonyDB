using OneShelf.Videos.App.Database.Models.Json;

namespace OneShelf.Videos.App.Database.Models;

public class Source
{
    public int Id { get; set; }

    public required string Title { get; set; }

    public long ChatId { get; set; }

    public Chat Chat { get; set; } = null!;

    public int? RootMessageId { get; set; }
}