using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Common.Songs;
using OneShelf.Common.Songs.FullTextSearch;

namespace OneShelf.Common.Database.Songs.Model;

[Index(nameof(SourceUniqueIdentifier), IsUnique = true)]
[Index(nameof(Index))]
public class Song : IValidatableObject, ISearchableSong
{
    public int Id { get; set; }

    public required int TenantId { get; set; }

    public Tenant Tenant { get; set; }

    public int Index { get; set; }

    public ICollection<Artist> Artists { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? FileId { get; set; }

    public ICollection<Message> Messages { get; set; } = null!;

    public string? AdditionalKeywords { get; set; }
    
    public string SourceUniqueIdentifier { get; set; } = null!;

    /// <summary>
    /// If exists, the song is in the full pending state (only the Rename action may take it out of that state).
    /// </summary>
    public string? UnparsedTitle { get; set; }

    public SongContent? Content { get; set; }

    public SongCategory? CategoryOverride { get; set; }

    public ICollection<Like> Likes { get; set; } = null!;

    public ICollection<Comment> Comments { get; set; } = null!;

    IEnumerable<ILike> ISearchableSong.Likes => Likes;

    public User CreatedByUser { get; set; } = null!;

    public long CreatedByUserId { get; set; }

    public DateTime CreatedOn { get; set; }

    public int? SameSongGroupId { get; set; }

    public SameSongGroup? SameSongGroup { get; set; }

    public SongStatus Status { get; set; }

    public int? RedirectToSongId { get; set; }

    public Song? RedirectToSong { get; set; }

    public ICollection<Song> RedirectsFromSongs { get; set; } = null!;

    public ICollection<Version> Versions { get; set; } = null!;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Title != Title.Trim().ToLowerInvariant()) yield return new($"{nameof(Title)} not trimmed or not lowercase.");

        if (AdditionalKeywords != null &&
            (AdditionalKeywords == string.Empty || AdditionalKeywords.Trim().ToLowerInvariant() != AdditionalKeywords))
            yield return new($"{nameof(AdditionalKeywords)} not null and empty or not trimmed or not lowercase.");

        if (SourceUniqueIdentifier != SourceUniqueIdentifier.Trim() || SourceUniqueIdentifier == string.Empty)
            yield return new($"{nameof(SourceUniqueIdentifier)} empty.");

        if (FileId != null && string.IsNullOrWhiteSpace(FileId))
            yield return new($"Empty {nameof(FileId)}.");

        if (UnparsedTitle != null && string.IsNullOrWhiteSpace(UnparsedTitle))
            yield return new($"Empty {nameof(UnparsedTitle)}.");
    }
}