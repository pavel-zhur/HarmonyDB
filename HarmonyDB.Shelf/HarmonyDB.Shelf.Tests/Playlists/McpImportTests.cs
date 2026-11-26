using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Jobs;
using HarmonyDB.Shelf.Model.Mcp;
using HarmonyDB.Shelf.Model.Me;
using HarmonyDB.Shelf.Model.Playlists;
using HarmonyDB.Shelf.Tests.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace HarmonyDB.Shelf.Tests.Playlists;

/// <summary>
/// A music service read for the person through the MCP: a playlist imported by a tool is read under the person's linked
/// account only with that service's linked permission, anonymously with its anonymous one only, and not at all without
/// either; the person's own button in the app reads as them. A track added by a tool needs the service's permission too.
/// </summary>
public sealed class McpImportTests(LibraryFixture fixture) : IClassFixture<LibraryFixture>
{
    private const string Link = "https://music.youtube.com/playlist?list=PL1";

    [DatabaseFact]
    public async Task WithTheLinkedPermissionThePlaylistIsReadAsThePerson()
    {
        var (browser, accountId) = await PersonAsync("mcp-linked", true);
        fixture.Audio.PlaylistPrivate = true;
        fixture.Audio.Tracks.Add(("l1", "Linked one", "X"));

        await using var client = await ConnectAsync(browser, accountId, BuiltInPermissions.McpLibraryWrite, BuiltInPermissions.McpYoutubeLinked);
        var answer = Json(await client.CallToolAsync("import_music_playlist", new Dictionary<string, object?> { ["url"] = Link }));
        Assert.Equal("asImporter", answer.GetProperty("reading").GetString());

        var job = await WaitAsync(client, answer.GetProperty("syncJobId").GetString()!);
        Assert.Equal("succeeded", job.GetProperty("status").GetString());
        Assert.Equal("person", fixture.Audio.PlaylistModes[^1]);
    }

    [DatabaseFact]
    public async Task WithTheAnonymousPermissionOnlyItIsReadAnonymouslyAndThePersonsButtonReadsAsThem()
    {
        var (browser, accountId) = await PersonAsync("mcp-anonymous", true);
        fixture.Audio.PlaylistPrivate = true;
        fixture.Audio.Tracks.Add(("a1", "Anonymous one", "Y"));

        await using var client = await ConnectAsync(browser, accountId, BuiltInPermissions.McpLibraryWrite, BuiltInPermissions.McpYoutubeAnonymous);
        var answer = Json(await client.CallToolAsync("import_music_playlist", new Dictionary<string, object?> { ["url"] = Link }));
        Assert.Equal("anonymous", answer.GetProperty("reading").GetString());
        var playlistId = answer.GetProperty("playlist").GetProperty("id").GetString()!;

        // the person linked an account, yet the tool's import reads anonymously: the private playlist does not open
        var job = await WaitAsync(client, answer.GetProperty("syncJobId").GetString()!);
        Assert.Equal("failed", job.GetProperty("status").GetString());
        Assert.Equal("not_found", job.GetProperty("errorCode").GetString());
        Assert.Equal("anonymous", fixture.Audio.PlaylistModes[^1]);

        // the person's own button reads as them
        var synced = await browser.SendAsync(HttpMethod.Post, $"/api/playlists/{playlistId}/sync");
        Assert.Equal(HttpStatusCode.OK, synced.StatusCode);
        var details = await WaitForButtonAsync(browser, playlistId);
        Assert.Equal(JobStatus.Succeeded, details.Sync!.Status);
        Assert.Equal("person", fixture.Audio.PlaylistModes[^1]);

        // a track of the service may be added with its anonymous permission: the call passes to the service's catalog
        var song = (await browser.GetAsync<SongList>($"/api/playlists/{playlistId}/songs")).Rows[0];
        var added = await client.CallToolAsync("add_recording_to_song", new Dictionary<string, object?>
        {
            ["playlistId"] = playlistId, ["songId"] = song.SongId, ["service"] = "youtube", ["trackId"] = "any-track",
        });
        Assert.NotEqual(true, added.IsError);
    }

