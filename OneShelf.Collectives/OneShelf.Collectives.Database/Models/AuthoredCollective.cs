using System.ComponentModel.DataAnnotations;

namespace OneShelf.Collectives.Database.Models;

/// <summary>
/// Own chords whose author is an opaque name of a person (as the calling service names them), not a Telegram user: those
/// written after the store came to name its authors so. The same as <see cref="Collective"/> otherwise; its id is unique
/// among both.
/// </summary>
public class AuthoredCollective : IValidatableObject
{
    public Guid Id { get; set; }

    public required string Author { get; set; }

    public required List<Version> Versions { get; set; }

    public CollectiveVisibility LatestVisibility { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Author))
            yield return new("The author is empty.");

        foreach (var version in Versions)
        {
            foreach (var result in version.Validate(validationContext))
            {
                yield return result;
            }
        }
    }
}