using System.Security.Claims;
using HarmonyDB.Shelf.Api.Common;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using Microsoft.EntityFrameworkCore;
using M = HarmonyDB.Shelf.Model.Playlists;

namespace HarmonyDB.Shelf.Api.Areas.Playlists.Services;

/// <summary>
/// A song's page, in one call: the song, its variants with their letters (by their order among the song's chords
/// variants not removed: the same for everyone), its recordings, the caller's choices, the marks, keys and notes the
/// caller sees (<see cref="ChordsAnnotations"/>), and in how many of the caller's playlists each chords are.
/// </summary>
public sealed class SongPages(ShelfDbContext db, PlaylistGate gate, Views views, ChordsAnnotations annotations)
{
    public async Task<M.SongPage> GetAsync(ClaimsPrincipal user, Guid playlistId, Guid songId, string? share, CancellationToken cancellationToken)
    {
        var viewer = user.GetAccountId();
        var (view, song, fromPrivate) = await gate.ReadSongAsync(viewer, playlistId, songId, share, cancellationToken);
        var items = await db.PlaylistSongItems.Where(x => x.SongId == songId)
            .Include(x => x.Chords).Include(x => x.Recording)
            .OrderBy(x => x.Position).ThenBy(x => x.Id)
            .AsNoTracking().ToListAsync(cancellationToken);
        var chordsIds = items.Where(x => x.ChordsId != null).Select(x => x.ChordsId!.Value).ToList();

        var seen = await annotations.ReadAsync(viewer, chordsIds, playlistId, songId, !fromPrivate, cancellationToken);
        var chords = await views.ChordsAsync(user, items.Where(x => x.Chords != null).Select(x => x.Chords!.SourceId), cancellationToken);
        var preference = viewer == null
            ? null
            : await db.PlaylistSongPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == viewer && x.SongId == songId, cancellationToken);
        var inMine = viewer == null
            ? []
            : await db.PlaylistSongItems
                .Where(x => x.ChordsId != null && chordsIds.Contains(x.ChordsId.Value) && x.Song.DeletedAt == null && x.Song.Playlist.DeletedAt == null
                            && x.Song.Playlist.Members.Any(m => m.AccountId == viewer && m.DeletedAt == null))
                .GroupBy(x => x.ChordsId!.Value)
                .Select(x => new { x.Key, Count = x.Select(i => i.Song.PlaylistId).Distinct().Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        // by its link from a private playlist, nothing tells whose it is: who added what is not named
        var people = await views.PeopleAsync(
            fromPrivate
                ? seen.People
                : seen.People.Concat(items.Where(x => x.AddedById != null).Select(x => x.AddedById!.Value)).Concat(song.AddedById is { } by ? [by] : []),
            cancellationToken);
        var live = items.Select(x => x.Id).ToHashSet();
        var index = 0;
        return new()
        {
            Song = Views.Song(song, people),
            Playlist = fromPrivate ? null : await views.SummaryAsync(viewer, view.Playlist, cancellationToken),
            FromPrivatePlaylist = fromPrivate,
            CanAdd = view.CanAdd,
            CanEdit = view.CanChange(song.AddedById),
            Variants = items.Where(x => x.Chords != null).Select(x => new M.ChordsVariant
            {
                ItemId = x.Id,
                Index = index++,
                Chords = chords[x.Chords!.SourceId],
                AddedBy = x.AddedById is { } by ? people.GetValueOrDefault(by) : null,
                AddedAt = x.AddedAt,
                InMyPlaylists = inMine.GetValueOrDefault(x.ChordsId!.Value),
            }).ToList(),
            Recordings = items.Where(x => x.Recording != null).Select(x => new M.RecordingVariant
            {
                ItemId = x.Id,
                Recording = Views.Recording(x.Recording!),
                AddedBy = x.AddedById is { } by ? people.GetValueOrDefault(by) : null,
                AddedAt = x.AddedAt,
            }).ToList(),
            // a choice pointing at a variant removed reads as none
            MyMainItemId = preference?.MainItemId is { } main && live.Contains(main) ? main : null,
            MyRecordingItemId = preference?.RecordingItemId is { } recording && live.Contains(recording) ? recording : null,
            People = seen.People.Where(people.ContainsKey).Select(x => people[x]).ToList(),
            Marks = seen.Marks,
            Keys = seen.Keys,
            Notes = seen.SongNotes,
            MyNotes = seen.MyNotes,
        };
    }
}
