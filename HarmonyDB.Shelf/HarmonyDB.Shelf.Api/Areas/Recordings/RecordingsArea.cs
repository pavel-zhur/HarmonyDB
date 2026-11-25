using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Chords;
using HarmonyDB.Shelf.Api.Areas.Media;
using HarmonyDB.Shelf.Api.Areas.Music;
using HarmonyDB.Shelf.Api.Areas.Recordings.Services;
using HarmonyDB.Shelf.Api.Common.Jobs;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model.Media;
using HarmonyDB.Shelf.Model.Recordings;

namespace HarmonyDB.Shelf.Api.Areas.Recordings;

/// <summary>
/// Recordings for chords: found automatically on both music services for an item of chords; a song's recordings in a
/// playlist — seen, chosen, added from the candidates, per playlist; a song put into a playlist as a recording alone.
/// Removing a recording, and the playlist's "search first on", are the playlists area's. Reading is open to whoever sees
/// the song.
/// </summary>
public sealed class RecordingsArea : IShelfArea
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddChordsReading(configuration);
        services.AddMusicCatalog(configuration);
        services.AddScoped<RecordingFinder>();
        services.AddScoped<ChordsRecordingsReader>();
        services.AddScoped<SongRecordingsService>();
        services.AddJobHandler<SongRecordingsJob>(configuration, SongRecordingsJob.Kind);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/recordings").WithTags("Recordings");

        group.MapGet("/chords/{id}", (ClaimsPrincipal user, string id, ChordsRecordingsReader reader, CancellationToken cancellationToken)
            => reader.GetAsync(user, id, cancellationToken)).WithName("GetChordsRecordings");

        var songs = group.MapGroup("/playlists/{playlistId:guid}/songs");

        songs.MapPost("", (ClaimsPrincipal user, Guid playlistId, AddRecordingRequest request, SongRecordingsService service, CancellationToken cancellationToken)
                => service.AddSongAsync(user, playlistId, request, cancellationToken))
            .RequireSignIn()
            .WithName("AddRecordingSong");

        songs.MapGet("/{songId:guid}", (ClaimsPrincipal user, Guid playlistId, Guid songId, string? share, SongRecordingsService service, CancellationToken cancellationToken)
            => service.ListAsync(user, playlistId, songId, share, cancellationToken)).WithName("GetSongRecordings");

        // the sound or video of a recording of the song, for whoever sees it (a public playlist's song for a guest; with
        // ?share= a song by its link or a playlist's): only a track the song's recordings read gives is signed
        songs.MapPost("/{songId:guid}/media", (ClaimsPrincipal user, Guid playlistId, Guid songId, string? share, TrackMediaRequest request, SongRecordingsService service, CancellationToken cancellationToken)
                => service.SignTrackAsync(user, playlistId, songId, share, request, cancellationToken))
            .AllowGuests()
            .RequireRateLimiting(MediaArea.AddressesRateLimit)
            .WithName("GetSongRecordingMedia");

        // "find another recording": looks for recordings of the song's chords (live searches), then answers as the GET
        songs.MapPost("/{songId:guid}/find", (ClaimsPrincipal user, Guid playlistId, Guid songId, string? share, SongRecordingsService service, CancellationToken cancellationToken)
                => service.FindAsync(user, playlistId, songId, share, cancellationToken))
            .RequireSignIn()
            .WithName("FindSongRecordings");

        songs.MapPost("/{songId:guid}", (ClaimsPrincipal user, Guid playlistId, Guid songId, AddRecordingRequest request, SongRecordingsService service, CancellationToken cancellationToken)
                => service.AddAsync(user, playlistId, songId, request, cancellationToken))
            .RequireSignIn()
            .WithName("AddSongRecording");

        songs.MapPut("/{songId:guid}/choice", (ClaimsPrincipal user, Guid playlistId, Guid songId, string? share, RecordingChoiceRequest request, SongRecordingsService service, CancellationToken cancellationToken)
                => service.ChooseAsync(user, playlistId, songId, share, request.ItemId, cancellationToken))
            .RequireSignIn()
            .WithName("ChooseSongRecording");
    }
}
