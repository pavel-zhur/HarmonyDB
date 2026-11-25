using System.Net;
using System.Text;
using System.Text.Json;
using HarmonyDB.Shelf.Api.Clients.Illustrations;
using HarmonyDB.Shelf.Api.Clients.OwnChords;
using HarmonyDB.Shelf.Api.Clients.Pdfs;
using HarmonyDB.Shelf.Api.Common.Jobs;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Jobs;
using HarmonyDB.Shelf.Tests.Api;
using HarmonyDB.Shelf.Tests.Auth;
using HarmonyDB.Shelf.Tests.Chords;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace HarmonyDB.Shelf.Tests.Jobs;

/// <summary>
/// A database of its own, migrated, and the API over it with the test sign-in (<see cref="FakeIndex.As"/>), the index
/// and the other services answering from fakes, the jobs looked at often, and two jobs of the tests' own.
/// </summary>
public sealed class WorkFixture : IAsyncLifetime
{
    private readonly string _name = $"shelf_test_{Guid.NewGuid():N}";

    public string ConnectionString { get; private set; } = string.Empty;

    public FakeIndex Index { get; } = new();

    public FakeService Store { get; } = new();

    public WebApplicationFactory<Program> Api { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        if (TestDatabase.Server == null)
            return;

        await using (var connection = new NpgsqlConnection(TestDatabase.Server))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {_name}", connection);
            await command.ExecuteNonQueryAsync();
        }

        ConnectionString = new NpgsqlConnectionStringBuilder(TestDatabase.Server) { Database = _name }.ConnectionString;
        await using (var db = CreateContext())
            await db.Database.MigrateAsync();

        var factory = new ShelfApiFactory();
        Api = Index.Api(factory, new Dictionary<string, string?>
            {
                ["ConnectionStrings:Shelf"] = ConnectionString,
                ["Jobs:Poll"] = "00:00:00.100",
                ["Jobs:Lease"] = "00:00:03",
                ["OwnChords:Placing:Pause"] = "00:00:01",
                ["Clients:OwnChords:Endpoint"] = FakeService.Endpoint,
                ["Clients:OwnChords:Key"] = FakeService.Key,
                ["Clients:Illustrations:Endpoint"] = FakeService.Endpoint,
                ["Clients:Illustrations:Key"] = FakeService.Key,
            })
            .WithWebHostBuilder(x => x.ConfigureTestServices(services =>
            {
                services.AddHttpClient<OwnChordsClient>().ConfigurePrimaryHttpMessageHandler(() => Store);
                var configuration = new ConfigurationBuilder().Build();
                services.AddJobHandler<TestJobs.Succeeding, TestJobs.Echo>(configuration, TestJobs.Succeeding.Kind);
                services.AddJobHandler<TestJobs.Failing>(configuration, TestJobs.Failing.Kind);
            }));
        Api.CreateClient().Dispose();
    }

    public ShelfDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ShelfDbContext>().UseShelfDatabase(ConnectionString).Options);

    /// <summary>
    /// A new account, with the sign-ins given ((provider, subject)).
    /// </summary>
    public async Task<Guid> AccountAsync(params (string Provider, string Subject)[] logins)
    {
        await using var db = CreateContext();
        var account = new Account { Id = Guid.NewGuid(), DisplayName = "A person", CreatedAt = DateTimeOffset.UtcNow };
        db.Accounts.Add(account);
        foreach (var (provider, subject) in logins)
            db.AccountLogins.Add(new() { Id = Guid.NewGuid(), AccountId = account.Id, Provider = provider, Subject = subject, CreatedAt = DateTimeOffset.UtcNow, LastSignInAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        return account.Id;
    }

    /// <summary>
    /// The job as its starter sees it, once it has ended (or the test fails after a while).
    /// </summary>
    public async Task<JobInfo> FinishedAsync(Guid accountId, Guid jobId)
    {
        using var client = Api.CreateClient();
        for (var i = 0; i < 300; i++)
        {
            var response = await client.SendAsync(FakeIndex.As(HttpMethod.Get, $"/api/jobs/{jobId}", accountId));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var job = JsonSerializer.Deserialize<JobInfo>(await response.Content.ReadAsStringAsync(), Browser.Json)!;
            if (job.Status is JobStatus.Succeeded or JobStatus.Failed)
                return job;
            await Task.Delay(100);
        }

        throw new TimeoutException($"The job {jobId} did not end.");
    }

    public async Task DisposeAsync()
    {
        if (TestDatabase.Server == null)
            return;

        await Api.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(TestDatabase.Server);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE {_name} WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// One of the other services the shelf calls with its key: every request kept, answered by <see cref="Answer"/>.
/// </summary>
public sealed class FakeService : HttpMessageHandler
{
    public const string Endpoint = "http://service.test/module/api/";
    public const string Key = "the-shelf-key-at-the-service-0123456789";

    public List<(string Operation, JsonElement Body)> Requests { get; } = [];

    public Func<string, JsonElement, (HttpStatusCode Status, object? Body)> Answer { get; set; } = (_, _) => (HttpStatusCode.InternalServerError, null);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Assert.Equal(Key, request.Headers.GetValues("X-Internal-Key").Single());
        var operation = request.RequestUri!.AbsolutePath.Split('/').Last();
        var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement.Clone();
        lock (Requests)
            Requests.Add((operation, body));

        var (status, answer) = Answer(operation, body);
        return new(status)
        {
            Content = answer == null ? null : new StringContent(JsonSerializer.Serialize(answer, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json"),
        };
    }
}

public static class TestJobs
{
    public sealed record Input(string Text);

    /// <summary>
    /// Tells a stage, then ends with a result and a file made of its input.
    /// </summary>
    public sealed record Echo : JobResult
    {
        public required string Text { get; init; }

        public required int Attempt { get; init; }
    }

    public sealed class Succeeding : IJobHandler
    {
        public const string Kind = "test_succeeding";

        public async Task<JobCompletion> RunAsync(JobRun run, CancellationToken cancellationToken)
        {
            var input = run.Input<Input>();
            await run.ReportAsync("working", 0.5, cancellationToken);
            return new(new Echo { Text = input.Text, Attempt = run.Attempt }, new("echo.txt", "text/plain", Encoding.UTF8.GetBytes(input.Text)));
        }
    }

    public sealed class Failing : IJobHandler
    {
        public const string Kind = "test_failing";

        public Task<JobCompletion> RunAsync(JobRun run, CancellationToken cancellationToken)
            => throw new JobFailedException("test_failure");
    }
}
