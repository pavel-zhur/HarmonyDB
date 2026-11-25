using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarmonyDB.Shelf.Api.Clients.Audio;
using HarmonyDB.Shelf.Api.Clients.Illustrations;
using HarmonyDB.Shelf.Api.Clients.Index;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Model.Me;
using HarmonyDB.Shelf.Tests.Auth;
using HarmonyDB.Shelf.Tests.Chords;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace HarmonyDB.Shelf.Tests.Playlists;

/// <summary>
/// A database of its own, migrated, and the API over it (Development, the dev sign-in on) with a music service that
/// answers from <see cref="FakeAudio"/>.
/// </summary>
public sealed class LibraryFixture : IAsyncLifetime
{
    private readonly string _name = $"shelf_test_{Guid.NewGuid():N}";

    public string ConnectionString { get; private set; } = string.Empty;

    public FakeAudio Audio { get; } = new();

    /// <summary>
    /// The illustrations' covers: by chords id, the picture.
    /// </summary>
    public FakeIllustrations Illustrations { get; } = new();

    /// <summary>
    /// The sources, through the index: an id "test-…" is open (titled "Title of …"), "closed-…" is not open to
    /// anyone, "away-…" does not answer.
    /// </summary>
    public FakeIndex Index { get; } = new() { Answer = Sources };

    public LibraryApiFactory Api { get; private set; } = null!;

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

        Api = new(ConnectionString, Audio, Index, Illustrations);
    }

    private static (HttpStatusCode, object?) Sources(string operation, JsonElement body)
    {
        if (operation != "V2GetHeaders")
            return (HttpStatusCode.NotFound, null);

        return (HttpStatusCode.OK, new GetHeadersResponse
        {
            Headers = body.GetProperty("ids").EnumerateArray().Select(x => x.GetString()!).Select(id => id switch
            {
                _ when id.StartsWith("test-", StringComparison.Ordinal) => new HeaderAnswer { Id = id, State = ItemState.Open, Header = FakeIndex.Header(id, "A source") },
                _ when id.StartsWith("away-", StringComparison.Ordinal) => new HeaderAnswer { Id = id, State = ItemState.Unavailable },
                _ => new HeaderAnswer { Id = id, State = ItemState.NotOpen },
            }).ToList(),
        });
    }

    public ShelfDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ShelfDbContext>().UseShelfDatabase(ConnectionString).Options);

    /// <summary>
    /// A browser signed in as a new person (the dev sign-in), and the person's account.
    /// </summary>
    public async Task<(Browser Browser, Guid Id)> PersonAsync(string name)
    {
        var browser = Api.NewBrowser();
        await browser.DevAsync($"{name}-{Guid.NewGuid():N}");
        return (browser, (await browser.GetAsync<Profile>("/api/me")).Id);
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

public sealed class LibraryApiFactory(string connectionString, FakeAudio audio, FakeIndex index, FakeIllustrations illustrations) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration(x => x.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Shelf"] = connectionString,
            ["Shelf:Secrets:Key"] = Convert.ToBase64String(new byte[32]),
            ["SignIn:Dev:Enabled"] = "true",
            ["SignIn:RateLimitPermits"] = "100000",
            ["SignIn:PublicOrigin"] = "http://localhost",
            ["Clients:Audio:BaseUrl"] = "http://audio.test",
            ["Clients:Audio:Key"] = "an-internal-key-of-the-tests-0123456789",
            ["Clients:Index:Endpoint"] = FakeIndex.Endpoint,
            ["Clients:Index:Key"] = FakeIndex.Key,
            ["Clients:Illustrations:Endpoint"] = "http://illustrations.test/api/",
            ["Clients:Illustrations:Key"] = "an-internal-key-of-the-tests-0123456789",
            ["Jobs:Poll"] = "00:00:00.200",
            // a few songs make a playlist large here
            ["Playlists:LibraryShowLimit"] = "5",
        }));
        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient<AudioClient>().ConfigurePrimaryHttpMessageHandler(() => audio);
            services.AddHttpClient<IndexClient>().ConfigurePrimaryHttpMessageHandler(() => index);
            services.AddHttpClient<IllustrationsClient>().ConfigurePrimaryHttpMessageHandler(() => illustrations);
        });
    }

    public Browser NewBrowser() => new(CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false }));
}

/// <summary>
/// The music service as the tests set it: a link resolves to a playlist whose tracks are <see cref="Tracks"/>.
/// </summary>
public sealed class FakeAudio : HttpMessageHandler
{
    public List<(string Id, string Title, string Artist)> Tracks { get; } = [];

    public string Title { get; set; } = "A playlist there";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        object? answer = path switch
        {
            "/internal/resolve" => new { service = "youtube", type = "playlist", id = "PL1" },
            "/internal/playlists/by-link" => new
            {
                storedAt = DateTimeOffset.UtcNow,
                fetchedNow = true,
                data = new
                {
                    service = "youtube",
                    id = "PL1",
                    youtube = new
                    {
                        playlist = new { id = "PL1", title = Title },
                        tracks = Tracks.Select(x => new { id = x.Id, title = x.Title, durationMs = 200000, artists = new[] { new { name = x.Artist } } }).ToList(),
                    },
                },
            },
            _ => null,
        };

        return Task.FromResult(answer == null
            ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = JsonContent.Create(new { code = "not_found", message = "" }) }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(answer, options: new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
    }
}

/// <summary>
/// The illustrations' service as the tests set it: the covers of the chords in <see cref="Covers"/>.
/// </summary>
public sealed class FakeIllustrations : HttpMessageHandler
{
    public Dictionary<string, Guid> Covers { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri!.AbsolutePath != "/api/ShelfCovers")
            return new HttpResponseMessage(HttpStatusCode.NotFound);

        var keys = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement.GetProperty("keys").EnumerateArray().Select(x => x.GetString()!);
        Dictionary<string, Guid> covers;
        lock (Covers)
            covers = keys.Where(Covers.ContainsKey).ToDictionary(x => x, x => Covers[x]);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { covers = covers.Select(x => new { key = x.Key, id = x.Value }) }),
        };
    }
}
