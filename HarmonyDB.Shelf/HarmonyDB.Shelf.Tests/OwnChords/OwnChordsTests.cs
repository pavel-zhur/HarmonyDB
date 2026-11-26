using System.Net;
using HarmonyDB.Shelf.Api.Areas.OwnChords.Services;
using HarmonyDB.Shelf.Api.Clients.OwnChords;
using HarmonyDB.Source.Api.Model.V2.Api;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Illustrations;
using HarmonyDB.Shelf.Model.OwnChords;
using HarmonyDB.Shelf.Tests.Api;
using HarmonyDB.Shelf.Tests.Auth;
using HarmonyDB.Shelf.Tests.Chords;
using HarmonyDB.Shelf.Tests.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace HarmonyDB.Shelf.Tests.OwnChords;

/// <summary>
/// Own chords: who the store is told the author is, what is refused before it, who may draw illustrations. Who opens
/// whose own chords is the own chords' source's (its tests).
/// </summary>
public sealed class OwnChordsTests(WorkFixture fixture) : IClassFixture<WorkFixture>
{
    [DatabaseFact]
    public async Task TheAuthorIsKnownByTheAccountAndItsTelegramSignIns()
    {
        var withTelegram = await fixture.AccountAsync(("telegram", "111"), ("google", "g-1"), ("telegram", "222"));
        var withoutTelegram = await fixture.AccountAsync(("google", "g-2"));
        fixture.Store.Answer = (_, _) => (HttpStatusCode.NotFound, null);
        using var client = fixture.Api.CreateClient();
        var id = Guid.NewGuid();

        foreach (var account in new[] { withTelegram, withoutTelegram })
        {
            // the store tells the chords are not this author's: the person is told there are none of theirs
            await Browser.AssertError(
                await client.SendAsync(FakeIndex.As(HttpMethod.Put, $"/api/own-chords/{id}", account, body: new UpdateOwnChordsRequest { Text = Text() })),
                HttpStatusCode.NotFound,
                OwnChordsErrorCodes.NoSuchOwnChords);
        }

        var authors = fixture.Store.Requests.Where(x => x.Operation == "OwnUpdate" && x.Body.GetProperty("collectiveId").GetGuid() == id).Select(x => x.Body.GetProperty("author")).ToList();
        Assert.Equal([$"account:{withTelegram:D}", "telegram:111", "telegram:222"], authors[0].GetProperty("subjects").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal([$"account:{withoutTelegram:D}"], authors[1].GetProperty("subjects").EnumerateArray().Select(x => x.GetString()));
    }

    [DatabaseFact]
    public async Task OwnChordsWrittenInTheOldShelfAreNotChangedOrDeletedHere()
    {
        var account = await fixture.AccountAsync(("telegram", "555"));
        var id = Guid.NewGuid();
        var old = new StoredOwnChords(id, new(["Artist"], "Title", ">Am\nla", StoredVisibility.Private, null, null, null, null),
            1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new($"https://own.example/{id}"), "telegram:555", false, null);
        fixture.Store.Answer = (operation, _) => operation switch
        {
            "OwnGet" => (HttpStatusCode.OK, old),
            "OwnUpdate" or "OwnDelete" => (HttpStatusCode.Conflict, null),
            _ => (HttpStatusCode.NotFound, null),
        };
        using var client = fixture.Api.CreateClient();

        await Browser.AssertError(
            await client.SendAsync(FakeIndex.As(HttpMethod.Put, $"/api/own-chords/{id}", account, body: new UpdateOwnChordsRequest { Text = Text() })),
            HttpStatusCode.Conflict,
            OwnChordsErrorCodes.NotEditable);
        await Browser.AssertError(
            await client.SendAsync(FakeIndex.As(HttpMethod.Delete, $"/api/own-chords/{id}", account)),
            HttpStatusCode.Conflict,
            OwnChordsErrorCodes.NotEditable);
        Assert.DoesNotContain(fixture.Store.Requests, x => x.Operation == "OwnDelete" && x.Body.GetProperty("collectiveId").GetGuid() == id);
    }

    [DatabaseFact]
    public async Task DeletingAProfileAsksTheStoreToRemoveItsPrivateOwnChordsFirst()
    {
        var account = await fixture.AccountAsync(("telegram", "444"));
        fixture.Store.Answer = (operation, _) => operation == "OwnRemovePrivate"
            ? (HttpStatusCode.OK, new { removed = 1 })
            : (HttpStatusCode.NotFound, null);

        await using var scope = fixture.Api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HarmonyDB.Shelf.Api.Areas.Me.Services.ProfileRemoval>().DeleteAsync(account, CancellationToken.None);

        // one removal by all the person's names (the store removes only what was written here); nothing one by one
        var removals = fixture.Store.Requests.Where(x => x.Operation == "OwnRemovePrivate"
            && x.Body.GetProperty("author").GetProperty("subjects")[0].GetString() == $"account:{account:D}").ToList();
        Assert.Equal([$"account:{account:D}", "telegram:444"], Assert.Single(removals).Body.GetProperty("author").GetProperty("subjects").EnumerateArray().Select(x => x.GetString()));
        Assert.DoesNotContain(fixture.Store.Requests, x => x.Operation == "OwnDelete");
    }

    [DatabaseFact]
    public async Task WhenTheStoreDoesNotAnswerTheProfileIsNotDeleted()
    {
        var account = await fixture.AccountAsync(("google", "g-unanswered"));
        fixture.Store.Answer = (_, _) => (HttpStatusCode.ServiceUnavailable, null);

        await using var scope = fixture.Api.Services.CreateAsyncScope();
        await Assert.ThrowsAsync<HarmonyDB.Shelf.Api.Foundation.Errors.ShelfException>(() =>
            scope.ServiceProvider.GetRequiredService<HarmonyDB.Shelf.Api.Areas.Me.Services.ProfileRemoval>().DeleteAsync(account, CancellationToken.None));
        Assert.Null((await scope.ServiceProvider.GetRequiredService<HarmonyDB.Shelf.Database.ShelfDbContext>().Accounts.FindAsync(account))!.DeletedAt);
    }
    [DatabaseFact]
    public async Task AnAccountsOwnChordsAreCountedAsItsListHoldsThem()
    {
        var account = await fixture.AccountAsync(("telegram", "333"));
        var text = new StoredText(["Artist"], "Title", ">Am\nla", StoredVisibility.Private, null, null, null, null);
        StoredOwnChords Stored() => new(Guid.NewGuid(), text, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new($"https://own.example/{Guid.NewGuid()}"), "telegram:333", false, null);
        fixture.Store.Answer = (operation, _) => operation == "OwnList"
            ? (HttpStatusCode.OK, new { items = new[] { Stored(), Stored(), Stored() } })
            : (HttpStatusCode.NotFound, null);

        await using var scope = fixture.Api.Services.CreateAsyncScope();
        Assert.Equal(3, await scope.ServiceProvider.GetRequiredService<OwnChordsService>().CountAsync(account, CancellationToken.None));
        var author = fixture.Store.Requests.Last(x => x.Operation == "OwnList").Body.GetProperty("author");
        Assert.Equal([$"account:{account:D}", "telegram:333"], author.GetProperty("subjects").EnumerateArray().Select(x => x.GetString()));
    }

    [DatabaseFact]
    public async Task NewOwnChordsArePlacedWhenTheSourcesAnswerAndOnlySavedWhenTheyDoNot()
    {
        var me = await fixture.AccountAsync();
        var collectiveId = Guid.NewGuid();
        var uri = $"https://own.example/?id={collectiveId}";
        var chordsId = $"own-{collectiveId}";
        fixture.Store.Answer = (operation, _) => operation == "OwnInsert"
            ? (HttpStatusCode.OK, new StoredOwnChords(collectiveId, new(["Somebody"], "A song", "words", StoredVisibility.Club, null, null, null, null),
                1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new Uri(uri), $"account:{me:D}", true, []))
            : (HttpStatusCode.InternalServerError, null);
        var answering = false;
        fixture.Index.Answer = (operation, body) => operation == "V2Resolve"
            ? (HttpStatusCode.OK, new ResolveResponse
            {
                Items =
                [
                    answering
                        ? new ResolvedUri { Uri = new(uri), State = ResolveState.Resolved, Id = chordsId }
                        : new ResolvedUri { Uri = new(uri), State = ResolveState.Unavailable },
                ],
            })
            : operation == "V2GetHeaders"
                ? (HttpStatusCode.OK, new GetHeadersResponse
                {
                    Headers = body.GetProperty("ids").EnumerateArray()
                        .Select(x => new HeaderAnswer { Id = x.GetString()!, State = HarmonyDB.Source.Api.Model.V2.ItemState.Open, Header = FakeIndex.Header(x.GetString()!, "Own") })
                        .ToList(),
                })
                : (HttpStatusCode.InternalServerError, null);
        using var client = fixture.Api.CreateClient();

        async Task<CreatedOwnChords> SaveAsync()
        {
            var saved = await client.SendAsync(FakeIndex.As(HttpMethod.Post, "/api/own-chords", me, body: new CreateOwnChordsRequest { Text = Text() }));
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            return System.Text.Json.JsonSerializer.Deserialize<CreatedOwnChords>(await saved.Content.ReadAsStringAsync(), Browser.Json)!;
        }

        async Task<List<HarmonyDB.Shelf.Database.Model.PlaylistSong>> PlacedAsync()
        {
            await using var db = fixture.CreateContext();
            return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                db.PlaylistSongItems.Where(x => x.Chords!.SourceId == chordsId).Select(x => x.Song));
        }

        // the sources do not answer: saved, the answer says so, and in no playlist
        var unanswered = await SaveAsync();
        Assert.Equal((false, null, collectiveId), (unanswered.Placed, unanswered.Item.ChordsId, unanswered.Item.Id));
        Assert.Empty(await PlacedAsync());

        // they answer: placed in the person's playlist of own chords
        answering = true;
        var answered = await SaveAsync();
        Assert.Equal((true, chordsId), (answered.Placed, answered.Item.ChordsId));
        var placed = Assert.Single(await PlacedAsync());
        Assert.Equal("A song", placed.Title);
        await using var check = fixture.CreateContext();
        Assert.Equal(HarmonyDB.Shelf.Database.Model.PlaylistSystemKind.OwnChords, check.Playlists.Single(x => x.Id == placed.PlaylistId).SystemKind);
    }

