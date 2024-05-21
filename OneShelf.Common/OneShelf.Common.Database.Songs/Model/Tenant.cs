using System.ComponentModel.DataAnnotations;

namespace OneShelf.Common.Database.Songs.Model;

public class Tenant : IValidatableObject
{
    public int Id { get; set; }

    public string? Title { get; set; }

    public string? PrivateDescription { get; set; }

    public int LatestUsedIndex { get; set; }

    public bool ArePdfsAllowed { get; set; }

    public ICollection<Song> Songs { get; set; } = null!;

    public ICollection<Artist> Artists { get; set; } = null!;

    public ICollection<LikeCategory> LikeCategories { get; set; } = null!;

    public ICollection<User> Messages { get; set; } = null!;

    public ICollection<Message> Users { get; set; } = null!;

    public static string PersonalPrivateDescription(long userId) => $"Personal {userId}";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Title != null && string.IsNullOrWhiteSpace(Title))
            yield return new($"{nameof(Title)} should be null or not empty.");
    }
}