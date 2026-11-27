using System.Net;
using System.Net.Http.Json;
using HarmonyDB.Shelf.Model.Me;
using HarmonyDB.Shelf.Model.Playlists;
using HarmonyDB.Shelf.Model.Sync;
using HarmonyDB.Shelf.Tests.Auth;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Tests.Playlists;

/// <summary>
/// What the shelf shows of its own asks no source: lists, a song's page, the recent list. And someone's private own
/// chords are theirs alone in a playlist others see.
/// </summary>
public sealed class KeptListsTests(LibraryFixture fixture) : IClassFixture<LibraryFixture>
{
    [DatabaseFact]
    public async Task ListsSongPagesAndTheRecentListAskNoSource()
    {
        var (ann, annId) = await fixture.PersonAsync("ann");
        var playlist = await CreateAsync(ann, PlaylistAccess.Private);
        var chords = $"test-{Guid.NewGuid():N}";
        var song = (await Ok<Changed<SongRef>>(ann, HttpMethod.Post, $"/api/playlists/{playlist}/songs", new AddSongRequest { Chords = chords, Title = "A song" })).Value.Id;

        // opened, with what the person was shown
        await Ok<SyncResponse>(ann, HttpMethod.Post, "/api/sync", new SyncRequest
        {
            AccountId = annId,
            Changes = [new Change { Type = ChangeType.Opened, Chords = chords, SongId = song, Title = "As shown", Artists = ["Shown artist"], Source = new() { Key = "s", Title = "The source as shown" } }],
        });

        int Asked() => fixture.Index.Requests.Count;
        var before = Asked();
        var rows = (await Ok<SongList>(ann, HttpMethod.Get, $"/api/playlists/{playlist}/songs")).Rows;
        var library = (await Ok<SongList>(ann, HttpMethod.Get, "/api/library")).Rows;
        var page = await Ok<SongPage>(ann, HttpMethod.Get, $"/api/playlists/{playlist}/songs/{song}");
        var recent = await Ok<List<RecentEntry>>(ann, HttpMethod.Get, "/api/library/recent");
        Assert.Equal(before, Asked());

        Assert.Equal([chords], rows.Single().ChordsIds);
        Assert.Contains(library, x => x.SongId == song);
        Assert.Equal($"Title of {chords}", page.Variants.Single().Chords.Title);
        var opened = recent.Single(x => x.Chords?.Id == chords);
        Assert.Equal(("As shown", "The source as shown"), (opened.Title, opened.Source!.Title));
    }

