using System.Net;
using System.Net.Http.Json;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Me;
using HarmonyDB.Shelf.Model.Media;
using HarmonyDB.Shelf.Model.Playlists;
using HarmonyDB.Shelf.Model.Recordings;
using HarmonyDB.Shelf.Model.Share;
using HarmonyDB.Shelf.Tests.Auth;
using HarmonyDB.Shelf.Tests.Playlists;

namespace HarmonyDB.Shelf.Tests.Share;

/// <summary>
/// The sound of a recording of a song the caller sees: by the song's playlist, or by a link to the song or its
/// playlist; only a recording of that song is signed.
/// </summary>
public sealed class SongTrackMediaTests(LibraryFixture fixture) : IClassFixture<LibraryFixture>
{
    [DatabaseFact]
    public async Task OnlyARecordingOfASongTheCallerSeesIsSigned()
    {
        var (ann, annId) = await fixture.PersonAsync("ann");
        var tag = Guid.NewGuid().ToString("N")[..8];
        var open = await PlaylistAsync(ann, PlaylistAccess.Public);
        var hidden = await PlaylistAsync(ann, PlaylistAccess.Private);
        var linked = await PlaylistAsync(ann, PlaylistAccess.Link);
        var (song, other, privateSong, linkedSong) = (await SongAsync(ann, open, tag), await SongAsync(ann, open, tag), await SongAsync(ann, hidden, tag), await SongAsync(ann, linked, tag));
        var (track, otherTrack, privateTrack, linkedTrack) = ($"t1-{tag}", $"t2-{tag}", $"t3-{tag}", $"t4-{tag}");
        await using (var db = fixture.CreateContext())
        {
            foreach (var (songId, trackId) in new[] { (song, track), (other, otherTrack), (privateSong, privateTrack), (linkedSong, linkedTrack) })
            {
                var recording = new global::HarmonyDB.Shelf.Database.Model.Recording { Id = Guid.NewGuid(), Service = global::HarmonyDB.Shelf.Database.Model.MusicService.Youtube, ServiceId = trackId, Title = trackId, UpdatedAt = DateTimeOffset.UtcNow };
                db.Recordings.Add(recording);
                db.PlaylistSongItems.Add(new() { Id = Guid.NewGuid(), SongId = songId, RecordingId = recording.Id, Position = 1, AddedById = annId, AddedAt = DateTimeOffset.UtcNow });
            }

            await db.SaveChangesAsync();
        }

        var guest = fixture.Api.NewBrowser();
        await guest.SendAsync(HttpMethod.Get, "/api/app/configuration");
        TrackMediaRequest Track(string id) => new() { Service = MusicService.Youtube, Id = id, Kind = MediaKind.Audio };

        // a public playlist's song: its recording, not another song's
        var signed = await Ok<MediaAddress>(guest, HttpMethod.Post, $"/api/recordings/playlists/{open}/songs/{song}/media", Track(track));
        Assert.StartsWith("/api/media/", signed.Url);
        await Browser.AssertError(await guest.SendAsync(HttpMethod.Post, $"/api/recordings/playlists/{open}/songs/{song}/media", Track(otherTrack)),
            HttpStatusCode.NotFound, RecordingsErrorCodes.NotOfTheSong);

        // a private playlist's song: nothing without its link; by the song's link, its recording only
        await Browser.AssertError(await guest.SendAsync(HttpMethod.Post, $"/api/recordings/playlists/{hidden}/songs/{privateSong}/media", Track(privateTrack)),
            HttpStatusCode.NotFound, PlaylistErrorCodes.NoSuchSong);
        var songLink = await Ok<ShareLinkItem>(ann, HttpMethod.Post, "/api/share", new CreateShareRequest { Kind = ShareKind.Song, SongId = privateSong });
        await Ok<MediaAddress>(guest, HttpMethod.Post, $"/api/share/{songLink.Token}/tracks", new SharedTrackMediaRequest { Service = MusicService.Youtube, Id = privateTrack, Kind = MediaKind.Audio });
        await Browser.AssertError(await guest.SendAsync(HttpMethod.Post, $"/api/share/{songLink.Token}/tracks", new SharedTrackMediaRequest { Service = MusicService.Youtube, Id = track, Kind = MediaKind.Audio }),
            HttpStatusCode.NotFound, RecordingsErrorCodes.NotOfTheSong);
        await Browser.AssertError(await guest.SendAsync(HttpMethod.Post, $"/api/share/{songLink.Token}/tracks", new SharedTrackMediaRequest { SongId = song, Service = MusicService.Youtube, Id = track, Kind = MediaKind.Audio }),
            HttpStatusCode.NotFound, PlaylistErrorCodes.NoSuchSong);

        // a playlist's link: a song of that playlist, named
        var playlistLink = await Ok<ShareLinkItem>(ann, HttpMethod.Post, "/api/share", new CreateShareRequest { Kind = ShareKind.Playlist, PlaylistId = linked });
        await Ok<MediaAddress>(guest, HttpMethod.Post, $"/api/share/{playlistLink.Token}/tracks", new SharedTrackMediaRequest { SongId = linkedSong, Service = MusicService.Youtube, Id = linkedTrack, Kind = MediaKind.Audio });
        await Browser.AssertError(await guest.SendAsync(HttpMethod.Post, $"/api/share/{playlistLink.Token}/tracks", new SharedTrackMediaRequest { SongId = privateSong, Service = MusicService.Youtube, Id = privateTrack, Kind = MediaKind.Audio }),
            HttpStatusCode.NotFound, PlaylistErrorCodes.NoSuchSong);

        // a wrong token, of any shape, is as none
        foreach (var token in new[] { "no-such-token-000000000000", "x" })
            await Browser.AssertError(await guest.SendAsync(HttpMethod.Post, $"/api/share/{token}/tracks", new SharedTrackMediaRequest { Service = MusicService.Youtube, Id = track, Kind = MediaKind.Audio }),
                HttpStatusCode.NotFound, PlaylistErrorCodes.NoSuchLink);
    }

    private static async Task<Guid> PlaylistAsync(Browser owner, PlaylistAccess access)
        => (await Ok<Changed<PlaylistDetails>>(owner, HttpMethod.Post, "/api/playlists", new CreatePlaylistRequest { Title = "A playlist", Access = access })).Value.Playlist.Id;

    private static async Task<Guid> SongAsync(Browser owner, Guid playlist, string tag)
        => (await Ok<Changed<SongRef>>(owner, HttpMethod.Post, $"/api/playlists/{playlist}/songs", new AddSongRequest { Chords = $"test-{tag}-{Guid.NewGuid():N}", Title = "A song" })).Value.Id;

    private static async Task<T> Ok<T>(Browser browser, HttpMethod method, string path, object? body = null)
    {
        var response = await browser.SendAsync(method, path, body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{method} {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Browser.Json))!;
    }
}
