using System.Net;
using System.Net.Http.Json;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Chords;
using HarmonyDB.Shelf.Model.Media;
using HarmonyDB.Shelf.Model.Playlists;
using HarmonyDB.Shelf.Model.Share;
using HarmonyDB.Shelf.Tests.Auth;
using HarmonyDB.Shelf.Tests.Playlists;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Tests.Share;

/// <summary>
/// Links of chords that carry their sharer's access: what a guest reads by one, what it does not open, how it stops;
/// and that an ordinary link gains nothing.
/// </summary>
public sealed class ShareAccessTests(LibraryFixture fixture) : IClassFixture<LibraryFixture>
{
    [DatabaseFact]
    public async Task AGuestReadsTheChordsAsTheSharerSawThemAndAnOrdinaryLinkGainsNothing()
    {
        var (ann, annId) = await fixture.PersonAsync("ann");
        await GrantAsync(annId);
        var guest = await GuestAsync();
        var chords = Keyed();

        // the guest cannot open them; an ordinary link does not change that
        Assert.Equal(ChordsState.NotOpen, (await Ok<ChordsContentResponse>(guest, HttpMethod.Get, $"/api/chords/{chords}")).State);
        var ordinary = await Ok<ShareLinkItem>(ann, HttpMethod.Post, "/api/share", new CreateShareRequest { Kind = ShareKind.Chords, Chords = chords });
        Assert.False(ordinary.CarriesAccess);
        var opened = await Ok<ShareView>(guest, HttpMethod.Get, $"/api/share/{ordinary.Token}");
        Assert.Equal((false, ChordsState.NotOpen), (opened.CarriesAccess, opened.Chords!.State));
        Assert.Equal(ChordsState.NotOpen, (await Ok<ChordsContentResponse>(guest, HttpMethod.Get, $"/api/share/{ordinary.Token}/chords")).State);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.SendAsync(HttpMethod.Post, $"/api/share/{ordinary.Token}/media")).StatusCode);

        // the link that carries Ann's access: none yet; then one per person and chords, with its address on the public page
        Assert.Null((await Ok<MyCarryingLink>(ann, HttpMethod.Get, $"/api/share/mine?chords={chords}")).Link);
        var carrying = await Ok<ShareLinkItem>(ann, HttpMethod.Post, "/api/share", new CreateShareRequest { Kind = ShareKind.Chords, Chords = chords, CarriesAccess = true });
        Assert.True(carrying.CarriesAccess);
        Assert.NotEqual(ordinary.Token, carrying.Token);
        Assert.Equal($"https://site.example/s/{carrying.Token}", carrying.PublicAddress);
        Assert.Equal(carrying, (await Ok<MyCarryingLink>(ann, HttpMethod.Get, $"/api/share/mine?chords={chords}")).Link);
        Assert.Equal(carrying.Token, (await Ok<ShareLinkItem>(ann, HttpMethod.Post, "/api/share", new CreateShareRequest { Kind = ShareKind.Chords, Chords = chords, CarriesAccess = true })).Token);

        var view = await Ok<ShareView>(guest, HttpMethod.Get, $"/api/share/{carrying.Token}");
        Assert.Equal((true, ChordsState.Open), (view.CarriesAccess, view.Chords!.State));
        var content = await Ok<ChordsContentResponse>(guest, HttpMethod.Get, $"/api/share/{carrying.Token}/chords?kinds=sheet");
        Assert.Equal(ChordsState.Open, content.State);
        Assert.NotNull(content.Content!.Sheet);
        Assert.NotNull((await Ok<ChordsMedia>(guest, HttpMethod.Post, $"/api/share/{carrying.Token}/media")).RecordingUrl);

        // read by Ann's keys alone, never as her account
        var caller = fixture.Index.Callers("V2GetContent").Last();
        Assert.Equal([LibraryFixture.Key], caller.GetProperty("keys").EnumerateArray().Select(x => x.GetString()));
        Assert.True(!caller.TryGetProperty("accountId", out var account) || account.ValueKind == System.Text.Json.JsonValueKind.Null);

        // the ordinary link still opens nothing to the guest
        Assert.Equal(ChordsState.NotOpen, (await Ok<ChordsContentResponse>(guest, HttpMethod.Get, $"/api/share/{ordinary.Token}/chords")).State);
    }

    [DatabaseFact]
    public async Task OnlyWhatIsOpenToTheSharerAndOnlyChordsAreSharedSo()
    {
        var (ann, annId) = await fixture.PersonAsync("ann");
        await GrantAsync(annId);
        var (bob, _) = await fixture.PersonAsync("bob");

        // Bob has not the key: nothing to carry
        await Browser.AssertError(await bob.SendAsync(HttpMethod.Post, "/api/share", new CreateShareRequest { Kind = ShareKind.Chords, Chords = Keyed(), CarriesAccess = true }),
            HttpStatusCode.Forbidden, PlaylistErrorCodes.ChordsNotOpen);

        // a song or a playlist does not carry access
        var playlist = (await Ok<Changed<PlaylistDetails>>(ann, HttpMethod.Post, "/api/playlists", new CreatePlaylistRequest { Title = "A playlist", Access = PlaylistAccess.Link })).Value.Playlist.Id;
        var chords = Keyed();
        var song = (await Ok<Changed<SongRef>>(ann, HttpMethod.Post, $"/api/playlists/{playlist}/songs", new AddSongRequest { Chords = chords, Title = "A song" })).Value.Id;
        await Browser.AssertError(await ann.SendAsync(HttpMethod.Post, "/api/share", new CreateShareRequest { Kind = ShareKind.Song, SongId = song, CarriesAccess = true }),
            HttpStatusCode.BadRequest, ErrorCodes.BadRequest);

        // the link opens its chords and nothing else of Ann's: not the playlist they are in, not by a song's link
        var carrying = await Ok<ShareLinkItem>(ann, HttpMethod.Post, "/api/share", new CreateShareRequest { Kind = ShareKind.Chords, Chords = chords, CarriesAccess = true });
        var guest = await GuestAsync();
        await Browser.AssertError(await guest.SendAsync(HttpMethod.Get, $"/api/playlists/{playlist}/songs?share={carrying.Token}"), HttpStatusCode.NotFound, PlaylistErrorCodes.NoSuchPlaylist);
        var songLink = await Ok<ShareLinkItem>(ann, HttpMethod.Post, "/api/share", new CreateShareRequest { Kind = ShareKind.Song, SongId = song });
        await Browser.AssertError(await guest.SendAsync(HttpMethod.Get, $"/api/share/{songLink.Token}/chords"), HttpStatusCode.BadRequest, ErrorCodes.BadRequest);
        var bySong = (await Ok<ShareView>(guest, HttpMethod.Get, $"/api/share/{songLink.Token}")).Song!;
        Assert.All(bySong.Variants, x => Assert.Equal(ChordsState.NotOpen, x.Chords.State));
    }

    [DatabaseFact]
    public async Task ALinkStopsWhenItsSharerRemovesItOrLosesTheKey()
    {
        var (ann, annId) = await fixture.PersonAsync("ann");
        await GrantAsync(annId);
        var (bob, _) = await fixture.PersonAsync("bob");
        var guest = await GuestAsync();

        // a key Ann loses is no longer carried
        var lost = await Ok<ShareLinkItem>(ann, HttpMethod.Post, "/api/share", new CreateShareRequest { Kind = ShareKind.Chords, Chords = Keyed(), CarriesAccess = true });
        Assert.Equal(ChordsState.Open, (await Ok<ChordsContentResponse>(guest, HttpMethod.Get, $"/api/share/{lost.Token}/chords")).State);
        await using (var db = fixture.CreateContext())
            await db.AccountPermissions.Where(x => x.AccountId == annId).ExecuteDeleteAsync();
        Assert.Equal(ChordsState.NotOpen, (await Ok<ChordsContentResponse>(guest, HttpMethod.Get, $"/api/share/{lost.Token}/chords")).State);
        await GrantAsync(annId);

        // removed by Ann only, and only a link that carries access
        var link = await Ok<ShareLinkItem>(ann, HttpMethod.Post, "/api/share", new CreateShareRequest { Kind = ShareKind.Chords, Chords = Keyed(), CarriesAccess = true });
        var ordinary = await Ok<ShareLinkItem>(ann, HttpMethod.Post, "/api/share", new CreateShareRequest { Kind = ShareKind.Chords, Chords = Keyed() });
        await Browser.AssertError(await bob.SendAsync(HttpMethod.Delete, $"/api/share/{link.Token}"), HttpStatusCode.NotFound, PlaylistErrorCodes.NoSuchLink);
        await Browser.AssertError(await ann.SendAsync(HttpMethod.Delete, $"/api/share/{ordinary.Token}"), HttpStatusCode.NotFound, PlaylistErrorCodes.NoSuchLink);
        await Browser.AssertError(await guest.SendAsync(HttpMethod.Delete, $"/api/share/{link.Token}"), HttpStatusCode.Unauthorized, ErrorCodes.SignInRequired);

        // the read of one's own link: Bob has none of Ann's, a guest is asked to sign in
        var linked = (await Ok<ShareView>(guest, HttpMethod.Get, $"/api/share/{link.Token}")).Chords!.Id;
        Assert.Equal(link.Token, (await Ok<MyCarryingLink>(ann, HttpMethod.Get, $"/api/share/mine?chords={linked}")).Link!.Token);
        Assert.Null((await Ok<MyCarryingLink>(bob, HttpMethod.Get, $"/api/share/mine?chords={linked}")).Link);
        await Browser.AssertError(await guest.SendAsync(HttpMethod.Get, $"/api/share/mine?chords={linked}"), HttpStatusCode.Unauthorized, ErrorCodes.SignInRequired);

        Assert.Equal(HttpStatusCode.NoContent, (await ann.SendAsync(HttpMethod.Delete, $"/api/share/{link.Token}")).StatusCode);
        Assert.Null((await Ok<MyCarryingLink>(ann, HttpMethod.Get, $"/api/share/mine?chords={linked}")).Link);
        await Browser.AssertError(await guest.SendAsync(HttpMethod.Get, $"/api/share/{link.Token}"), HttpStatusCode.NotFound, PlaylistErrorCodes.NoSuchLink);
        await Browser.AssertError(await guest.SendAsync(HttpMethod.Get, $"/api/share/{link.Token}/chords"), HttpStatusCode.NotFound, PlaylistErrorCodes.NoSuchLink);
        await Browser.AssertError(await guest.SendAsync(HttpMethod.Post, $"/api/share/{link.Token}/media"), HttpStatusCode.NotFound, PlaylistErrorCodes.NoSuchLink);
        await Browser.AssertError(await ann.SendAsync(HttpMethod.Delete, $"/api/share/{link.Token}"), HttpStatusCode.NotFound, PlaylistErrorCodes.NoSuchLink);
    }

    // ------------------------------------------------------------------ helpers

    private static string Keyed() => $"keyed-{Guid.NewGuid():N}";

    private async Task GrantAsync(Guid accountId)
    {
        await using var db = fixture.CreateContext();
        db.AccountPermissions.Add(new() { AccountId = accountId, Permission = LibraryFixture.Key, GrantedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// A guest's browser with its CSRF cookie.
    /// </summary>
    private async Task<Browser> GuestAsync()
    {
        var guest = fixture.Api.NewBrowser();
        await guest.SendAsync(HttpMethod.Get, "/api/app/configuration");
        return guest;
    }

    private static async Task<T> Ok<T>(Browser browser, HttpMethod method, string path, object? body = null)
    {
        var response = await browser.SendAsync(method, path, body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{method} {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Browser.Json))!;
    }
}
