using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using HarmonyDB.Shelf.Model.Playlists;
using HarmonyDB.Shelf.Tests.Auth;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace HarmonyDB.Shelf.Tests.Playlists;

/// <summary>
/// A shared playlist of ten thousand songs, read as a person types: each call answers in its page, and how long it takes
/// is written out.
/// </summary>
public sealed class BigPlaylistTests(LibraryFixture fixture, ITestOutputHelper output) : IClassFixture<LibraryFixture>
{
    private const int Songs = 10000;

    [DatabaseFact]
    public async Task ATenThousandSongPlaylistIsReadAPageAtATime()
    {
        var (ann, annId) = await fixture.PersonAsync("ann");
        var created = await ann.SendAsync(HttpMethod.Post, "/api/playlists", new CreatePlaylistRequest { Title = "Big", Access = PlaylistAccess.Public });
        var playlist = (await created.Content.ReadFromJsonAsync<Changed<PlaylistDetails>>(Browser.Json))!.Value.Playlist.Id;

        await using (var db = fixture.CreateContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                CREATE TEMP TABLE big AS SELECT n, gen_random_uuid() AS song, gen_random_uuid() AS chords, gen_random_uuid() AS item,
                    gen_random_uuid() AS recording, gen_random_uuid() AS recording_item
                FROM generate_series(1, {Songs}) AS n;
                INSERT INTO chords (id, source_id, title, artists, kinds, created_at)
                SELECT chords, 'test-big-' || n, 'Chords ' || n, ARRAY['Artist ' || (n % 100)], ARRAY['sheet'], now() FROM big;
                INSERT INTO playlist_songs (id, playlist_id, title, artists, position, added_by_id, added_at)
                SELECT song, {playlist}, 'Song ' || n, ARRAY['Artist ' || (n % 100)], n, {annId}, now() - n * interval '1 minute' FROM big;
                INSERT INTO playlist_song_items (id, song_id, chords_id, position, added_by_id, added_at)
                SELECT item, song, chords, 0, {annId}, now() - n * interval '1 minute' FROM big;
                INSERT INTO recordings (id, service, service_id, title, artists, cover_url, updated_at)
                SELECT recording, 'youtube', 'big-' || n, 'Song ' || n, ARRAY['Artist ' || (n % 100)], 'https://covers.test/' || n || '.jpg', now() FROM big;
                INSERT INTO playlist_song_items (id, song_id, recording_id, position, added_at)
                SELECT recording_item, song, recording, 1, now() FROM big;
                INSERT INTO marks (id, account_id, chords_id, kind, created_at)
                SELECT gen_random_uuid(), {annId}, chords, 'heart_red', now() FROM big WHERE n % 20 = 0;
                """);
        }

        var calls = new (string Name, string Path, int Total)[]
        {
            ("the first page, in order", $"/api/playlists/{playlist}/songs?limit=200", Songs),
            ("the last page", $"/api/playlists/{playlist}/songs?limit=200&offset=9800", Songs),
            ("typed \"song 77\"", $"/api/playlists/{playlist}/songs?limit=200&q=song%2077", 111),
            ("typed an artist", $"/api/playlists/{playlist}/songs?limit=200&q=artist%2042", 100),
            ("popular", $"/api/playlists/{playlist}/songs?limit=200&sort=popular", Songs),
            ("new chords", $"/api/playlists/{playlist}/songs?limit=200&sort=newChords", Songs),
            ("my marks", $"/api/playlists/{playlist}/songs?limit=200&sort=myMarks", Songs),
            ("only what I marked", $"/api/playlists/{playlist}/songs?limit=200&who=me", Songs / 20),
            ("by title", $"/api/playlists/{playlist}/songs?limit=200&sort=title", Songs),
            ("the library, the playlist shown in it", "/api/library?limit=200", Songs),
            ("the library, typed", "/api/library?limit=200&q=song%2077", 111),
        };

        foreach (var (name, path, total) in calls)
        {
            // the first call warms the source's answers for the page's chords
            await ann.SendAsync(HttpMethod.Get, path);
            var watch = Stopwatch.StartNew();
            var response = await ann.SendAsync(HttpMethod.Get, path);
            watch.Stop();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var list = (await response.Content.ReadFromJsonAsync<SongList>(Browser.Json))!;
            output.WriteLine($"{name}: {watch.ElapsedMilliseconds} ms, {list.Total} songs, {list.Rows.Count} rows");
            Assert.Equal(total, list.Total);
            Assert.True(watch.ElapsedMilliseconds < 3000, $"{name} took {watch.ElapsedMilliseconds} ms");
            Assert.All(list.Rows, x => Assert.StartsWith("https://covers.test/", x.Cover));
        }

        // the playlists, with their covers
        var timed = Stopwatch.StartNew();
        var mine = await ann.GetAsync<List<PlaylistSummary>>("/api/playlists");
        timed.Stop();
        output.WriteLine($"my playlists, with covers: {timed.ElapsedMilliseconds} ms");
        Assert.Equal(["https://covers.test/1.jpg", "https://covers.test/2.jpg", "https://covers.test/3.jpg", "https://covers.test/4.jpg"], mine.Single(x => x.Id == playlist).Covers);
    }
}
