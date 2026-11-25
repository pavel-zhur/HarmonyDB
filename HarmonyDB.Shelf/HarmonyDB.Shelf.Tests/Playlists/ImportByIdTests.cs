using System.Net;
using System.Net.Http.Json;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Jobs;
using HarmonyDB.Shelf.Model.Me;
using HarmonyDB.Shelf.Model.Playlists;
using HarmonyDB.Shelf.Tests.Auth;

namespace HarmonyDB.Shelf.Tests.Playlists;

/// <summary>
/// A playlist of a music service imported by its service and id, as the person's lists name it: the same playlist as
/// one imported by its link, read with the importer's own account when they linked one.
/// </summary>
public sealed class ImportByIdTests(LibraryFixture fixture) : IClassFixture<LibraryFixture>
{
    [DatabaseFact]
    public async Task APlaylistImportedByItsIdIsReadAsTheImporterAndIsTheOneOfItsLink()
    {
        var (ann, _) = await fixture.PersonAsync("ann");
        var (bob, _) = await fixture.PersonAsync("bob");
        fixture.Audio.Tracks.AddRange([("t1", "First", "X"), ("t2", "Second", "Y")]);
        var linked = await ann.SendAsync(HttpMethod.Put, "/api/me/music-links/youtube", new PutMusicLinkRequest { Credentials = "cookie: a=b" });
        Assert.Equal(HttpStatusCode.OK, linked.StatusCode);

        // ann's own: read with her account
        var imported = await ImportAsync(ann, new() { Service = MusicService.Youtube, PlaylistId = "PL1" });
        Assert.Equal((MusicService.Youtube, "PL1", (string?)null, true), Origin(imported.Value));
        var playlist = imported.Value.Playlist.Id;
        await WaitForSyncAsync(ann, playlist);
        Assert.Equal("person", fixture.Audio.PlaylistModes[^1]);
        Assert.Equal("A playlist there", (await Ok<PlaylistDetails>(ann, HttpMethod.Get, $"/api/playlists/{playlist}")).Playlist.Title);
        Assert.Equal(["First", "Second"], (await Ok<SongList>(ann, HttpMethod.Get, $"/api/playlists/{playlist}/songs")).Rows.Select(x => x.Title));

        // the same playlist by its id or by its link is imported once
        foreach (var again in new ImportPlaylistRequest[] { new() { Service = MusicService.Youtube, PlaylistId = " PL1 " }, new() { Url = "https://music.youtube.com/playlist?list=PL1" } })
            await Browser.AssertError(await ann.SendAsync(HttpMethod.Post, "/api/playlists/import", again), HttpStatusCode.Conflict, PlaylistErrorCodes.AlreadyImported);

        // synced later by the importer's button
        fixture.Audio.Tracks.Add(("t3", "Third", "Z"));
        await Ok<PlaylistDetails>(ann, HttpMethod.Post, $"/api/playlists/{playlist}/sync");
        await WaitForSyncAsync(ann, playlist);
        Assert.Equal(["First", "Second", "Third"], (await Ok<SongList>(ann, HttpMethod.Get, $"/api/playlists/{playlist}/songs")).Rows.Select(x => x.Title));

        // one who linked no account: read anonymously
        var bobs = (await ImportAsync(bob, new() { Service = MusicService.Youtube, PlaylistId = "PL1" })).Value.Playlist.Id;
        await WaitForSyncAsync(bob, bobs);
        Assert.Equal("anonymous", fixture.Audio.PlaylistModes[^1]);

        // a link, or a service with an id: one of the two, whole
        foreach (var bad in new ImportPlaylistRequest[]
                 {
                     new(),
                     new() { Service = MusicService.Youtube },
                     new() { PlaylistId = "PL1" },
                     new() { Url = "https://music.youtube.com/playlist?list=PL1", Service = MusicService.Youtube, PlaylistId = "PL1" },
                     new() { Service = MusicService.Youtube, PlaylistId = " " },
                 })
            await Browser.AssertError(await ann.SendAsync(HttpMethod.Post, "/api/playlists/import", bad), HttpStatusCode.BadRequest, ErrorCodes.BadRequest);
    }

    private static (MusicService, string, string?, bool) Origin(PlaylistDetails details)
        => (details.Playlist.Origin!.Service, details.Playlist.Origin.PlaylistId, details.Playlist.Origin.Url, details.Playlist.Origin.CanSync);

    private static Task<Changed<PlaylistDetails>> ImportAsync(Browser browser, ImportPlaylistRequest request)
        => Ok<Changed<PlaylistDetails>>(browser, HttpMethod.Post, "/api/playlists/import", request);

    private static async Task WaitForSyncAsync(Browser browser, Guid playlist)
    {
        for (var i = 0; i < 100; i++)
        {
            var sync = (await Ok<PlaylistDetails>(browser, HttpMethod.Get, $"/api/playlists/{playlist}")).Sync;
            if (sync is { Status: JobStatus.Succeeded or JobStatus.Failed })
            {
                Assert.Equal(JobStatus.Succeeded, sync.Status);
                return;
            }

            await Task.Delay(100);
        }

        Assert.Fail("The sync did not finish.");
    }

    private static async Task<T> Ok<T>(Browser browser, HttpMethod method, string path, object? body = null)
    {
        var response = await browser.SendAsync(method, path, body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{method} {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Browser.Json))!;
    }
}
