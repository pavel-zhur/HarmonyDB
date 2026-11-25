using System.Net;
using System.Net.Http.Json;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Chords;
using HarmonyDB.Shelf.Model.Playlists;
using HarmonyDB.Shelf.Model.Sync;
using HarmonyDB.Shelf.Tests.Auth;
using HarmonyDB.Shelf.Tests.Chords;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;

namespace HarmonyDB.Shelf.Tests.Playlists;

/// <summary>
/// The library kept on a device: its chords' ids, told again only when the library changed, and their content, many
/// at once, of the library's chords only.
/// </summary>
public sealed class OfflineTests(LibraryFixture fixture) : IClassFixture<LibraryFixture>
{
    [DatabaseFact]
    public async Task TheLibrarysChordsAreListedWhenItChangedAndOnlyTheirContentIsRead()
    {
        // the sources: "test-…" open, anything else not open
        var contentsAsked = new List<List<string>>();
        fixture.Index.Answer = (operation, body) =>
        {
            var ids = operation switch
            {
                "V2GetHeaders" or "V2GetContents" => body.GetProperty("ids").EnumerateArray().Select(x => x.GetString()!).ToList(),
                _ => null,
            };
            bool Open(string id) => id.StartsWith("test-", StringComparison.Ordinal);
            switch (operation)
            {
                case "V2GetHeaders":
                    return (HttpStatusCode.OK, new GetHeadersResponse
                    {
                        Headers = ids!.Select(id => Open(id)
                            ? new HeaderAnswer { Id = id, State = ItemState.Open, Header = FakeIndex.Header(id, "A source") }
                            : new HeaderAnswer { Id = id, State = ItemState.NotOpen }).ToList(),
                    });
                case "V2GetContents":
                    lock (contentsAsked) contentsAsked.Add(ids!);
                    return (HttpStatusCode.OK, new GetContentsResponse
                    {
                        Items = ids!.Select(id => Open(id)
                            ? new GetContentResponse { Id = id, State = ItemState.Open, Header = FakeIndex.Header(id, "A source"), Content = new() { Sheet = new() { Root = new NodeHtml { Name = "pre" } } } }
                            : new GetContentResponse { Id = id, State = ItemState.NotOpen }).ToList(),
                    });
                default:
                    return (HttpStatusCode.NotFound, null);
            }
        };

        var (ann, annId) = await fixture.PersonAsync("ann");
        var tag = Guid.NewGuid().ToString("N");
        var (a, b, marked, closed, outside) = ($"test-a-{tag}", $"test-b-{tag}", $"test-m-{tag}", $"closed-{tag}", $"test-out-{tag}");
        var playlist = (await Ok<Changed<PlaylistDetails>>(ann, HttpMethod.Post, "/api/playlists", new CreatePlaylistRequest { Title = "Mine", Access = PlaylistAccess.Private })).Value.Playlist.Id;
        await Ok<Changed<SongRef>>(ann, HttpMethod.Post, $"/api/playlists/{playlist}/songs", new AddSongRequest { Chords = a, Title = "A" });
        var songB = (await Ok<Changed<SongRef>>(ann, HttpMethod.Post, $"/api/playlists/{playlist}/songs", new AddSongRequest { Chords = b, Title = "B" })).Value.Id;
        await Ok<SyncResponse>(ann, HttpMethod.Post, "/api/sync", new SyncRequest
        {
            AccountId = annId,
            Changes = [new() { Type = ChangeType.Mark, Chords = marked, Kind = "star", On = true }],
        });

        // a mark left on chords that are no longer open to the person
        await using (var db = fixture.CreateContext())
        {
            var row = new global::HarmonyDB.Shelf.Database.Model.ChordsItem { Id = Guid.NewGuid(), SourceId = closed, CreatedAt = DateTimeOffset.UtcNow };
            db.Chords.Add(row);
            db.Marks.Add(new() { Id = Guid.NewGuid(), AccountId = annId, ChordsId = row.Id, Kind = "star", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        // the whole list, then nothing while nothing changes, then the whole list again after a change
        var first = await Ok<OfflineChordsList>(ann, HttpMethod.Get, "/api/library/offline/chords");
        Assert.True(first.Changed);
        Assert.Equal(new[] { a, b, marked, closed }.Order(StringComparer.Ordinal), first.Chords);

        var same = await Ok<OfflineChordsList>(ann, HttpMethod.Get, $"/api/library/offline/chords?since={first.Watermark}");
        Assert.False(same.Changed);
        Assert.Empty(same.Chords);

        await Ok<UndoOnly>(ann, HttpMethod.Delete, $"/api/playlists/{playlist}/songs/{songB}");
        var after = await Ok<OfflineChordsList>(ann, HttpMethod.Get, $"/api/library/offline/chords?since={same.Watermark}");
        Assert.True(after.Changed);
        Assert.Equal(new[] { a, marked, closed }.Order(StringComparer.Ordinal), after.Chords);

        // the content of the library's chords only, in the order asked
        var contents = await Ok<OfflineContents>(ann, HttpMethod.Post, "/api/library/offline/contents", new OfflineContentsRequest { Chords = [closed, outside, a, b] });
        Assert.Equal(
            [(closed, OfflineContentState.NotOpen), (outside, OfflineContentState.NotInLibrary), (a, OfflineContentState.Open), (b, OfflineContentState.NotInLibrary)],
            contents.Items.Select(x => (x.Chords, x.State)));
        Assert.NotNull(contents.Items[2].Content!.Sheet);
        Assert.Equal($"Title of {a}", contents.Items[2].Header!.Title);
        Assert.Equal([closed, a], Assert.Single(contentsAsked));

        // too many, twice, or a recording: refused
        foreach (var bad in new[]
                 {
                     new OfflineContentsRequest { Chords = Enumerable.Range(0, OfflineContentsRequest.MaxChords + 1).Select(i => $"test-{i}").ToList() },
                     new OfflineContentsRequest { Chords = [a, a] },
                     new OfflineContentsRequest { Chords = [a], Kinds = [ChordsKind.Recording] },
                     new OfflineContentsRequest { Chords = [a], Kinds = [ChordsKind.Sheet, ChordsKind.Score] },
                     new OfflineContentsRequest { Chords = [a], Kinds = [ChordsKind.Notes] },
                 })
            await Browser.AssertError(await ann.SendAsync(HttpMethod.Post, "/api/library/offline/contents", bad), HttpStatusCode.BadRequest, ErrorCodes.BadRequest);

        await Browser.AssertError(await fixture.Api.NewBrowser().SendAsync(HttpMethod.Get, "/api/library/offline/chords"), HttpStatusCode.Unauthorized, ErrorCodes.SignInRequired);
    }

    private static async Task<T> Ok<T>(Browser browser, HttpMethod method, string path, object? body = null)
    {
        var response = await browser.SendAsync(method, path, body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{method} {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Browser.Json))!;
    }
}
