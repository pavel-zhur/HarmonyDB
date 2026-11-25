using HarmonyDB.Shelf.Model.Jobs;

namespace HarmonyDB.Shelf.Model.Import;

/// <summary>
/// Links to pages of chords, one per line or each apart, up to a hundred.
/// </summary>
public record ImportLinksRequest
{
    public required IReadOnlyList<string> Links { get; init; }
}

/// <summary>
/// What became of one link.
/// </summary>
public enum ImportLinkState
{
    /// <summary>The chords are in the person's playlist of imported chords (put there now, or before).</summary>
    Imported,

    /// <summary>No source knows the page, or the chords on it are not open to the person (the sources do not say
    /// which).</summary>
    NotFound,

    /// <summary>A source that may know the page did not answer: try again later.</summary>
    Unavailable,

    /// <summary>Not a link to a web page.</summary>
    NotALink,
}

public record ImportLinkResult
{
    /// <summary>
    /// The link as it was given.
    /// </summary>
    public required string Link { get; init; }

    public required ImportLinkState State { get; init; }

    /// <summary>
    /// The chords' id, when imported.
    /// </summary>
    public string? ChordsId { get; init; }

    /// <summary>
    /// The song of the playlist of imported chords they are in, when imported.
    /// </summary>
    public Guid? SongId { get; init; }
}

/// <summary>
/// What a job of the kind "import" ends with.
/// </summary>
public record ImportResult : JobResult
{
    public required Guid PlaylistId { get; init; }

    /// <summary>
    /// In the order given; a link given twice is answered once.
    /// </summary>
    public required IReadOnlyList<ImportLinkResult> Links { get; init; }
}

public static class ImportErrorCodes
{
    /// <summary>400: no links, or more than may be imported at once (details: <c>max</c>).</summary>
    public const string BadLinks = "import_bad_links";
}
