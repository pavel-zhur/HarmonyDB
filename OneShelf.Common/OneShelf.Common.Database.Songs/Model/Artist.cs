using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Common.Songs.FullTextSearch;

namespace OneShelf.Common.Database.Songs.Model;

[Index(nameof(Name), nameof(TenantId), IsUnique = true)]
public class Artist : IValidatableObject, ISearchableArtist
{
    public int Id { get; set; }

    public required int TenantId { get; set; }

    public Tenant Tenant { get; set; }

    public string Name { get; set; } = null!;

    public ICollection<Song> Songs { get; set; } = null!;

    public ICollection<ArtistSynonym> Synonyms { get; set; } = null!;

    IEnumerable<string> ISearchableArtist.Synonyms => Synonyms.Select(x => x.Title);

    public ICollection<Message> Messages { get; set; } = null!;

    public SongCategory? CategoryOverride { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().ToLowerInvariant() != Name) yield return new($"{nameof(Name)} empty or not trimmed or not lowercase.");
    }
}