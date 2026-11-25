using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Playlists.Services;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Sync;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Api.Areas.Library.Services;

/// <summary>
/// What the person opened lately, the latest first (the browser words how long ago).
/// </summary>
public sealed class RecentService(ShelfDbContext db, Views views)
{
    public const int MaxLimit = 200;

    public async Task<List<RecentEntry>> ListAsync(ClaimsPrincipal user, int limit, CancellationToken cancellationToken)
    {
        var me = user.GetAccountId()!.Value;
        var rows = await db.Recent.Where(x => x.AccountId == me)
            .OrderByDescending(x => x.OpenedAt).ThenBy(x => x.Id)
            .Take(Math.Clamp(limit, 1, MaxLimit))
            .Include(x => x.Chords)
            .AsNoTracking().ToListAsync(cancellationToken);

        // a song is given while it is there and the person still sees it: of their playlists, or a public one
        var songIds = rows.Where(x => x.SongId != null).Select(x => x.SongId!.Value).Distinct().ToList();
        var songs = await db.PlaylistSongs
            .Where(x => songIds.Contains(x.Id) && x.Playlist.DeletedAt == null
                        && (x.Playlist.Access == PlaylistAccess.Public || x.Playlist.Members.Any(m => m.AccountId == me && m.DeletedAt == null)))
            .AsNoTracking().ToDictionaryAsync(x => x.Id, cancellationToken);
        var chords = await views.ChordsAsync(user, rows.Where(x => x.Chords != null).Select(x => x.Chords!.SourceId), cancellationToken);
        var people = await views.PeopleAsync(songs.Values.Where(x => x.AddedById != null).Select(x => x.AddedById!.Value), cancellationToken);
        var covers = await views.SongCoversAsync(me, songs.Keys, cancellationToken);

        return rows
            .Select(x =>
            {
                var answer = x.Chords == null ? null : chords[x.Chords.SourceId];
                var song = x.SongId is { } id && songs.TryGetValue(id, out var found) ? Views.Song(found, people) : null;
                // seen under the chords' title as their source gives it; not open: the title the song has in its playlist
                var named = answer?.Header != null;
                return new RecentEntry
                {
                    Chords = answer,
                    Song = song,
                    Title = named ? answer!.Header!.Title : song?.Title,
                    Artists = named ? answer!.Header!.Artists : song?.Artists ?? [],
                    Cover = song == null ? null : covers.GetValueOrDefault(song.Id),
                    OpenedAt = x.OpenedAt,
                };
            })
            .Where(x => x.Chords != null || x.Song != null)
            .ToList();
    }
}
