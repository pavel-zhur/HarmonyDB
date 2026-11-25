using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using HarmonyDB.Shelf.Api.Areas.PublicPage.Services;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Playlists;
using HarmonyDB.Shelf.Model.Share;
using HarmonyDB.Shelf.Tests.Auth;
using HarmonyDB.Shelf.Tests.Playlists;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace HarmonyDB.Shelf.Tests.PublicPage;

/// <summary>
/// The previews of the public page's addresses: what each kind of address shows, that one that does not open shows
/// nothing, that people's text is escaped, that the picture is chosen the same way every time; and the sitemap.
/// </summary>
public sealed partial class PublicPageTests(LibraryFixture fixture) : IClassFixture<LibraryFixture>
{
    [DatabaseFact]
    public async Task TheShowcaseAndItsSongsHaveTheirPreviewsAndTheSitemap()
    {
        var (ann, _) = await fixture.PersonAsync("ann");
        var playlist = (await Ok<Changed<PlaylistDetails>>(ann, HttpMethod.Post, "/api/playlists",
            new CreatePlaylistRequest { Title = "Show <b>\"&'", Description = "Songs to play", Access = PlaylistAccess.Public })).Value.Playlist.Id;
        var chords = $"test-{Guid.NewGuid():N}";
        var song = (await Ok<Changed<SongRef>>(ann, HttpMethod.Post, $"/api/playlists/{playlist}/songs",
            new AddSongRequest { Chords = chords, Title = "A <script>alert(1)</script> song" })).Value.Id;
        List<Guid> images = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        lock (fixture.Illustrations.Images)
            fixture.Illustrations.Images[chords] = images;

        using var api = Showcase(playlist);
        using var client = api.CreateClient();

        var main = await PreviewAsync(client, "/");
        Assert.Equal("Show &lt;b&gt;&quot;&amp;&#x27;", Tag(main, "og:title"));
        Assert.Equal("Songs to play", Tag(main, "og:description"));
        Assert.Equal("https://site.example/icon-512.png", Tag(main, "og:image"));
        Assert.Equal("https://site.example/", Tag(main, "og:url"));

        // a song of it: its title (escaped), its artists, one of its illustrations, the same one every time
        var page = await PreviewAsync(client, $"/songs/{song}");
        Assert.DoesNotContain("<script>", page);
        Assert.Contains("<title>A &lt;script&gt;alert(1)&lt;/script&gt; song — An artist</title>", page);
        Assert.Equal("An artist", Tag(page, "og:description"));
        var picture = Tag(page, "og:image");
        var chosen = PagePreviews.Choose($"/songs/{song}", images.Order().ToList());
        Assert.Equal($"https://site.example/api/illustrations/images/{chosen}/512", picture);
        Assert.Equal(chosen, PagePreviews.Choose($"/songs/{song}", images.Order().ToList()));
        Assert.Equal("512", Tag(page, "og:image:width"));

        // a song of another playlist is not one of the showcase
        var other = (await Ok<Changed<PlaylistDetails>>(ann, HttpMethod.Post, "/api/playlists", new CreatePlaylistRequest { Title = "Other", Access = PlaylistAccess.Public })).Value.Playlist.Id;
        var elsewhere = (await Ok<Changed<SongRef>>(ann, HttpMethod.Post, $"/api/playlists/{other}/songs", new AddSongRequest { Chords = $"test-{Guid.NewGuid():N}", Title = "x" })).Value.Id;
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync($"/api/public/preview/songs/{elsewhere}")).StatusCode);

        var sitemap = await client.GetStringAsync("/api/public/sitemap");
        Assert.Contains("<loc>https://site.example/</loc>", sitemap);
        Assert.Contains($"<loc>https://site.example/songs/{song}</loc>", sitemap);
        Assert.DoesNotContain(elsewhere.ToString(), sitemap);
    }

    [DatabaseFact]
    public async Task ALinkShowsWhatItsHolderSeesAndOneThatDoesNotOpenShowsNothing()
    {
        var (ann, annId) = await fixture.PersonAsync("ann");
        await using (var db = fixture.CreateContext())
        {
            db.AccountPermissions.Add(new() { AccountId = annId, Permission = LibraryFixture.Key, GrantedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        using var client = fixture.Api.CreateClient();
        var keyed = $"keyed-{Guid.NewGuid():N}";

        // the link that carries Ann's access: the chords as she saw them; an ordinary one to them: nothing
        var carrying = await Ok<ShareLinkItem>(ann, HttpMethod.Post, "/api/share", new CreateShareRequest { Kind = ShareKind.Chords, Chords = keyed, CarriesAccess = true });
        Assert.Equal($"Title of {keyed}", Tag(await PreviewAsync(client, $"/s/{carrying.Token}"), "og:title"));
        var ordinary = await Ok<ShareLinkItem>(ann, HttpMethod.Post, "/api/share", new CreateShareRequest { Kind = ShareKind.Chords, Chords = keyed });
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync($"/api/public/preview/s/{ordinary.Token}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync($"/api/public/preview/chords/{keyed}")).StatusCode);

        // chords open to a guest, by their id; a song by its link, from a private playlist, and by a playlist's link
        var open = $"test-{Guid.NewGuid():N}";
        Assert.Equal($"Title of {open}", Tag(await PreviewAsync(client, $"/chords/{open}"), "og:title"));
        var hidden = (await Ok<Changed<PlaylistDetails>>(ann, HttpMethod.Post, "/api/playlists", new CreatePlaylistRequest { Title = "Mine", Access = PlaylistAccess.Private })).Value.Playlist.Id;
        var song = (await Ok<Changed<SongRef>>(ann, HttpMethod.Post, $"/api/playlists/{hidden}/songs", new AddSongRequest { Chords = open, Title = "Hidden song" })).Value.Id;
        var songLink = await Ok<ShareLinkItem>(ann, HttpMethod.Post, "/api/share", new CreateShareRequest { Kind = ShareKind.Song, SongId = song });
        Assert.Equal("Hidden song", Tag(await PreviewAsync(client, $"/s/{songLink.Token}"), "og:title"));
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync($"/api/public/preview/s/{songLink.Token}/{Guid.NewGuid()}")).StatusCode);

        // a broken token, a removed link, an address the server knows nothing of: nothing
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/public/preview/s/no-such-token")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ann.SendAsync(HttpMethod.Delete, $"/api/share/{carrying.Token}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync($"/api/public/preview/s/{carrying.Token}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/public/preview/somewhere/else")).StatusCode);
    }

    // ------------------------------------------------------------------ helpers

    private WebApplicationFactory<Program> Showcase(Guid playlist)
        => fixture.Api.WithWebHostBuilder(x => x.ConfigureAppConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["App:ShowcasePlaylistId"] = playlist.ToString(),
        })));

    private static async Task<string> PreviewAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync("/api/public/preview" + path);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {(int)response.StatusCode}");
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        return await response.Content.ReadAsStringAsync();
    }

    private static string? Tag(string html, string property)
    {
        var match = MetaPattern().Matches(html).FirstOrDefault(x => x.Groups[1].Value == property);
        return match?.Groups[2].Value;
    }

    [GeneratedRegex("<meta (?:property|name)=\"([^\"]+)\" content=\"([^\"]*)\">")]
    private static partial Regex MetaPattern();

    private static async Task<T> Ok<T>(Browser browser, HttpMethod method, string path, object? body = null)
    {
        var response = await browser.SendAsync(method, path, body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{method} {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Browser.Json))!;
    }
}
