using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OneShelf.Common.Database.Songs.Model;

public class SongContent : IValidatableObject
{
    [Key]
    [ForeignKey(nameof(Song))]
    public int SongId { get; set; }

    public byte[] Content { get; set; } = null!;

    public Song Song { get; set; } = null!;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Content.Length == 0) yield return new($"{nameof(Content)} empty.");
    }
}