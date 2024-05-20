using Microsoft.EntityFrameworkCore;
using OneShelf.Common.Database.Songs.Model.Enums;

namespace OneShelf.Common.Database.Songs.Model;

[Index(nameof(InteractionType), nameof(CreatedOn))]
public class Interaction
{
    public int Id { get; set; }

    public long UserId { get; set; }

    public User User { get; set; } = null!;

    public DateTime CreatedOn { get; set; }

    public string? ShortInfoSerialized { get; set; }

    public string Serialized { get; set; } = null!;

    public InteractionType InteractionType { get; set; }
}