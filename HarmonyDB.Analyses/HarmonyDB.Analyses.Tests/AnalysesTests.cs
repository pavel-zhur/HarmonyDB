using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HarmonyDB.Analyses.Api;
using HarmonyDB.Analyses.Api.Models;
using HarmonyDB.Analyses.Api.Services;
using HarmonyDB.Index.Api;
using HarmonyDB.Source.Api.Client.V2;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using HarmonyDB.Source.Api.Model.V2.Contents;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using OneShelf.Common.Api.Hosting;

namespace HarmonyDB.Analyses.Tests;

/// <summary>
/// The analyses module as the common application hosts it, beside the index that relays to it, on a real port and a
/// real PostgreSQL: a role and a schema of its own are made for the test (as CreateServiceRole.sql makes them) and
/// dropped after it. The server is given in ANALYSES_TEST_DATABASE (a superuser's connection string); without it the
/// tests are reported skipped.
/// </summary>
public sealed class AnalysesTests : IAsyncLifetime
{
    private const string WriteKey = "write-key-0123456789abcdefghijklmnopqrstuvwxyz";
    private const string IndexKey = "index-key-0123456789abcdefghijklmnopqrstuvwxyz";
    private const string ShelfKey = "shelf-key-0123456789abcdefghijklmnopqrstuvwxyz";
    private const string Template = "https://site.example/chords/{id}";

    private static readonly string? Server = Environment.GetEnvironmentVariable("ANALYSES_TEST_DATABASE");

    private static readonly Caller Guest = new() { Subjects = [], Keys = [] };