    [DatabaseFact]
    public async Task SomeonesPrivateOwnChordsAreSeenOnlyByWhoeverAddedThem()
    {
        var (ann, annId) = await fixture.PersonAsync("ann");
        var (bob, bobId) = await fixture.PersonAsync("bob");
        var playlist = await CreateAsync(ann, PlaylistAccess.Public);
        var shared = (await Ok<Changed<SongRef>>(ann, HttpMethod.Post, $"/api/playlists/{playlist}/songs", new AddSongRequest { Chords = $"test-{Guid.NewGuid():N}", Title = "Shared" })).Value.Id;
        var other = (await Ok<Changed<SongRef>>(ann, HttpMethod.Post, $"/api/playlists/{playlist}/songs", new AddSongRequest { Chords = $"test-{Guid.NewGuid():N}", Title = "Other" })).Value.Id;

        // Ann's private own chords: a second variant of one song, and the only variant of another
        Guid onlyPrivate;
        await using (var db = fixture.CreateContext())
        {
            var mine = new HarmonyDB.Shelf.Database.Model.ChordsItem { Id = Guid.NewGuid(), SourceId = $"own-{annId}-{Guid.NewGuid():N}", CreatedAt = DateTimeOffset.UtcNow };
            var alone = new HarmonyDB.Shelf.Database.Model.ChordsItem { Id = Guid.NewGuid(), SourceId = $"own-{annId}-{Guid.NewGuid():N}", CreatedAt = DateTimeOffset.UtcNow };
            db.Chords.AddRange(mine, alone);
            db.PlaylistSongItems.Add(new() { Id = Guid.NewGuid(), SongId = shared, ChordsId = mine.Id, Position = 1, AddedById = annId, AddedAt = DateTimeOffset.UtcNow, Private = true, Address = "https://own.example/1" });
            var song = await db.PlaylistSongs.SingleAsync(x => x.Id == other);
            onlyPrivate = Guid.NewGuid();
            db.PlaylistSongs.Add(new() { Id = onlyPrivate, PlaylistId = playlist, Title = "Only private", Artists = [], Position = song.Position + 1, AddedById = annId, AddedAt = DateTimeOffset.UtcNow });
            db.PlaylistSongItems.Add(new() { Id = Guid.NewGuid(), SongId = onlyPrivate, ChordsId = alone.Id, Position = 0, AddedById = annId, AddedAt = DateTimeOffset.UtcNow, Private = true });
            await db.SaveChangesAsync();
        }

        // Ann sees all of it
        Assert.Equal(3, (await Ok<SongList>(ann, HttpMethod.Get, $"/api/playlists/{playlist}/songs")).Total);
        Assert.Equal(2, (await Ok<SongPage>(ann, HttpMethod.Get, $"/api/playlists/{playlist}/songs/{shared}")).Variants.Count);

        // Bob, and a guest, see neither the private variant nor the song of nothing else
        var guest = fixture.Api.NewBrowser();
        foreach (var someone in new[] { bob, guest })
        {
            var list = await Ok<SongList>(someone, HttpMethod.Get, $"/api/playlists/{playlist}/songs");
            Assert.Equal(2, list.Total);
            Assert.DoesNotContain(list.Rows, x => x.SongId == onlyPrivate);
            Assert.Single((await Ok<SongPage>(someone, HttpMethod.Get, $"/api/playlists/{playlist}/songs/{shared}")).Variants);
            Assert.Equal(HttpStatusCode.NotFound, (await someone.SendAsync(HttpMethod.Get, $"/api/playlists/{playlist}/songs/{onlyPrivate}")).StatusCode);
        }

        // Bob's copy takes what he sees; his library's chords for his device do not hold Ann's private ones
        var bobs = await CreateAsync(bob, PlaylistAccess.Private);
        var copied = (await Ok<Changed<AddedSong>>(bob, HttpMethod.Post, $"/api/playlists/{bobs}/songs", new AddSongRequest { FromSongId = shared })).Value;
        Assert.Single((await Ok<SongPage>(bob, HttpMethod.Get, $"/api/playlists/{bobs}/songs/{copied.Id}")).Variants);
        Assert.True((await bob.SendAsync(HttpMethod.Post, $"/api/playlists/{playlist}/follow")).IsSuccessStatusCode);
        var device = await Ok<OfflineChordsList>(bob, HttpMethod.Get, "/api/library/offline/chords");
        Assert.DoesNotContain(device.Chords, x => x.Id.StartsWith($"own-{annId}", StringComparison.Ordinal));
        Assert.NotEqual(Guid.Empty, bobId);
    }

    private static async Task<Guid> CreateAsync(Browser owner, PlaylistAccess access)
        => (await Ok<Changed<PlaylistDetails>>(owner, HttpMethod.Post, "/api/playlists", new CreatePlaylistRequest { Title = "A playlist", Access = access })).Value.Playlist.Id;

    private static async Task<T> Ok<T>(Browser browser, HttpMethod method, string path, object? body = null)
    {
        var response = await browser.SendAsync(method, path, body);
        Assert.True(response.IsSuccessStatusCode, $"{method} {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return response.StatusCode == HttpStatusCode.NoContent ? default! : (await response.Content.ReadFromJsonAsync<T>(Browser.Json))!;
    }
}
