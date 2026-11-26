using System.Security.Claims;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Model.Music;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Api.Areas.Music.Services;

/// <summary>
/// What the shelf knows of the tracks in a list, from its own tables (no source is asked): the songs of the person's
/// playlists that hold a track, and the chords made from it that the shelf knows (an analysis of it) — those whose
/// header it keeps, which it keeps only of items open to everyone, so the mark shows nothing a person may not open.
/// (Whether the sources have chords for it is not told: almost everything has a few, and almost all of them are wrong.)
/// </summary>
public sealed class TrackMarks(ShelfDbContext db)
{
    /// <summary>
    /// Builds an answer with its tracks marked: <paramref name="build"/> is called with a function that marks a track,
    /// once to learn the tracks and once more to build the answer.
    /// </summary>
    public async Task<T> ApplyAsync<T>(ClaimsPrincipal user, Func<Func<MusicTrack, MusicTrack>, T> build, CancellationToken cancellationToken)
    {
        var tracks = new List<MusicTrack>();
        build(x =>
        {
            tracks.Add(x);
            return x;
        });

        var marks = await LoadAsync(user.GetAccountId(), tracks, cancellationToken);
        return build(x => marks.TryGetValue((x.Service, x.Id), out var mark)
            ? x with { InMySongs = mark.InMySongs, MadeInto = mark.MadeInto }
            : x);
    }

    private async Task<Dictionary<(Model.Me.MusicService, string), (IReadOnlyList<MusicTrackSong> InMySongs, string? MadeInto)>> LoadAsync(
        Guid? accountId, IReadOnlyList<MusicTrack> tracks, CancellationToken cancellationToken)
    {
        var result = new Dictionary<(Model.Me.MusicService, string), (IReadOnlyList<MusicTrackSong>, string?)>();
        foreach (var group in tracks.GroupBy(x => x.Service))
        {
            var service = MusicShapes.ToDatabase(group.Key);
            var ids = group.Select(x => x.Id).Distinct().ToList();
            var rows = await db.Recordings.AsNoTracking()
                .Where(r => r.Service == service && ids.Contains(r.ServiceId))
                .Select(r => new
                {
                    r.ServiceId,
                    InMySongs = db.PlaylistSongItems
                        .Where(i => accountId != null
                                    && i.RecordingId == r.Id
                                    && i.Song.DeletedAt == null
                                    && i.Song.Playlist.DeletedAt == null
                                    && i.Song.Playlist.Members.Any(m => m.AccountId == accountId && m.DeletedAt == null))
                        .OrderBy(i => i.Song.PlaylistId).ThenBy(i => i.SongId)
                        .Select(i => new MusicTrackSong { PlaylistId = i.Song.PlaylistId, SongId = i.SongId })
                        .ToList(),
                    MadeInto = db.Chords
                        .Where(c => c.MadeFromRecordingId == r.Id && c.SourceKey != null)
                        .OrderByDescending(c => c.CreatedAt).ThenBy(c => c.SourceId)
                        .Select(c => c.SourceId)
                        .FirstOrDefault(),
                })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
                result[(group.Key, row.ServiceId)] = (row.InMySongs, row.MadeInto);
        }

        return result;
    }
}
