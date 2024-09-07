using Microsoft.EntityFrameworkCore;
using OneShelf.Videos.Database.Models.Json;

namespace OneShelf.Videos.Database.Models;

[Index(nameof(ChatId), nameof(RootMessageIdOr0), IsUnique = true)]
public class Topic
{
    public int Id { get; set; }

    public required long ChatId { get; set; }

    public Chat Chat { get; set; } = null!;

    public int RootMessageIdOr0 { get; set; }

    public required string OriginalTitle { get; set; }

    public required string Title { get; set; }

    public ICollection<Message> Messages { get; set; }

    public ICollection<AlbumConstraint> AlbumConstraints { get; set; }
}