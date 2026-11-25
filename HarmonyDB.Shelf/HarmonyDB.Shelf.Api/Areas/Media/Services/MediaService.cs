using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Chords.Services;
using HarmonyDB.Shelf.Api.Clients.Audio;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Media;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Contents;

namespace HarmonyDB.Shelf.Api.Areas.Media.Services;

/// <summary>
/// Gives media addresses — of a music service's track, or of the material of an item of chords as its source gives
/// it to the asker — and streams them from the music service, always by the service account or anonymously, never
/// with a person's own account.
/// </summary>
public sealed class MediaService(MediaSigner signer, ChordsReader chords, SourceCallers callers, AudioMediaClient audio)
{
    private static readonly string[] PassedHeaders = ["Content-Range", "Accept-Ranges", "ETag", "Last-Modified"];

    public MediaAddress SignTrack(TrackMediaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Id))
            throw ShelfException.BadRequest();

        var expiresAt = signer.NewExpiry();
        var url = signer.Sign(new()
        {
            Kind = request.Kind == MediaKind.Video ? MediaTarget.Video : MediaTarget.Audio,
            Service = ServiceName(request.Service),
            Id = request.Id,
        }, expiresAt);
        return new() { Url = url, ExpiresAt = expiresAt };
    }

    /// <summary>
    /// The addresses of the item's recording, its separate instruments and its notes file, when the item is open to
    /// the asker; not found when it is not (nothing more is said of it).
    /// </summary>
    public Task<ChordsMedia> SignChordsAsync(ClaimsPrincipal user, string id, CancellationToken cancellationToken)
        => SignChordsAsync(callers.For(user), id, cancellationToken);

    /// <summary>
    /// The same, for the caller given.
    /// </summary>
    public async Task<ChordsMedia> SignChordsAsync(Caller caller, string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw ShelfException.BadRequest();

        var content = await chords.GetOpenContentAsync(caller, id, [ContentKind.Recording, ContentKind.Notes], cancellationToken)
                      ?? throw ShelfException.NotFound();
        if (content.Recording == null && content.Notes?.Midi == null)
            throw ShelfException.NotFound(MediaErrorCodes.NoMedia);

        // one expiry for all, so that the parts are played together
        var expiresAt = signer.NewExpiry();
        string Sign(AudioRef audioRef) => signer.Sign(Target(audioRef), expiresAt);

        return new()
        {
            RecordingUrl = content.Recording == null ? null : Sign(content.Recording.Audio),
            DurationSeconds = content.Recording?.Duration,
            Stems = content.Recording?.Stems.Select(x => new StemMedia { Stem = Enum.Parse<MediaStem>(x.Stem.ToString()), Url = Sign(x.Audio) }).ToList() ?? [],
            MidiUrl = content.Notes?.Midi == null ? null : signer.Sign(new() { Kind = MediaTarget.Blob, Id = content.Notes.Midi.Key }, expiresAt),
            ExpiresAt = expiresAt,
        };
    }

    /// <summary>
    /// Streams what the address opens, with the Range asked.
    /// </summary>
    public async Task StreamAsync(HttpContext context, string token)
    {
        var (target, expiresAt) = signer.Verify(token);
        var (path, acting) = target.Kind switch
        {
            MediaTarget.Audio or MediaTarget.Video when target.Service is "yandex" or "youtube"
                => ($"{target.Service}/tracks/{Uri.EscapeDataString(target.Id)}/{target.Kind}", AudioActing.Service),
            MediaTarget.Blob => ($"blobs/{string.Join('/', target.Id.Split('/').Select(Uri.EscapeDataString))}", (AudioActing?)null),
            _ => throw ShelfException.NotFound(MediaErrorCodes.Invalid),
        };

        var request = context.Request;
        var method = HttpMethods.IsHead(request.Method) ? HttpMethod.Head : HttpMethod.Get;
        HttpResponseMessage answer;
        try
        {
            answer = await audio.OpenAsync(method, path, acting, request.Headers.Range.ToString(), request.Headers.IfRange.ToString(), context.RequestAborted);
        }
        catch (AudioException)
        {
            throw new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);
        }

        using (answer)
        {
            var status = (int)answer.StatusCode;
            if (!answer.IsSuccessStatusCode && status != StatusCodes.Status416RangeNotSatisfiable)
            {
                throw status == StatusCodes.Status404NotFound
                    ? ShelfException.NotFound()
                    : new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);
            }

            var response = context.Response;
            response.StatusCode = status;
            foreach (var name in PassedHeaders)
            {
                if (answer.Headers.TryGetValues(name, out var values) || answer.Content.Headers.TryGetValues(name, out values))
                    response.Headers[name] = values.ToArray();
            }

            if (answer.Content.Headers.ContentType != null)
                response.ContentType = answer.Content.Headers.ContentType.ToString();
            if (answer.Content.Headers.ContentLength is { } length)
                response.ContentLength = length;

            var seconds = Math.Max(0, (long)(expiresAt - DateTimeOffset.UtcNow).TotalSeconds);
            response.Headers.CacheControl = $"private, max-age={seconds}";
            response.Headers.Expires = expiresAt.ToString("R");

            if (method == HttpMethod.Get)
            {
                await using var body = await answer.Content.ReadAsStreamAsync(context.RequestAborted);
                await body.CopyToAsync(response.Body, context.RequestAborted);
            }
        }
    }

    private static MediaTarget Target(AudioRef audioRef) => audioRef switch
    {
        { Track: { } track } => new()
        {
            Kind = MediaTarget.Audio,
            Service = track.Service == HarmonyDB.Source.Api.Model.V2.Contents.MusicService.Yandex ? "yandex" : "youtube",
            Id = track.Id,
        },
        { Blob: { } blob } => new() { Kind = MediaTarget.Blob, Id = blob.Key },
        _ => throw new ShelfException(StatusCodes.Status502BadGateway, ErrorCodes.Unavailable),
    };

    private static string ServiceName(Model.Me.MusicService service) => service switch
    {
        Model.Me.MusicService.Yandex => "yandex",
        Model.Me.MusicService.Youtube => "youtube",
        _ => throw ShelfException.BadRequest(),
    };
}
