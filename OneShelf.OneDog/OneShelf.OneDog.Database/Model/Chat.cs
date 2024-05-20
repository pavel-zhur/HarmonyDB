using Microsoft.EntityFrameworkCore;

namespace OneShelf.OneDog.Database.Model;

[Index(nameof(ChatId))]
[Index(nameof(DomainId), nameof(ChatId), IsUnique = true)]
public class Chat
{
    public int Id { get; set; }

    public int DomainId { get; init; }

    public Domain Domain { get; init; } = null!;

    public long ChatId { get; init; }

    public required string Type { get; set; }

    public string? Title { get; set; }

    public bool? IsForum { get; set; }

    public DateTime FirstUpdateReceivedOn { get; init; }
    
    public DateTime LastUpdateReceivedOn { get; set; }

    public int UpdatesCount { get; set; }
}