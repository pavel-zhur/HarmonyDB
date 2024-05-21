using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using OneShelf.Common.Database.Songs.Model.Enums;

namespace OneShelf.Common.Database.Songs.Model;

[Index(nameof(Uri))]
[Index(nameof(SongId), nameof(Uri), IsUnique = true)]
[Index(nameof(CollectiveId), IsUnique = true)]
[Index(nameof(CollectiveSearchTag))]
public class Version : IValidatableObject
{
    public int Id { get; set; }

    public int SongId { get; set; }

    public Song Song { get; set; } = null!;

    [Column(TypeName = "nvarchar(1000)")]
    public Uri Uri { get; set; } = null!;

    public long UserId { get; set; }

    public User User { get; set; } = null!;

    public ICollection<Like> Likes { get; set; } = null!;

    public ICollection<Comment> Comments { get; set; } = null!;

    public DateTime CreatedOn { get; set; }

    public string? PublishedSettings { get; set; }

    public VersionCollectiveType? CollectiveType { get; set; }

    public int? CollectiveSearchTag { get; set; }

    public Guid? CollectiveId { get; set; }

    public bool IsDefaultTemplate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CollectiveSearchTag.HasValue != CollectiveType.HasValue || CollectiveId.HasValue != CollectiveType.HasValue)
        {
            yield return new($"The {nameof(CollectiveSearchTag)} and {nameof(CollectiveType)} and {nameof(CollectiveId)} may only be set or not set together.");
        }
    }
}