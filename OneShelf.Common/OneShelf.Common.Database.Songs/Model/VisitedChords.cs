using System.ComponentModel.DataAnnotations;

namespace OneShelf.Common.Database.Songs.Model;

public class VisitedChords : IValidatableObject
{
    public int Id { get; set; }

    public DateTime ViewedOn { get; set; }

    public long UserId { get; set; }

    public User User { get; set; } = null!;

    public Uri? Uri { get; set; }

    public int? SongId { get; set; }

    public Song? Song { get; set; }

    public string? ExternalId { get; set; }

    public string? SearchQuery { get; set; }

    public int? Transpose { get; set; }

    public string Artists { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Source { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if ((ExternalId != null) != (SearchQuery != null))
            yield return new($"{nameof(ExternalId)} and {nameof(SearchQuery)} should be set or not set together.");

        if (SearchQuery != null && string.IsNullOrWhiteSpace(SearchQuery))
            yield return new($"{nameof(SearchQuery)} whitespace.");

        if (string.IsNullOrWhiteSpace(Artists))
            yield return new($"{nameof(Artists)} empty.");

        if (string.IsNullOrWhiteSpace(Title))
            yield return new($"{nameof(Title)} empty.");
    }
}