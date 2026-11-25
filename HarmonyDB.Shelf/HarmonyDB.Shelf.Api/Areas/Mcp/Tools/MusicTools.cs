using System.ComponentModel;
using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Media.Services;
using HarmonyDB.Shelf.Api.Areas.Music.Services;
using HarmonyDB.Shelf.Api.Areas.OAuth.Services;
using HarmonyDB.Shelf.Api.Areas.Playlists.Services;
using HarmonyDB.Shelf.Api.Clients.Audio;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Me;
using HarmonyDB.Shelf.Model.Media;
using HarmonyDB.Shelf.Model.Music;

namespace HarmonyDB.Shelf.Api.Areas.Mcp.Tools;

/// <summary>
/// The navigation of the music services, one set of tools with a <c>service</c> argument (yandex, youtube). Each
/// service has its own permissions: <c>mcp.{service}.anonymous</c> for its catalog (read anonymously; sound and video by
/// the service account) and <c>mcp.{service}.linked</c> for the person's own lists through the account they linked.
/// A tool is listed when some service's permission allows it; a call is allowed for the service asked by that
/// service's permission (<see cref="Allowed"/>), else refused with <c>permission_denied</c>.
/// </summary>
public static class MusicTools
{

    public static IEnumerable<McpTool> All() =>
    [
        Catalog("music_search",
            "Search a music service for tracks and artists. Track ids are what the other music tools, " +
            "add_recording_to_song and music_media_link take.",
            McpToolEffect.ReadOutside,
            async (
                ClaimsPrincipal user, PermissionCatalog permissions, MusicCatalog catalog,
                MusicService service,
                [Description("Search words.")] string query,
                int? offset = null,
                [Description("1-25, default 10.")] int? limit = null,
                CancellationToken cancellationToken = default) =>
            {
                var library = Allowed(user, permissions, service, false);
                if (string.IsNullOrWhiteSpace(query) || query.Length > 200)
                    throw ShelfException.BadRequest();
                var (tracks, artists) = await catalog.SearchAsync(service, query.Trim(), cancellationToken);
                return new { tracks = Tracks(tracks, offset, limit, library), artists = artists.Take(5).Select(Slim.Artist).ToList() };
            }),

        Catalog("music_artist",
            "An artist: short description, popular tracks, albums, similar artists (the first few of each).",
            McpToolEffect.ReadOutside,
            async (
                ClaimsPrincipal user, PermissionCatalog permissions, MusicCatalog catalog,
                MusicService service,
                string artistId,
                CancellationToken cancellationToken = default) =>
            {
                var library = Allowed(user, permissions, service, false);
                var page = await catalog.GetArtistAsync(user, service, artistId, cancellationToken);
                return new
                {
                    artist = Slim.Artist(page.Artist),
                    description = Slim.Short(page.Description, 300),
                    popularTracks = page.PopularTracks.Take(5).Select(x => Slim.Track(x, library)).ToList(),
                    albums = page.Albums.Take(5).Select(Slim.Album).ToList(),
                    albumsTotal = page.Albums.Count,
                    similarArtists = page.SimilarArtists.Take(5).Select(Slim.Artist).ToList(),
                };
            }),

        Catalog("music_album",
            "An album and its tracks.",
            McpToolEffect.ReadOutside,
            async (
                ClaimsPrincipal user, PermissionCatalog permissions, MusicCatalog catalog,
                MusicService service,
                string albumId,
                int? offset = null,
                [Description("1-25, default 20.")] int? limit = null,
                CancellationToken cancellationToken = default) =>
            {
                var library = Allowed(user, permissions, service, false);
                var page = await catalog.GetAlbumAsync(user, service, albumId, cancellationToken);
                return new { album = Slim.Album(page.Album), tracks = Tracks(page.Tracks, offset, limit ?? 20, library) };
            }),

        Catalog("music_track",
            "A track: title, artists, album, duration, whether it has lyrics.",
            McpToolEffect.ReadOutside,
            async (
                ClaimsPrincipal user, PermissionCatalog permissions, MusicCatalog catalog,
                MusicService service,
                string trackId,
                CancellationToken cancellationToken = default) =>
            {
                var library = Allowed(user, permissions, service, false);
                var page = await catalog.GetTrackAsync(user, service, trackId, cancellationToken);
                return new { track = Slim.Track(page.Track, library), page.LyricsAvailable };
            }),

        Catalog("music_lyrics",
            "A track's lyrics as text, and their writers.",
            McpToolEffect.ReadOutside,
            async (
                ClaimsPrincipal user, PermissionCatalog permissions, MusicCatalog catalog,
                MusicService service,
                string trackId,
                CancellationToken cancellationToken = default) =>
            {
                Allowed(user, permissions, service, false);
                var lyrics = await catalog.GetLyricsAsync(service, trackId, cancellationToken);
                return new { lyrics.Available, lyrics.Writers, text = string.Join('\n', lyrics.Lines.Select(x => x.Text)) };
            }),

        Catalog("music_similar",
            "Tracks, albums and artists similar to a track.",
            McpToolEffect.ReadOutside,
            async (
                ClaimsPrincipal user, PermissionCatalog permissions, MusicCatalog catalog,
                MusicService service,
                string trackId,
                [Description("1-25, default 10.")] int? limit = null,
                CancellationToken cancellationToken = default) =>
            {
                var library = Allowed(user, permissions, service, false);
                var similar = await catalog.GetSimilarAsync(user, service, trackId, cancellationToken);
                return new
                {
                    tracks = Tracks(similar.Tracks, 0, limit, library).Items,
                    albums = similar.Albums.Take(5).Select(Slim.Album).ToList(),
                    artists = similar.Artists.Take(5).Select(Slim.Artist).ToList(),
                };
            }),

        Catalog("music_radio",
            "A radio: tracks that go with a track, or with an artist (give one).",
            McpToolEffect.ReadOutside,
            async (
                ClaimsPrincipal user, PermissionCatalog permissions, MusicCatalog catalog,
                MusicService service,
                [Description("A trackId.")] string? trackId = null,
                [Description("Or an artistId.")] string? artistId = null,
                [Description("1-25, default 10.")] int? limit = null,
                CancellationToken cancellationToken = default) =>
            {
                var library = Allowed(user, permissions, service, false);
                if ((trackId == null) == (artistId == null))
                    throw ShelfException.BadRequest();
                var radio = trackId != null
                    ? await catalog.GetRadioAsync(user, service, trackId, cancellationToken)
                    : await catalog.GetArtistRadioAsync(user, service, artistId!, cancellationToken);
                return new { radio.Title, tracks = Tracks(radio.Tracks, 0, limit, library).Items };
            }),

        Catalog("music_chart",
            "The service's chart: the most played tracks now.",
            McpToolEffect.ReadOutside,
            async (
                ClaimsPrincipal user, PermissionCatalog permissions, MusicCatalog catalog,
                MusicService service,
                [Description("Country code (e.g. 'US') where the service has country charts.")] string? country = null,
                int? offset = null,
                [Description("1-25, default 10.")] int? limit = null,
                CancellationToken cancellationToken = default) =>
            {
                var library = Allowed(user, permissions, service, false);
                var chart = await catalog.GetChartAsync(user, service, country, cancellationToken);
                return new { tracks = Tracks(chart.Tracks, offset, limit, library) };
            }),

        Catalog("music_genres",
            "The service's genres and moods (id, title, group) for music_genre.",
            McpToolEffect.ReadOutside,
            async (
                ClaimsPrincipal user, PermissionCatalog permissions, MusicCatalog catalog,
                MusicService service,
                int? offset = null,
                [Description("1-25, default 25.")] int? limit = null,
                CancellationToken cancellationToken = default) =>
            {
                Allowed(user, permissions, service, false);
                var categories = await catalog.GetCategoriesAsync(service, cancellationToken);
                var all = categories.Groups
                    .SelectMany(g => g.Categories.SelectMany(c => c.Children.Prepend(c)).Select(c => new { c.Id, c.Title, group = g.Title }))
                    .ToList();
                return Slim.Paged(all, offset, limit, x => x, 25);
            }),

        Catalog("music_genre",
            "A genre or mood: its tracks (paged), and its first albums, playlists and artists.",
            McpToolEffect.ReadOutside,
            async (
                ClaimsPrincipal user, PermissionCatalog permissions, MusicCatalog catalog,
                MusicService service,
                string genreId,
                int? offset = null,
                [Description("1-25, default 10.")] int? limit = null,
                CancellationToken cancellationToken = default) =>
            {
                var library = Allowed(user, permissions, service, false);
                var page = await catalog.GetCategoryAsync(user, service, genreId, 0, cancellationToken);
                return new
                {
                    page.Title,
                    tracks = Tracks(page.Tracks, offset, limit, library),
                    albums = page.Albums.Take(5).Select(Slim.Album).ToList(),
                    playlists = page.Playlists.Take(5).Select(Slim.MusicPlaylist).ToList(),
                    artists = page.Artists.Take(5).Select(Slim.Artist).ToList(),
                };
            }),

        Catalog("music_playlist",
            "A public playlist of the service and its tracks.",
            McpToolEffect.ReadOutside,
            async (
                ClaimsPrincipal user, PermissionCatalog permissions, MusicCatalog catalog,
                MusicService service,
                string playlistId,
                int? offset = null,
                [Description("1-25, default 20.")] int? limit = null,
                CancellationToken cancellationToken = default) =>
            {
                var library = Allowed(user, permissions, service, false);
                var page = await catalog.GetPlaylistAsync(user, service, playlistId, AudioActing.Anonymous, cancellationToken);
                return new { playlist = Slim.MusicPlaylist(page.Playlist), tracks = Tracks(page.Tracks, offset, limit ?? 20, library) };
            }),

        Catalog("music_media_link",
            "A link to a track's sound (default) or video, to play or download; valid a few minutes (until expiresAt).",
            McpToolEffect.ReadOutside,
            (
                ClaimsPrincipal user, PermissionCatalog permissions, MediaService media, McpAddresses addresses,
                MusicService service,
                string trackId,
                [Description("audio (default) or video.")] MediaKind? kind = null) =>
            {
                Allowed(user, permissions, service, false);
                var signed = media.SignTrack(new() { Service = service, Id = trackId, Kind = kind ?? MediaKind.Audio });
                return signed with { Url = addresses.Url(signed.Url) };
            }),

        Linked("music_my_playlists",
            "This person's own playlists on the service, through the account they linked in their profile (error " +
            "no_music_link if none).",
            async (
                ClaimsPrincipal user, PermissionCatalog permissions, MusicMine mine,
                MusicService service,
                int? offset = null,
                [Description("1-25, default 20.")] int? limit = null,
                CancellationToken cancellationToken = default) =>
            {
                Allowed(user, permissions, service, true);
                var list = await mine.GetPlaylistsAsync(user, service, cancellationToken);
                return Slim.Paged(list.Playlists, offset, limit, Slim.MusicPlaylist, 20);
            }),

        Linked("music_my_playlist",
            "One of this person's own playlists on the service, with its tracks.",
            async (
                ClaimsPrincipal user, PermissionCatalog permissions, MusicMine mine,
                MusicService service,
                string playlistId,
                int? offset = null,
                [Description("1-25, default 20.")] int? limit = null,
                CancellationToken cancellationToken = default) =>
            {
                var library = Allowed(user, permissions, service, true);
                var page = await mine.GetPlaylistAsync(user, service, playlistId, cancellationToken);
                return new { playlist = Slim.MusicPlaylist(page.Playlist), tracks = Tracks(page.Tracks, offset, limit ?? 20, library) };
            }),

        Linked("music_my_likes",
            "The tracks this person liked on the service.",
            async (
                ClaimsPrincipal user, PermissionCatalog permissions, MusicMine mine,
                MusicService service,
                int? offset = null,
                [Description("1-25, default 20.")] int? limit = null,
                CancellationToken cancellationToken = default) =>
            {
                var library = Allowed(user, permissions, service, true);
                return Tracks((await mine.GetLikesAsync(user, service, cancellationToken)).Tracks, offset, limit ?? 20, library);
            }),

        Linked("music_my_history",
            "What this person listened to lately on the service.",
            async (
                ClaimsPrincipal user, PermissionCatalog permissions, MusicMine mine,
                MusicService service,
                int? offset = null,
                [Description("1-25, default 20.")] int? limit = null,
                CancellationToken cancellationToken = default) =>
            {
                var library = Allowed(user, permissions, service, true);
                return Tracks((await mine.GetHistoryAsync(user, service, cancellationToken)).Tracks, offset, limit ?? 20, library);
            }),
    ];

