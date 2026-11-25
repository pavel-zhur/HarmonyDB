using System.Security.Claims;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Model.Music;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Api.Areas.Music.Services;

/// <summary>
/// What a person has of the tracks in a list: whether a track is already in one of their playlists, and whether there
/// is an analysis of it open to them. (Whether the sources have chords for it is not told: almost everything has a few,
/// and almost all of them are wrong.)
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
            ? x with { InMyPlaylists = mark.InMyPlaylists, HasAnalysis = mark.HasAnalysis }
            : x);
    }

    private async Task<Dictionary<(Model.Me.MusicService, string), (bool InMyPlaylists, bool HasAnalysis)>> LoadAsync(
        Guid? accountId, IReadOnlyList<MusicTrack> tracks, CancellationToken cancellationToken)
    {
        var result = new Dictionary<(Model.Me.MusicService, string), (bool, bool)>();
        foreach (var group in tracks.GroupBy(x => x.Service))
        {
            var service = MusicShapes.ToDatabase(group.Key);
            var ids = group.Select(x => x.Id).Distinct().ToList();
            var rows = await db.Recordings.AsNoTracking()
                .Where(r => r.Service == service && ids.Contains(r.ServiceId))
                .Select(r => new
                {
                    r.ServiceId,
                    InMyPlaylists = accountId != null && db.PlaylistSongItems.Any(i =>
                        i.RecordingId == r.Id
                        && i.Song.DeletedAt == null
                        && i.Song.Playlist.DeletedAt == null
                        && i.Song.Playlist.Members.Any(m => m.AccountId == accountId && m.DeletedAt == null)),
                    HasAnalysis = db.Analyses.Any(a => a.RecordingId == r.Id && (a.OwnerId == null || a.OwnerId == accountId)),
                })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
                result[(group.Key, row.ServiceId)] = (row.InMyPlaylists, row.HasAnalysis);
        }

        return result;
    }
}
