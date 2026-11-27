using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
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
using OneShelf.Common.Api.Hosting;

namespace HarmonyDB.Index.Api.Tests;

/// <summary>
/// The index module hosted as in the API, relaying to fake sources over real HTTP: one that answers, one that is down,
/// one too slow, one that refuses.
/// </summary>
public sealed class RelayV2Tests : IAsyncLifetime
{
    private const string FunctionKey = "function-key-0123456789abcdefghijklmnopqrstuvwxyz";
    private const string ShelfKey = "shelf-key-0123456789abcdefghijklmnopqrstuvwxyz";
    private const string OtherKey = "other-key-0123456789abcdefghijklmnopqrstuvwxyz";
    private const string SourceKey = "source-key-0123456789abcdefghijklmnopqrstuvwxyz";

    private static readonly TimeSpan RelayTimeout = TimeSpan.FromSeconds(1);

    private static readonly byte[] IconBytes = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

    private static readonly byte[] FileBytes = Enumerable.Range(0, 100_000).Select(x => (byte)x).ToArray();

    private static readonly Caller Caller = new()
    {
        Subjects = ["person-1", "person-1-before"],
        Keys = ["first", "second"],
    };

    private FakeSource _good = null!;
    private FakeSource _slow = null!;
    private FakeSource _refusing = null!;
    private FakeSource _broken = null!;
    private WebApplication _host = null!;
    private HttpClient _http = null!;
    private SourceV2Client _relay = null!;

