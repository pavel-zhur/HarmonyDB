using System.ComponentModel.DataAnnotations;

namespace OneShelf.Collectives.Database.Models;

/// <summary>
/// Own chords whose author is an account of the new shelf (a uuid), not a Telegram user: those written in the new shelf.
/// The same as <see cref="Collective"/> otherwise; its id is unique among both.
/// </summary>
public class AccountCollective : IValidatableObject
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public required List<Version> Versions { get; set; }

    public CollectiveVisibility LatestVisibility { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AccountId == Guid.Empty)
            yield return new("The account is empty.");

        foreach (var version in Versions)
        {
            foreach (var result in version.Validate(validationContext))
            {
                yield return result;
            }
        }
    }
}
