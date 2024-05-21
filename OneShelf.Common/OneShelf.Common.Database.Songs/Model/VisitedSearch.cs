using System.ComponentModel.DataAnnotations;

namespace OneShelf.Common.Database.Songs.Model;

public class VisitedSearch : IValidatableObject
{
    public int Id { get; set; }

    public DateTime SearchedOn { get; set; }

    public string Query { get; set; } = null!;

    public long UserId { get; set; }

    public User User { get; set; } = null!;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Query))
            yield return new($"{nameof(Query)} whitespace.");
    }
}