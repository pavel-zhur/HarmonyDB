namespace HarmonyDB.Shelf.Model.Playlists;

/// <summary>
/// Where chords are among the person's own playlists (those they are a member of; not those they follow): for the
/// song page's "to a playlist" and its "here" keys of other playlists.
/// </summary>
public record ChordsPlaces
{
    /// <summary>
    /// The person's playlists, their system playlists first, then by when they joined: for the "to a playlist" dialog
    /// (the ones with <see cref="MyPlaylist.CanAdd"/>), each marked by <see cref="Places"/> whether it holds the chords.
    /// </summary>
    public required IReadOnlyList<MyPlaylist> Playlists { get; init; }

    /// <summary>
    /// Each of the chords asked, in each of those playlists that holds it: the song it is in there.
    /// </summary>
    public required IReadOnlyList<ChordsPlace> Places { get; init; }

    /// <summary>
    /// The person's "here" keys for the chords asked, in each of their playlists that has one.
    /// </summary>
    public required IReadOnlyList<ChordsHereKey> Keys { get; init; }
}

public record MyPlaylist
{
    public required Guid Id { get; init; }

    /// <summary>
    /// For a system playlist the frontend words the title by <see cref="SystemKind"/>.
    /// </summary>
    public required string Title { get; init; }

    public required PlaylistSystemKind SystemKind { get; init; }

    /// <summary>
    /// Whether the person may add to it.
    /// </summary>
    public required bool CanAdd { get; init; }
}

public record ChordsPlace
{
    /// <summary>
    /// The chords' id.
    /// </summary>
    public required string Chords { get; init; }

    public required Guid PlaylistId { get; init; }

    public required Guid SongId { get; init; }
}

public record ChordsHereKey
{
    public required string Chords { get; init; }

    public required Guid PlaylistId { get; init; }

    /// <summary>
    /// −11 … +11.
    /// </summary>
    public required int Semitones { get; init; }
}
