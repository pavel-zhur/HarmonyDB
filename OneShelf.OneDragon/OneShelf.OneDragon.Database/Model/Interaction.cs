using System.ComponentModel.DataAnnotations.Schema;
using OneShelf.OneDragon.Database.Model.Enums;

namespace OneShelf.OneDragon.Database.Model;

public class Interaction
{
    public int Id { get; set; }

    public long UserId { get; set; }

    public User User { get; set; } = null!;

    public long? ChatId { get; set; }

    public Chat? Chat { get; set; } = null!;

    public DateTime CreatedOn { get; set; }

    public string? ShortInfoSerialized { get; set; }

    public string Serialized { get; set; } = null!;

    [Column(TypeName = "nvarchar(50)")]
    public InteractionType InteractionType { get; set; }
}