using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Music.Services;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Music;
using HarmonyDB.Shelf.Tests.Auth;

namespace HarmonyDB.Shelf.Tests.Music;

/// <summary>
/// The marks of the tracks of an answer over a real database: which chords made from a track the shelf knows.
/// </summary>
public sealed class TrackMarksTests(ShelfDatabaseFixture database) : IClassFixture<ShelfDatabaseFixture>
{
    [DatabaseFact]
    public async Task ATrackLeadsToTheNewestChordsMadeFromItThatAreOpenToEveryone()
    {
        var now = DateTimeOffset.UtcNow;
        var tag = Guid.NewGuid().ToString("N");
        Recording Track(string id) => new() { Id = Guid.NewGuid(), Service = MusicService.Youtube, ServiceId = $"{id}-{tag}", Title = id, UpdatedAt = now };
        var several = Track("several");
        var closed = Track("closed");
        ChordsItem Chords(string id, Recording from, bool openToEveryone, int minutesAgo) => new()
        {
            // the track chords were made from is kept only of chords that may be shown to everyone
            Id = Guid.NewGuid(), SourceId = $"{id}-{tag}", MadeFromRecordingId = openToEveryone ? from.Id : null, CreatedAt = now.AddMinutes(-minutesAgo),
        };

        await using (var db = database.CreateContext())
        {
            db.AddRange(several, closed);
            db.Chords.AddRange(Chords("older", several, true, 30), Chords("newer", several, true, 20), Chords("only-some", closed, false, 10));
            await db.SaveChangesAsync();
        }

        var tracks = new[] { several, closed }
            .Select(x => new MusicTrack { Service = Model.Me.MusicService.Youtube, Id = x.ServiceId, Artists = [], InMySongs = [] })
            .Append(new MusicTrack { Service = Model.Me.MusicService.Youtube, Id = $"unknown-{tag}", Artists = [], InMySongs = [] })
            .ToList();

        await using (var db = database.CreateContext())
        {
            var marked = await new TrackMarks(db).ApplyAsync(new ClaimsPrincipal(new ClaimsIdentity()), m => tracks.Select(m).ToList(), CancellationToken.None);
            Assert.Equal([$"newer-{tag}", null, null], marked.Select(x => x.MadeInto));
        }
    }
}
