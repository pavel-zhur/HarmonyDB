using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using OneShelf.Collectives.Api.Model.VInternal;
using OneShelf.Collectives.Database;
using OneShelf.Common.Api.Hosting;
using OneShelf.Common.Database;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Services;
using DatabaseVersion = OneShelf.Collectives.Database.Models.Version;
using ModelCollective = OneShelf.Collectives.Api.Model.V2.Sub.Collective;
using ModelVisibility = OneShelf.Collectives.Api.Model.V2.Sub.CollectiveVisibility;

namespace OneShelf.Collectives.Api.Tests;

/// <summary>
/// The own chords' store as the common application hosts it, over real databases (the store's and the old shelf's, each
/// made for the test and dropped after it, on the PostgreSQL given in COLLECTIVES_TEST_DATABASE as a superuser's
/// connection string; without it the tests are reported skipped): an edit gives a new code, and chords written in the
/// old shelf are read here and not changed here.
/// </summary>
public sealed class OwnFunctionsTests : IAsyncLifetime
{
    private const string Key = "shelf-key-0123456789abcdefghijklmnopqrstuvwxyz";
    private const long OldAuthor = 555;

    private static readonly string? Server = Environment.GetEnvironmentVariable("COLLECTIVES_TEST_DATABASE");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string _songs = $"songs_{Guid.NewGuid():N}";
    private readonly string _store = $"store_{Guid.NewGuid():N}";
    private WebApplication? _host;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        if (Server == null) return;

