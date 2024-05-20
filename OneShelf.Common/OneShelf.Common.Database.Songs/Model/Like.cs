using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using OneShelf.Common.Songs;

namespace OneShelf.Common.Database.Songs.Model;

[Index(nameof(SongId))]
[Index(nameof(UserId), nameof(SongId), nameof(VersionId), nameof(LikeCategoryId), IsUnique = true)]
public class Like : IValidatableObject, ILike
{
    public int Id { get; set; }

    public long UserId { get; set; }

    public User User { get; set; } = null!;

    public Song Song { get; set; } = null!;

    public int SongId { get; set; }

    public int? VersionId { get; set; }

    public Version? Version { get; set; }

    public byte Level { get; set; }

    public int? Transpose { get; set; }

    public DateTime CreatedOn { get; set; }

    public int? LikeCategoryId { get; set; }

    public LikeCategory? LikeCategory { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Level > 4) yield return new("Level between 0 and 4 expected.");

        if (Level == 0 && VersionId == null && Version == null && LikeCategory == null && LikeCategoryId == null)
            yield return new("The 0 like level is possible only for a version like or a category like.");

        if (Transpose.HasValue && !VersionId.HasValue)
            yield return new($"{nameof(VersionId)} is required if {nameof(Transpose)} is set.");

        if (Level > 0 && LikeCategoryId.HasValue)
            yield return new("The level greater than 0 is possible only without a category.");
    }
}