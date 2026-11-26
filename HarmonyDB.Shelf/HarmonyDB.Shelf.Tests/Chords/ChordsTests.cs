using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarmonyDB.Shelf.Model.Chords;
using HarmonyDB.Shelf.Tests.Api;
using HarmonyDB.Shelf.Tests.Auth;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Tests.Chords;

/// <summary>
/// Chords from the sources for a person: the keys passed as they are, the three states carried through, the sources'
/// labels kept per key set and person.
/// </summary>
public sealed class ChordsTests(ShelfDatabaseFixture database) : IClassFixture<ShelfDatabaseFixture>
{
    private const string Custom = "custom.one";

    [Fact]
    public async Task TheKeysGoAsTheyAreAndNothingIsMadeUp()
    {
        var index = new FakeIndex { Answer = (_, body) => (HttpStatusCode.OK, new GetContentResponse { Id = body.GetProperty("id").GetString()!, State = ItemState.NotOpen }) };
        using var api = index.Api(new ShelfApiFactory());
        using var client = api.CreateClient();
        var person = Guid.NewGuid();

        await client.SendAsync(FakeIndex.As(HttpMethod.Get, "/api/chords/x1", null));
        await client.SendAsync(FakeIndex.As(HttpMethod.Get, "/api/chords/x1", person, $"pdf,{Custom}"));
        await client.SendAsync(FakeIndex.As(HttpMethod.Get, "/api/chords/x1", person, "pdf"));
        await client.SendAsync(FakeIndex.As(HttpMethod.Get, "/api/chords/x1", person, $"pdf,{Custom}", clientAllowed: "pdf"));

        var callers = index.Callers("V2GetContent").Select(x => (Account: string.Join(",", x.GetProperty("subjects").EnumerateArray().Select(s => s.GetString())), Keys: string.Join(',', x.GetProperty("keys").EnumerateArray().Select(k => k.GetString())))).ToList();
        Assert.Equal(
        [
            (string.Empty, string.Empty),           // a guest: no account, and the guest's defaults have no custom key
            ($"account:{person:D}", Custom),        // the custom key only, never a built-in permission
            ($"account:{person:D}", string.Empty),
            ($"account:{person:D}", string.Empty),      // a client acting for the person: only what it was allowed
        ], callers);
    }

    [Fact]
    public async Task TheThreeStatesAreCarriedThrough()
    {
        var index = new FakeIndex
        {
            Answer = (operation, body) => operation switch
            {
                "V2GetContent" when body.GetProperty("id").GetString() == "closed" => (HttpStatusCode.OK, new GetContentResponse { Id = "closed", State = ItemState.NotOpen }),
                "V2GetContent" => (HttpStatusCode.InternalServerError, null),
                // one answer for each id asked (the ones kept already are not asked again)
                "V2GetHeaders" => (HttpStatusCode.OK, new GetHeadersResponse
                {
                    Headers = body.GetProperty("ids").EnumerateArray().Select(x => x.GetString() switch
                    {
                        "open" => new HeaderAnswer { Id = "open", State = ItemState.Open, Header = FakeIndex.Header("open", "Label", "an icon") },
                        "closed" => new HeaderAnswer { Id = "closed", State = ItemState.NotOpen },
                        var id => new HeaderAnswer { Id = id!, State = ItemState.Unavailable },
                    }).ToList(),
                }),
                _ => (HttpStatusCode.NotFound, null),
            },
        };
        using var api = index.Api(new ShelfApiFactory());
        using var client = api.CreateClient();

        var closed = await ReadAsync<ChordsContentResponse>(await client.GetAsync("/api/chords/closed?kinds=sheet,timeline"));
        Assert.Equal(ChordsState.NotOpen, closed.State);
        Assert.Null(closed.Header);
        Assert.Null(closed.Content);
        Assert.Null(closed.ChordsId);

        // the source is broken: that is not "not open to you"
        var broken = await ReadAsync<ChordsContentResponse>(await client.GetAsync("/api/chords/broken"));
        Assert.Equal(ChordsState.Unavailable, broken.State);

        var headers = await ReadAsync<ChordsHeadersResponse>(await client.SendAsync(FakeIndex.As(HttpMethod.Post, "/api/chords/headers", null, body: new { ids = new[] { "open", "closed", "away" } })));
        Assert.Equal([ChordsState.Open, ChordsState.NotOpen, ChordsState.Unavailable], headers.Items.Select(x => x.State));
        Assert.Equal("/api/chords/icons/an%20icon", headers.Items[0].Header!.Source.IconUrl);
        Assert.All(headers.Items.Skip(1), x => Assert.Null(x.Header));

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/chords/open?kinds=sheet,lyrcs")).StatusCode);
    }

