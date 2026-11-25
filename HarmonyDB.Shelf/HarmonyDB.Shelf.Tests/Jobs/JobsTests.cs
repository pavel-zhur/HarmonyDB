using System.Net;
using System.Text.Json;
using HarmonyDB.Shelf.Api.Common.Jobs;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Jobs;
using HarmonyDB.Shelf.Tests.Auth;
using HarmonyDB.Shelf.Tests.Chords;
using Microsoft.Extensions.DependencyInjection;

namespace HarmonyDB.Shelf.Tests.Jobs;

/// <summary>
/// The background jobs: they run to their end with their stages, result and file; one left running by a stopped app is
/// taken again; one interrupted too often is given up; a person sees only their own.
/// </summary>
public sealed class JobsTests(WorkFixture fixture) : IClassFixture<WorkFixture>
{
    [DatabaseFact]
    public async Task AJobRunsToItsEndAndItsFileIsTheStartersOnly()
    {
        var me = await fixture.AccountAsync();
        var other = await fixture.AccountAsync();
        var job = await EnqueueAsync(me, TestJobs.Succeeding.Kind, "hello");
        Assert.Equal(JobStatus.Queued, job.Status);

        var done = await fixture.FinishedAsync(me, job.Id);
        Assert.Equal(JobStatus.Succeeded, done.Status);
        Assert.Equal("hello", done.Result!.Value.GetProperty("echo").GetString());
        Assert.Equal(1, done.Result.Value.GetProperty("attempt").GetInt32());
        Assert.Equal("echo.txt", done.File!.FileName);

        using var client = fixture.Api.CreateClient();
        var file = await client.SendAsync(FakeIndex.As(HttpMethod.Get, $"/api/jobs/{job.Id}/file", me));
        Assert.Equal("hello", await file.Content.ReadAsStringAsync());

        // another person sees neither the job, nor its file, nor it in their list
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(FakeIndex.As(HttpMethod.Get, $"/api/jobs/{job.Id}", other))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(FakeIndex.As(HttpMethod.Get, $"/api/jobs/{job.Id}/file", other))).StatusCode);
        Assert.Empty(await ListAsync(client, other));
        Assert.Contains(await ListAsync(client, me), x => x.Id == job.Id);

        // a guest has no jobs
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(FakeIndex.As(HttpMethod.Get, "/api/jobs", null))).StatusCode);
    }

    [DatabaseFact]
    public async Task AFailureIsItsCode()
    {
        var me = await fixture.AccountAsync();
        var done = await fixture.FinishedAsync(me, (await EnqueueAsync(me, TestJobs.Failing.Kind, "x")).Id);
        Assert.Equal(JobStatus.Failed, done.Status);
        Assert.Equal("test_failure", done.ErrorCode);
        Assert.Null(done.File);
    }

    [DatabaseFact]
    public async Task AJobLeftRunningByAStoppedAppIsTakenAgainOrGivenUp()
    {
        var me = await fixture.AccountAsync();
        var left = await InsertRunningAsync(me, attempts: 1);
        var tooOften = await InsertRunningAsync(me, attempts: 3);

        var again = await fixture.FinishedAsync(me, left);
        Assert.Equal(JobStatus.Succeeded, again.Status);
        Assert.Equal(2, again.Result!.Value.GetProperty("attempt").GetInt32());

        var givenUp = await fixture.FinishedAsync(me, tooOften);
        Assert.Equal(JobStatus.Failed, givenUp.Status);
        Assert.Equal(JobsErrorCodes.Interrupted, givenUp.ErrorCode);
    }

    private async Task<JobInfo> EnqueueAsync(Guid accountId, string kind, string text)
    {
        await using var scope = fixture.Api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<JobQueue>().EnqueueAsync(accountId, kind, null, new TestJobs.Input(text), CancellationToken.None);
    }

    /// <summary>
    /// A job as a copy of the app that stopped while running it leaves it: running, its lease run out.
    /// </summary>
    private async Task<Guid> InsertRunningAsync(Guid accountId, int attempts)
    {
        await using var db = fixture.CreateContext();
        var job = new Job
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Kind = TestJobs.Succeeding.Kind,
            State = JobState.Running,
            Input = JsonSerializer.Serialize(new TestJobs.Input("left"), JobQueue.JsonOptions),
            Attempts = attempts,
            CreatedAt = DateTimeOffset.UtcNow,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            LockedUntil = DateTimeOffset.UtcNow.AddMinutes(-1),
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        return job.Id;
    }

    private static async Task<List<JobInfo>> ListAsync(HttpClient client, Guid accountId)
    {
        var response = await client.SendAsync(FakeIndex.As(HttpMethod.Get, "/api/jobs", accountId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonSerializer.Deserialize<List<JobInfo>>(await response.Content.ReadAsStringAsync(), Browser.Json)!;
    }
}
