using HarmonyDB.Shelf.Model.Chords;
using HarmonyDB.Shelf.Model.Me;
using HarmonyDB.Shelf.Model.Music;

namespace HarmonyDB.Shelf.Model.Recordings;

/// <summary>
/// The recordings found automatically on both music services for an item of chords, by its title and artists, best
/// first. They are guesses: the services and the sources name songs differently.
/// </summary>
public sealed record ChordsRecordings
{
    public required string Id { get; init; }

    public required ChordsState State { get; init; }

    /// <summary>
    /// False when a music service did not answer the search: what was found on the other is given, and the search is
    /// made again next time.
    /// </summary>
    public required bool Complete { get; init; }

    public required IReadOnlyList<MusicTrack> Found { get; init; }
}

/// <summary>
/// A recording of a song in a playlist.
/// </summary>
public sealed record SongRecording
{
    /// <summary>The song's item that holds it.</summary>
    public required Guid ItemId { get; init; }

    public required MusicTrack Track { get; init; }

    /// <summary>Who added it; null when it was put in automatically (or its adder's account is gone).</summary>
    public Guid? AddedBy { get; init; }

    /// <summary>Whether this person may remove it.</summary>
    public required bool CanRemove { get; init; }
}

/// <summary>
/// The recordings of a song in a playlist, as the playlist keeps them, and the candidates found for its chords that
/// are not among them.
/// </summary>
public sealed record SongRecordings
{
    public required Guid SongId { get; init; }

    public required Guid PlaylistId { get; init; }

    /// <summary>
    /// The service the playlist looks on first; the other is the fallback. Its recordings come first.
    /// </summary>
    public required MusicService SearchFirst { get; init; }

    public required IReadOnlyList<SongRecording> Recordings { get; init; }

    /// <summary>
    /// The recording this person chose to listen to, when they chose one.
    /// </summary>
    public Guid? MyChoice { get; init; }

    /// <summary>
    /// Found for the song's chords and not among its recordings ("find another recording").
    /// </summary>
    public required IReadOnlyList<MusicTrack> Candidates { get; init; }

    /// <summary>Whether this person may add recordings here.</summary>
    public required bool CanAdd { get; init; }
}

public sealed record AddRecordingRequest
{
    public required MusicService Service { get; init; }

    /// <summary>The track's id on the service.</summary>
    public required string TrackId { get; init; }
}

public sealed record RecordingChoiceRequest
{
    /// <summary>The recording item to listen to; null for none.</summary>
    public Guid? ItemId { get; init; }
}
