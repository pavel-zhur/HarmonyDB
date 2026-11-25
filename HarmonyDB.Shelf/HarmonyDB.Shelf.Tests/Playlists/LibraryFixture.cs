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
    /// The sources, through the index: an id "test-…" is open (titled "Title of …"), "keyed-…" is open to a caller
    /// with the key <see cref="Key"/> only (its content a sheet and a recording), "closed-…" is not open to anyone,
    /// "away-…" does not answer.
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

    /// <summary>
    /// The custom permission of the tests: the key that opens the "keyed-…" chords.
    /// </summary>
    public const string Key = "custom.one";

    private static (HttpStatusCode, object?) Sources(string operation, JsonElement body)
    {
        var keyed = body.TryGetProperty("caller", out var caller) && caller.GetProperty("keys").EnumerateArray().Any(x => x.GetString() == Key);
        if (operation == "V2GetContent" && body.GetProperty("id").GetString() is { } asked && asked.StartsWith("keyed-", StringComparison.Ordinal))
        {
            return (HttpStatusCode.OK, keyed
                ? new GetContentResponse
                {
                    Id = asked,
                    State = ItemState.Open,
                    Header = FakeIndex.Header(asked, "A keyed source"),
                    Content = new()
                    {
                        Sheet = new() { Root = new HarmonyDB.Common.Representations.OneShelf.NodeHtml { Name = "pre" } },
                        Recording = new() { Audio = new() { Blob = new() { Key = $"{asked}/take.mp3", ContentType = "audio/mpeg" } }, Duration = 100, Stems = [] },
                    },
                }
                : new GetContentResponse { Id = asked, State = ItemState.NotOpen });
        }

        if (operation != "V2GetHeaders")
            return (HttpStatusCode.NotFound, null);

        return (HttpStatusCode.OK, new GetHeadersResponse
        {
            Headers = body.GetProperty("ids").EnumerateArray().Select(x => x.GetString()!).Select(id => id switch
            {
                _ when id.StartsWith("test-", StringComparison.Ordinal) => new HeaderAnswer { Id = id, State = ItemState.Open, Header = FakeIndex.Header(id, "A source") },
                _ when id.StartsWith("keyed-", StringComparison.Ordinal) && keyed => new HeaderAnswer { Id = id, State = ItemState.Open, Header = FakeIndex.Header(id, "A keyed source") },
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
            ["PublicPage:Origin"] = "https://site.example",
            ["Permissions:Custom:0:Key"] = LibraryFixture.Key,
            ["Permissions:Custom:0:Label"] = "Custom one",
            ["Media:SigningKey"] = Convert.ToBase64String(Enumerable.Range(1, 32).Select(x => (byte)x).ToArray()),
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
/// The music service as the tests set it: a link resolves to the YouTube playlist <c>PL1</c>, whose tracks are
/// <see cref="Tracks"/>; any credentials of YouTube are an account. While <see cref="PlaylistPrivate"/>, the playlist
/// opens only to an account (anonymously it is not found, as the service answers of a private one).
/// </summary>
public sealed class FakeAudio : HttpMessageHandler
{
    public List<(string Id, string Title, string Artist)> Tracks { get; } = [];

    public string Title { get; set; } = "A playlist there";

    public bool PlaylistPrivate { get; set; }

    /// <summary>
    /// How each asking of the playlist acted (anonymous, person …), in order.
    /// </summary>
    public List<string?> PlaylistModes { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path == "/internal/youtube/playlists/PL1")
        {
            var mode = request.Headers.TryGetValues(AudioClient.ModeHeader, out var modes) ? modes.Single() : null;
            lock (PlaylistModes)
                PlaylistModes.Add(mode);
            if (PlaylistPrivate && mode != "person")
                path = "/not-open";
        }

        object? answer = path switch
        {
            "/internal/resolve" => new { service = "youtube", type = "playlist", id = "PL1" },
            "/internal/youtube/me" => new { authed = true, account = "Someone there" },
            "/internal/youtube/playlists/PL1" => new
            {
                storedAt = DateTimeOffset.UtcNow,
                fetchedNow = true,
                data = new
                {
                    playlist = new { id = "PL1", title = Title },
                    tracks = Tracks.Select(x => new { id = x.Id, title = x.Title, durationMs = 200000, artists = new[] { new { name = x.Artist } } }).ToList(),
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
/// The illustrations' service as the tests set it: the covers of the chords in <see cref="Covers"/>, their pictures in
/// <see cref="Images"/>, copies of the sides 256 and 512.
/// </summary>
public sealed class FakeIllustrations : HttpMessageHandler
{
    public Dictionary<string, Guid> Covers { get; } = new();

    public Dictionary<string, List<Guid>> Images { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        switch (request.RequestUri!.AbsolutePath)
        {
            case "/api/ShelfPresets":
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { presets = new[] { "a" }, sizes = new[] { 256, 512 } }, options: web) };
            case "/api/ShelfRead":
                var key = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement.GetProperty("key").GetString()!;
                List<Guid> images;
                lock (Images)
                    images = Images.GetValueOrDefault(key)?.ToList() ?? [];
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { images = images.Select(x => new { id = x, preset = "a", createdOn = DateTimeOffset.UtcNow }) }, options: web),
                };
        }

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
