using System.Net;
using System.Net.Http.Json;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Tests.Api;
using HarmonyDB.Shelf.Tests.Auth;
using HarmonyDB.Shelf.Tests.Chords;
using HarmonyDB.Source.Api.Model.V2.Api;

namespace HarmonyDB.Shelf.Tests.Search;

/// <summary>
/// The one search over a real database: found chords are never grouped into songs, except that where they are already
/// in a song of a playlist the person sees, that song stands in their place.
/// </summary>
public sealed class SearchGroupingTests(ShelfDatabaseFixture database) : IClassFixture<ShelfDatabaseFixture>
{
    [DatabaseFact]
    public async Task FoundChordsStandAloneUnlessASongTheyAreInIsSeen()
    {
        var (me, _) = await SeedAsync();
        var index = new FakeIndex
        {
            Answer = (_, _) => (HttpStatusCode.OK, new SearchResponse
            {
                Headers = new[] { "s1", "s2", "s3", "s4", "s5" }.Select(x => FakeIndex.Header(x, "Label")).ToList(),
                Failures = [new() { Source = "a broken one", Reason = SourceFailureReason.Timeout }],
            }),
        };
        using var api = index.Api(new ShelfApiFactory(), new Dictionary<string, string?> { ["ConnectionStrings:Shelf"] = database.ConnectionString });
        using var client = api.CreateClient();

        async Task<Model.Search.SearchResponse> SearchAsync(Guid? account, string query, string parts)
        {
            var response = await client.SendAsync(FakeIndex.As(HttpMethod.Get, $"/api/search?q={query}&parts={parts}", account));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<Model.Search.SearchResponse>(Browser.Json))!;
        }

        // mine: my own playlist, and a public one I am not in; not a private one of another, not one I keep out of my
        // library
        var mine = await SearchAsync(me, "zzz", "songs,chords");
        Assert.Equal(["Mine song", "Public song"], mine.Songs!.Select(x => x.Title));
        Assert.Equal(["s1"], mine.Songs![0].FoundChords);
        Assert.Equal(["s3", "s4", "s5"], mine.Chords!.Select(x => x.Id));
        Assert.Equal("a broken one", mine.ChordsFailures!.Single().Source);

        // a guest sees the public playlists
        var guest = await SearchAsync(null, "zzz", "chords");
        Assert.Equal(["Public song", "Kept out song"], guest.Songs!.Select(x => x.Title));
        Assert.Equal(["s1", "s3", "s4"], guest.Chords!.Select(x => x.Id));

        // a song found by its title; the sources are not asked for the quick part
        var requests = index.Requests.Count;
        var byTitle = await SearchAsync(me, "public%20SONG", "songs");
        Assert.Equal(["Public song"], byTitle.Songs!.Select(x => x.Title));
        Assert.Empty(byTitle.Songs![0].FoundChords);
        Assert.Null(byTitle.Chords);
        Assert.Equal(requests, index.Requests.Count);

        // too short to go out to the sources
        Assert.Null((await SearchAsync(me, "z", "chords")).Chords);
        Assert.Equal(requests, index.Requests.Count);
    }

    private async Task<(Guid Me, Guid Other)> SeedAsync()
    {
        await using var db = database.CreateContext();
        var now = DateTimeOffset.UtcNow;
        var me = new Account { Id = Guid.NewGuid(), DisplayName = "me", CreatedAt = now };
        var other = new Account { Id = Guid.NewGuid(), DisplayName = "other", CreatedAt = now };
        db.Accounts.AddRange(me, other);

        var chords = Enumerable.Range(1, 5).ToDictionary(x => $"s{x}", x => new ChordsItem { Id = Guid.NewGuid(), SourceId = $"s{x}", CreatedAt = now });
        db.Chords.AddRange(chords.Values);

        void Playlist(Account owner, PlaylistAccess access, string song, string sourceId, Account? alsoMember = null, bool showInLibrary = true)
        {
            var playlist = new Playlist { Id = Guid.NewGuid(), OwnerId = owner.Id, Title = $"Playlist of {song}", Access = access, CreatedAt = now, UpdatedAt = now };
            playlist.Members.Add(new() { Id = Guid.NewGuid(), AccountId = owner.Id, Role = PlaylistRole.Owner, JoinedAt = now });
            if (alsoMember != null)
                playlist.Members.Add(new() { Id = Guid.NewGuid(), AccountId = alsoMember.Id, Role = PlaylistRole.Member, ShowInLibrary = showInLibrary, JoinedAt = now });

            var playlistSong = new PlaylistSong { Id = Guid.NewGuid(), Title = song, Artists = ["An artist"], Position = 1, AddedAt = now };
            playlistSong.Items.Add(new() { Id = Guid.NewGuid(), ChordsId = chords[sourceId].Id, Position = 1, AddedAt = now });
            playlist.Songs.Add(playlistSong);
            db.Playlists.Add(playlist);
        }

        Playlist(me, PlaylistAccess.Private, "Mine song", "s1");
        Playlist(other, PlaylistAccess.Public, "Public song", "s2");
        Playlist(other, PlaylistAccess.Private, "Hidden song", "s3");
        Playlist(other, PlaylistAccess.Public, "Kept out song", "s5", me, showInLibrary: false);
        await db.SaveChangesAsync();
        return (me.Id, other.Id);
    }
}
