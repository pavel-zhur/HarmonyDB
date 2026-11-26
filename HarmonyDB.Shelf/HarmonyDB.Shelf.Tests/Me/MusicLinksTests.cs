using System.Net;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Me;
using HarmonyDB.Shelf.Tests.Auth;
using HarmonyDB.Shelf.Tests.Playlists;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Tests.Me;

/// <summary>
/// A music link goes only after the music service has forgotten what it keeps under it: replaced, or removed.
/// </summary>
public sealed class MusicLinksTests(LibraryFixture fixture) : IClassFixture<LibraryFixture>
{
    [DatabaseFact]
    public async Task ALinkIsReplacedOrRemovedOnlyOnceTheMusicServiceHasPurgedIt()
    {
        var (ann, annId) = await fixture.PersonAsync("ann");
        async Task<Guid?> LinkAsync()
        {
            await using var db = fixture.CreateContext();
            return (await db.MusicLinks.SingleOrDefaultAsync(x => x.AccountId == annId))?.Id;
        }

        Assert.Equal(HttpStatusCode.OK, (await ann.SendAsync(HttpMethod.Put, "/api/me/music-links/youtube", new PutMusicLinkRequest { Credentials = "cookie: a=b" })).StatusCode);
        var first = (await LinkAsync())!.Value;

        // replaced: the old one purged first
        Assert.Equal(HttpStatusCode.OK, (await ann.SendAsync(HttpMethod.Put, "/api/me/music-links/youtube", new PutMusicLinkRequest { Credentials = "cookie: c=d" })).StatusCode);
        var second = (await LinkAsync())!.Value;
        Assert.NotEqual(first, second);
        Assert.Contains(first.ToString(), fixture.Audio.Purges.Subjects);

        // the music service does not answer: the link stays
        fixture.Audio.Purges.Down = true;
        try
        {
            await Browser.AssertError(await ann.SendAsync(HttpMethod.Delete, "/api/me/music-links/youtube"), HttpStatusCode.ServiceUnavailable, ErrorCodes.Unavailable);
            Assert.Equal(second, await LinkAsync());
        }
        finally
        {
            fixture.Audio.Purges.Down = false;
        }

        // it answers: purged, then removed
        Assert.Equal(HttpStatusCode.NoContent, (await ann.SendAsync(HttpMethod.Delete, "/api/me/music-links/youtube")).StatusCode);
        Assert.Contains(second.ToString(), fixture.Audio.Purges.Subjects);
        Assert.Null(await LinkAsync());
    }
}
