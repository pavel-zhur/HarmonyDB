using System.Security.Claims;
using System.Security.Cryptography;
using HarmonyDB.Shelf.Api.Areas.Playlists.Services;
using HarmonyDB.Shelf.Api.Common;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Share;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlaylistErrorCodes = HarmonyDB.Shelf.Model.Playlists.PlaylistErrorCodes;

namespace HarmonyDB.Shelf.Api.Areas.Share.Services;

/// <summary>
/// Links (and their QR codes) to a song of a playlist, to chords, and to a playlist of <c>link</c> access; and what
/// each shows to whoever opens it, signed in or not (the concept's "links and guests"): a song's link shows the song
/// and its chords, and its playlist only when the playlist is not private ("from a private playlist" otherwise);
/// a playlist's link opens the playlist while its access is <c>link</c> (or public). One link per thing: sharing it
/// again gives the same link.
/// </summary>
public sealed class ShareService(ShelfDbContext db, PlaylistGate gate, SongPages songPages, Views views, ChordsLookup chordsRows, TimeProvider time)
{
    public async Task<ShareLinkItem> CreateAsync(ClaimsPrincipal user, CreateShareRequest request, CancellationToken cancellationToken)
    {
        var me = user.GetAccountId()!.Value;
        ShareLink link;
        switch (request.Kind)
        {
            case ShareKind.Song when request.SongId is { } songId:
                var song = await db.PlaylistSongs.SingleOrDefaultAsync(x => x.Id == songId, cancellationToken)
                           ?? throw ShelfException.NotFound(PlaylistErrorCodes.NoSuchSong);
                await gate.ReadSongAsync(me, song.PlaylistId, songId, request.Share, cancellationToken);
                link = new() { Token = string.Empty, Kind = ShareTargetKind.Song, SongId = songId };
                break;
            case ShareKind.Playlist when request.PlaylistId is { } playlistId:
                var view = await gate.ReadAsync(me, playlistId, request.Share, cancellationToken);
                if (view.Playlist.Access != PlaylistAccess.Link)
                    throw ShelfException.BadRequest(PlaylistErrorCodes.NotLinkAccess);
                link = new() { Token = string.Empty, Kind = ShareTargetKind.Playlist, PlaylistId = playlistId };
                break;
            case ShareKind.Chords when request.Chords != null:
                var (chords, _) = await chordsRows.GetOrCreateAsync(me, request.Chords, true, cancellationToken);
                link = new() { Token = string.Empty, Kind = ShareTargetKind.Chords, ChordsId = chords.Id };
                break;
            default:
                throw ShelfException.BadRequest();
        }

        var existing = await FindAsync(link, cancellationToken);
        if (existing != null)
            return new() { Token = existing.Token, Kind = request.Kind };

        link.Token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(18));
        link.CreatedById = me;
        link.CreatedAt = time.GetUtcNow();
        db.ShareLinks.Add(link);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new() { Token = link.Token, Kind = request.Kind };
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // made by someone else at the same moment: theirs is the one
            db.Entry(link).State = EntityState.Detached;
            return new() { Token = (await FindAsync(link, cancellationToken))!.Token, Kind = request.Kind };
        }
    }

    public async Task<ShareView> OpenAsync(ClaimsPrincipal user, string token, CancellationToken cancellationToken)
    {
        var viewer = user.GetAccountId();
        var link = await db.ShareLinks.Include(x => x.Chords).AsNoTracking().SingleOrDefaultAsync(x => x.Token == token, cancellationToken)
                   ?? throw ShelfException.NotFound(PlaylistErrorCodes.NoSuchLink);
        try
        {
            switch (link.Kind)
            {
                case ShareTargetKind.Playlist:
                    var view = await gate.ReadAsync(viewer, link.PlaylistId!.Value, token, cancellationToken);
                    return new() { Kind = ShareKind.Playlist, Playlist = await views.SummaryAsync(viewer, view.Playlist, cancellationToken) };
                case ShareTargetKind.Song:
                    // the song, or the one it was linked into since
                    var song = await gate.SharedSongAsync(link.SongId!.Value, cancellationToken);
                    if (song == null || song.DeletedAt != null)
                        throw ShelfException.NotFound(PlaylistErrorCodes.NoSuchLink);
                    return new() { Kind = ShareKind.Song, Song = await songPages.GetAsync(user, song.PlaylistId, song.Id, token, cancellationToken) };
                default:
                    var chords = link.Chords!.SourceId;
                    return new() { Kind = ShareKind.Chords, Chords = (await views.ChordsAsync(user, [chords], cancellationToken))[chords] };
            }
        }
        catch (ShelfException exception) when (exception.StatusCode == StatusCodes.Status404NotFound)
        {
            // what the link led to is gone, or no longer open by it
            throw ShelfException.NotFound(PlaylistErrorCodes.NoSuchLink);
        }
    }

    private Task<ShareLink?> FindAsync(ShareLink link, CancellationToken cancellationToken)
        => db.ShareLinks.AsNoTracking().SingleOrDefaultAsync(
            x => x.Kind == link.Kind && x.PlaylistId == link.PlaylistId && x.SongId == link.SongId && x.ChordsId == link.ChordsId,
            cancellationToken);
}