        await Execute($"CREATE DATABASE \"{_songs}\"; ");
        await Execute($"CREATE DATABASE \"{_store}\";");

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DatabaseProvider"] = "PostgreSql",
            ["ConnectionStrings:SongsDatabase"] = Connection(_songs),
            ["ConnectionStrings:CollectivesDatabase"] = Connection(_store),
            ["Collectives:CollectivesOptions:UrlTemplate"] = "https://own.example/?id={0}&tag={1}&version={2}",
            ["Collectives:CollectivesOptions:UrlIdPattern"] = @"^https\:\/\/own\.example\/\?id\=([0-9a-f\-]{36})\&tag\=[0-9]{6}\&version\=[0-9]+$",
            ["Collectives:InternalClients:0:Name"] = "shelf",
            ["Collectives:InternalClients:0:Key"] = Key,
            ["Collectives:InternalClients:0:Scopes:0"] = CollectivesModule.InternalScope,
        });
        builder.AddApiModules(new CollectivesModule());

        _host = builder.Build();
        _host.MapApiModules();
        await _host.StartAsync();

        await using (var songs = Songs())
            await songs.Database.MigrateAsync();
        await using (var store = Store())
            await store.Database.MigrateAsync();

        _client = _host.GetTestClient();
        _client.DefaultRequestHeaders.Add("X-Internal-Key", Key);
    }

    public async Task DisposeAsync()
    {
        if (_host == null) return;

        await _host.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await Execute($"DROP DATABASE \"{_songs}\" WITH (FORCE);");
        await Execute($"DROP DATABASE \"{_store}\" WITH (FORCE);");
    }

    [DatabaseFact]
    public async Task AnEditGivesANewCodeSoTheLinkToTheOlderVersionOpensNothing()
    {
        var author = new { subjects = new[] { "account:a" } };
        var inserted = await Post<OwnCollective>("OwnInsert", new { author, collective = Text("First", ModelVisibility.Private) });
        var updated = await Post<OwnCollective>("OwnUpdate", new { author, collectiveId = inserted.CollectiveId, collective = Text("Second", ModelVisibility.Private) });

        Assert.True(inserted.Editable);
        Assert.Equal(2, updated.VersionNumber);
        Assert.NotEqual(inserted.SearchTag, updated.SearchTag);
        Assert.Contains($"tag={updated.SearchTag}&version=2", updated.Uri.ToString());
    }

    [DatabaseFact]
    public async Task OldChordsAreReadHereAndNotChangedHere()
    {
        var collectiveId = Guid.NewGuid();
        await using (var store = Store())
        {
            await store.UpsertCollective(new()
            {
                Id = collectiveId,
                CreatedByUserId = OldAuthor,
                LatestVisibility = Database.Models.CollectiveVisibility.Private,
                Versions = [new DatabaseVersion { Title = "First", Authors = ["Somebody"], Contents = "Am\nwords", SearchTag = 111111, CreatedOn = DateTime.UtcNow, Visibility = Database.Models.CollectiveVisibility.Private }],
            });
        }

        var author = new { subjects = new[] { "account:a", $"telegram:{OldAuthor}" } };
        var read = await Post<OwnCollective>("OwnGet", new { author, collectiveId });
        Assert.False(read.Editable);
        Assert.Equal("First", read.Collective.Title);
        Assert.Contains((await Post<OwnListResponse>("OwnList", new { author })).Items, x => x.CollectiveId == collectiveId && !x.Editable);

        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsJsonAsync("http://localhost/collectives/api/OwnUpdate", new { author, collectiveId, collective = Text("Second", ModelVisibility.Public) }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsJsonAsync("http://localhost/collectives/api/OwnDelete", new { author, collectiveId }, Json)).StatusCode);

        await using (var store = Store())
        {
            var kept = await store.GetCollective(collectiveId);
            Assert.Single(kept!.Versions);
            Assert.Equal(Database.Models.CollectiveVisibility.Private, kept.LatestVisibility);
        }

        // another person's names reach nothing
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync("http://localhost/collectives/api/OwnGet", new { author = new { subjects = new[] { "telegram:1" } }, collectiveId }, Json)).StatusCode);
    }
    [DatabaseFact]
    public async Task RemovingPrivateOwnChordsRemovesTheirRowsAndLeavesTheOthers()
    {
        var author = new { subjects = new[] { "account:b", $"telegram:{OldAuthor}" } };
        var privateOne = await Post<OwnCollective>("OwnInsert", new { author, collective = Text("Private", ModelVisibility.Private) });
        var deletedPrivate = await Post<OwnCollective>("OwnInsert", new { author, collective = Text("Deleted", ModelVisibility.Private) });
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsJsonAsync("http://localhost/collectives/api/OwnDelete", new { author, collectiveId = deletedPrivate.CollectiveId }, Json)).StatusCode);
        var club = await Post<OwnCollective>("OwnInsert", new { author, collective = Text("Club", ModelVisibility.Club) });
        var shown = await Post<OwnCollective>("OwnInsert", new { author, collective = Text("Public", ModelVisibility.Public) });
        var someoneElses = await Post<OwnCollective>("OwnInsert", new { author = new { subjects = new[] { "account:c" } }, collective = Text("Theirs", ModelVisibility.Private) });
        var old = Guid.NewGuid();
        await using (var store = Store())
        {
            await store.UpsertCollective(new()
            {
                Id = old,
                CreatedByUserId = OldAuthor,
                LatestVisibility = Database.Models.CollectiveVisibility.Private,
                Versions = [new DatabaseVersion { Title = "Old", Authors = ["Somebody"], Contents = "Am\nwords", SearchTag = 111111, CreatedOn = DateTime.UtcNow, Visibility = Database.Models.CollectiveVisibility.Private }],
            });
        }

        Assert.Equal(2, (await Post<OwnRemovePrivateResponse>("OwnRemovePrivate", new { author })).Removed);
        Assert.Equal(0, (await Post<OwnRemovePrivateResponse>("OwnRemovePrivate", new { author })).Removed);

        await using (var store = Store())
        {
            var left = await store.AuthoredCollectives.Select(x => x.Id).ToListAsync();
            Assert.DoesNotContain(privateOne.CollectiveId, left);
            Assert.DoesNotContain(deletedPrivate.CollectiveId, left);
            Assert.Contains(club.CollectiveId, left);
            Assert.Contains(shown.CollectiveId, left);
            Assert.Contains(someoneElses.CollectiveId, left);
            Assert.NotNull(await store.GetCollective(old));
        }
    }

    private static ModelCollective Text(string title, ModelVisibility visibility) => new()
    {
        Title = title,
        Authors = ["Somebody"],
        Contents = "Am\nwords",
        Visibility = visibility,
    };

    private async Task<T> Post<T>(string operation, object body)
    {
        var response = await _client.PostAsJsonAsync($"http://localhost/collectives/api/{operation}", body, Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private SongsDatabase Songs() => Create<SongsDatabase>(new DbContextOptionsBuilder<SongsDatabase>()
        .UseDatabase(DatabaseProvider.PostgreSql, Connection(_songs), SongsDatabase.PostgreSqlMigrationsAssembly).Options);

    private CollectivesDatabase Store() => Create<CollectivesDatabase>(new DbContextOptionsBuilder<CollectivesDatabase>()
        .UseDatabase(DatabaseProvider.PostgreSql, Connection(_store), CollectivesDatabase.PostgreSqlMigrationsAssembly).Options);

    /// <summary>
    /// A context as the application makes it (its sets are made by the context, not given).
    /// </summary>
    private static T Create<T>(DbContextOptions options) where T : DbContext
        => ActivatorUtilities.CreateInstance<T>(new ServiceCollection().AddLogging().AddSingleton<SongsDatabaseMemory>().BuildServiceProvider(), options);

    private static string Connection(string database) => new NpgsqlConnectionStringBuilder(Server) { Database = database }.ConnectionString;

    private static async Task Execute(string sql)
    {
        await using var connection = new NpgsqlConnection(Server);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// A test that needs the PostgreSQL of COLLECTIVES_TEST_DATABASE; skipped without it.
/// </summary>
public sealed class DatabaseFactAttribute : FactAttribute
{
    public DatabaseFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("COLLECTIVES_TEST_DATABASE") == null)
            Skip = "COLLECTIVES_TEST_DATABASE is not set.";
    }
}
