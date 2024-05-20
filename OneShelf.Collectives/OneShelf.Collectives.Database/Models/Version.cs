using System.ComponentModel.DataAnnotations;
using OneShelf.Common;

namespace OneShelf.Collectives.Database.Models;

public record Version : IValidatableObject
{
    public DateTime CreatedOn { get; set; }

    public int SearchTag { get; set; }

    public required List<string> Authors { get; set; }

    public required string Title { get; set; }

    public required string Contents { get; set; }

    public CollectiveVisibility Visibility { get; set; }

    public string? Description { get; set; }

    public string? Tonality { get; set; }

    public int? Bpm { get; set; }

    public string? VersionComment { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Authors.Any() || Authors.Any(x => x.Trim() != x) || Authors.AnyDuplicates(out _) || Authors.Any(string.IsNullOrWhiteSpace))
            yield return new("Authors not trimmed or absent or whitespace or duplicate.");

        if (string.IsNullOrWhiteSpace(Title) || Title.Trim() != Title)
            yield return new("Title whitespace or not trimmed.");

        if (string.IsNullOrWhiteSpace(Contents))
            yield return new("Empty contents.");
    }
}