    [DatabaseFact]
    public async Task WithoutTheServicesPermissionNothingIsReadAndWithoutAnyMusicTheToolsAreNotListed()
    {
        var (browser, accountId) = await PersonAsync("mcp-none", true);
        fixture.Audio.PlaylistPrivate = false;
        var reads = fixture.Audio.PlaylistModes.Count;

        // another service's permission: the tools are listed, the calls for this service refused, naming its permissions
        await using (var client = await ConnectAsync(browser, accountId, BuiltInPermissions.McpLibraryWrite, BuiltInPermissions.McpYandexAnonymous, BuiltInPermissions.McpYandexLinked))
        {
            var imported = await client.CallToolAsync("import_music_playlist", new Dictionary<string, object?> { ["url"] = Link });
            Assert.True(imported.IsError);
            var error = Json(imported);
            Assert.Equal("permission_denied", error.GetProperty("error").GetString());
            Assert.Equal("mcp.youtube.linked mcp.youtube.anonymous", error.GetProperty("details").GetProperty("permission").GetString());

            var playlist = Json(await client.CallToolAsync("create_playlist", new Dictionary<string, object?> { ["title"] = "Mine" })).GetProperty("playlist").GetProperty("id").GetString()!;
            var song = await browser.SendAsync(HttpMethod.Post, $"/api/playlists/{playlist}/songs", new AddSongRequest { Chords = "test-1" });
            Assert.Equal(HttpStatusCode.OK, song.StatusCode);
            var songId = (await song.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("value").GetProperty("id").GetString();
            var added = await client.CallToolAsync("add_recording_to_song", new Dictionary<string, object?>
            {
                ["playlistId"] = playlist, ["songId"] = songId, ["service"] = "youtube", ["trackId"] = "t1",
            });
            Assert.True(added.IsError);
            Assert.Equal("permission_denied", Json(added).GetProperty("error").GetString());
        }

        await using (var db = fixture.CreateContext())
            Assert.False(await db.Playlists.AnyAsync(x => x.ImportedById == accountId));
        Assert.Equal(reads, fixture.Audio.PlaylistModes.Count);

        // no music permission at all: not listed
        await using (var client = await ConnectAsync(browser, accountId, BuiltInPermissions.McpLibraryWrite))
        {
            var tools = (await client.ListToolsAsync()).Select(x => x.Name).ToHashSet();
            Assert.Contains("create_playlist", tools);
            Assert.DoesNotContain("import_music_playlist", tools);
            Assert.DoesNotContain("add_recording_to_song", tools);
        }
    }

    /// <summary>
    /// A person with every music and library permission given, and (when <paramref name="linked"/>) an account of YouTube
    /// linked: what a client may do is what its key is allowed.
    /// </summary>
    private async Task<(Browser Browser, Guid AccountId)> PersonAsync(string name, bool linked)
    {
        var (browser, accountId) = await fixture.PersonAsync(name);
        await using (var db = fixture.CreateContext())
        {
            db.AccountPermissions.AddRange(new[]
                {
                    BuiltInPermissions.McpLibraryRead, BuiltInPermissions.McpLibraryWrite,
                    BuiltInPermissions.McpYoutubeAnonymous, BuiltInPermissions.McpYoutubeLinked,
                    BuiltInPermissions.McpYandexAnonymous, BuiltInPermissions.McpYandexLinked,
                }
                .Select(x => new AccountPermission { AccountId = accountId, Permission = x, GrantedAt = DateTimeOffset.UtcNow, GrantedBy = "tests" }));
            await db.SaveChangesAsync();
        }

        if (linked)
            Assert.Equal(HttpStatusCode.OK, (await browser.SendAsync(HttpMethod.Put, "/api/me/music-links/youtube", new PutMusicLinkRequest { Credentials = "cookie: a=b" })).StatusCode);

        return (browser, accountId);
    }

    private async Task<McpClient> ConnectAsync(Browser browser, Guid accountId, params string[] permissions)
    {
        var response = await browser.SendAsync(HttpMethod.Post, "/api/mcp/keys", new CreatePersonalKeyRequest { Name = $"key-{accountId:N}", Permissions = permissions });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var key = (await response.Content.ReadFromJsonAsync<CreatedPersonalKey>(Browser.Json))!.Key;
        return await McpClient.CreateAsync(new HttpClientTransport(new()
        {
            Endpoint = new Uri("http://localhost/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {key}" },
        }, fixture.Api.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false })));
    }

    private static async Task<JsonElement> WaitAsync(McpClient client, string jobId)
    {
        for (var i = 0; i < 100; i++)
        {
            var job = Json(await client.CallToolAsync("get_job", new Dictionary<string, object?> { ["jobId"] = jobId })).GetProperty("job");
            if (job.GetProperty("status").GetString() is "succeeded" or "failed")
                return job;

            await Task.Delay(100);
        }

        Assert.Fail("The job did not finish.");
        return default;
    }

    private static async Task<PlaylistDetails> WaitForButtonAsync(Browser browser, string playlistId)
    {
        for (var i = 0; i < 100; i++)
        {
            var details = await browser.GetAsync<PlaylistDetails>($"/api/playlists/{playlistId}");
            if (details.Sync is { Status: JobStatus.Succeeded or JobStatus.Failed })
                return details;

            await Task.Delay(100);
        }

        Assert.Fail("The sync did not finish.");
        return null!;
    }

    private static JsonElement Json(CallToolResult result) => JsonDocument.Parse(((TextContentBlock)result.Content[0]).Text).RootElement;
}
