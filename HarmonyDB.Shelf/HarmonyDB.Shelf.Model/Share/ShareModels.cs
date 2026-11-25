using HarmonyDB.Shelf.Model.Chords;
using HarmonyDB.Shelf.Model.Playlists;

namespace HarmonyDB.Shelf.Model.Share;

public enum ShareKind
{
    /// <summary>A playlist of <c>link</c> access.</summary>
    Playlist,

    /// <summary>A song of a playlist.</summary>
    Song,

    /// <summary>Chords alone.</summary>
    Chords,
}

public record CreateShareRequest
{
    public required ShareKind Kind { get; init; }

    public Guid? PlaylistId { get; init; }

    public Guid? SongId { get; init; }

    /// <summary>
    /// The chords' id.
    /// </summary>
    public string? Chords { get; init; }

    /// <summary>
    /// The link the caller opened the song by, if it was.
    /// </summary>
    public string? Share { get; init; }
}

public record ShareLinkItem
{
    /// <summary>
    /// The link's token: the app opens it at <c>/s/{token}</c> and reads it by <c>GET /api/share/{token}</c>.
    /// </summary>
    public required string Token { get; init; }

    public required ShareKind Kind { get; init; }
}

/// <summary>
/// What a link opens. A playlist: its summary (its songs: <c>GET /api/playlists/{id}/songs?share={token}</c>). A song:
/// its page; from a private playlist without the playlist. Chords: the chords (their content from the chords' area).
/// </summary>
public record ShareView
{
    public required ShareKind Kind { get; init; }

    public PlaylistSummary? Playlist { get; init; }

    public SongPage? Song { get; init; }

    public ChordsAnswer? Chords { get; init; }
}
