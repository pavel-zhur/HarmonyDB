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

namespace HarmonyDB.Shelf.Tests.OwnChords;

/// <summary>
/// Own chords: who the store is told the author is, what is refused before it, who may draw illustrations. Who opens
/// whose own chords is the own chords' source's (its tests).
/// </summary>
public sealed class OwnChordsTests(WorkFixture fixture) : IClassFixture<WorkFixture>
{
    [DatabaseFact]
    public async Task TheAuthorIsTheAccountAndItsTelegramUsers()
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

        var authors = fixture.Store.Requests.Where(x => x.Operation == "OwnUpdate").Select(x => x.Body.GetProperty("author")).ToList();
        Assert.Equal(withTelegram, authors[0].GetProperty("accountId").GetGuid());
        Assert.Equal([111L, 222L], authors[0].GetProperty("telegramUserIds").EnumerateArray().Select(x => x.GetInt64()).Order());
        Assert.Equal(withoutTelegram, authors[1].GetProperty("accountId").GetGuid());
        Assert.Empty(authors[1].GetProperty("telegramUserIds").EnumerateArray());
    }

    [DatabaseFact]
    public async Task NewOwnChordsSavedWhileTheSourcesDoNotAnswerArePlacedWhenTheyDo()
    {
        var me = await fixture.AccountAsync();
        var collectiveId = Guid.NewGuid();
        var uri = $"https://own.example/?id={collectiveId}";
        var chordsId = $"own-{collectiveId}";
        fixture.Store.Answer = (operation, _) => operation == "OwnInsert"
            ? (HttpStatusCode.OK, new StoredOwnChords(collectiveId, new(["Somebody"], "A song", "words", StoredVisibility.Club, null, null, null, null),
                1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new Uri(uri), null, me, []))
            : (HttpStatusCode.InternalServerError, null);
        var resolves = 0;
        fixture.Index.Answer = (operation, body) => operation == "V2Resolve"
            ? (HttpStatusCode.OK, new ResolveResponse
            {
                Items =
                [
                    Interlocked.Increment(ref resolves) <= 2
                        ? new ResolvedUri { Uri = new(uri), State = ResolveState.Unavailable }
                        : new ResolvedUri { Uri = new(uri), State = ResolveState.Resolved, Id = chordsId },
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
        var saved = await client.SendAsync(FakeIndex.As(HttpMethod.Post, "/api/own-chords", me, body: new CreateOwnChordsRequest { Text = Text() }));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var details = System.Text.Json.JsonSerializer.Deserialize<OwnChordsDetails>(await saved.Content.ReadAsStringAsync(), Browser.Json)!;
        Assert.Null(details.Item.ChordsId);
        Assert.NotNull(details.PlacingJobId);

        // while the sources do not answer, it waits in the queue for its time, holding no worker
        var waited = false;
        for (var i = 0; i < 100 && !waited; i++)
        {
            await using (var waiting = fixture.CreateContext())
                waited = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(
                    waiting.Jobs.Where(x => x.Id == details.PlacingJobId && x.State == HarmonyDB.Shelf.Database.Model.JobState.Queued && x.NotBefore > DateTimeOffset.UtcNow && x.Attempts == 0));
            await Task.Delay(50);
        }

        await using (var seen = fixture.CreateContext())
        {
            var row = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking(seen.Jobs).Single(x => x.Id == details.PlacingJobId);
            Assert.True(waited, $"{row.State} {row.Attempts} {row.NotBefore:O} {row.ErrorCode} resolves={resolves}");
        }

        var job = await fixture.FinishedAsync(me, details.PlacingJobId!.Value);
        Assert.True(job.Status == Model.Jobs.JobStatus.Succeeded, job.ErrorCode);
        Assert.Equal(chordsId, Assert.IsType<OwnChordsPlacingResult>(job.Result).ChordsId);

        await using var db = fixture.CreateContext();
        var placed = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.PlaylistSongItems.Where(x => x.Chords!.SourceId == chordsId).Select(x => x.Song));
        Assert.Equal("A song", placed.Title);
        Assert.Equal(HarmonyDB.Shelf.Database.Model.PlaylistSystemKind.OwnChords, db.Playlists.Single(x => x.Id == placed.PlaylistId).SystemKind);
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
