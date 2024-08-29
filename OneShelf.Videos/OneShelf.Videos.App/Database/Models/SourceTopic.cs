using OneShelf.Videos.App.Database.Models.Json;

namespace OneShelf.Videos.App.Database.Models;

public class SourceTopic
{
    public int Id { get; set; }

    public int SourceId { get; set; }

    public Source Source { get; set; } = null!;

    public long ChatId { get; set; }

    public Chat Chat { get; set; } = null!;

    public int? RootMessageId { get; set; }
}