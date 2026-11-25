using HarmonyDB.Shelf.Model.Jobs;
using HarmonyDB.Source.Api.Model.V2;

namespace HarmonyDB.Shelf.Model.OwnChords;

/// <summary>
/// Who sees own chords.
/// </summary>
public enum OwnChordsVisibility
{
    /// <summary>The author only.</summary>
    Private,

    /// <summary>Anyone who has them: by a link, in a playlist. Found by the author's search only.</summary>
    Link,

    /// <summary>Anyone; found by everyone's search too.</summary>
    Public,
}

/// <summary>
/// What the editor writes: one version of own chords.
/// </summary>
public record OwnChordsText
{
    public required string Title { get; init; }

    public required IReadOnlyList<string> Artists { get; init; }

    /// <summary>
    /// The text with chords in the editor's markup: a line that begins with "&gt;" is a line of chords over the next
    /// one, a chord in "&lt;" and "&gt;" stands in the line, a line in "[" and "]" is a heading.
    /// </summary>
    public required string Contents { get; init; }

    /// <summary>
    /// What the author says about them, shown under the chords.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The key, a note and an optional "m" ("Am", "F#").
    /// </summary>
    public string? Key { get; init; }

    public int? Bpm { get; init; }

    public required OwnChordsVisibility Visibility { get; init; }

    /// <summary>
    /// What changed in this version, in the author's words.
    /// </summary>
    public string? VersionComment { get; init; }
}

/// <summary>
/// The person's own chords: the latest version.
/// </summary>
public record OwnChordsItem
{
    /// <summary>
    /// Their id in the editor (<c>/api/own-chords/{id}</c>).
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Their id as chords (<c>/api/chords/{chordsId}</c>, playlists, marks …); null when the sources did not answer now.
    /// </summary>
    public string? ChordsId { get; init; }

    public required OwnChordsText Text { get; init; }

    /// <summary>
    /// The number of the latest version, from 1.
    /// </summary>
    public required int Version { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Written in the old shelf (by the person's Telegram account).
    /// </summary>
    public required bool FromOldShelf { get; init; }
}

public record OwnChordsList
{
    /// <summary>
    /// The latest changed first.
    /// </summary>
    public required IReadOnlyList<OwnChordsItem> Items { get; init; }
}

public record OwnChordsVersion
{
    public required int Version { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required OwnChordsText Text { get; init; }
}

/// <summary>
/// Own chords with every version, the first first.
/// </summary>
public record OwnChordsDetails
{
    public required OwnChordsItem Item { get; init; }

    public required IReadOnlyList<OwnChordsVersion> History { get; init; }

    /// <summary>
    /// New own chords saved while the sources did not answer: the job that places them in the playlists once they do
    /// (<c>/api/jobs/{id}</c>, its result <see cref="OwnChordsPlacingResult"/>).
    /// </summary>
    public Guid? PlacingJobId { get; init; }
}

/// <summary>
/// What a job of the kind "own_chords_placing" ends with.
/// </summary>
public record OwnChordsPlacingResult : JobResult
{
    public required string ChordsId { get; init; }
}

/// <summary>
/// New own chords. They always go to the person's playlist of own chords; besides, as a variant of a song of a playlist,
/// or as new songs in other playlists.
/// </summary>
public record CreateOwnChordsRequest
{
    public required OwnChordsText Text { get; init; }

    /// <summary>
    /// A variant of this song of this playlist (<see cref="SongId"/> with <see cref="PlaylistId"/>).
    /// </summary>
    public Guid? PlaylistId { get; init; }

    public Guid? SongId { get; init; }

    /// <summary>
    /// Also new songs in these playlists.
    /// </summary>
    public IReadOnlyList<Guid>? AlsoInPlaylists { get; init; }
}

public record UpdateOwnChordsRequest
{
    public required OwnChordsText Text { get; init; }
}

public record OwnChordsPreviewRequest
{
    public required string Contents { get; init; }
}

/// <summary>
/// How the text will look: the content of chords as <c>GET /api/chords/{id}</c> gives it, with the sheet only.
/// </summary>
public record OwnChordsPreview
{
    public required Content Content { get; init; }
}

public static class OwnChordsErrorCodes
{
    /// <summary>404: no such own chords of the person's.</summary>
    public const string NoSuchOwnChords = "no_such_own_chords";

    /// <summary>400: a title is needed (details: <c>max</c> when it is too long).</summary>
    public const string TitleRequired = "own_chords_title_required";

    /// <summary>400: at least one artist is needed, each not empty (details: <c>max</c> when there are too many or one is
    /// too long).</summary>
    public const string ArtistsRequired = "own_chords_artists_required";

    /// <summary>400: the text is needed (details: <c>max</c> when it is too long).</summary>
    public const string ContentsRequired = "own_chords_contents_required";

    /// <summary>400: a field is too long or out of range (details: <c>field</c>, <c>max</c>).</summary>
    public const string Invalid = "own_chords_invalid";

    /// <summary>404: the chords to copy are not open to the person.</summary>
    public const string ChordsNotOpen = "chords_not_open";

    /// <summary>400: a song is given without its playlist, or the other way round.</summary>
    public const string SongNeedsPlaylist = "song_needs_playlist";
}