    private readonly string _role = $"analyses_test_{Guid.NewGuid():N}";
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"analyses-{Guid.NewGuid():N}");
    private WebApplication? _host;
    private HttpClient _write = null!;
    private SourceV2Client _index = null!;

    public async Task InitializeAsync()
    {
        if (Server == null) return;

        await Execute($"""CREATE ROLE "{_role}" LOGIN PASSWORD 'test'; CREATE SCHEMA "{_role}" AUTHORIZATION "{_role}"; ALTER ROLE "{_role}" SET search_path = "{_role}";""");
        Directory.CreateDirectory(_folder);

        // the relay calls the module over HTTP, as the common application does: on a port taken before
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var server = new NpgsqlConnectionStringBuilder(Server);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:AnalysesDatabase"] = new NpgsqlConnectionStringBuilder { Host = server.Host, Port = server.Port, Database = server.Database, Username = _role, Password = "test" }.ConnectionString,
            ["Analyses:AnalysesOptions:SourceKey"] = "analyses",
            ["Analyses:AnalysesOptions:SourceTitle"] = "Analyses",
            ["Analyses:AnalysesOptions:PublicUriTemplate"] = Template,
            ["Analyses:AnalysesOptions:FilesFolder"] = _folder,
            ["Analyses:AnalysesOptions:MaxFileBytes"] = "100000",
            ["Analyses:InternalClients:0:Name"] = "index",
            ["Analyses:InternalClients:0:Key"] = IndexKey,
            ["Analyses:InternalClients:0:Scopes:0"] = AnalysesOptions.SourceScope,
            ["Analyses:InternalClients:1:Name"] = "analyses",
            ["Analyses:InternalClients:1:Key"] = WriteKey,
            ["Analyses:InternalClients:1:Scopes:0"] = AnalysesOptions.WriteScope,
            ["Index:InternalClients:0:Name"] = "shelf",
            ["Index:InternalClients:0:Key"] = ShelfKey,
            ["Index:InternalClients:0:Scopes:0"] = "index",
            ["Index:RelayV2:Sources:0:Name"] = "analyses",
            ["Index:RelayV2:Sources:0:Endpoint"] = $"http://127.0.0.1:{port}/analyses/api/",
            ["Index:RelayV2:Sources:0:Key"] = IndexKey,
            ["Index:RelayV2:Sources:0:IdPrefixes:0"] = AnalysisStore.IdPrefix,
        });
        builder.AddApiModules(new IndexModule(), new AnalysesModule());

        _host = builder.Build();
        _host.MapApiModules();
        await _host.StartAsync();

        _write = new() { BaseAddress = new($"http://127.0.0.1:{port}/analyses/api/") };
        _write.DefaultRequestHeaders.Add("X-Internal-Key", WriteKey);
        _index = new(new HttpClient(), new()
        {
            Name = "index",
            Endpoint = new($"http://127.0.0.1:{port}/index/api/"),
            Key = ShelfKey,
            Timeout = TimeSpan.FromSeconds(30),
        }, NullLogger<SourceV2Client>.Instance);
    }

    public async Task DisposeAsync()
    {
        if (_host == null) return;

        await _host.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await Execute($"""DROP SCHEMA "{_role}" CASCADE; DROP ROLE "{_role}";""");
        Directory.Delete(_folder, recursive: true);
    }

    [DatabaseFact]
    public async Task AnAnalysisIsPutWithItsFilesAndReadThroughTheIndex()
    {
        var recording = Bytes(5000, 1);
        var bass = Bytes(3000, 2);
        var put = await PutAsync("song-1", WithFiles(stems: true), new() { Service = "youtube", Id = "track-1" });
        Assert.Equal(AnalysisStore.IdOf("analyses", "song-1"), put.Id);
        Assert.Equal(["bass.flac", "recording.mp3"], put.Files);

        // until every file is kept, the recording is not had, and neither are its files
        await PutFileAsync(put.Id, "recording.mp3", "audio/mpeg", recording);
        Assert.DoesNotContain(ContentKind.Recording, (await HeaderAsync(put.Id)).Kinds);
        Assert.Null(await _index.GetFile(Guest, put.Id, "recording.mp3", null, false, null, null));

        await PutFileAsync(put.Id, "bass.flac", "audio/flac", bass);
        var header = await HeaderAsync(put.Id);
        Assert.Equal([ContentKind.Timeline, ContentKind.Recording], header.Kinds);
        Assert.Equal(new Uri($"https://site.example/chords/{put.Id}"), header.Address);
        Assert.True(header.IsPublic);
        Assert.Equal(("youtube", "track-1"), (header.MadeFrom!.Service, header.MadeFrom.Id));

        var content = await _index.GetContent(Guest, put.Id, [ContentKind.Recording], null);
        Assert.Equal("recording.mp3", content.Content!.Recording!.Audio.File!.Name);

        // the whole file, a part, the headers alone, a range of another version
        using (var whole = (await _index.GetFile(Guest, put.Id, "recording.mp3", null, false, null, null))!)
        {
            Assert.Equal(200, whole.StatusCode);
            Assert.Equal(recording, await ReadAsync(whole));
            Assert.Equal("audio/mpeg", whole.Headers["Content-Type"].Single());
            Assert.Equal($"\"{Convert.ToHexString(SHA256.HashData(recording)).ToLowerInvariant()}\"", whole.Headers["ETag"].Single());
        }

        using (var part = (await _index.GetFile(Guest, put.Id, "bass.flac", null, false, "bytes=100-199", null))!)
        {
            Assert.Equal(206, part.StatusCode);
            Assert.Equal(bass[100..200], await ReadAsync(part));
        }

        using (var head = (await _index.GetFile(Guest, put.Id, "bass.flac", null, true, null, null))!)
            Assert.Equal((200, "3000"), (head.StatusCode, head.Headers["Content-Length"].Single()));

        using (var stale = (await _index.GetFile(Guest, put.Id, "bass.flac", null, false, "bytes=0-9", "\"other\""))!)
            Assert.Equal(200, stale.StatusCode);

        // a name the content does not give, an id that is not there
        Assert.Null(await _index.GetFile(Guest, put.Id, "drums.flac", null, false, null, null));
        Assert.Null(await _index.GetFile(Guest, AnalysisStore.IdPrefix + "0", "recording.mp3", null, false, null, null));

        // the public page's address resolves to the item
        var resolved = (await _index.Resolve(Guest, [new($"https://site.example/chords/{put.Id}"), new("https://site.example/chords/ha-0")])).Items;
        Assert.Equal([(ResolveState.Resolved, put.Id), (ResolveState.NotResolved, null)], resolved.Select(x => (x.State, x.Id)));

        Assert.Equal([put.Id], (await _index.Search(Guest, "Title song-1")).Headers.Select(x => x.Id));
        Assert.Equal(["analyses"], (await _index.GetSources(Guest)).Sources.Select(x => x.Key));
    }

    [DatabaseFact]
    public async Task PutAgainItKeepsItsIdAndLetsGoOfTheFilesItNoLongerNames()
    {
        var put = await PutAsync("song-2", WithFiles(stems: true));
        await PutFileAsync(put.Id, "recording.mp3", "audio/mpeg", Bytes(100, 3));
        await PutFileAsync(put.Id, "bass.flac", "audio/flac", Bytes(100, 4));
        var kept = Directory.GetFiles(_folder, "bass.flac.*", SearchOption.AllDirectories);
        Assert.Single(kept);

        var again = await PutAsync("song-2", WithFiles(stems: false));
        Assert.Equal(put.Id, again.Id);
        Assert.Equal(["recording.mp3"], again.Files);
        Assert.False(File.Exists(kept.Single()));
        Assert.Contains(ContentKind.Recording, (await HeaderAsync(put.Id)).Kinds);
        Assert.Null(await _index.GetFile(Guest, put.Id, "bass.flac", null, false, null, null));

        // a file sent again replaces the kept one
        await PutFileAsync(put.Id, "recording.mp3", "audio/mpeg", Bytes(200, 5));
        Assert.Single(Directory.GetFiles(_folder, "recording.mp3.*", SearchOption.AllDirectories));
        var files = await _write.GetFromJsonAsync<List<AnalysisFileInfo>>($"analyses/{put.Id}/files", SourceV2Json.Options);
        Assert.Equal(200, files!.Single().Bytes);
    }

    [DatabaseFact]
    public async Task WhatIsNotNamedOrTooLargeOrOfAnotherClientIsRefused()
    {
        var put = await PutAsync("song-3", WithFiles(stems: false));

        await AssertError(await _write.PutAsync($"analyses/{put.Id}/files/recording.mp3", Body(Bytes(10, 1), "audio/flac")), HttpStatusCode.BadRequest, AnalysesErrorCodes.FileNotNamed);
        await AssertError(await _write.PutAsync($"analyses/{put.Id}/files/other.mp3", Body(Bytes(10, 1), "audio/mpeg")), HttpStatusCode.BadRequest, AnalysesErrorCodes.FileNotNamed);
        await AssertError(await _write.PutAsync($"analyses/{put.Id}/files/recording.mp3", Body(Bytes(100_001, 1), "audio/mpeg")), HttpStatusCode.RequestEntityTooLarge, AnalysesErrorCodes.FileTooLarge);
        Assert.Empty(Directory.GetFiles(_folder, "*", SearchOption.AllDirectories));

        await AssertError(await _write.PutAsync("analyses", Json(new { key = "song-4", title = "x", artists = Array.Empty<string>(), content = new { } })), HttpStatusCode.BadRequest, AnalysesErrorCodes.TimelineMissing);

        // the source's own key does not write
        using var index = new HttpClient { BaseAddress = _write.BaseAddress };
        index.DefaultRequestHeaders.Add("X-Internal-Key", IndexKey);
        Assert.Equal(HttpStatusCode.Forbidden, (await index.PutAsync("analyses", Json(new { }))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await _write.DeleteAsync($"analyses/{put.Id}")).StatusCode);
        Assert.Equal(ItemState.NotOpen, (await _index.GetHeaders(Guest, [put.Id])).Headers.Single().State);
    }

    private static Content WithFiles(bool stems) => new()
    {
        Timeline = new() { Duration = 10, Keys = [], Bars = [], Chords = [], Sections = [] },
        Recording = new()
        {
            Audio = new() { File = new() { Name = "recording.mp3", ContentType = "audio/mpeg" } },
            Duration = 10,
            Stems = stems ? [new() { Stem = Stem.Bass, Audio = new() { File = new() { Name = "bass.flac", ContentType = "audio/flac" } } }] : [],
        },
    };

    private async Task<PutAnalysisResponse> PutAsync(string key, Content content, MusicServiceTrack? madeFrom = null)
    {
        var response = await _write.PutAsync("analyses", Json(new PutAnalysisRequest { Key = key, Title = $"Title {key}", Artists = ["Artist"], Content = content, MadeFrom = madeFrom }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PutAnalysisResponse>(SourceV2Json.Options))!;
    }

    private async Task PutFileAsync(string id, string name, string contentType, byte[] bytes)
        => Assert.Equal(HttpStatusCode.OK, (await _write.PutAsync($"analyses/{id}/files/{name}", Body(bytes, contentType))).StatusCode);

    private async Task<Header> HeaderAsync(string id) => (await _index.GetHeaders(Guest, [id])).Headers.Single().Header!;

    private static async Task<byte[]> ReadAsync(SourceFile file)
    {
        await using var body = await file.ReadBodyAsync(CancellationToken.None);
        using var copy = new MemoryStream();
        await body.CopyToAsync(copy);
        return copy.ToArray();
    }

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<AnalysesError>(SourceV2Json.Options))!.Code);
    }

    private static byte[] Bytes(int length, int seed) => Enumerable.Range(0, length).Select(x => (byte)(x * seed)).ToArray();

    private static ByteArrayContent Body(byte[] bytes, string contentType)
    {
        var body = new ByteArrayContent(bytes);
        body.Headers.ContentType = new(contentType);
        return body;
    }

    private static StringContent Json(object value) => new(JsonSerializer.Serialize(value, SourceV2Json.Options), Encoding.UTF8, "application/json");

    private static async Task Execute(string sql)
    {
        await using var connection = new NpgsqlConnection(Server);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// A test that needs the PostgreSQL of ANALYSES_TEST_DATABASE; skipped without it.
/// </summary>
public sealed class DatabaseFactAttribute : FactAttribute
{
    public DatabaseFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ANALYSES_TEST_DATABASE") == null)
            Skip = "ANALYSES_TEST_DATABASE is not set.";
    }
}
