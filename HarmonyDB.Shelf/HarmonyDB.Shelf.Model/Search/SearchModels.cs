using HarmonyDB.Shelf.Model.Chords;
using HarmonyDB.Shelf.Model.Me;
using HarmonyDB.Shelf.Model.Music;

namespace HarmonyDB.Shelf.Model.Search;

/// <summary>
/// The parts of the one search. The app may ask the quick part (songs) as one types, and the others once the typing
/// pauses.
/// </summary>
public enum SearchPart
{
    /// <summary>Songs of the playlists the person sees, by their titles and artists. Quick.</summary>
    Songs,

    /// <summary>Chords from the sources (at least 2 characters).</summary>
    Chords,

    /// <summary>Artists and recordings of the music services (at least 3 characters).</summary>
    Music,
}

public enum SearchFailureReason
{
    Timeout,
    Unavailable,
}

/// <summary>
/// A source that did not answer: its results are missing, which is not "nothing found".
/// </summary>
public sealed record SearchSourceFailure
{
    /// <summary>The source's name as the shelf's index knows it.</summary>
    public required string Source { get; init; }

    public required SearchFailureReason Reason { get; init; }
}

/// <summary>
/// A song of a playlist the person sees, standing in the results for the chords found that are in it, or found by its
/// title or artists.
/// </summary>
public sealed record SearchSong
{
    public required Guid PlaylistId { get; init; }

    public required string PlaylistTitle { get; init; }

    public required Guid SongId { get; init; }

    public required string Title { get; init; }

    public required IReadOnlyList<string> Artists { get; init; }

    /// <summary>
    /// The chords found by the search that are in this song (their ids at the sources); empty when it was found by its
    /// title or artists only.
    /// </summary>
    public required IReadOnlyList<string> FoundChords { get; init; }
}

/// <summary>
/// The one search. A part not asked, or not searched for a query this short, is null.
/// </summary>
public sealed record SearchResponse
{
    public required string Query { get; init; }

    public IReadOnlyList<SearchSong>? Songs { get; init; }

    /// <summary>
    /// The chords found that are in none of the songs above: each item on its own, never grouped into songs.
    /// </summary>
    public IReadOnlyList<ChordsHeader>? Chords { get; init; }

    public IReadOnlyList<SearchSourceFailure>? ChordsFailures { get; init; }

    public IReadOnlyList<MusicArtist>? Artists { get; init; }

    public IReadOnlyList<MusicTrack>? Recordings { get; init; }

    /// <summary>
    /// The music services that did not answer.
    /// </summary>
    public IReadOnlyList<MusicService>? MusicFailures { get; init; }
}
