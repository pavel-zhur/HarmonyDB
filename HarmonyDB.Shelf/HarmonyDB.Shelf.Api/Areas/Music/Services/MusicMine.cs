using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Me.Services;
using HarmonyDB.Shelf.Api.Clients.Audio;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model.Me;
using HarmonyDB.Shelf.Model.Music;
using static HarmonyDB.Shelf.Api.Clients.Audio.AudioShapes;

namespace HarmonyDB.Shelf.Api.Areas.Music.Services;

/// <summary>
/// The person's own lists on a music service — their playlists, likes and history — read with the account they linked
/// in the profile, so that they may make a playlist of the shelf from one.
/// </summary>
public sealed class MusicMine(MusicLinkStore links, MusicCatalog catalog)
{
    public async Task<MusicPlaylistList> GetPlaylistsAsync(ClaimsPrincipal user, MusicService service, CancellationToken cancellationToken)
    {
        var acting = await ActingAsync(user, service, cancellationToken);
        return service == MusicService.Yandex
            ? new() { Playlists = MusicShapes.Playlists((await catalog.GetAsync<YPlaylists>(service, "me/playlists", cancellationToken, acting)).Playlists) }
            : new() { Playlists = MusicShapes.Playlists((await catalog.GetAsync<TPlaylists>(service, "me/playlists", cancellationToken, acting)).Playlists) };
    }

    public async Task<MusicPlaylistPage> GetPlaylistAsync(ClaimsPrincipal user, MusicService service, string id, CancellationToken cancellationToken)
        => await catalog.GetPlaylistAsync(user, service, id, await ActingAsync(user, service, cancellationToken), cancellationToken);

    public async Task<MusicTrackList> GetLikesAsync(ClaimsPrincipal user, MusicService service, CancellationToken cancellationToken)
    {
        var acting = await ActingAsync(user, service, cancellationToken);
        var tracks = service == MusicService.Yandex
            ? MusicShapes.Tracks((await catalog.GetAsync<YLikes>(service, "me/likes", cancellationToken, acting)).Tracks)
            : MusicShapes.Tracks((await catalog.GetAsync<TLikes>(service, "me/likes", cancellationToken, acting)).Tracks);
        return await catalog.Tracks(user, tracks, cancellationToken);
    }

    public async Task<MusicTrackList> GetHistoryAsync(ClaimsPrincipal user, MusicService service, CancellationToken cancellationToken)
    {
        var acting = await ActingAsync(user, service, cancellationToken);
        var tracks = service == MusicService.Yandex
            ? MusicShapes.Tracks((await catalog.GetAsync<YHistory>(service, "me/history", cancellationToken, acting)).Tabs?
                .SelectMany(x => x.Items ?? []).SelectMany(x => x.Tracks ?? []))
            : MusicShapes.Tracks((await catalog.GetAsync<TTracks>(service, "me/history", cancellationToken, acting)).Tracks);
        return await catalog.Tracks(user, tracks, cancellationToken);
    }

    private async Task<AudioActing> ActingAsync(ClaimsPrincipal user, MusicService service, CancellationToken cancellationToken)
        => await links.GetActingAsync(user.GetAccountId()!.Value, service, cancellationToken)
           ?? throw ShelfException.Conflict(MusicErrorCodes.NoMusicLink);
}
