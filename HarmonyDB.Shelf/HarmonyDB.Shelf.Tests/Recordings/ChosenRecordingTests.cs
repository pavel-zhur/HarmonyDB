using System.Net;
using System.Net.Http.Json;
using HarmonyDB.Shelf.Model.Me;
using HarmonyDB.Shelf.Model.Playlists;
using HarmonyDB.Shelf.Model.Recordings;
using HarmonyDB.Shelf.Tests.Auth;
using HarmonyDB.Shelf.Tests.Playlists;

namespace HarmonyDB.Shelf.Tests.Recordings;

/// <summary>
/// A recording a person chooses — any track, found for the song or not — is added to a song by whoever may add to the
/// playlist.
/// </summary>
public sealed class ChosenRecordingTests(LibraryFixture fixture) : IClassFixture<LibraryFixture>
{
    [DatabaseFact]
    public async Task AnyTrackIsAddedToASongByWhoeverMayAdd()
    {
        var (ann, _) = await fixture.PersonAsync("ann");
        var (bob, _) = await fixture.PersonAsync("bob");
        var playlist = (await Ok<Changed<PlaylistDetails>>(ann, HttpMethod.Post, "/api/playlists", new CreatePlaylistRequest { Title = "Mine", Access = PlaylistAccess.Public })).Value.Playlist.Id;
        var chords = $"test-chosen-{Guid.NewGuid():N}";
        var song = (await Ok<Changed<SongRef>>(ann, HttpMethod.Post, $"/api/playlists/{playlist}/songs", new AddSongRequest { Chords = chords, Title = "A song" })).Value.Id;
        var track = $"chosen-{Guid.NewGuid():N}";
        var path = $"/api/recordings/playlists/{playlist}/songs/{song}";

        // not a member: refused, nothing added
        await Browser.AssertError(await bob.SendAsync(HttpMethod.Post, path, new AddRecordingRequest { Service = MusicService.Youtube, TrackId = track }),
            HttpStatusCode.Forbidden, PlaylistErrorCodes.MembersOnly);

        // the owner adds a track nothing found for the song
        var recordings = await Ok<SongRecordings>(ann, HttpMethod.Get, path);
        Assert.DoesNotContain(recordings.Candidates, x => x.Id == track);
        await Ok<Changed<SongRef>>(ann, HttpMethod.Post, path, new AddRecordingRequest { Service = MusicService.Youtube, TrackId = track });

        var added = Assert.Single((await Ok<SongRecordings>(ann, HttpMethod.Get, path)).Recordings, x => x.Track.Id == track);
        Assert.Equal((MusicService.Youtube, $"Track {track}"), (added.Track.Service, added.Track.Title));
    }

    private static async Task<T> Ok<T>(Browser browser, HttpMethod method, string path, object? body = null)
    {
        var response = await browser.SendAsync(method, path, body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{method} {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Browser.Json))!;
    }
}