    // the answer of a list carries the person's own marks: it reads the database
    [DatabaseFact]
    public async Task TheLabelIsKeptPerKeysAndPersonOnly()
    {
        // the source names itself by the keys it is given
        var index = new FakeIndex
        {
            Answer = (_, body) =>
            {
                var keys = string.Join(',', body.GetProperty("caller").GetProperty("keys").EnumerateArray().Select(x => x.GetString()));
                return (HttpStatusCode.OK, new GetHeadersResponse
                {
                    Headers = body.GetProperty("ids").EnumerateArray()
                        .Select(x => new HeaderAnswer { Id = x.GetString()!, State = ItemState.Open, Header = FakeIndex.Header(x.GetString()!, $"label for [{keys}]") })
                        .ToList(),
                });
            },
        };
        using var api = index.Api(new ShelfApiFactory(), new Dictionary<string, string?> { ["ConnectionStrings:Shelf"] = database.ConnectionString });
        using var client = api.CreateClient();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        async Task<string> LabelAsync(Guid? account, string permissions)
            => (await ReadAsync<ChordsHeadersResponse>(await client.SendAsync(FakeIndex.As(HttpMethod.Post, "/api/chords/headers", account, permissions, new { ids = new[] { "item" } }))))
                .Items.Single().Header!.Source.Title;

        Assert.Equal($"label for [{Custom}]", await LabelAsync(first, Custom));
        Assert.Equal($"label for [{Custom}]", await LabelAsync(first, Custom));
        Assert.Single(index.Requests);

        // another person, though with the same keys, and a guest, are asked for anew
        Assert.Equal($"label for [{Custom}]", await LabelAsync(second, Custom));
        Assert.Equal("label for []", await LabelAsync(null, string.Empty));
        Assert.Equal("label for []", await LabelAsync(first, string.Empty));
        Assert.Equal(4, index.Requests.Count);
    }

    [DatabaseFact]
    public async Task TheRowKeepsOnlyWhatAnAnonymousCallerGets()
    {
        // the source gives the title to a caller with keys, and its own (or none) to one without
        var anonymousTitle = (string?)null;
        var index = new FakeIndex
        {
            Answer = (_, body) =>
            {
                var caller = body.GetProperty("caller");
                var keyed = caller.GetProperty("keys").GetArrayLength() > 0 || caller.GetProperty("subjects").GetArrayLength() > 0;
                return (HttpStatusCode.OK, new GetContentResponse
                {
                    Id = "secret",
                    State = ItemState.Open,
                    Header = FakeIndex.Header("secret", "Label") with { Title = keyed ? "Seen with keys" : anonymousTitle, Artists = keyed ? ["Someone"] : [] },
                    Content = new(),
                });
            },
        };
        using var api = index.Api(new ShelfApiFactory(), new Dictionary<string, string?> { ["ConnectionStrings:Shelf"] = database.ConnectionString });
        using var client = api.CreateClient();

        async Task<HarmonyDB.Shelf.Database.Model.ChordsItem> RowAsync()
        {
            await using var db = database.CreateContext();
            return await db.Chords.AsNoTracking().SingleAsync(x => x.SourceId == "secret");
        }

        // a person with keys, and a signed-in person with none: what they see is theirs only
        var seen = await ReadAsync<ChordsContentResponse>(await client.SendAsync(FakeIndex.As(HttpMethod.Get, "/api/chords/secret", Guid.NewGuid(), Custom)));
        Assert.Equal("Seen with keys", seen.Header!.Title);
        await ReadAsync<ChordsContentResponse>(await client.SendAsync(FakeIndex.As(HttpMethod.Get, "/api/chords/secret", Guid.NewGuid())));
        var row = await RowAsync();
        Assert.Null(row.Title);
        Assert.Empty(row.Artists);
        Assert.Null(row.SourceKey);

        // an anonymous caller's header is kept; an empty title later does not replace it
        anonymousTitle = "Public title";
        await ReadAsync<ChordsContentResponse>(await client.GetAsync("/api/chords/secret"));
        Assert.Equal("Public title", (await RowAsync()).Title);

        await using (var db = database.CreateContext())
            await db.Chords.Where(x => x.SourceId == "secret").ExecuteUpdateAsync(x => x.SetProperty(c => c.HeaderUpdatedAt, DateTimeOffset.UtcNow.AddDays(-1)));
        anonymousTitle = null;
        await ReadAsync<ChordsContentResponse>(await client.GetAsync("/api/chords/secret"));
        Assert.Equal("Public title", (await RowAsync()).Title);
    }

    [Fact]
    public async Task WithoutTheIndexItIsUnavailable()
    {
        using var api = new ShelfApiFactory();
        using var client = api.CreateClient();
        var response = await client.GetAsync("/api/chords/x");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        // the search marks its chords part failed and answers
        var search = await ReadAsync<Model.Search.SearchResponse>(await client.GetAsync("/api/search?q=abc&parts=chords"));
        Assert.Empty(search.Chords!);
        Assert.Equal("index", search.ChordsFailures!.Single().Source);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(Browser.Json))!;
    }
}
