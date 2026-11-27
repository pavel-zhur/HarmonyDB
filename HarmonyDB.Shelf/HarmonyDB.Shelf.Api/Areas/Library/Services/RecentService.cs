using HarmonyDB.Shelf.Api.Areas.Chords.Services;
using HarmonyDB.Shelf.Model.Chords;
using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Playlists.Services;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Sync;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Api.Areas.Library.Services;

/// <summary>
/// What the person opened lately, the latest first (the browser words how long ago), named as the person was shown it
/// when they opened it — kept with the row, as the old shelf kept its visits; no source is asked.
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
        var songs = await db.PlaylistSongs.SeenBy(me)
            .Where(x => songIds.Contains(x.Id) && x.Playlist.DeletedAt == null
                        && (x.Playlist.Access == PlaylistAccess.Public || x.Playlist.Members.Any(m => m.AccountId == me && m.DeletedAt == null)))
            .AsNoTracking().ToDictionaryAsync(x => x.Id, cancellationToken);
        var people = await views.PeopleAsync(songs.Values.Where(x => x.AddedById != null).Select(x => x.AddedById!.Value), cancellationToken);
        var covers = await views.SongCoversAsync(me, songs.Keys, cancellationToken);

        return rows
            .Select(x =>
            {
                var song = x.SongId is { } id && songs.TryGetValue(id, out var found) ? Views.Song(found, people) : null;
                // as shown when opened; a row from before that was kept: the chords' kept title, else the song's
                var title = x.Title ?? x.Chords?.Title ?? song?.Title;
                var artists = x.Title != null ? x.Artists ?? [] : x.Chords?.Title != null ? x.Chords.Artists : song?.Artists ?? [];
                return new RecentEntry
                {
                    Chords = x.Chords == null ? null : ChordsReader.ToRef(x.Chords, null),
                    Song = song,
                    Title = title,
                    Artists = artists,
                    Source = x.SourceTitle == null ? null : new ChordsSource { Key = x.SourceKey ?? string.Empty, Title = x.SourceTitle, IconUrl = x.SourceIconUrl },
                    Cover = song == null ? null : covers.GetValueOrDefault(song.Id),
                    OpenedAt = x.OpenedAt,
                };
            })
            .Where(x => x.Chords != null || x.Song != null)
            .ToList();
    }
}
