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
/// The own chords' source contract, version 2, hosted as in the API, over a fake store of the collectives and a fake
/// shelf that tells who an account is: who opens and who finds a public, a club and a private collective.
/// </summary>
public sealed class SelfV2VisibilityTests : IAsyncLifetime
{
    private const string Key = "index-key-0123456789abcdefghijklmnopqrstuvwxyz";
    private const string ShelfKey = "shelf-key-0123456789abcdefghijklmnopqrstuvwxyz";
    private const string Host = "chords.example";
    private const long AuthorTelegramId = 111;

    private static readonly Guid Public = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Club = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid Private = Guid.Parse("10000000-0000-0000-0000-000000000003");

    /// <summary>Private, written in the shelf by the author's account (the others, in the old shelf by the author's Telegram user).</summary>
    private static readonly Guid AccountPrivate = Guid.Parse("10000000-0000-0000-0000-000000000004");
    private static readonly Guid Missing = Guid.Parse("10000000-0000-0000-0000-000000000009");

    private static readonly Guid Author = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid Other = Guid.Parse("20000000-0000-0000-0000-000000000002");

    /// <summary>The shelf fails when asked about this account.</summary>
    private static readonly Guid Untold = Guid.Parse("20000000-0000-0000-0000-000000000003");

    private static readonly Caller Guest = new() { Keys = [] };

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
            ["SourcesSelf:SelfV2:ShelfAccountsEndpoint"] = $"{fakes}/shelf/",
            ["SourcesSelf:SelfV2:ShelfAccountsKey"] = ShelfKey,
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
        // caller, opens public, club, private (either written in the old shelf or in the shelf)
        { "guest", true, true, false },
        { "author", true, true, true },
        { "other", true, true, false },
    };

    [Theory]
    [MemberData(nameof(Callers))]
    public async Task WhoOpensWhatByItsIdOrAddress(string who, bool opensPublic, bool opensClub, bool opensPrivate)
    {
        var caller = CallerOf(who);
        var expected = new[] { opensPublic, opensClub, opensPrivate, opensPrivate }.Select(x => x ? ItemState.Open : ItemState.NotOpen).ToList();

        var headers = await _source.GetHeaders(caller, [Id(Public), Id(Club), Id(Private), Id(AccountPrivate)]);
        Assert.Equal(expected, headers.Headers.Select(x => x.State));

        foreach (var (collective, state) in new[] { Public, Club, Private, AccountPrivate }.Zip(expected))
        {
            var content = await _source.GetContent(caller, Id(collective), [ContentKind.Sheet]);
            Assert.Equal(state, content.State);
            Assert.Equal(state == ItemState.Open, content.Content?.Sheet != null);
        }

        // many at once: the same answers, in the order asked; one that does not exist is not open
        var contents = await _source.GetContents(caller, [Id(Public), Id(Club), Id(Private), Id(AccountPrivate), Id(Missing), "mj-not-an-id"], [ContentKind.Sheet], CancellationToken.None);
        Assert.Equal([Id(Public), Id(Club), Id(Private), Id(AccountPrivate), Id(Missing), "mj-not-an-id"], contents.Items.Select(x => x.Id));
        Assert.Equal([.. expected, ItemState.NotOpen, ItemState.NotOpen], contents.Items.Select(x => x.State));
        Assert.All(contents.Items, x => Assert.Equal(x.State == ItemState.Open, x.Content?.Sheet != null));

        var resolved = await _source.Resolve(caller, [Address(Public), Address(Club), Address(Private), Address(AccountPrivate)]);
        Assert.Equal(
            expected.Select(x => x == ItemState.Open ? ResolveState.Resolved : ResolveState.NotResolved),
            resolved.Items.Select(x => x.State));
    }

    [Theory]
    [InlineData("guest", new[] { "Alpha public" })]
    [InlineData("author", new[] { "Alpha club", "Alpha private", "Alpha private in the shelf", "Alpha public" })]
    [InlineData("other", new[] { "Alpha public" })]
    public async Task WhoFindsWhat(string who, string[] titles)
    {
        var response = await _source.Search(CallerOf(who), "alpha");

        Assert.Equal(titles, response.Headers.Select(x => x.Title));
        Assert.Empty(response.Failures);
    }

    [Fact]
    public async Task WhenTheShelfDoesNotAnswerNothingTellsWhatExists()
    {
        var caller = CallerOf("untold");

        // a search keeps what is open to all, and says that something may be missing, whatever the words
        var found = await _source.Search(caller, "alpha");
        Assert.Equal(["Alpha public"], found.Headers.Select(x => x.Title));
        var failure = Assert.Single(found.Failures);
        Assert.Equal(SearchFailureReason.Unavailable, failure.Reason);

        var nothing = await _source.Search(caller, "nonsense");
        Assert.Empty(nothing.Headers);
        Assert.Equal(found.Failures, nothing.Failures);

        // by id: what is open to all is open; a private one and one that does not exist are alike
        var headers = await _source.GetHeaders(caller, [Id(Public), Id(Club), Id(Private), Id(AccountPrivate), Id(Missing), "mj-not-an-id"]);
        Assert.Equal(
            [ItemState.Open, ItemState.Open, ItemState.Unavailable, ItemState.Unavailable, ItemState.Unavailable, ItemState.Unavailable],
            headers.Headers.Select(x => x.State));
        Assert.Equal(ItemState.Unavailable, (await _source.GetContent(caller, Id(Missing), [ContentKind.Sheet])).State);

        // a guest is not asked about
        Assert.Equal(ItemState.NotOpen, (await _source.GetContent(Guest, Id(Missing), [ContentKind.Sheet])).State);
    }

    private static Caller CallerOf(string who) => who switch
    {
        "guest" => Guest,
        "author" => new() { AccountId = Author, Keys = [] },
        "other" => new() { AccountId = Other, Keys = [] },
        "untold" => new() { AccountId = Untold, Keys = [] },
        _ => throw new ArgumentOutOfRangeException(nameof(who)),
    };

    private static string Id(Guid collective) => $"mj-{collective:D}";

    private static Uri Address(Guid collective) => new($"https://{Host}/?id={collective:D}&tag=123456&version=1");

    private static OwnCollective Version(Guid id, string title, CollectiveVisibility visibility, bool inTheShelf = false) => new()
    {
        CollectiveId = id,
        Collective = new()
        {
            Title = title,
            Authors = ["Somebody"],
            Contents = "Am C\nwords",
            Visibility = visibility,
        },
        VersionNumber = 1,
        CreatedOn = DateTime.UnixEpoch,
        UpdatedOn = DateTime.UnixEpoch,
        SearchTag = 123456,
        AuthorTelegramUserId = inTheShelf ? null : AuthorTelegramId,
        AuthorAccountId = inTheShelf ? Author : null,
        Uri = Address(id),
    };

    /// <summary>
    /// The store of the collectives (its read of own chords) and the shelf's accounts, on a real port.
    /// </summary>
    private static async Task<WebApplication> StartFakes()
    {
        var all = new[]
        {
            Version(Public, "Alpha public", CollectiveVisibility.Public),
            Version(Club, "Alpha club", CollectiveVisibility.Club),
            Version(Private, "Alpha private", CollectiveVisibility.Private),
            Version(AccountPrivate, "Alpha private in the shelf", CollectiveVisibility.Private, true),
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
        app.MapGet("/shelf/accounts/{accountId:guid}/logins/telegram", (HttpContext context, Guid accountId) =>
        {
            if (context.Request.Headers["X-Internal-Key"] != ShelfKey) return Results.StatusCode(401);
            if (accountId == Untold) return Results.StatusCode(500);
            return Results.Json(new { subjects = accountId == Author ? new[] { AuthorTelegramId.ToString() } : ["222"] }, json);
        });

        await app.StartAsync();
        return app;
    }
}
