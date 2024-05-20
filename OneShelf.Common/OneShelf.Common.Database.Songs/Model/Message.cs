using System.ComponentModel.DataAnnotations;
using OneShelf.Common.Database.Songs.Model.Enums;

namespace OneShelf.Common.Database.Songs.Model;

public class Message : IValidatableObject
{
    public int Id { get; set; }

    public required int TenantId { get; set; }

    public Tenant Tenant { get; set; }

    public int MessageId { get; set; }

    public MessageType Type { get; set; }

    public int? ArtistId { get; set; }

    public Artist? Artist { get; set; }

    public string? Hash { get; set; }

    public SongCategory? Category { get; set; }

    public string? Part { get; set; }

    public int? SongId { get; set; }

    public Song? Song { get; set; }

    public string? FileId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if ((ArtistId != null) != (Type == MessageType.Artist))
            yield return new($"{nameof(ArtistId)} expected only for {MessageType.Artist}.");

        if ((Category != null) != (Type == MessageType.CategoryPart))
            yield return new($"{nameof(Category)} expected only for {MessageType.CategoryPart}.");

        if (Part != null && Type != MessageType.CategoryPart)
            yield return new($"{nameof(Part)} possible only for {MessageType.CategoryPart}.");

        if (new[]
            {
                Type is MessageType.Song,
                SongId.HasValue,
            }.Distinct().Count() > 1)
            yield return new($"{nameof(SongId)} expected only for {MessageType.Song}.");

        if (FileId != null && Type != MessageType.Song)
            yield return new($"{nameof(FileId)} only available for {MessageType.Song}.");
    }
}