    /// <summary>
    /// Every service's music permission: a tool of another kind that makes the shelf read a service (its track, its
    /// playlist) for the person is listed with one of them, and its call checks the service's own
    /// (<see cref="MayRead"/>, <see cref="PlaylistReading"/>).
    /// </summary>
    public static string[] AnyMusic { get; } =
    [
        BuiltInPermissions.McpYandexAnonymous,
        BuiltInPermissions.McpYandexLinked,
        BuiltInPermissions.McpYoutubeAnonymous,
        BuiltInPermissions.McpYoutubeLinked,
    ];

    private static McpTool Catalog(string name, string description, McpToolEffect effect, Delegate handler)
        => new(name, description, effect, handler) { AnyOf = [BuiltInPermissions.McpYandexAnonymous, BuiltInPermissions.McpYoutubeAnonymous] };

    private static McpTool Linked(string name, string description, Delegate handler)
        => new(name, description, McpToolEffect.ReadOutside, handler) { AnyOf = [BuiltInPermissions.McpYandexLinked, BuiltInPermissions.McpYoutubeLinked] };

    /// <summary>
    /// A call that makes the shelf read what anyone may read of the service: allowed by either of the service's
    /// permissions.
    /// </summary>
    /// <exception cref="ShelfException">403 <c>permission_denied</c>, naming the permissions.</exception>
    public static void MayRead(ClaimsPrincipal user, PermissionCatalog permissions, MusicService service)
    {
        var (anonymous, linked) = (Permission(service, false), Permission(service, true));
        if (!permissions.Has(user, anonymous) && !permissions.Has(user, linked))
            throw Denied(linked, anonymous);
    }

