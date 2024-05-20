using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace OneShelf.Common.Database.Songs.Model;

[Index(nameof(ArtistId), nameof(Title), IsUnique = true)]
public class ArtistSynonym : IValidatableObject
{
    public int Id { get; set; }

    public int ArtistId { get; set; }

    public Artist Artist { get; set; } = null!;

    public string Title { get; set; } = null!;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Title) || Title.Trim().ToLowerInvariant() != Title) yield return new($"{nameof(Title)} empty or not trimmed or not lowercase.");
    }
}