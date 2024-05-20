using Microsoft.EntityFrameworkCore;
using OneShelf.OneDog.Database.Model.Enums;

namespace OneShelf.OneDog.Database.Model;

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

    public required int DomainId { get; init; }

    public Domain Domain { get; init; } = null!;
}