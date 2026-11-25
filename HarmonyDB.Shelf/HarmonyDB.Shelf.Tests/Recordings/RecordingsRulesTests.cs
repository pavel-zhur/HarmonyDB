using System.Net;
using System.Net.Http.Json;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Recordings;
using HarmonyDB.Shelf.Tests.Api;
using HarmonyDB.Shelf.Tests.Auth;
using HarmonyDB.Shelf.Tests.Chords;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Tests.Recordings;

/// <summary>
/// A song's recordings in a playlist over a real database: reading only reads, the candidates are only those found
/// for chords open to the reader, and who sees and who may change goes by the playlist's rules.
/// </summary>
public sealed class RecordingsRulesTests(ShelfDatabaseFixture database) : IClassFixture<ShelfDatabaseFixture>
{
    [DatabaseFact]
    public async Task ReadingOnlyReadsAndShowsOnlyWhatIsOpen()
    {
        var (owner, playlistId, songId) = await SeedAsync();
        var index = new FakeIndex
        {
            Answer = (_, body) => (HttpStatusCode.OK, new GetHeadersResponse
            {
                Headers = body.GetProperty("ids").EnumerateArray().Select(x => x.GetString() == "c-open"
                    ? new HeaderAnswer { Id = "c-open", State = ItemState.Open, Header = FakeIndex.Header("c-open", "Label") }
                    : new HeaderAnswer { Id = x.GetString()!, State = ItemState.NotOpen }).ToList(),
            }),
        };
        using var api = index.Api(new ShelfApiFactory(), new Dictionary<string, string?> { ["ConnectionStrings:Shelf"] = database.ConnectionString });
        using var client = api.CreateClient();
        var path = $"/api/recordings/playlists/{playlistId}/songs/{songId}";

        var mine = await client.SendAsync(FakeIndex.As(HttpMethod.Get, path, owner));
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        var recordings = (await mine.Content.ReadFromJsonAsync<SongRecordings>(Browser.Json))!;
        Assert.Empty(recordings.Recordings);
        Assert.Equal(["open-track"], recordings.Candidates.Select(x => x.Id));
        Assert.True(recordings.CanAdd);

        await using (var db = database.CreateContext())
            Assert.Equal(2, await db.PlaylistSongItems.CountAsync(x => x.SongId == songId));

        // a private playlist is not seen by others
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(FakeIndex.As(HttpMethod.Get, path, Guid.NewGuid()))).StatusCode);

        // a public one is seen by all, changed by its members only
        await using (var db = database.CreateContext())
            await db.Playlists.Where(x => x.Id == playlistId).ExecuteUpdateAsync(x => x.SetProperty(p => p.Access, PlaylistAccess.Public));
        var guest = (await (await client.GetAsync(path)).Content.ReadFromJsonAsync<SongRecordings>(Browser.Json))!;
        Assert.False(guest.CanAdd);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.SendAsync(FakeIndex.As(HttpMethod.Put, $"{path}/choice", null, body: new RecordingChoiceRequest()))).StatusCode);
        var outsider = await client.SendAsync(FakeIndex.As(HttpMethod.Post, path, Guid.NewGuid(), body: new { service = "yandex", trackId = "1" }));
        await Browser.AssertError(outsider, HttpStatusCode.Forbidden, "members_only");
    }

    private async Task<(Guid Owner, Guid Playlist, Guid Song)> SeedAsync()
    {
        await using var db = database.CreateContext();
        var now = DateTimeOffset.UtcNow;
        var owner = new Account { Id = Guid.NewGuid(), DisplayName = "owner", CreatedAt = now };
        var open = new ChordsItem { Id = Guid.NewGuid(), SourceId = "c-open", CreatedAt = now, RecordingsSearchedAt = now };
        var closed = new ChordsItem { Id = Guid.NewGuid(), SourceId = "c-closed", CreatedAt = now, RecordingsSearchedAt = now };
        var openTrack = new Recording { Id = Guid.NewGuid(), Service = MusicService.Yandex, ServiceId = "open-track", Title = "Open", UpdatedAt = now };
        var closedTrack = new Recording { Id = Guid.NewGuid(), Service = MusicService.Yandex, ServiceId = "closed-track", Title = "Closed", UpdatedAt = now };
        db.AddRange(owner, open, closed, openTrack, closedTrack);
        db.ChordsRecordings.AddRange(
            new ChordsRecording { ChordsId = open.Id, RecordingId = openTrack.Id, Rank = 0, FoundAt = now },
            new ChordsRecording { ChordsId = closed.Id, RecordingId = closedTrack.Id, Rank = 0, FoundAt = now });

        var playlist = new Playlist { Id = Guid.NewGuid(), OwnerId = owner.Id, Title = "Mine", Access = PlaylistAccess.Private, CreatedAt = now, UpdatedAt = now };
        playlist.Members.Add(new() { Id = Guid.NewGuid(), AccountId = owner.Id, Role = PlaylistRole.Owner, JoinedAt = now });
        var song = new PlaylistSong { Id = Guid.NewGuid(), Title = "A song", Position = 1, AddedById = owner.Id, AddedAt = now };
        song.Items.Add(new() { Id = Guid.NewGuid(), ChordsId = open.Id, Position = 0, AddedById = owner.Id, AddedAt = now });
        song.Items.Add(new() { Id = Guid.NewGuid(), ChordsId = closed.Id, Position = 1, AddedById = owner.Id, AddedAt = now });
        playlist.Songs.Add(song);
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync();
        return (owner.Id, playlist.Id, song.Id);
    }
}
