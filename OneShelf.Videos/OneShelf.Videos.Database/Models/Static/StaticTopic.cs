using Microsoft.EntityFrameworkCore;

namespace OneShelf.Videos.Database.Models.Static;

[PrimaryKey(nameof(StaticChatId), nameof(RootMessageIdOr0))]
public class StaticTopic
{
    public int Id { get; set; }

    public required long StaticChatId { get; set; }

    public StaticChat StaticChat { get; set; } = null!;

    public int RootMessageIdOr0 { get; set; }

    public required string OriginalTitle { get; set; }

    public required string Title { get; set; }

    public ICollection<StaticMessage> StaticMessages { get; set; }
}