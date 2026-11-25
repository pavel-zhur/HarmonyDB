using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Music.Services;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Music;
using HarmonyDB.Shelf.Tests.Auth;

namespace HarmonyDB.Shelf.Tests.Music;

/// <summary>
/// The marks of the tracks of an answer over a real database: which analysis of a track a person is led to.
/// </summary>
public sealed class TrackMarksTests(ShelfDatabaseFixture database) : IClassFixture<ShelfDatabaseFixture>
{
    [DatabaseFact]
    public async Task ATrackLeadsToItsNewestAnalysisOpenToThePerson()
    {
        var now = DateTimeOffset.UtcNow;
        var me = new Account { Id = Guid.NewGuid(), DisplayName = "me", CreatedAt = now };
        var other = new Account { Id = Guid.NewGuid(), DisplayName = "other", CreatedAt = now };
        var tag = Guid.NewGuid().ToString("N");
        Recording Track(string id) => new() { Id = Guid.NewGuid(), Service = MusicService.Youtube, ServiceId = $"{id}-{tag}", Title = id, UpdatedAt = now };
        var several = Track("several");
        var mine = Track("mine");
        var none = Track("none");
        Analysis Of(Recording recording, string id, Account? owner, int minutesAgo) => new()
        {
            Id = Guid.NewGuid(), SourceId = $"{id}-{tag}", Title = id, RecordingId = recording.Id, OwnerId = owner?.Id, CreatedBy = "tests",
            CreatedAt = now.AddMinutes(-minutesAgo), UpdatedAt = now,
        };

        await using (var db = database.CreateContext())
        {
            db.AddRange(me, other, several, mine, none);
            db.Analyses.AddRange(
                Of(several, "older", null, 30),
                Of(several, "newer", null, 20),
                Of(several, "others-own", other, 10),
                Of(mine, "my-own", me, 10));
            await db.SaveChangesAsync();
        }

        var tracks = new[] { several, mine, none }
            .Select(x => new MusicTrack { Service = Model.Me.MusicService.Youtube, Id = x.ServiceId, Artists = [], InMyPlaylists = false, HasAnalysis = false })
            .Append(new MusicTrack { Service = Model.Me.MusicService.Youtube, Id = $"unknown-{tag}", Artists = [], InMyPlaylists = false, HasAnalysis = false })
            .ToList();

        async Task<IReadOnlyList<(bool, string?)>> MarkAsync(ClaimsPrincipal user)
        {
            await using var db = database.CreateContext();
            var marked = await new TrackMarks(db).ApplyAsync(user, m => tracks.Select(m).ToList(), CancellationToken.None);
            return marked.Select(x => (x.HasAnalysis, x.AnalysisChords)).ToList();
        }

        var person = new ClaimsPrincipal(ShelfIdentity.Create("tests", me.Id, [], null));
        Assert.Equal([(true, $"newer-{tag}"), (true, $"my-own-{tag}"), (false, null), (false, null)], await MarkAsync(person));
        Assert.Equal([(true, $"newer-{tag}"), (false, null), (false, null), (false, null)], await MarkAsync(new ClaimsPrincipal(new ClaimsIdentity())));
    }
}
