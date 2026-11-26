using System.Text.Json;
using HarmonyDB.Source.Api.Client.V2;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OneShelf.Collectives.Api.Model.V2.Sub;
using OneShelf.Collectives.Api.Model.VInternal;
using OneShelf.Common.Api.Hosting;

namespace OneShelf.Sources.Self.Api.Tests;

/// <summary>
/// The own chords' source contract, version 2, hosted as in the API, over a fake store of the collectives: who opens and
/// who finds a public, a club and a private collective, by its id, by its link, by a search.
/// </summary>
public sealed class SelfV2VisibilityTests : IAsyncLifetime
{
    private const string Key = "index-key-0123456789abcdefghijklmnopqrstuvwxyz";
    private const string Host = "chords.example";

    private static readonly Guid Public = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Club = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid Private = Guid.Parse("10000000-0000-0000-0000-000000000003");

    /// <summary>Private, authored by a name of the author (the others, in the old shelf by the author's Telegram user).</summary>
    private static readonly Guid AuthoredPrivate = Guid.Parse("10000000-0000-0000-0000-000000000004");
    private static readonly Guid Missing = Guid.Parse("10000000-0000-0000-0000-000000000009");

    private static readonly Caller Guest = new() { Subjects = [], Keys = [] };

    private WebApplication _fakes = null!;
    private WebApplication _host = null!;
    private SourceV2Client _source = null!;

