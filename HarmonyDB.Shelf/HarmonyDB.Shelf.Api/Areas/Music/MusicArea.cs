using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Music.Services;
using HarmonyDB.Shelf.Api.Clients.Audio;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Me;
using HarmonyDB.Shelf.Model.Music;

namespace HarmonyDB.Shelf.Api.Areas.Music;

/// <summary>
/// The navigation of the music services, Yandex Music and YouTube Music: artists, albums, tracks, similar, radio, lyrics,
/// the chart, genres and moods, their playlists; the home page's blocks from the person's playlists; the person's own
/// lists on a service they linked. Open to guests, except the person's own lists and blocks.
/// </summary>
public sealed class MusicArea : IShelfArea
{
    /// <summary>
    /// A music service that is not there yet: its pages answer "soon".
    /// </summary>
    private const string SoonService = "vk";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddMusicCatalog(configuration);

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/music/{service}").WithTags("Music");

        group.MapGet("/artists/{id}", (ClaimsPrincipal user, string service, string id, MusicCatalog catalog, CancellationToken cancellationToken)
            => catalog.GetArtistAsync(user, Parse(service), id, cancellationToken)).WithName("GetMusicArtist");

        group.MapGet("/artists/{id}/albums", (string service, string id, int? page, MusicCatalog catalog, CancellationToken cancellationToken)
            => catalog.GetArtistAlbumsAsync(Parse(service), id, Page(page), cancellationToken)).WithName("GetMusicArtistAlbums");

        group.MapGet("/artists/{id}/tracks", (ClaimsPrincipal user, string service, string id, int? page, MusicCatalog catalog, CancellationToken cancellationToken)
            => catalog.GetArtistTracksAsync(user, Parse(service), id, Page(page), cancellationToken)).WithName("GetMusicArtistTracks");

        group.MapGet("/artists/{id}/similar", (string service, string id, MusicCatalog catalog, CancellationToken cancellationToken)
            => catalog.GetSimilarArtistsAsync(Parse(service), id, cancellationToken)).WithName("GetMusicSimilarArtists");

        group.MapGet("/artists/{id}/radio", (ClaimsPrincipal user, string service, string id, MusicCatalog catalog, CancellationToken cancellationToken)
            => catalog.GetArtistRadioAsync(user, Parse(service), id, cancellationToken)).WithName("GetMusicArtistRadio");

        group.MapGet("/albums/{id}", (ClaimsPrincipal user, string service, string id, MusicCatalog catalog, CancellationToken cancellationToken)
            => catalog.GetAlbumAsync(user, Parse(service), id, cancellationToken)).WithName("GetMusicAlbum");

        group.MapGet("/tracks/{id}", (ClaimsPrincipal user, string service, string id, MusicCatalog catalog, CancellationToken cancellationToken)
            => catalog.GetTrackAsync(user, Parse(service), id, cancellationToken)).WithName("GetMusicTrack");

        group.MapGet("/tracks/{id}/similar", (ClaimsPrincipal user, string service, string id, MusicCatalog catalog, CancellationToken cancellationToken)
            => catalog.GetSimilarAsync(user, Parse(service), id, cancellationToken)).WithName("GetMusicSimilar");

        group.MapGet("/tracks/{id}/radio", (ClaimsPrincipal user, string service, string id, MusicCatalog catalog, CancellationToken cancellationToken)
            => catalog.GetRadioAsync(user, Parse(service), id, cancellationToken)).WithName("GetMusicRadio");

        group.MapGet("/tracks/{id}/lyrics", (string service, string id, MusicCatalog catalog, CancellationToken cancellationToken)
            => catalog.GetLyricsAsync(Parse(service), id, cancellationToken)).WithName("GetMusicLyrics");

        group.MapGet("/playlists/{id}", (ClaimsPrincipal user, string service, string id, MusicCatalog catalog, CancellationToken cancellationToken)
            => catalog.GetPlaylistAsync(user, Parse(service), id, AudioActing.Anonymous, cancellationToken)).WithName("GetMusicPlaylist");

