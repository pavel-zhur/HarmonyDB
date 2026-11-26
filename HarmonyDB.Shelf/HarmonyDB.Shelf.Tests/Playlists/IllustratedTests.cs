using System.Net;
using System.Net.Http.Json;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Playlists;
using HarmonyDB.Shelf.Model.Sync;
using HarmonyDB.Shelf.Tests.Auth;

namespace HarmonyDB.Shelf.Tests.Playlists;

/// <summary>
/// The chords of the person's library that have an illustration.
/// </summary>
public sealed class IllustratedTests(LibraryFixture fixture) : IClassFixture<LibraryFixture>
{
    [DatabaseFact]
    public async Task OnlyTheLibrarysOpenChordsWithAnIllustrationAreGivenByTitle()
    {
        var (ann, annId) = await fixture.PersonAsync("ann");
        var tag = Guid.NewGuid().ToString("N");
        string Id(string name) => $"test-{name}-{tag}";
        var (b, a, plain, closed, marked, elsewhere) = (Id("b"), Id("a"), Id("plain"), $"closed-{tag}", Id("marked"), Id("elsewhere"));
        lock (fixture.Illustrations.Covers)
        {
            foreach (var id in new[] { b, a, closed, marked, elsewhere })
                fixture.Illustrations.Covers[id] = Guid.NewGuid();
        }

        var playlist = (await Ok<Changed<PlaylistDetails>>(ann, HttpMethod.Post, "/api/playlists", new CreatePlaylistRequest { Title = "Mine", Access = PlaylistAccess.Private })).Value.Playlist.Id;
        foreach (var chords in new[] { b, a, plain })
            await Ok<Changed<SongRef>>(ann, HttpMethod.Post, $"/api/playlists/{playlist}/songs", new AddSongRequest { Chords = chords, Title = chords });
        await Ok<SyncResponse>(ann, HttpMethod.Post, "/api/sync", new SyncRequest { AccountId = annId, Changes = [new() { Type = ChangeType.Mark, Chords = marked, Kind = "star", On = true }, new() { Type = ChangeType.Mark, Chords = closed, Kind = "star", On = true }] });

        var all = await Ok<IllustratedChordsList>(ann, HttpMethod.Get, "/api/library/illustrated");
        Assert.Equal(3, all.Total);
        Assert.Equal([a, b, marked], all.Items.Select(x => x.Chords));
        Assert.All(all.Items, x => Assert.Equal($"/api/illustrations/images/{fixture.Illustrations.Covers[x.Chords]}/256", x.PictureUrl));
        Assert.Equal($"Title of {a}", all.Items[0].Title);

        var second = await Ok<IllustratedChordsList>(ann, HttpMethod.Get, "/api/library/illustrated?offset=1&limit=1");
        Assert.Equal((3, 1), (second.Total, second.Offset));
        Assert.Equal([b], second.Items.Select(x => x.Chords));

        await Browser.AssertError(await fixture.Api.NewBrowser().SendAsync(HttpMethod.Get, "/api/library/illustrated"), HttpStatusCode.Unauthorized, ErrorCodes.SignInRequired);
    }

    [DatabaseFact]
    public async Task PresetsAndPicturesSayTheirMood()
    {
        var (ann, _) = await fixture.PersonAsync("ann");
        var chords = $"test-mood-{Guid.NewGuid():N}";
        lock (fixture.Illustrations.Images)
            fixture.Illustrations.Images[chords] = [Guid.NewGuid()];

        var presets = await Ok<Model.Illustrations.IllustrationPresetList>(ann, HttpMethod.Get, "/api/illustrations/presets");
        Assert.Equal([("a", "evil")], presets.Presets.Select(x => (x.Key, x.Mood)));
        var drawn = await Ok<Model.Illustrations.ChordsIllustrations>(ann, HttpMethod.Get, $"/api/illustrations/{chords}");
        Assert.Equal(("a", "evil"), (drawn.Images.Single().Preset, drawn.Images.Single().Mood));
    }

    private static async Task<T> Ok<T>(Browser browser, HttpMethod method, string path, object? body = null)
    {
        var response = await browser.SendAsync(method, path, body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{method} {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Browser.Json))!;
    }
}
