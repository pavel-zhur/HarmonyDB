using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Chords.Services;
using HarmonyDB.Shelf.Api.Areas.Music.Services;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Model.Chords;
using HarmonyDB.Shelf.Model.Music;
using HarmonyDB.Shelf.Model.Recordings;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Api.Areas.Recordings.Services;

/// <summary>
/// The recordings found for an item of chords, when it is open to the person (found the first time they are asked
/// for).
/// </summary>
public sealed class ChordsRecordingsReader(ShelfDbContext db, ChordsReader chords, RecordingFinder finder, TrackMarks marks)
{
    public async Task<ChordsRecordings> GetAsync(ClaimsPrincipal user, string id, CancellationToken cancellationToken)
    {
        var (state, row, header) = await chords.GetOpenRowAsync(user, id, cancellationToken);
        if (row == null)
            return new() { Id = id, State = state, Complete = true, Found = [] };

        var complete = await finder.EnsureFoundAsync(row, header!, cancellationToken);
        var found = await db.ChordsRecordings.AsNoTracking()
            .Where(x => x.ChordsId == row.Id)
            .OrderBy(x => x.Rank)
            .Select(x => x.Recording)
            .ToListAsync(cancellationToken);

        return await marks.ApplyAsync(user, m => new ChordsRecordings
        {
            Id = id,
            State = ChordsState.Open,
            Complete = complete,
            Found = found.Select(x => m(MusicShapes.Track(x))).ToList(),
        }, cancellationToken);
    }
}
