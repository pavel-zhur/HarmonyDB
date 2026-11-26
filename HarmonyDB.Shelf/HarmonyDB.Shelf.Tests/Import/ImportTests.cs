using System.Net;
using System.Text.Json;
using HarmonyDB.Shelf.Api.Areas.Import.Services;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Import;
using HarmonyDB.Shelf.Model.Jobs;
using HarmonyDB.Shelf.Tests.Auth;
using HarmonyDB.Shelf.Tests.Chords;
using HarmonyDB.Shelf.Tests.Jobs;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HarmonyDB.Shelf.Tests.Import;

/// <summary>
/// Importing links: what became of each, and the chords in the person's playlist of imported chords.
/// </summary>
public sealed class ImportTests(WorkFixture fixture) : IClassFixture<WorkFixture>
{
    [Fact]
    public void TheLinksAreEachLineTrimmedOnce()
        => Assert.Equal(
            ["https://a.example/1", "https://a.example/2", "not a link"],
            ImportService.Split(["https://a.example/1\n  https://a.example/2 \n\n", "https://a.example/1", " not a link "]));

    [DatabaseFact]
    public async Task EachLinkSaysWhatBecameOfIt()
    {
        var me = await fixture.AccountAsync();
        var open = $"import-{Guid.NewGuid():N}";
        fixture.Index.Answer = (operation, body) => operation switch
        {
            "V2Resolve" => (HttpStatusCode.OK, new ResolveResponse
            {
                Items = body.GetProperty("uris").EnumerateArray().Select(x => new Uri(x.GetString()!)).Select(uri => uri.Host switch
                {
                    "known.example" => new ResolvedUri { Uri = uri, State = ResolveState.Resolved, Id = open },
                    "down.example" => new ResolvedUri { Uri = uri, State = ResolveState.Unavailable },
                    _ => new ResolvedUri { Uri = uri, State = ResolveState.NotResolved },
                }).ToList(),
            }),
            "V2GetHeaders" => (HttpStatusCode.OK, new GetHeadersResponse
            {
                Headers = body.GetProperty("ids").EnumerateArray().Select(x => new HeaderAnswer { Id = x.GetString()!, State = ItemState.Open, Header = FakeIndex.Header(x.GetString()!, "Source") }).ToList(),
            }),
            _ => (HttpStatusCode.InternalServerError, null),
        };

        string[] links = ["https://known.example/song", "https://unknown.example/song", "https://down.example/song", "ftp://known.example/song", "https://known.example/song?again"];
        using var client = fixture.Api.CreateClient();
        var started = await client.SendAsync(FakeIndex.As(HttpMethod.Post, "/api/import/links", me, body: new ImportLinksRequest { Links = links }));
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var job = JsonSerializer.Deserialize<JobInfo>(await started.Content.ReadAsStringAsync(), Browser.Json)!;

        var done = await fixture.FinishedAsync(me, job.Id);
        Assert.Equal(JobStatus.Succeeded, done.Status);
        var result = Assert.IsType<ImportResult>(done.Result);
        Assert.Equal(
            [ImportLinkState.Imported, ImportLinkState.NotFound, ImportLinkState.Unavailable, ImportLinkState.NotALink, ImportLinkState.Imported],
            result.Links.Select(x => x.State));
        Assert.Equal(result.Links[0].SongId, result.Links[4].SongId);

        // the chords are one song of the person's playlist of imported chords, titled as the source titles them
        await using var db = fixture.CreateContext();
        var songs = await db.PlaylistSongs.Where(x => x.PlaylistId == result.PlaylistId).ToListAsync();
        Assert.Equal($"Title of {open}", Assert.Single(songs).Title);
        Assert.Equal(PlaylistSystemKind.ImportedChords, (await db.Playlists.SingleAsync(x => x.Id == result.PlaylistId)).SystemKind);
    }

    [DatabaseFact]
    public async Task ALineLongerThanALinkIsNotALinkAndTheRestIsImported()
    {
        var me = await fixture.AccountAsync();
        var tag = Guid.NewGuid().ToString("N");
        var tooLong = $"https://unknown-{tag}.example/" + new string('я', ImportService.MaxLinkBytes / 2);
        var unknown = $"https://unknown-{tag}.example/song";
        fixture.Index.Answer = (operation, body) => operation == "V2Resolve"
            ? (HttpStatusCode.OK, new ResolveResponse
            {
                Items = body.GetProperty("uris").EnumerateArray().Select(x => new ResolvedUri { Uri = new(x.GetString()!), State = ResolveState.NotResolved }).ToList(),
            })
            : (HttpStatusCode.InternalServerError, null);

        using var client = fixture.Api.CreateClient();
        var started = await client.SendAsync(FakeIndex.As(HttpMethod.Post, "/api/import/links", me, body: new ImportLinksRequest { Links = [tooLong, unknown] }));
        var done = await fixture.FinishedAsync(me, JsonSerializer.Deserialize<JobInfo>(await started.Content.ReadAsStringAsync(), Browser.Json)!.Id);

        Assert.Equal(JobStatus.Succeeded, done.Status);
        Assert.Equal([ImportLinkState.NotALink, ImportLinkState.NotFound], Assert.IsType<ImportResult>(done.Result).Links.Select(x => x.State));
        await using var db = fixture.CreateContext();
        Assert.Equal([unknown], await db.UnreadLinks.Where(x => x.Link.Contains(tag)).Select(x => x.Link).ToListAsync());
    }