    /// <summary>
    /// How a playlist of the service imported for the person is read: as the person, through the account they linked,
    /// with the service's linked permission; anonymously with its anonymous one only.
    /// </summary>
    /// <exception cref="ShelfException">403 <c>permission_denied</c>, naming the permissions, with neither.</exception>
    public static OriginReading PlaylistReading(ClaimsPrincipal user, PermissionCatalog permissions, MusicService service)
    {
        if (permissions.Has(user, Permission(service, true)))
            return OriginReading.AsImporter;

        MayRead(user, permissions, service);
        return OriginReading.Anonymous;
    }

    /// <summary>
    /// Whether the caller may use this service so (its catalog, or its own lists): the service's permission, in force for
    /// the caller. Answers whether tracks may say they are in the person's playlists (that is the library's to tell).
    /// </summary>
    /// <exception cref="ShelfException">403 <c>permission_denied</c>.</exception>
    private static bool Allowed(ClaimsPrincipal user, PermissionCatalog permissions, MusicService service, bool linked)
    {
        var permission = Permission(service, linked);
        if (!permissions.Has(user, permission))
            throw Denied(permission);

        return permissions.Has(user, BuiltInPermissions.McpLibraryRead);
    }

    private static string Permission(MusicService service, bool linked) => (service, linked) switch
    {
        (MusicService.Yandex, false) => BuiltInPermissions.McpYandexAnonymous,
        (MusicService.Yandex, true) => BuiltInPermissions.McpYandexLinked,
        (MusicService.Youtube, false) => BuiltInPermissions.McpYoutubeAnonymous,
        (MusicService.Youtube, true) => BuiltInPermissions.McpYoutubeLinked,
        _ => throw ShelfException.BadRequest(),
    };

    /// <summary>
    /// The refusal of a call the person did not allow: its details name what it needs (one of them, when several).
    /// </summary>
    private static ShelfException Denied(params string[] permissions)
        => new(StatusCodes.Status403Forbidden, ErrorCodes.PermissionDenied, new Dictionary<string, string> { ["permission"] = string.Join(' ', permissions) });

    private static Slim.Page<object> Tracks(IReadOnlyList<MusicTrack> tracks, int? offset, int? limit, bool library)
        => Slim.Paged(tracks, offset, limit, x => Slim.Track(x, library));
}
