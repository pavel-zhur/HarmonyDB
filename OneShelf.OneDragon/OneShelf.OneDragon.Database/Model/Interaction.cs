using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using OneShelf.OneDragon.Database.Model.Enums;
using OneShelf.Telegram.Ai.Model;

namespace OneShelf.OneDragon.Database.Model;

[Index(nameof(InteractionType), nameof(UserId), nameof(ChatId), nameof(CreatedOn))]
public class Interaction : IInteraction<InteractionType>
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

    public int UpdateId { get; set; }

    public Update Update { get; set; } = null!;
}