    public async Task InitializeAsync()
    {
        _fakes = await StartFakes();
        var fakes = _fakes.Urls.Single();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SourcesSelf:SelfApiOptions:SourceName"] = "Own",
            ["SourcesSelf:SelfApiOptions:ShortSourceName"] = "O",
            ["SourcesSelf:SelfApiOptions:Hosts:0"] = Host,
            ["SourcesSelf:CollectivesApiClientOptions:Endpoint"] = $"{fakes}/api/",
            ["SourcesSelf:CollectivesApiClientOptions:MasterCode"] = "code",
            ["SourcesSelf:InternalClients:0:Name"] = "index",
            ["SourcesSelf:InternalClients:0:Key"] = Key,
            ["SourcesSelf:InternalClients:0:Scopes:0"] = "source",
            ["SourcesSelf:SelfV2:AddressTemplate"] = "https://site.example/chords/{id}?code={code}&version={version}",
        });
        builder.AddApiModules(new SourcesSelfModule());

        _host = builder.Build();
        _host.MapApiModules();
        await _host.StartAsync();

        _source = new(_host.GetTestClient(), new()
        {
            Name = "own",
            Endpoint = new("http://localhost/sourcesself/api/"),
            Key = Key,
        }, NullLogger<SourceV2Client>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
        await _fakes.DisposeAsync();
    }

    public static TheoryData<string, bool, bool, bool> Callers => new()
    {
        // caller, opens public, club, private (either written in the old shelf or authored by a name)
        { "guest", true, true, false },
        { "author", true, true, true },
        { "other", true, true, false },
    };

    [Theory]
    [MemberData(nameof(Callers))]
    public async Task WhoOpensWhatByItsId(string who, bool opensPublic, bool opensClub, bool opensPrivate)
    {
        var caller = CallerOf(who);
        var expected = new[] { opensPublic, opensClub, opensPrivate, opensPrivate }.Select(x => x ? ItemState.Open : ItemState.NotOpen).ToList();

        var headers = await _source.GetHeaders(caller, [Id(Public), Id(Club), Id(Private), Id(AuthoredPrivate)]);
        Assert.Equal(expected, headers.Headers.Select(x => x.State));

        foreach (var (collective, state) in new[] { Public, Club, Private, AuthoredPrivate }.Zip(expected))
        {
            var content = await _source.GetContent(caller, Id(collective), [ContentKind.Sheet], null);
            Assert.Equal(state, content.State);
            Assert.Equal(state == ItemState.Open, content.Content?.Sheet != null);
        }

        // many at once: the same answers, in the order asked; one that does not exist is not open
        var contents = await _source.GetContents(caller, [Id(Public), Id(Club), Id(Private), Id(AuthoredPrivate), Id(Missing), "mj-not-an-id"], [ContentKind.Sheet], CancellationToken.None);
        Assert.Equal([Id(Public), Id(Club), Id(Private), Id(AuthoredPrivate), Id(Missing), "mj-not-an-id"], contents.Items.Select(x => x.Id));
        Assert.Equal([.. expected, ItemState.NotOpen, ItemState.NotOpen], contents.Items.Select(x => x.State));
        Assert.All(contents.Items, x => Assert.Equal(x.State == ItemState.Open, x.Content?.Sheet != null));
    }

    [Fact]
    public async Task APrivateItemOpensToWhoeverHoldsItsCurrentLink()
    {
        foreach (var address in new[] { StoreAddress(Private, 2), OwnAddress(Private, 2), OwnAddress(AuthoredPrivate, 2) })
        {
            var id = address.ToString().Contains(Private.ToString()) ? Id(Private) : Id(AuthoredPrivate);
            Assert.Equal(ItemState.Open, (await _source.GetContent(Guest, id, [ContentKind.Sheet], address)).State);
            Assert.Equal(id, (await _source.Resolve(Guest, [address])).Items.Single().Id);
        }

        // an older version's link, a wrong code, the link of another item: nothing more than the id opens
        var older = OwnAddress(Private, 1);
        var wrongCode = new Uri(OwnAddress(Private, 2).ToString().Replace("123456", "654321"));
        foreach (var address in new[] { older, wrongCode, OwnAddress(AuthoredPrivate, 2) })
            Assert.Equal(ItemState.NotOpen, (await _source.GetContent(CallerOf("other"), Id(Private), [ContentKind.Sheet], address)).State);

        // an older link resolves for the author only; an older link of a club item for everyone
        Assert.Equal(ResolveState.NotResolved, (await _source.Resolve(Guest, [older])).Items.Single().State);
        Assert.Equal(ResolveState.Resolved, (await _source.Resolve(CallerOf("author"), [older])).Items.Single().State);
        Assert.Equal(ResolveState.Resolved, (await _source.Resolve(Guest, [OwnAddress(Club, 1)])).Items.Single().State);

        // a batch read opens by ids only; the header gives the current address in this source's own shape
        Assert.Equal(ItemState.NotOpen, (await _source.GetContents(Guest, [Id(Private)], [ContentKind.Sheet], CancellationToken.None)).Items.Single().State);
        var header = (await _source.GetHeaders(CallerOf("author"), [Id(Private)])).Headers.Single().Header!;
        Assert.Equal(OwnAddress(Private, 2), header.Address);
    }

    [Fact]
    public async Task APrivateLinkOpensWithWhatAMessengerAddedToIt()
    {
        foreach (var address in new[] { StoreAddress(Private, 2), OwnAddress(Private, 2) })
        {
            var shared = new Uri(address + "&fbclid=IwAR0abc&utm_source=telegram#top");
            Assert.Equal(ItemState.Open, (await _source.GetContent(Guest, Id(Private), [ContentKind.Sheet], shared)).State);
            Assert.Equal(Id(Private), (await _source.Resolve(Guest, [shared])).Items.Single().Id);
        }

        // the parameters in another order are the same address; a changed part is not
        var reordered = new Uri($"https://site.example/chords/mj-{Private:D}?utm_source=x&version=2&code=123456");
        Assert.Equal(ItemState.Open, (await _source.GetContent(Guest, Id(Private), [ContentKind.Sheet], reordered)).State);
        var otherPath = new Uri($"https://site.example/other/mj-{Private:D}?code=123456&version=2");
        Assert.Equal(ResolveState.NotResolved, (await _source.Resolve(Guest, [otherPath])).Items.Single().State);
        var twoCodes = new Uri($"https://site.example/chords/mj-{Private:D}?code=123456&code=654321&version=2");
        Assert.Equal(ResolveState.NotResolved, (await _source.Resolve(Guest, [twoCodes])).Items.Single().State);
    }

    [Theory]
    [InlineData("guest", new[] { "Alpha public" })]
    [InlineData("author", new[] { "Alpha club", "Alpha private", "Alpha private by name", "Alpha public" })]
    [InlineData("other", new[] { "Alpha public" })]
    public async Task WhoFindsWhat(string who, string[] titles)
    {
        var response = await _source.Search(CallerOf(who), "alpha");

        Assert.Equal(titles, response.Headers.Select(x => x.Title));
        Assert.Empty(response.Failures);

        // a code alone finds nothing
        Assert.Empty((await _source.Search(CallerOf(who), "123456")).Headers);
    }

    [Fact]
    public async Task TheSourceShowsOneLabel()
    {
        var sources = await _source.GetSources(Guest);
        Assert.Equal(["Own"], sources.Sources.Select(x => x.Key));
        Assert.Empty(sources.Failures);
    }

    private static Caller CallerOf(string who) => who switch
    {
        "guest" => Guest,
        "author" => new() { Subjects = ["account:a", "telegram:111"], Keys = [] },
        "other" => new() { Subjects = ["account:b", "telegram:222"], Keys = [] },
        _ => throw new ArgumentOutOfRangeException(nameof(who)),
    };
    private static string Id(Guid collective) => $"mj-{collective:D}";

    private static Uri StoreAddress(Guid collective, int version) => new($"https://{Host}/?id={collective:D}&tag=123456&version={version}");

    private static Uri OwnAddress(Guid collective, int version) => new($"https://site.example/chords/mj-{collective:D}?code=123456&version={version}");

    private static OwnCollective Version(Guid id, string title, CollectiveVisibility visibility, string author = "telegram:111") => new()
    {
        CollectiveId = id,
        Collective = new()
        {
            Title = title,
            Authors = ["Somebody"],
            Contents = "Am C\nwords",
            Visibility = visibility,
        },
        VersionNumber = 2,
        CreatedOn = DateTime.UnixEpoch,
        UpdatedOn = DateTime.UnixEpoch,
        SearchTag = 123456,
        Author = author,
        Editable = !author.StartsWith("telegram:", StringComparison.Ordinal),
        Uri = StoreAddress(id, 2),
    };

    /// <summary>
    /// The store of the collectives (its read of own chords), on a real port.
    /// </summary>
    private static async Task<WebApplication> StartFakes()
    {
        var all = new[]
        {
            Version(Public, "Alpha public", CollectiveVisibility.Public),
            Version(Club, "Alpha club", CollectiveVisibility.Club),
            Version(Private, "Alpha private", CollectiveVisibility.Private),
            Version(AuthoredPrivate, "Alpha private by name", CollectiveVisibility.Private, "account:a"),
        };
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();

        app.MapPost("/api/Items", async (HttpContext context) =>
        {
            var request = await JsonSerializer.DeserializeAsync<ItemsRequest>(context.Request.Body, json);
            await Results.Json(new ItemsResponse { Items = all.Where(x => request!.CollectiveIds?.Contains(x.CollectiveId) ?? true).ToList() }, json)
                .ExecuteAsync(context);
        });
        await app.StartAsync();
        return app;
    }
}
