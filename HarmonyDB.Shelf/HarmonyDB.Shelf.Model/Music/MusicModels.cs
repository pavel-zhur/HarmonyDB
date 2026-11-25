using HarmonyDB.Shelf.Model.Me;

namespace HarmonyDB.Shelf.Model.Music;

/// <summary>
/// An artist named on a track or an album; the id is missing where the service does not give one.
/// </summary>
public sealed record MusicArtistRef
{
    public string? Id { get; init; }

    public string? Name { get; init; }
}

public sealed record MusicArtist
{
    public required MusicService Service { get; init; }

    public required string Id { get; init; }

    public string? Name { get; init; }

    public string? CoverUrl { get; init; }
}

public sealed record MusicAlbumRef
{
    public string? Id { get; init; }

    public string? Title { get; init; }
}

public sealed record MusicAlbum
{
    public required MusicService Service { get; init; }

    public required string Id { get; init; }

    public string? Title { get; init; }

    /// <summary>The service's type of it: an album, a single, a compilation … as the service names it.</summary>
    public string? Type { get; init; }

    public int? Year { get; init; }

    public string? CoverUrl { get; init; }

    public int? TrackCount { get; init; }

    public required IReadOnlyList<MusicArtistRef> Artists { get; init; }
}

/// <summary>
/// A recording of a music service, with what this person has of it here.
/// </summary>
public sealed record MusicTrack
{
    public required MusicService Service { get; init; }

    public required string Id { get; init; }

    public string? Title { get; init; }

    /// <summary>The service's version of the track ("Live", "Remastered" …).</summary>
    public string? Version { get; init; }

    public required IReadOnlyList<MusicArtistRef> Artists { get; init; }

    public MusicAlbumRef? Album { get; init; }

    public int? DurationMs { get; init; }

    public string? CoverUrl { get; init; }

    /// <summary>Whether it is a video (YouTube).</summary>
    public bool? IsVideo { get; init; }

    /// <summary>False when the service says it cannot be played.</summary>
    public bool? Available { get; init; }

    /// <summary>Whether it is already in a playlist of this person (always false for a guest).</summary>
    public required bool InMyPlaylists { get; init; }

    /// <summary>Whether there is an analysis of it (chords recognized from it), open to this person.</summary>
    public required bool HasAnalysis { get; init; }

    /// <summary>
    /// That analysis's chords, as they are opened (<c>GET /api/chords/{id}</c>): the newest, when there are several.
    /// Given when <see cref="HasAnalysis"/> is true.
    /// </summary>
    public string? AnalysisChords { get; init; }
}

public sealed record MusicPlaylist
{
    public required MusicService Service { get; init; }

    public required string Id { get; init; }

    public string? Title { get; init; }

    public string? CoverUrl { get; init; }

    public int? TrackCount { get; init; }

    public string? Owner { get; init; }
}

public sealed record MusicArtistPage
{
    public required MusicArtist Artist { get; init; }

    public string? Description { get; init; }

    public required IReadOnlyList<MusicTrack> PopularTracks { get; init; }

    public required IReadOnlyList<MusicAlbum> Albums { get; init; }

    /// <summary>Singles and other releases beside the albums.</summary>
    public required IReadOnlyList<MusicAlbum> OtherReleases { get; init; }

    public required IReadOnlyList<MusicTrack> Videos { get; init; }

    public required IReadOnlyList<MusicArtist> SimilarArtists { get; init; }
}

public sealed record MusicAlbumPage
{
    public required MusicAlbum Album { get; init; }

    public required IReadOnlyList<MusicTrack> Tracks { get; init; }
}

public sealed record MusicTrackPage
{
    public required MusicTrack Track { get; init; }

    public required bool LyricsAvailable { get; init; }
}

/// <summary>
/// A list of tracks: a radio, an artist's tracks, the person's likes or history …
/// </summary>
public sealed record MusicTrackList
{
    public string? Title { get; init; }

    public required IReadOnlyList<MusicTrack> Tracks { get; init; }
}

public sealed record MusicAlbumList
{
    public required IReadOnlyList<MusicAlbum> Albums { get; init; }
}

public sealed record MusicArtistList
{
    public required IReadOnlyList<MusicArtist> Artists { get; init; }
}

public sealed record MusicPlaylistList
{
    public required IReadOnlyList<MusicPlaylist> Playlists { get; init; }
}

/// <summary>
/// Things like the track: tracks, and on YouTube also albums and artists.
/// </summary>
public sealed record MusicSimilar
{
    public required IReadOnlyList<MusicTrack> Tracks { get; init; }

    public required IReadOnlyList<MusicAlbum> Albums { get; init; }

    public required IReadOnlyList<MusicArtist> Artists { get; init; }
}

public sealed record MusicChart
{
    public required IReadOnlyList<MusicTrack> Tracks { get; init; }

    public required IReadOnlyList<MusicArtist> Artists { get; init; }
}

/// <summary>
/// A genre or a mood. Yandex's genres have sub-genres.
/// </summary>
public sealed record MusicCategory
{
    public required string Id { get; init; }

    public string? Title { get; init; }

    public string? Color { get; init; }

    public string? ImageUrl { get; init; }

    public required IReadOnlyList<MusicCategory> Children { get; init; }
}

public sealed record MusicCategoryGroup
{
    public string? Title { get; init; }

    public required IReadOnlyList<MusicCategory> Categories { get; init; }
}

public sealed record MusicCategories
{
    public required IReadOnlyList<MusicCategoryGroup> Groups { get; init; }
}

public sealed record MusicCategoryPage
{
    public string? Title { get; init; }

    public required IReadOnlyList<MusicAlbum> Albums { get; init; }

    public required IReadOnlyList<MusicPlaylist> Playlists { get; init; }

    public required IReadOnlyList<MusicTrack> Tracks { get; init; }

    public required IReadOnlyList<MusicArtist> Artists { get; init; }
}

public sealed record MusicPlaylistPage
{
    public required MusicPlaylist Playlist { get; init; }

    public required IReadOnlyList<MusicTrack> Tracks { get; init; }
}

public sealed record MusicLyricWord
{
    public required int StartMs { get; init; }

    public required string Text { get; init; }
}

public sealed record MusicLyricLine
{
    /// <summary>Null when the lyrics are not timed.</summary>
    public int? StartMs { get; init; }

    public int? EndMs { get; init; }

    public required string Text { get; init; }

    /// <summary>By words, where the service has them.</summary>
    public IReadOnlyList<MusicLyricWord>? Words { get; init; }
}

public sealed record MusicLyrics
{
    public required bool Available { get; init; }

    public required bool Synced { get; init; }

    public IReadOnlyList<string>? Writers { get; init; }

    public required IReadOnlyList<MusicLyricLine> Lines { get; init; }
}

/// <summary>
/// "Artists you play" on the home page: from the chords and recordings in this person's playlists.
/// </summary>
public sealed record MusicHomeArtists
{
    public required IReadOnlyList<MusicArtist> Artists { get; init; }
}

/// <summary>
/// "Similar to your playlist" on the home page.
/// </summary>
public sealed record MusicHomeSimilar
{
    /// <summary>The playlist it is similar to; null when the person has none with recordings on this service.</summary>
    public Guid? PlaylistId { get; init; }

    public string? PlaylistTitle { get; init; }

    public required IReadOnlyList<MusicTrack> Tracks { get; init; }
}
