using System.ComponentModel.DataAnnotations;

namespace OneShelf.Illustrations.Database.Models;

public class Illustration : IllustrationHeader, IValidatableObject
{
    public required byte[] Image { get; set; }

    public required string Version { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Url)) yield return new("Url required.");
        if (string.IsNullOrWhiteSpace(Version)) yield return new("Version required.");
        if (Image?.Any() != true) yield return new("Image required.");
    }
}