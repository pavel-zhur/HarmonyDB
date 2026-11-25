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

    /// <summary>
    /// Chords only: a link that shows the chords to whoever holds it as they are open to the caller now, even where a
    /// guest could not open them (the caller's access is kept with the link, on the server). One such link per person
    /// and chords: asking again gives the same link, carrying the caller's access as it is then. Its sharer removes it
    /// with <c>DELETE /api/share/{token}</c>.
    /// </summary>
    public bool CarriesAccess { get; init; }
}

public record ShareLinkItem
{
    /// <summary>
    /// The link's token: the app opens it at <c>/s/{token}</c> and reads it by <c>GET /api/share/{token}</c>.
    /// </summary>
    public required string Token { get; init; }

    public required ShareKind Kind { get; init; }

    /// <summary>
    /// Whether the link carries its sharer's access (<see cref="CreateShareRequest.CarriesAccess"/>).
    /// </summary>
    public bool CarriesAccess { get; init; }

    /// <summary>
    /// The link's full address on the public page (<c>&lt;its origin&gt;/s/{token}</c>); null when there is no public page.
    /// </summary>
    public string? PublicAddress { get; init; }
}

/// <summary>
/// The caller's own link that carries their access to the chords asked, if they have made one.
/// </summary>
public record MyCarryingLink
{
    public ShareLinkItem? Link { get; init; }
}

/// <summary>
/// What a link opens. A playlist: its summary (its songs: <c>GET /api/playlists/{id}/songs?share={token}</c>). A song:
/// its page; from a private playlist without the playlist. Chords: the chords, their content by
/// <c>GET /api/share/{token}/chords</c> and their media by <c>POST /api/share/{token}/media</c> — for a link that
/// carries its sharer's access, as they are open to the sharer; otherwise as they are open to the one who opens it.
/// </summary>
public record ShareView
{
    public required ShareKind Kind { get; init; }

    public PlaylistSummary? Playlist { get; init; }

    public SongPage? Song { get; init; }

    public ChordsAnswer? Chords { get; init; }

    /// <summary>
    /// Whether the link carries its sharer's access.
    /// </summary>
    public bool CarriesAccess { get; init; }
}
