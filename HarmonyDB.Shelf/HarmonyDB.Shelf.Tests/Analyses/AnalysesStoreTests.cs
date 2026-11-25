using System.Net;
using System.Net.Http.Json;
using HarmonyDB.Shelf.Api.Areas.Analyses;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Tests.Auth;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using HarmonyDB.Source.Api.Model.V2.Contents;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Tests.Analyses;

/// <summary>
/// The analyses over a real database: who they are open to, their kinds and files, putting them again, the playlist,
/// the scopes and bad requests.
/// </summary>
public sealed class AnalysesStoreTests(ShelfDatabaseFixture database) : IClassFixture<ShelfDatabaseFixture>
{
    private const string SourceKey = "a-source-key-of-the-tests-0123456789";
    private const string WriteKey = "a-write-key-of-the-tests-01234567890";

    private AuthApiFactory? _api;

    private AuthApiFactory Api => _api ??= new(database.ConnectionString, settings: new Dictionary<string, string?>
    {
        ["InternalClients:1:Name"] = "index",
        ["InternalClients:1:Key"] = SourceKey,
        ["InternalClients:1:Scopes:0"] = AnalysesArea.SourceScope,
        ["InternalClients:2:Name"] = "analyses",
        ["InternalClients:2:Key"] = WriteKey,
        ["InternalClients:2:Scopes:0"] = AnalysesArea.WriteScope,
        ["Analyses:SourceKey"] = "tests",
        ["Analyses:SourceTitle"] = "Tests",
    });

    [DatabaseFact]
    public async Task PutAgainItIsTheSameAnalysis()
    {
        var first = await PutAsync("again", "First title", null);
        var second = await PutAsync("again", "Second title", null);
        Assert.Equal(first.Id, second.Id);

        await using var db = database.CreateContext();
        var analysis = await db.Analyses.Include(x => x.Contents).SingleAsync(x => x.SourceId == first.Id);
        Assert.Equal("Second title", analysis.Title);
        Assert.Equal(["recording", "timeline"], analysis.Contents.Select(x => x.Kind).Order());
    }

    [DatabaseFact]
    public async Task APrivateAnalysisIsOpenToItsOwnerOnly()
    {
        var owner = await AccountAsync("owner");
        var other = await AccountAsync("other");
        var put = await PutAsync("private", "Hidden song", owner);

        foreach (var (caller, open) in new[] { (Caller(null), false), (Caller(other), false), (Caller(owner), true) })
        {
            var search = await SourceAsync<SearchResponse>("V2Search", new SearchRequest { Caller = caller, Query = "hidden" });
            Assert.Equal(open, search.Headers.Any(x => x.Id == put.Id));

            var headers = await SourceAsync<GetHeadersResponse>("V2GetHeaders", new GetHeadersRequest { Caller = caller, Ids = [put.Id] });
            Assert.Equal(open ? ItemState.Open : ItemState.NotOpen, headers.Headers.Single().State);

            var content = await SourceAsync<GetContentResponse>("V2GetContent", new GetContentRequest { Caller = caller, Id = put.Id, Kinds = [ContentKind.Sheet] });
            Assert.Equal(open ? ItemState.Open : ItemState.NotOpen, content.State);
            Assert.Equal(open, content.Content?.Sheet != null);
        }
    }