    [DatabaseFact]
    public async Task ALinkNotFoundIsKeptForTheAdminWithWhoPastedItWhoGoesWithTheProfile()
    {
        var me = await fixture.AccountAsync();
        var link = $"https://unknown-{Guid.NewGuid():N}.example/song";
        fixture.Index.Answer = (operation, body) => operation == "V2Resolve"
            ? (HttpStatusCode.OK, new ResolveResponse
            {
                Items = body.GetProperty("uris").EnumerateArray().Select(x => new ResolvedUri { Uri = new(x.GetString()!), State = ResolveState.NotResolved }).ToList(),
            })
            : (HttpStatusCode.InternalServerError, null);
        fixture.Store.Answer = (_, _) => (HttpStatusCode.OK, new { removed = 0 });
        using var client = fixture.Api.CreateClient();

        // pasted in two imports
        for (var i = 0; i < 2; i++)
        {
            var started = await client.SendAsync(FakeIndex.As(HttpMethod.Post, "/api/import/links", me, body: new ImportLinksRequest { Links = [link] }));
            var job = JsonSerializer.Deserialize<JobInfo>(await started.Content.ReadAsStringAsync(), Browser.Json)!;
            Assert.Equal(JobStatus.Succeeded, (await fixture.FinishedAsync(me, job.Id)).Status);
        }

        async Task<HarmonyDB.Shelf.Model.Admin.AdminUnreadLink> ListedAsync()
        {
            await using var scope = fixture.Api.Services.CreateAsyncScope();
            var links = await scope.ServiceProvider.GetRequiredService<HarmonyDB.Shelf.Api.Areas.Admin.Services.UnreadLinksAdmin>().ListAsync(500, CancellationToken.None);
            return Assert.Single(links, x => x.Link == link);
        }

        var listed = await ListedAsync();
        Assert.Equal((2, me), (listed.Times, Assert.Single(listed.People).Id));

        // the person deletes their profile: the link stays, who pasted it goes
        await using (var scope = fixture.Api.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<HarmonyDB.Shelf.Api.Areas.Me.Services.ProfileRemoval>().DeleteAsync(me, CancellationToken.None);

        listed = await ListedAsync();
        Assert.Equal((2, 0), (listed.Times, listed.People.Count));
        await using var db = fixture.CreateContext();
        Assert.All(await db.UnreadLinks.Where(x => x.Link == link).ToListAsync(), x => Assert.Null(x.AccountId));
    }

    [DatabaseFact]
    public async Task ManyLinksAreImportedWhateverTheLookupsTheyStart()
    {
        var me = await fixture.AccountAsync();
        var prefix = $"many-{Guid.NewGuid():N}";
        fixture.Index.Answer = (operation, body) => operation switch
        {
            "V2Resolve" => (HttpStatusCode.OK, new ResolveResponse
            {
                Items = body.GetProperty("uris").EnumerateArray().Select(x => new Uri(x.GetString()!))
                    .Select(uri => new ResolvedUri { Uri = uri, State = ResolveState.Resolved, Id = $"{prefix}{uri.AbsolutePath.Replace('/', '-')}" }).ToList(),
            }),
            "V2GetHeaders" => (HttpStatusCode.OK, new GetHeadersResponse
            {
                Headers = body.GetProperty("ids").EnumerateArray().Select(x => new HeaderAnswer { Id = x.GetString()!, State = ItemState.Open, Header = FakeIndex.Header(x.GetString()!, "Source") }).ToList(),
            }),
            _ => (HttpStatusCode.InternalServerError, null),
        };

        // more links than a person may start jobs of one kind: each song added queues the shelf's lookup of its recordings
        var links = Enumerable.Range(1, 6).Select(i => $"https://known.example/song{i}").ToArray();
        using var client = fixture.Api.CreateClient();
        var started = await client.SendAsync(FakeIndex.As(HttpMethod.Post, "/api/import/links", me, body: new ImportLinksRequest { Links = links }));
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var job = JsonSerializer.Deserialize<JobInfo>(await started.Content.ReadAsStringAsync(), Browser.Json)!;

        var done = await fixture.FinishedAsync(me, job.Id);
        Assert.Equal(JobStatus.Succeeded, done.Status);
        var result = Assert.IsType<ImportResult>(done.Result);
        Assert.All(result.Links, x => Assert.Equal(ImportLinkState.Imported, x.State));

        // the lookups are the shelf's: queued with the songs, one each, and not in the person's list of jobs
        await using var db = fixture.CreateContext();
        var songs = await db.PlaylistSongs.Where(x => x.PlaylistId == result.PlaylistId).Select(x => x.Id.ToString()).ToListAsync();
        Assert.Equal(6, songs.Count);
        Assert.Equal(6, await db.Jobs.CountAsync(x => x.Kind == HarmonyDB.Shelf.Api.Areas.Recordings.Services.SongRecordingsJob.Kind && songs.Contains(x.SubjectId!)));
        var listed = await (await client.SendAsync(FakeIndex.As(HttpMethod.Get, "/api/jobs", me))).Content.ReadAsStringAsync();
        Assert.DoesNotContain(HarmonyDB.Shelf.Api.Areas.Recordings.Services.SongRecordingsJob.Kind, listed);
        Assert.Contains(job.Id.ToString(), listed);
    }
}
