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
        var result = done.Result!.Value.Deserialize<ImportResult>(Browser.Json)!;
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
}