        group.MapGet("/chart", (ClaimsPrincipal user, string service, string? country, MusicCatalog catalog, CancellationToken cancellationToken)
            => catalog.GetChartAsync(user, Parse(service), country, cancellationToken)).WithName("GetMusicChart");

        group.MapGet("/categories", (string service, MusicCatalog catalog, CancellationToken cancellationToken)
            => catalog.GetCategoriesAsync(Parse(service), cancellationToken)).WithName("GetMusicCategories");

        group.MapGet("/categories/{id}", (ClaimsPrincipal user, string service, string id, int? offset, MusicCatalog catalog, CancellationToken cancellationToken)
            => catalog.GetCategoryAsync(user, Parse(service), id, Math.Max(0, offset ?? 0), cancellationToken)).WithName("GetMusicCategory");

        // the home page's blocks from the person's playlists (empty for a guest)
        group.MapGet("/home/artists", (ClaimsPrincipal user, string service, MusicHome home, CancellationToken cancellationToken)
            => home.GetArtistsAsync(user, Parse(service), cancellationToken)).WithName("GetMusicHomeArtists");

        group.MapGet("/home/similar", (ClaimsPrincipal user, string service, Guid? playlistId, MusicHome home, CancellationToken cancellationToken)
            => home.GetSimilarAsync(user, Parse(service), playlistId, cancellationToken)).WithName("GetMusicHomeSimilar");

        // the person's own lists, with the account linked in the profile
        group.MapGet("/me/playlists", (ClaimsPrincipal user, string service, MusicMine mine, CancellationToken cancellationToken)
            => mine.GetPlaylistsAsync(user, Parse(service), cancellationToken)).RequireSignIn().WithName("GetMyMusicPlaylists");

        group.MapGet("/me/playlists/{id}", (ClaimsPrincipal user, string service, string id, MusicMine mine, CancellationToken cancellationToken)
            => mine.GetPlaylistAsync(user, Parse(service), id, cancellationToken)).RequireSignIn().WithName("GetMyMusicPlaylist");

        group.MapGet("/me/likes", (ClaimsPrincipal user, string service, MusicMine mine, CancellationToken cancellationToken)
            => mine.GetLikesAsync(user, Parse(service), cancellationToken)).RequireSignIn().WithName("GetMyMusicLikes");

        group.MapGet("/me/history", (ClaimsPrincipal user, string service, MusicMine mine, CancellationToken cancellationToken)
            => mine.GetHistoryAsync(user, Parse(service), cancellationToken)).RequireSignIn().WithName("GetMyMusicHistory");
    }

    /// <summary>
    /// The service of the address: <c>yandex</c> or <c>youtube</c>; one marked "soon" answers 501 <c>not_yet</c>.
    /// </summary>
    public static MusicService Parse(string service) => service switch
    {
        "yandex" => MusicService.Yandex,
        "youtube" => MusicService.Youtube,
        SoonService => throw new ShelfException(StatusCodes.Status501NotImplemented, ErrorCodes.NotYet),
        _ => throw ShelfException.NotFound(MusicErrorCodes.NoSuchService),
    };

    private static int Page(int? page) => Math.Max(0, page ?? 0);
}

public static class MusicCatalogExtensions
{
    /// <summary>
    /// The music services' navigation for the browser API (<see cref="MusicCatalog"/> and what stands beside it); safe
    /// to call from every area that needs it.
    /// </summary>
    public static IServiceCollection AddMusicCatalog(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(x => x.ServiceType == typeof(MusicCatalog)))
            return services;

        services.AddMemoryCache();
        services.AddAudioMusicClients(configuration);
        services.AddScoped<TrackMarks>();
        services.AddScoped<MusicCatalog>();
        services.AddScoped<MusicHome>();
        services.AddScoped<MusicMine>();
        return services;
    }
}