    [DatabaseFact]
    public async Task AKindThatNamesFilesIsThereOnceTheyAreStored()
    {
        var put = await PutAsync("files", "With a recording", null);
        Assert.Equal(["take.mp3"], put.Files);

        var kinds = await KindsAsync(put.Id);
        Assert.DoesNotContain(ContentKind.Recording, kinds);
        var content = await SourceAsync<GetContentResponse>("V2GetContent", new GetContentRequest { Caller = Caller(null), Id = put.Id, Kinds = [ContentKind.Recording] });
        Assert.Null(content.Content!.Recording);

        // the file stored, as the music service's upload leaves it
        await using (var db = database.CreateContext())
        {
            var analysis = await db.Analyses.SingleAsync(x => x.SourceId == put.Id);
            db.AnalysisFiles.Add(new()
            {
                AnalysisId = analysis.Id, Name = "take.mp3", BlobKey = $"analyses/{analysis.Id:N}/take.mp3", ContentType = "audio/mpeg", Size = 3,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        Assert.Contains(ContentKind.Recording, await KindsAsync(put.Id));
    }

    [DatabaseFact]
    public async Task ThePlaylistHoldsExactlyTheAnalysesGiven()
    {
        var a = await PutAsync("playlist-a", "Song A", null);
        var b = await PutAsync("playlist-b", "Song B", null);

        var both = await PlaylistAsync([a.Id, b.Id]);
        Assert.Equal((2, 0), (both.Added, both.Removed));
        Assert.Equal((0, 0), ((await PlaylistAsync([a.Id, b.Id])).Added, (await PlaylistAsync([a.Id, b.Id])).Removed));

        var one = await PlaylistAsync([b.Id]);
        Assert.Equal((0, 1), (one.Added, one.Removed));
        Assert.Equal((0, 0), ((await PlaylistAsync([b.Id])).Added, (await PlaylistAsync([b.Id])).Removed));

        // taken out and put back: a song of its own again, the removed one stays removed
        var back = await PlaylistAsync([a.Id, b.Id]);
        Assert.Equal((1, 0), (back.Added, back.Removed));

        await using var db = database.CreateContext();
        var songs = await db.PlaylistSongs.IgnoreQueryFilters().Where(x => x.PlaylistId == back.PlaylistId).ToListAsync();
        Assert.Equal(3, songs.Count);
        Assert.Equal(["Song A", "Song B"], songs.Where(x => x.DeletedAt == null).OrderBy(x => x.Position).Select(x => x.Title));
        var playlist = await db.Playlists.Include(x => x.Members).SingleAsync(x => x.Id == back.PlaylistId);
        Assert.Equal(PlaylistAccess.Public, playlist.Access);
        Assert.Equal(PlaylistRole.Owner, playlist.Members.Single().Role);
    }

    [DatabaseFact]
    public async Task ScopesAndBadRequestsAreRefused()
    {
        using var client = Api.CreateClient();

        // the source's key does not write, the writer's key does not read the source
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, HttpMethod.Put, "/internal/analyses", SourceKey, Put("scopes", "x", null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, HttpMethod.Post, "/internal/source/V2Search", WriteKey, new SearchRequest { Caller = Caller(null), Query = "x" })).StatusCode);

        // no caller, a section with neither a label nor a name, a meter that is not one
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, HttpMethod.Post, "/internal/source/V2Search", SourceKey, new { query = "x" })).StatusCode);
        var parts = Put("bad-part", "x", null) with
        {
            Content = Put("bad-part", "x", null).Content with
            {
                Timeline = Timeline() with { Sections = [new() { Start = 0, End = 4 }] },
            },
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, HttpMethod.Put, "/internal/analyses", WriteKey, parts)).StatusCode);
        var meter = Put("bad-meter", "x", null) with
        {
            Content = Put("bad-meter", "x", null).Content with
            {
                Timeline = Timeline() with { Bars = [Timeline().Bars[0] with { TimeSignature = "four" }] },
            },
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, HttpMethod.Put, "/internal/analyses", WriteKey, meter)).StatusCode);
    }

    // ------------------------------------------------------------------ helpers

    private static Caller Caller(Guid? account) => new() { AccountId = account, Keys = [] };

    private static Timeline Timeline() => new()
    {
        Duration = 8,
        Keys = [new() { Key = "C", Start = 0, End = 8 }],
        Bars = [new() { Start = 0, End = 4, Beats = [0, 1, 2, 3], TimeSignature = "4/4", Tempo = new() { Bpm = 60, BeatUnit = new() { Denominator = 4 } } }],
        Chords = [new() { Start = 0, End = 8, Symbol = new() { Kind = ChordSymbolKind.Chord, Name = "C" } }],
        Sections = [],
    };

    private static PutAnalysisRequest Put(string key, string title, Guid? owner) => new()
    {
        Key = key,
        Title = title,
        OwnerId = owner,
        Content = new()
        {
            Timeline = Timeline(),
            Recording = new() { Audio = new() { Blob = new() { Key = "take.mp3", ContentType = "audio/mpeg" } }, Duration = 8 },
        },
    };

    private async Task<PutAnalysisResponse> PutAsync(string key, string title, Guid? owner)
    {
        using var client = Api.CreateClient();
        var response = await Send(client, HttpMethod.Put, "/internal/analyses", WriteKey, Put(key, title, owner));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PutAnalysisResponse>(SourceV2Json.Options))!;
    }

    private async Task<PublishAnalysesPlaylistResponse> PlaylistAsync(IReadOnlyList<string> ids)
    {
        using var client = Api.CreateClient();
        var response = await Send(client, HttpMethod.Put, "/internal/analyses/playlist", WriteKey,
            new PublishAnalysesPlaylistRequest { SystemAccount = "Tests system", Title = "Tests playlist", Ids = ids });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PublishAnalysesPlaylistResponse>(SourceV2Json.Options))!;
    }

    private async Task<T> SourceAsync<T>(string operation, object request)
    {
        using var client = Api.CreateClient();
        var response = await Send(client, HttpMethod.Post, $"/internal/source/{operation}", SourceKey, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(SourceV2Json.Options))!;
    }

    private async Task<IReadOnlyList<ContentKind>> KindsAsync(string id)
        => (await SourceAsync<GetHeadersResponse>("V2GetHeaders", new GetHeadersRequest { Caller = Caller(null), Ids = [id] })).Headers.Single().Header!.Kinds;

    private async Task<Guid> AccountAsync(string name)
    {
        await using var db = database.CreateContext();
        var account = new Account { Id = Guid.NewGuid(), DisplayName = name, CreatedAt = DateTimeOffset.UtcNow };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string key, object body)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body, body.GetType(), options: SourceV2Json.Options) };
        request.Headers.Add("X-Internal-Key", key);
        return client.SendAsync(request);
    }
}
