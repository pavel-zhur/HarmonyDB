using System.Security.Claims;
using System.Security.Cryptography;
using HarmonyDB.Shelf.Api.Areas.Chords.Services;
using HarmonyDB.Shelf.Api.Areas.Playlists.Services;
using HarmonyDB.Shelf.Api.Areas.PublicPage.Services;
using HarmonyDB.Shelf.Api.Common;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Chords;
using HarmonyDB.Shelf.Model.Share;
using HarmonyDB.Source.Api.Model.V2;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using PlaylistErrorCodes = HarmonyDB.Shelf.Model.Playlists.PlaylistErrorCodes;

namespace HarmonyDB.Shelf.Api.Areas.Share.Services;

/// <summary>
/// Links (and their QR codes) to a song of a playlist, to chords, and to a playlist of <c>link</c> access; and what
/// each shows to whoever opens it, signed in or not (the concept's "links and guests"): a song's link shows the song
/// and its chords, and its playlist only when the playlist is not private ("from a private playlist" otherwise);
/// a playlist's link opens the playlist while its access is <c>link</c> (or public). One link per thing: sharing it
/// again gives the same link.
/// <para>
/// A link of chords may carry its sharer's access: whoever holds it reads those chords, and nothing else, as the
/// sharer's custom keys open them, never as the sharer's account. The keys are kept with the link when it is made (and
/// made again); when it is opened, only those the sharer still has count, so a link never opens more than its sharer
/// could now. It stops when its sharer removes it, and with its sharer's profile. It does not expire, as no link does.
/// </para>
/// </summary>
public sealed class ShareService(
    ShelfDbContext db, PlaylistGate gate, SongPages songPages, Views views, ChordsLookup chordsRows, ChordsReader reader, SourceCallers callers, IOptionsMonitor<PublicPageOptions> publicPage, TimeProvider time)
{
    public async Task<ShareLinkItem> CreateAsync(ClaimsPrincipal user, CreateShareRequest request, CancellationToken cancellationToken)
    {
        var me = user.GetAccountId()!.Value;
        if (request.CarriesAccess)
            return await CreateCarryingAsync(user, me, request, cancellationToken);

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
            return Item(existing.Token, request.Kind, false);

        link.Token = NewToken();
        link.CreatedById = me;
        link.CreatedAt = time.GetUtcNow();
        db.ShareLinks.Add(link);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Item(link.Token, request.Kind, false);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // made by someone else at the same moment: theirs is the one
            db.Entry(link).State = EntityState.Detached;
            return Item((await FindAsync(link, cancellationToken))!.Token, request.Kind, false);
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
                    if (link.CarriedKeys == null)
                        return new() { Kind = ShareKind.Chords, Chords = (await views.ChordsAsync(user, [chords], cancellationToken))[chords] };

                    var carried = await CarriedCallerAsync(link, cancellationToken);
                    return new()
                    {
                        Kind = ShareKind.Chords,
                        Chords = (await reader.GetHeadersAsync(carried, [chords], cancellationToken)).Single(),
                        CarriesAccess = true,
                    };
            }
        }
        catch (ShelfException exception) when (exception.StatusCode == StatusCodes.Status404NotFound)
        {
            // what the link led to is gone, or no longer open by it
            throw ShelfException.NotFound(PlaylistErrorCodes.NoSuchLink);
        }
    }

    /// <summary>
    /// The content of a link's chords in the kinds asked: as they are open to the link's sharer when it carries their
    /// access, otherwise as they are open to the caller (the same as <c>GET /api/chords/{id}</c>). With the caller's own
    /// marks, keys and notes on them.
    /// </summary>
    public async Task<ChordsContentResponse> ContentAsync(ClaimsPrincipal user, string token, IReadOnlyList<ContentKind> kinds, CancellationToken cancellationToken)
    {
        var (id, caller) = await ChordsCallerAsync(user, token, cancellationToken);
        return await reader.GetContentAsync(caller, user.GetAccountId(), id, kinds, cancellationToken);
    }

    /// <summary>
    /// The song a link opens: a song's link, its song (or the one it was linked into since; <paramref name="songId"/>,
    /// when given, must be that one); a playlist's link, the song of that playlist named by <paramref name="songId"/>.
    /// Whether the caller sees it by the link is the song's read's to tell (the link is passed on as its share).
    /// </summary>
    /// <exception cref="ShelfException">404 <c>no_such_link</c>: no such link, or one of chords; 404 <c>no_such_song</c>:
    /// the link opens no such song.</exception>
    public async Task<(Guid PlaylistId, Guid SongId)> SongOfLinkAsync(string token, Guid? songId, CancellationToken cancellationToken)
    {
        var link = await db.ShareLinks.AsNoTracking().SingleOrDefaultAsync(x => x.Token == token, cancellationToken);
        switch (link?.Kind)
        {
            case ShareTargetKind.Song:
                var song = await gate.SharedSongAsync(link.SongId!.Value, cancellationToken);
                if (song == null || song.DeletedAt != null)
                    throw ShelfException.NotFound(PlaylistErrorCodes.NoSuchLink);
                if (songId != null && songId != song.Id)
                    throw ShelfException.NotFound(PlaylistErrorCodes.NoSuchSong);
                return (song.PlaylistId, song.Id);
            case ShareTargetKind.Playlist when songId is { } id:
                return (link.PlaylistId!.Value, id);
            case ShareTargetKind.Playlist:
                throw ShelfException.BadRequest();
            default:
                throw ShelfException.NotFound(PlaylistErrorCodes.NoSuchLink);
        }
    }

    /// <summary>
    /// The id of a link's chords and who they are read for: the link's carried access, or the caller.
    /// </summary>
    /// <exception cref="ShelfException">404 <c>no_such_link</c>; 400 <c>bad_request</c>: the link is not of chords.</exception>
    public async Task<(string Id, Caller Caller)> ChordsCallerAsync(ClaimsPrincipal user, string token, CancellationToken cancellationToken)
    {
        var link = await db.ShareLinks.Include(x => x.Chords).AsNoTracking().SingleOrDefaultAsync(x => x.Token == token, cancellationToken)
                   ?? throw ShelfException.NotFound(PlaylistErrorCodes.NoSuchLink);
        if (link.Kind != ShareTargetKind.Chords)
            throw ShelfException.BadRequest();

        return (link.Chords!.SourceId, link.CarriedKeys == null ? callers.For(user) : await CarriedCallerAsync(link, cancellationToken));
    }

    /// <summary>
    /// Removes a link that carries the caller's access: it stops at once (media addresses it gave before play on until
    /// they expire).
    /// </summary>
    /// <exception cref="ShelfException">404 <c>no_such_link</c>: no such link of the caller's that carries access.</exception>
    public async Task RemoveAsync(ClaimsPrincipal user, string token, CancellationToken cancellationToken)
    {
        var me = user.GetAccountId()!.Value;
        var removed = await db.ShareLinks.Where(x => x.Token == token && x.CarriedKeys != null && x.CreatedById == me).ExecuteDeleteAsync(cancellationToken);
        if (removed == 0)
            throw ShelfException.NotFound(PlaylistErrorCodes.NoSuchLink);
    }

    /// <summary>
    /// The caller's own link that carries their access to the chords (by their id), if they have made one; nothing is
    /// made.
    /// </summary>
    public async Task<MyCarryingLink> MineAsync(ClaimsPrincipal user, string? chords, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(chords))
            throw ShelfException.BadRequest();

        var me = user.GetAccountId()!.Value;
        var token = await db.ShareLinks.Where(x => x.CarriedKeys != null && x.CreatedById == me && x.Chords!.SourceId == chords)
            .Select(x => x.Token).SingleOrDefaultAsync(cancellationToken);
        return new() { Link = token == null ? null : Item(token, ShareKind.Chords, true) };
    }

    private async Task<ShareLinkItem> CreateCarryingAsync(ClaimsPrincipal user, Guid me, CreateShareRequest request, CancellationToken cancellationToken)
    {
        if (request.Kind != ShareKind.Chords || request.Chords == null)
            throw ShelfException.BadRequest();

        var (chords, _) = await chordsRows.GetOrCreateAsync(me, request.Chords, true, cancellationToken);
        var keys = callers.For(user).Keys.ToList();

        // the link opens as the sharer's keys alone, with no account: one that would open nothing (the sharer's own
        // private chords, their private analysis) is not made
        switch ((await reader.GetHeadersAsync(new Caller { Keys = keys }, [request.Chords], cancellationToken)).Single().State)
        {
            case ChordsState.NotOpen:
                throw ShelfException.Forbidden(PlaylistErrorCodes.ChordsNotOpen);
            case ChordsState.Unavailable:
                throw new ShelfException(StatusCodes.Status503ServiceUnavailable, Model.ErrorCodes.Unavailable);
        }
        for (var attempt = 0; ; attempt++)
        {
            var link = await db.ShareLinks.SingleOrDefaultAsync(x => x.CarriedKeys != null && x.ChordsId == chords.Id && x.CreatedById == me, cancellationToken);
            if (link == null)
            {
                link = new()
                {
                    Token = NewToken(),
                    Kind = ShareTargetKind.Chords,
                    ChordsId = chords.Id,
                    CreatedById = me,
                    CreatedAt = time.GetUtcNow(),
                    CarriedKeys = keys,
                };
                db.ShareLinks.Add(link);
            }
            else
            {
                link.CarriedKeys = keys;
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return Item(link.Token, ShareKind.Chords, true);
            }
            catch (DbUpdateException exception) when (attempt == 0 && IsUniqueViolation(exception))
            {
                // made at the same moment by another request of the same person: that one is the link
                db.Entry(link).State = EntityState.Detached;
            }
        }
    }

    /// <summary>
    /// Who the chords of a link that carries access are read for: no account, and those of the keys kept with the link
    /// that its sharer still has. A link whose sharer is gone opens nothing.
    /// </summary>
    private async Task<Caller> CarriedCallerAsync(ShareLink link, CancellationToken cancellationToken)
    {
        if (link.CreatedById is not { } sharer
            || !await db.Accounts.AnyAsync(x => x.Id == sharer && x.DeletedAt == null, cancellationToken))
            throw ShelfException.NotFound(PlaylistErrorCodes.NoSuchLink);

        var now = callers.For(await chordsRows.PersonAsync(sharer, cancellationToken)).Keys;
        return new() { AccountId = null, Keys = link.CarriedKeys!.Intersect(now, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList() };
    }

    private Task<ShareLink?> FindAsync(ShareLink link, CancellationToken cancellationToken)
        => db.ShareLinks.AsNoTracking().SingleOrDefaultAsync(
            x => x.CarriedKeys == null && x.Kind == link.Kind && x.PlaylistId == link.PlaylistId && x.SongId == link.SongId && x.ChordsId == link.ChordsId,
            cancellationToken);

    private ShareLinkItem Item(string token, ShareKind kind, bool carriesAccess) => new()
    {
        Token = token,
        Kind = kind,
        CarriesAccess = carriesAccess,
        PublicAddress = publicPage.CurrentValue.Origin is { } origin ? $"{origin}/s/{token}" : null,
    };

    private static string NewToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(18));

    private static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