    [Fact]
    public void TheTextIsTrimmedAndWhatIsNeededIsThere()
    {
        var text = OwnChordsService.Normalize(Text() with { Title = "  A song ", Artists = [" One ", "", "One", "Two"], Description = "  ", Key = " Am " });
        Assert.Equal("A song", text.Title);
        Assert.Equal(["One", "Two"], text.Artists);
        Assert.Null(text.Description);
        Assert.Equal("Am", text.Key);

        Assert.Equal(OwnChordsErrorCodes.TitleRequired, Refused(Text() with { Title = " " }));
        Assert.Equal(OwnChordsErrorCodes.ArtistsRequired, Refused(Text() with { Artists = [" "] }));
        Assert.Equal(OwnChordsErrorCodes.ContentsRequired, Refused(Text() with { Contents = "\n " }));
        Assert.Equal(OwnChordsErrorCodes.Invalid, Refused(Text() with { Bpm = 0 }));
    }

    [Fact]
    public async Task OwnChordsAreForSignedInPeopleAndDrawingForThoseWithThePermission()
    {
        using var factory = new ShelfApiFactory();
        using var client = factory.CreateClient();
        var person = Guid.NewGuid();

        await Browser.AssertError(await client.SendAsync(FakeIndex.As(HttpMethod.Post, "/api/own-chords", null, body: new CreateOwnChordsRequest { Text = Text() })),
            HttpStatusCode.Unauthorized, ErrorCodes.SignInRequired);
        await Browser.AssertError(await client.SendAsync(FakeIndex.As(HttpMethod.Post, "/api/illustrations/x", null, body: new DrawIllustrationsRequest { Preset = "plain" })),
            HttpStatusCode.Unauthorized, ErrorCodes.SignInRequired);
        await Browser.AssertError(await client.SendAsync(FakeIndex.As(HttpMethod.Post, "/api/illustrations/x", person, BuiltInPermissions.Pdf, body: new DrawIllustrationsRequest { Preset = "plain" })),
            HttpStatusCode.Forbidden, ErrorCodes.PermissionDenied);
    }

    private static OwnChordsText Text() => new()
    {
        Title = "A song",
        Artists = ["Somebody"],
        Contents = ">Am  C\nwords",
        Visibility = OwnChordsVisibility.Link,
    };

    private static string Refused(OwnChordsText text)
        => Assert.Throws<ShelfException>(() => OwnChordsService.Normalize(text)).Code;
}
