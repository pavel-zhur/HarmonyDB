using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace OneShelf.Common.Database.Songs.Model;

[Index(nameof(UserId), nameof(SongId), nameof(VersionId), IsUnique = true)]
public class Comment : IValidatableObject
{
    public int Id { get; set; }

    public long UserId { get; set; }

    public User User { get; set; } = null!;

    public Song Song { get; set; } = null!;

    public int SongId { get; set; }

    public int? VersionId { get; set; }

    public Version? Version { get; set; }

    public string? Text { get; set; }

    public DateTime CreatedOn { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Text != null && (Text != Text.Trim() || string.IsNullOrWhiteSpace(Text)))
            yield return new($"{nameof(Text)} empty but not null.");
    }
}