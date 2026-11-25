namespace HarmonyDB.Shelf.Api.Clients.Audio;

/// <summary>
/// The music service's links and playlists, for importing a playlist by its link.
/// </summary>
public sealed partial class AudioClient
{
    /// <summary>
    /// What a link of either service names (<c>GET /internal/resolve</c>); 400 <c>link_unknown</c> for a link it does not
    /// know.
    /// </summary>
    public Task<AudioResolved> ResolveAsync(string url, CancellationToken cancellationToken)
        => SendAsync<AudioResolved>(HttpMethod.Get, $"internal/resolve?url={Uri.EscapeDataString(url)}", null, cancellationToken);

    /// <summary>
    /// How long asking a whole playlist anew may take: the music service reads a long one in many pages.
    /// </summary>
    public static readonly TimeSpan PlaylistTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// A playlist of either service by its link, asked anew (<c>GET /internal/playlists/by-link</c>).
    /// </summary>
    public Task<AudioAnswer<AudioLinkedPlaylist>> GetPlaylistByLinkAsync(string url, AudioActing acting, CancellationToken cancellationToken)
        => SendAsync<AudioAnswer<AudioLinkedPlaylist>>(HttpMethod.Get, $"internal/playlists/by-link?url={Uri.EscapeDataString(url)}&fresh=true", acting, PlaylistTimeout, cancellationToken);
}

public sealed record AudioResolved
{
    /// <summary>
    /// <c>yandex</c> or <c>youtube</c>.
    /// </summary>
    public required string Service { get; init; }

    /// <summary>
    /// <c>track</c>, <c>album</c>, <c>artist</c> or <c>playlist</c>.
    /// </summary>
    public required string Type { get; init; }

    public required string Id { get; init; }
}

/// <summary>
/// The music service's envelope of a navigation answer.
/// </summary>
public sealed record AudioAnswer<T>
{
    public DateTimeOffset StoredAt { get; init; }

    public bool FetchedNow { get; init; }

    public required T Data { get; init; }
}

public sealed record AudioLinkedPlaylist
{
    public required string Service { get; init; }

    public required string Id { get; init; }

    public AudioPlaylistPage? Yandex { get; init; }

    public AudioPlaylistPage? Youtube { get; init; }
}

/// <summary>
/// A playlist with its tracks: the part of either service's answer that both have.
/// </summary>
public sealed record AudioPlaylistPage
{
    public required AudioPlaylistHeader Playlist { get; init; }

    public IReadOnlyList<AudioTrack> Tracks { get; init; } = [];
}

public sealed record AudioPlaylistHeader
{
    public string? Title { get; init; }

    public string? Description { get; init; }
}

public sealed record AudioTrack
{
    /// <summary>
    /// The track's id on the service; YouTube gives none for a track it no longer has.
    /// </summary>
    public string? Id { get; init; }

    public string? Title { get; init; }

    /// <summary>
    /// Yandex: the version of the track ("live", "remix" …).
    /// </summary>
    public string? Version { get; init; }

    public long? DurationMs { get; init; }

    public string? Cover { get; init; }

    public IReadOnlyList<AudioArtist> Artists { get; init; } = [];
}

public sealed record AudioArtist
{
    public string? Name { get; init; }
}
