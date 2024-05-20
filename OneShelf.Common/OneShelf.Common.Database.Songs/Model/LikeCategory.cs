using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using OneShelf.Common.Database.Songs.Model.Enums;

namespace OneShelf.Common.Database.Songs.Model;

[Index(nameof(UserId), nameof(Name), IsUnique = true)]
public class LikeCategory : IValidatableObject
{
    public int Id { get; set; }

    public required int TenantId { get; set; }

    public Tenant Tenant { get; set; }

    public long UserId { get; set; }

    public User User { get; set; } = null!;

    public required string Name { get; set; }

    public required string CssColor { get; set; }

    public required string CssIcon { get; set; }

    public int Order { get; set; }

    public float PrivateWeight { get; set; }

    public LikeCategoryAccess Access { get; set; }

    public ICollection<Like> Likes { get; set; } = null!;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(CssColor)) yield return new($"{nameof(CssColor)} should not be empty.");
        if (PrivateWeight is < 0 or > 5) yield return new($"{nameof(PrivateWeight)} out of range.");
        if (string.IsNullOrWhiteSpace(CssIcon)) yield return new($"{nameof(CssIcon)} should not be empty.");
        if (!Enum.GetValues<LikeCategoryAccess>().Contains(Access)) throw new($"Invalid {nameof(Access)}.");
    }
}