    public async Task InitializeAsync()
    {
        _good = await FakeSource.Start("g-", TimeSpan.Zero, null);
        _slow = await FakeSource.Start("s-", TimeSpan.FromSeconds(5), null);
        _refusing = await FakeSource.Start("r-", TimeSpan.Zero, HttpStatusCode.Forbidden);
        _broken = await FakeSource.Start("b-", TimeSpan.Zero, null, answersOutOfContract: true);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FunctionKeys:0"] = FunctionKey,
            ["Index:InternalClients:0:Name"] = "shelf",
            ["Index:InternalClients:0:Key"] = ShelfKey,
            ["Index:InternalClients:0:Scopes:0"] = "index",
            ["Index:InternalClients:1:Name"] = "other",
            ["Index:InternalClients:1:Key"] = OtherKey,
            ["Index:InternalClients:1:Scopes:0"] = "pdf",
            ["Index:RelayV2:Timeout"] = RelayTimeout.ToString(),
            ["Index:RelayV2:Sources:0:Name"] = "good",
            ["Index:RelayV2:Sources:0:Endpoint"] = _good.Endpoint.ToString(),
            ["Index:RelayV2:Sources:0:Key"] = SourceKey,
            ["Index:RelayV2:Sources:0:IdPrefixes:0"] = "g-",
            ["Index:RelayV2:Sources:1:Name"] = "down",
            ["Index:RelayV2:Sources:1:Endpoint"] = "http://127.0.0.1:1/api/",
            ["Index:RelayV2:Sources:1:Key"] = SourceKey,
            ["Index:RelayV2:Sources:1:IdPrefixes:0"] = "d-",
            ["Index:RelayV2:Sources:2:Name"] = "slow",
            ["Index:RelayV2:Sources:2:Endpoint"] = _slow.Endpoint.ToString(),
            ["Index:RelayV2:Sources:2:Key"] = SourceKey,
            ["Index:RelayV2:Sources:2:IdPrefixes:0"] = "s-",
            ["Index:RelayV2:Sources:3:Name"] = "refusing",
            ["Index:RelayV2:Sources:3:Endpoint"] = _refusing.Endpoint.ToString(),
            ["Index:RelayV2:Sources:3:Key"] = SourceKey,
            ["Index:RelayV2:Sources:3:IdPrefixes:0"] = "r-",
            ["Index:RelayV2:Sources:4:Name"] = "broken",
            ["Index:RelayV2:Sources:4:Endpoint"] = _broken.Endpoint.ToString(),
            ["Index:RelayV2:Sources:4:Key"] = SourceKey,
            ["Index:RelayV2:Sources:4:IdPrefixes:0"] = "b-",
        });
        builder.AddApiModules(new IndexModule());

        _host = builder.Build();
        _host.MapApiModules();
        await _host.StartAsync();

        _http = _host.GetTestClient();
        _relay = new(_http, new()
        {
            Name = "index",
            Endpoint = new("http://localhost/index/api/"),
            Key = ShelfKey,
            Timeout = TimeSpan.FromSeconds(30),
        }, NullLogger<SourceV2Client>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
        await _good.DisposeAsync();
        await _slow.DisposeAsync();
        await _refusing.DisposeAsync();
        await _broken.DisposeAsync();
    }

    [Fact]
    public async Task SearchMergesWhatWasFoundAndNamesTheSourcesThatFailed()
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await _relay.Search(Caller, "query");
        stopwatch.Stop();

        Assert.Equal(["g-1"], response.Headers.Select(x => x.Id));
        Assert.Equal(
            [("down", SourceFailureReason.Unavailable), ("slow", SourceFailureReason.Timeout), ("refusing", SourceFailureReason.Unavailable),
                ("broken", SourceFailureReason.Unavailable)],
            response.Failures.Select(x => (x.Source, x.Reason)));
        Assert.True(stopwatch.Elapsed < RelayTimeout * 3, $"The slow source held the search for {stopwatch.Elapsed}.");

        // the caller reaches the source as it was sent
        var received = Assert.Single(_good.Callers);
        Assert.Equal(Caller.Subjects, received.Subjects);
        Assert.Equal(Caller.Keys, received.Keys);
    }

    [Fact]
    public async Task HeadersAreRoutedByPrefixAndAnsweredInTheOrderAsked()
    {
        var response = await _relay.GetHeaders(Caller, ["s-1", "g-2", "x-1", "d-1", "r-1", "g-3"]);

        Assert.Equal(
            [
                ("s-1", ItemState.Unavailable), ("g-2", ItemState.Open), ("x-1", ItemState.NotOpen),
                ("d-1", ItemState.Unavailable), ("r-1", ItemState.Unavailable), ("g-3", ItemState.Open),
            ],
            response.Headers.Select(x => (x.Id, x.State)));
        Assert.Equal("g-2", response.Headers[1].Header!.Id);

        // the good source was asked once, for its own ids only
        Assert.Single(_good.Callers);
        Assert.Equal(["g-2", "g-3"], _good.HeaderIdsAsked.Single());
    }

    [Fact]
    public async Task LabelsAreRoutedByPrefixWhatIsNotKnownOrNotAnsweredLeftOut()
    {
        var response = await _relay.GetLabels(Caller, ["s-1", "g-2", "x-1", "d-1", "g-gone", "g-3"]);

        Assert.Equal(["g-2", "g-3"], response.Labels.Select(x => x.Id));
        Assert.All(response.Labels, x => Assert.Equal("g-", x.Source.Key));
        Assert.Equal(Caller.Subjects, _good.Callers.Last().Subjects);
    }
    [Fact]
    public async Task TheSourcesAreAskedAllAndTheirLabelsComeInTheOrderOfTheSources()
    {
        var response = await _relay.GetSources(Caller);

        Assert.Equal(["g-", "b-"], response.Sources.Select(x => x.Key));
        Assert.Equal([("down", SourceFailureReason.Unavailable), ("slow", SourceFailureReason.Timeout), ("refusing", SourceFailureReason.Unavailable)],
            response.Failures.Select(x => (x.Source, x.Reason)));
        Assert.Equal(Caller.Subjects, _good.Callers.Last().Subjects);
    }

    [Fact]
    public async Task ContentMakesTheRoundTripInTheWireFormat()
    {
        var response = await _relay.GetContent(Caller, "g-7", [ContentKind.Sheet, ContentKind.Timeline], new Uri("https://example.com/g-7?code=1"));

        Assert.Equal(ItemState.Open, response.State);
        Assert.Equal([ContentKind.Sheet, ContentKind.Timeline], response.Header!.Kinds);
        Assert.Equal("F#m", response.Header.Attributes.Key);
        Assert.Equal(new Uri("https://example.com/g-7"), response.Header.Address);
        // the address the person came by reaches the source as it was sent
        Assert.Equal(new Uri("https://example.com/g-7?code=1"), _good.AddressesAsked.Last());
        Assert.NotNull(response.Content);

        // on the wire: camel case, enums as camel-case strings
        var raw = await Post(SourceV2Urls.V2GetContent, """{"caller":{"subjects":[],"keys":[]},"id":"x-1","kinds":["sheet","timeline"]}""", ShelfKey);
        Assert.Equal(HttpStatusCode.OK, raw.StatusCode);
        using var json = JsonDocument.Parse(await raw.Content.ReadAsStringAsync());
        Assert.Equal("x-1", json.RootElement.GetProperty("id").GetString());
        Assert.Equal("notOpen", json.RootElement.GetProperty("state").GetString());

        var resolved = await _relay.Resolve(Caller, [new("https://example.com/g-7"), new("https://example.com/nowhere")]);
        Assert.Equal([ResolveState.Resolved, ResolveState.Unavailable], resolved.Items.Select(x => x.State));
        Assert.Equal("g-7", resolved.Items[0].Id);
    }

    [Fact]
    public async Task OnlyInternalClientsWithTheScopeGetIn()
    {
        Assert.Equal(HttpStatusCode.OK, (await Post(SourceV2Urls.V2Ping, "", ShelfKey)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(SourceV2Urls.V2Ping, "", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(SourceV2Urls.V2Ping, "", ShelfKey + "x")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(SourceV2Urls.V2Ping, "", OtherKey)).StatusCode);

        // the old ways do not open it
        using var withFunctionKey = new HttpRequestMessage(HttpMethod.Post, $"/index/api/{SourceV2Urls.V2Ping}?code={FunctionKey}");
        withFunctionKey.Headers.Add("x-functions-key", FunctionKey);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _http.SendAsync(withFunctionKey)).StatusCode);

        // an analysis tool: the internal client with the scope passes the access check (no caches here, so it fails
        // after it), the one without is refused
        Assert.NotEqual(HttpStatusCode.Unauthorized, (await Post("VExternal1TonalitiesLoops", "{}", ShelfKey)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post("VExternal1TonalitiesLoops", "{}", OtherKey)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post("VExternal1TonalitiesLoops", "{}", null)).StatusCode);
    }

    [Fact]
    public async Task AnIconNameIsRoutedByPrefix()
    {
        var icon = await _relay.GetIcon("g-icon");
        Assert.Equal(IconBytes, icon!.Content);
        Assert.Equal("image/png", icon.ContentType);

        Assert.Null(await _relay.GetIcon("g-other"));
        Assert.Null(await _relay.GetIcon("x-icon"));
        await Assert.ThrowsAsync<SourceUnavailableException>(() => _relay.GetIcon("d-icon"));
    }

    [Fact]
    public async Task WithoutSourcesTheIndexDoesNotStart()
    {
        await using var host = BuildIndexAlone(new()
        {
            ["Index:InternalClients:0:Name"] = "shelf",
            ["Index:InternalClients:0:Key"] = ShelfKey,
            ["Index:InternalClients:0:Scopes:0"] = "index",
        });

        await Assert.ThrowsAsync<Microsoft.Extensions.Options.OptionsValidationException>(() => host.StartAsync());
    }

    private static WebApplication BuildIndexAlone(Dictionary<string, string?> configuration)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(configuration);
        builder.AddApiModules(new IndexModule());
        var host = builder.Build();
        host.MapApiModules();
        return host;
    }

    [Fact]
    public async Task ContentsAreSplitBySourceAndJoinedInTheOrderAskedAFailingSourceFailingOnlyItsOwn()
    {
        var response = await _relay.GetContents(Caller, ["s-1", "g-2", "x-1", "g-gone", "d-1", "r-1", "g-3"], [ContentKind.Sheet]);

        Assert.Equal(
            [
                ("s-1", ItemState.Unavailable), ("g-2", ItemState.Open), ("x-1", ItemState.NotOpen), ("g-gone", ItemState.NotOpen),
                ("d-1", ItemState.Unavailable), ("r-1", ItemState.Unavailable), ("g-3", ItemState.Open),
            ],
            response.Items.Select(x => (x.Id, x.State)));
        Assert.Equal("g-3", response.Items[6].Header!.Id);
        Assert.NotNull(response.Items[6].Content);
        Assert.Null(response.Items[3].Content);

        // the good source was asked once, for its own ids only, in their order
        Assert.Equal(["g-2", "g-gone", "g-3"], _good.ContentIdsAsked.Single());

        // more ids than one call may ask, an id twice, an empty one: refused
        var tooMany = string.Join(',', Enumerable.Range(0, GetContentsRequest.MaxIds + 1).Select(i => $"\"g-{i}\""));
        foreach (var ids in new[] { tooMany, "\"g-1\",\"g-1\"", "\"\"" })
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(SourceV2Urls.V2GetContents, $$"""{"caller":{"subjects":[],"keys":[]},"ids":[{{ids}}],"kinds":["sheet"]}""", ShelfKey)).StatusCode);
    }

    [Fact]
    public async Task AnIncompleteRequestIsBad()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(SourceV2Urls.V2Search, """{"caller":null,"query":"x"}""", ShelfKey)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(SourceV2Urls.V2Search, """{"query":"x"}""", ShelfKey)).StatusCode);
    }

    private async Task<HttpResponseMessage> Post(string operation, string body, string? key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/index/api/{operation}");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        if (key != null) request.Headers.Add("X-Internal-Key", key);
        return await _http.SendAsync(request);
    }

    [Fact]
    public async Task AFileIsPassedOnAsItsSourceAnswersItWithRanges()
    {
        var request = GetFileRequest.Encode(new() { Caller = Caller, Id = "g-1", Name = "sound.mp3" });

        HttpRequestMessage Ask(HttpMethod method, string encoded, string? range = null, string? ifRange = null)
        {
            var message = new HttpRequestMessage(method, $"/index/api/{SourceV2Urls.V2GetFile}");
            message.Headers.Add("X-Internal-Key", ShelfKey);
            message.Headers.Add(GetFileRequest.HeaderName, encoded);
            if (range != null) message.Headers.TryAddWithoutValidation("Range", range);
            if (ifRange != null) message.Headers.TryAddWithoutValidation("If-Range", ifRange);
            return message;
        }

        var whole = await _http.SendAsync(Ask(HttpMethod.Get, request));
        Assert.Equal(HttpStatusCode.OK, whole.StatusCode);
        Assert.Equal(FileBytes, await whole.Content.ReadAsByteArrayAsync());
        Assert.Equal("audio/mpeg", whole.Content.Headers.ContentType!.MediaType);
        Assert.Equal("\"v1\"", whole.Headers.ETag!.Tag);

        var part = await _http.SendAsync(Ask(HttpMethod.Get, request, "bytes=10-19", "\"v1\""));
        Assert.Equal(HttpStatusCode.PartialContent, part.StatusCode);
        Assert.Equal(FileBytes[10..20], await part.Content.ReadAsByteArrayAsync());
        Assert.Equal($"bytes 10-19/{FileBytes.Length}", part.Content.Headers.ContentRange!.ToString());

        // a range of another version of the file gets the whole of it
        Assert.Equal(HttpStatusCode.OK, (await _http.SendAsync(Ask(HttpMethod.Get, request, "bytes=10-19", "\"v0\""))).StatusCode);

        var head = await _http.SendAsync(Ask(HttpMethod.Head, request));
        Assert.Equal((HttpStatusCode.OK, FileBytes.Length), (head.StatusCode, (int)head.Content.Headers.ContentLength!.Value));

        Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, (await _http.SendAsync(Ask(HttpMethod.Get, request, $"bytes={FileBytes.Length}-"))).StatusCode);

        // the caller reaches the source as it was sent
        Assert.Equal(Caller.Keys, _good.Callers.Last().Keys);

        // no such file, an id of no source, a source down or refusing, a request out of the contract
        Assert.Equal(HttpStatusCode.NotFound, (await _http.SendAsync(Ask(HttpMethod.Get, GetFileRequest.Encode(new() { Caller = Caller, Id = "g-1", Name = "other" })))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.SendAsync(Ask(HttpMethod.Get, GetFileRequest.Encode(new() { Caller = Caller, Id = "x-1", Name = "sound.mp3" })))).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await _http.SendAsync(Ask(HttpMethod.Get, GetFileRequest.Encode(new() { Caller = Caller, Id = "d-1", Name = "sound.mp3" })))).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await _http.SendAsync(Ask(HttpMethod.Get, GetFileRequest.Encode(new() { Caller = Caller, Id = "r-1", Name = "sound.mp3" })))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _http.SendAsync(Ask(HttpMethod.Get, "not-a-request"))).StatusCode);
    }

    [Fact]
    public async Task AFileOfASlowSourceIsCutOnlyWhileItsHeadersAreAwaited()
    {
        var stopwatch = Stopwatch.StartNew();
        await Assert.ThrowsAsync<SourceUnavailableException>(() => _relay.GetFile(Caller, "s-1", "sound.mp3", null, false, null, null));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(4), $"The slow source held the file for {stopwatch.Elapsed}.");

        // through the client, the body is read after the headers came
        using var file = await _relay.GetFile(Caller, "g-1", "sound.mp3", null, false, "bytes=0-4", null);
        Assert.NotNull(file);
        Assert.Equal(206, file!.StatusCode);
        await using var body = await file.ReadBodyAsync(CancellationToken.None);
        using var copy = new MemoryStream();
        await body.CopyToAsync(copy);
        Assert.Equal(FileBytes[..5], copy.ToArray());
    }
    /// <summary>
    /// A source on a real port: it knows every id of its prefix, finds one item, resolves its own addresses.
    /// </summary>
    private sealed class FakeSource : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private FakeSource(WebApplication app, Uri endpoint)
        {
            _app = app;
            Endpoint = endpoint;
        }

        public Uri Endpoint { get; }

        public ConcurrentQueue<Caller> Callers { get; } = new();

        public ConcurrentQueue<IReadOnlyList<string>> HeaderIdsAsked { get; } = new();

        public ConcurrentQueue<IReadOnlyList<string>> ContentIdsAsked { get; } = new();

        public ConcurrentQueue<Uri?> AddressesAsked { get; } = new();

        /// <param name="answersOutOfContract">The source answers a search with a list missing ("headers": null).</param>
        public static async Task<FakeSource> Start(string prefix, TimeSpan delay, HttpStatusCode? refuseWith, bool answersOutOfContract = false)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            var app = builder.Build();
            FakeSource? source = null;

            Header Header(string id) => new()
            {
                Id = id,
                Source = new() { Key = "fake", Title = "Fake" },
                Address = new($"https://example.com/{id}"),
                Title = "Title",
                Artists = ["Artist"],
                Kinds = [ContentKind.Sheet, ContentKind.Timeline],
                Attributes = new() { Key = "F#m" },
                IsPublic = true,
            };

            // a request delegate: it writes its answer itself
            async Task Serve<TRequest>(HttpContext context, Func<TRequest, object> answer)
            {
                if (context.Request.Headers["X-Internal-Key"] != SourceKey)
                {
                    context.Response.StatusCode = 401;
                    return;
                }

                if (refuseWith.HasValue)
                {
                    context.Response.StatusCode = (int)refuseWith.Value;
                    return;
                }

                await Task.Delay(delay, context.RequestAborted);
                var request = await JsonSerializer.DeserializeAsync<TRequest>(context.Request.Body, SourceV2Json.Options);
                await Results.Json(answer(request!), SourceV2Json.Options).ExecuteAsync(context);
            }

            app.MapPost($"/api/{SourceV2Urls.V2Search}", (HttpContext c) => answersOutOfContract
                ? Results.Text("""{"headers":null,"failures":[]}""", "application/json").ExecuteAsync(c)
                : Serve<SearchRequest>(c, r =>
                {
                    source!.Callers.Enqueue(r.Caller);
                    return new SearchResponse { Headers = [Header($"{prefix}1")], Failures = [] };
                }));
            app.MapPost($"/api/{SourceV2Urls.V2GetHeaders}", (HttpContext c) => Serve<GetHeadersRequest>(c, r =>
            {
                source!.Callers.Enqueue(r.Caller);
                source.HeaderIdsAsked.Enqueue(r.Ids);
                return new GetHeadersResponse
                {
                    Headers = r.Ids.Select(id => new HeaderAnswer { Id = id, State = ItemState.Open, Header = Header(id) }).ToList(),
                };
            }));
            app.MapPost($"/api/{SourceV2Urls.V2GetLabels}", (HttpContext c) => Serve<GetLabelsRequest>(c, r =>
            {
                source!.Callers.Enqueue(r.Caller);
                // an item of the prefix with "gone" in it is not known
                return new GetLabelsResponse
                {
                    Labels = r.Ids.Where(id => id.StartsWith(prefix) && !id.Contains("gone")).Select(id => new ItemLabel { Id = id, Source = new() { Key = prefix, Title = prefix } }).ToList(),
                };
            }));            app.MapPost($"/api/{SourceV2Urls.V2GetContent}", (HttpContext c) => Serve<GetContentRequest>(c, r =>
            {
                source!.AddressesAsked.Enqueue(r.Address);
                return new GetContentResponse { Id = r.Id, State = ItemState.Open, Header = Header(r.Id), Content = new() };
            }));
            app.MapPost($"/api/{SourceV2Urls.V2GetSources}", (HttpContext c) => Serve<GetSourcesRequest>(c, r =>
            {
                source!.Callers.Enqueue(r.Caller);
                return new GetSourcesResponse { Sources = [new() { Key = prefix, Title = prefix }], Failures = [] };
            }));
            app.MapPost($"/api/{SourceV2Urls.V2GetContents}", (HttpContext c) => Serve<GetContentsRequest>(c, r =>
            {
                source!.ContentIdsAsked.Enqueue(r.Ids);
                return new GetContentsResponse
                {
                    // an item of the prefix with "gone" in it is not open
                    Items = r.Ids.Select(id => id.Contains("gone")
                        ? new GetContentResponse { Id = id, State = ItemState.NotOpen }
                        : new GetContentResponse { Id = id, State = ItemState.Open, Header = Header(id), Content = new() }).ToList(),
                };
            }));
            app.MapPost($"/api/{SourceV2Urls.V2Resolve}", (HttpContext c) => Serve<ResolveRequest>(c, r => new ResolveResponse
            {
                Items = r.Uris.Select(uri =>
                {
                    var id = uri.AbsolutePath.TrimStart('/');
                    return id.StartsWith(prefix)
                        ? new ResolvedUri { Uri = uri, State = ResolveState.Resolved, Id = id }
                        : new ResolvedUri { Uri = uri, State = ResolveState.NotResolved };
                }).ToList(),
            }));

            // a file of every id of the prefix: "sound.mp3", FileBytes long, with ranges; the caller is noted
            app.MapMethods($"/api/{SourceV2Urls.V2GetFile}", ["GET", "HEAD"], async context =>
            {
                if (context.Request.Headers["X-Internal-Key"] != SourceKey)
                {
                    context.Response.StatusCode = 401;
                    return;
                }

                if (refuseWith.HasValue)
                {
                    context.Response.StatusCode = (int)refuseWith.Value;
                    return;
                }

                await Task.Delay(delay, context.RequestAborted);
                var request = GetFileRequest.Decode(context.Request.Headers[GetFileRequest.HeaderName]);
                if (request == null)
                {
                    context.Response.StatusCode = 400;
                    return;
                }

                source!.Callers.Enqueue(request.Caller);
                if (request.Name != "sound.mp3")
                {
                    context.Response.StatusCode = 404;
                    return;
                }

                await Results.Bytes(FileBytes, "audio/mpeg", entityTag: new("\"v1\""), enableRangeProcessing: true).ExecuteAsync(context);
            });

            app.MapPost($"/api/{SourceV2Urls.V2GetIcon}", async context =>
            {
                var request = await JsonSerializer.DeserializeAsync<GetIconRequest>(context.Request.Body, SourceV2Json.Options);
                if (context.Request.Headers["X-Internal-Key"] != SourceKey)
                    context.Response.StatusCode = 401;
                else if (request!.Name != $"{prefix}icon")
                    context.Response.StatusCode = 404;
                else
                    await Results.Bytes(IconBytes, "image/png").ExecuteAsync(context);
            });

            await app.StartAsync();
            source = new(app, new($"{app.Urls.Single()}/api/"));
            return source;
        }

        public async ValueTask DisposeAsync() => await _app.DisposeAsync();
    }
}
