using System.Net;
using System.Text.Json;
using HarmonyDB.Shelf.Api.Areas.Illustrations.Services;
using HarmonyDB.Shelf.Api.Areas.OwnChords.Services;
using HarmonyDB.Shelf.Api.Clients.Illustrations;
using HarmonyDB.Shelf.Api.Common.Jobs;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Jobs;
using HarmonyDB.Shelf.Tests.Auth;
using HarmonyDB.Shelf.Tests.Chords;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HarmonyDB.Shelf.Tests.Jobs;

/// <summary>
/// What bounds a person's work: the jobs of a kind going at once, the pictures of a day, one batch of chords at a time;
/// and a placing done again adds nothing twice.
/// </summary>
public sealed class LimitsTests(WorkFixture fixture) : IClassFixture<WorkFixture>
{
    [DatabaseFact]
    public async Task APersonHasAtMostSoManyJobsOfAKindGoing()
    {
        var me = await fixture.AccountAsync();
        await using var scope = fixture.Api.Services.CreateAsyncScope();
        var jobs = scope.ServiceProvider.GetRequiredService<JobQueue>();

        // a kind no handler takes stays queued
        for (var i = 0; i < 3; i++)
            await jobs.EnqueueAsync(me, "test_waiting", null, new { }, CancellationToken.None);

        var refused = await Assert.ThrowsAsync<ShelfException>(() => jobs.EnqueueAsync(me, "test_waiting", null, new { }, CancellationToken.None));
        Assert.Equal(429, refused.StatusCode);
        Assert.Equal(JobsErrorCodes.TooManyJobs, refused.Code);

        // asked at the same moment, too
        var together = await fixture.AccountAsync();
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            await using var own = fixture.Api.Services.CreateAsyncScope();
            try
            {
                await own.ServiceProvider.GetRequiredService<JobQueue>().EnqueueAsync(together, "test_waiting", null, new { }, CancellationToken.None);
                return true;
            }
            catch (ShelfException)
            {
                return false;
            }
        }));
        Assert.Equal(3, outcomes.Count(x => x));

        // another kind, and another person, are not held back
        await jobs.EnqueueAsync(me, "test_waiting_too", null, new { }, CancellationToken.None);
        await jobs.EnqueueAsync(await fixture.AccountAsync(), "test_waiting", null, new { }, CancellationToken.None);
    }

    [DatabaseFact]
    public async Task TheDayCountsWhatIsDrawnAndWhatIsGoing()
    {
        var me = await fixture.AccountAsync();
        var now = DateTimeOffset.UtcNow;
        await using (var db = fixture.CreateContext())
        {
            Job Of(JobState state, string chords, object? result, DateTimeOffset? started, DateTimeOffset? finished) => new()
            {
                Id = Guid.NewGuid(),
                AccountId = me,
                Kind = IllustrationsService.JobKind,
                SubjectId = chords,
                State = state,
                Input = JsonSerializer.Serialize(new { chordsId = chords, preset = "plain", keys = Array.Empty<string>() }, JobQueue.JsonOptions),
                Result = result == null ? null : JsonSerializer.Serialize(result, JobQueue.JsonOptions),
                CreatedAt = started ?? now,
                StartedAt = started,
                FinishedAt = finished,
                // held by a worker that is not this one: the tests' worker does not take it
                LockedUntil = finished == null ? now.AddHours(1) : null,
            };

            db.Jobs.AddRange(
                Of(JobState.Succeeded, "a", new { made = 3 }, now.AddHours(-2), now.AddHours(-2).AddMinutes(1)),
                Of(JobState.Succeeded, "b", new { made = 5 }, now.AddDays(-2), now.AddDays(-2).AddMinutes(1)),   // another day
                Of(JobState.Running, "c", null, now, null),
                Of(JobState.Running, "d", null, now, null),
                Of(JobState.Failed, "e", null, now.AddHours(-1), now.AddHours(-1).AddMinutes(2)));
            await db.SaveChangesAsync();
        }

        // the failed batch kept two pictures while it ran, the other is of another time
        var store = new FakeIllustrations
        {
            Images =
            [
                new(Guid.NewGuid(), "plain", now.AddHours(-1).AddMinutes(1)),
                new(Guid.NewGuid(), "plain", now.AddHours(-1).AddMinutes(1)),
                new(Guid.NewGuid(), "plain", now.AddDays(-3)),
            ],
        };
        await using var scope = fixture.Api.WithWebHostBuilder(x => x.ConfigureServices(s =>
                s.AddHttpClient<IllustrationsClient>().ConfigurePrimaryHttpMessageHandler(() => store)))
            .Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IllustrationsService>();

        Assert.Equal(3 + 2 * IllustrationsService.MaxBatchImages + 2, await service.UsedTodayAsync(me, CancellationToken.None));
    }

    [DatabaseFact]
    public async Task OneBatchOfChordsAtATime()
    {
        var me = await fixture.AccountAsync();
        var other = await fixture.AccountAsync();
        fixture.Index.Answer = (operation, body) => operation == "V2GetHeaders"
            ? (HttpStatusCode.OK, new GetHeadersResponse
            {
                Headers = body.GetProperty("ids").EnumerateArray()
                    .Select(x => new HeaderAnswer { Id = x.GetString()!, State = ItemState.Open, Header = FakeIndex.Header(x.GetString()!, "Source") }).ToList(),
            })
            : (HttpStatusCode.InternalServerError, null);

        await using var scope = fixture.Api.WithWebHostBuilder(x => x.ConfigureServices(s =>
                s.AddHttpClient<IllustrationsClient>().ConfigurePrimaryHttpMessageHandler(() => new FakeIllustrations())))
            .Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IllustrationsService>();
        var chords = $"chords-{Guid.NewGuid():N}";

        var first = await service.DrawAsync(FakeIllustrations.Person(me), chords, "plain", CancellationToken.None);
        var second = await service.DrawAsync(FakeIllustrations.Person(other), chords, "plain", CancellationToken.None);
        Assert.Equal(first, second);

        await using var db = fixture.CreateContext();
        Assert.Single(await db.Jobs.Where(x => x.SubjectId == chords).ToListAsync());
    }

    [DatabaseFact]
    public async Task APlacingDoneAgainAddsNothingTwice()
    {
        var me = await fixture.AccountAsync();
        var chordsId = $"own-{Guid.NewGuid():N}";
        fixture.Index.Answer = (operation, body) => operation == "V2GetHeaders"
            ? (HttpStatusCode.OK, new GetHeadersResponse
            {
                Headers = body.GetProperty("ids").EnumerateArray()
                    .Select(x => new HeaderAnswer { Id = x.GetString()!, State = ItemState.Open, Header = FakeIndex.Header(x.GetString()!, "Own") }).ToList(),
            })
            : (HttpStatusCode.InternalServerError, null);

        var placing = new OwnChordsService.Placing(new("https://own.example/1"), [], "A song", ["Somebody"], null, null, []);
        for (var i = 0; i < 2; i++)
        {
            await using var scope = fixture.Api.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<OwnChordsService>().PlaceAsync(me, chordsId, placing, CancellationToken.None);
        }

        await using var db = fixture.CreateContext();
        Assert.Single(await db.PlaylistSongItems.Where(x => x.Chords!.SourceId == chordsId).ToListAsync());
    }
}

/// <summary>
/// The illustrations service as the shelf calls it: its presets, and the images of any key.
/// </summary>
public sealed class FakeIllustrations : HttpMessageHandler
{
    public List<StoredIllustration> Images { get; init; } = [];

    public static System.Security.Claims.ClaimsPrincipal Person(Guid account)
        => new(HarmonyDB.Shelf.Api.Foundation.Permissions.ShelfIdentity.Create("test", account, [HarmonyDB.Shelf.Database.AccountSubjects.Of(account)], ["illustrations.generate"], null));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        object answer = request.RequestUri!.AbsolutePath.Split('/').Last() switch
        {
            "ShelfPresets" => new { presets = new[] { "plain" }, sizes = new[] { 256 } },
            "ShelfRead" => new { images = Images },
            _ => throw new InvalidOperationException(request.RequestUri.ToString()),
        };

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = System.Net.Http.Json.JsonContent.Create(answer, options: new(JsonSerializerDefaults.Web)) });
    }
}
