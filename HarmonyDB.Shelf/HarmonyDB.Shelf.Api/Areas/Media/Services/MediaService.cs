using System.Security.Claims;
using HarmonyDB.Shelf.Api.Common;
using HarmonyDB.Shelf.Api.Areas.Chords.Services;
using HarmonyDB.Shelf.Api.Clients.Audio;
using HarmonyDB.Shelf.Api.Clients.Index;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Media;
using HarmonyDB.Source.Api.Client.V2;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Contents;

namespace HarmonyDB.Shelf.Api.Areas.Media.Services;

/// <summary>
/// Gives media addresses — of a music service's track, or of the material of an item of chords as its source gives
/// it to the asker — and streams them: a track from the music service, always by the service account or anonymously,
/// never with a person's own account; a file of an item from its source through the index, for the caller the address
/// was made for (the source decides at play time).
/// </summary>
public sealed class MediaService(MediaSigner signer, ChordsReader chords, SourceCallers callers, AudioMediaClient audio, IndexClient index)
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
    public Task<ChordsMedia> SignChordsAsync(ClaimsPrincipal user, string id, Uri? address, CancellationToken cancellationToken)
        => SignChordsAsync(callers.For(user), id, address, cancellationToken);

    /// <summary>
    /// The same, for the caller given, who came by the address given: the files' addresses are played for that caller,
    /// with that address.
    /// </summary>
    public async Task<ChordsMedia> SignChordsAsync(Caller caller, string id, Uri? address, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw ShelfException.BadRequest();

        var content = await chords.GetOpenContentAsync(caller, id, [ContentKind.Recording, ContentKind.Notes], address, cancellationToken)
                      ?? throw ShelfException.NotFound();

        // one expiry for all, so that the parts are played together
        var expiresAt = signer.NewExpiry();
        string? Sign(AudioRef audioRef) => Target(caller, id, address, audioRef) is { } target ? signer.Sign(target, expiresAt) : null;

        var recordingUrl = content.Recording == null ? null : Sign(content.Recording.Audio);
        var midiUrl = content.Notes?.Midi is { } midi ? Sign(new() { File = midi }) : null;
        if (recordingUrl == null && midiUrl == null)
            throw ShelfException.NotFound(MediaErrorCodes.NoMedia);

        return new()
        {
            RecordingUrl = recordingUrl,
            DurationSeconds = recordingUrl == null ? null : content.Recording!.Duration,
            Stems = recordingUrl == null
                ? []
                : content.Recording!.Stems
                    .Select(x => (stem: x.Stem, url: Sign(x.Audio)))
                    .Where(x => x.url != null)
                    .Select(x => new StemMedia { Stem = Enum.Parse<MediaStem>(x.stem.ToString()), Url = x.url! })
                    .ToList(),
            MidiUrl = midiUrl,
            ExpiresAt = expiresAt,
        };
    }

    /// <summary>
    /// Streams what the address opens, with the Range asked.
    /// </summary>
    public async Task StreamAsync(HttpContext context, string token)
    {
        var (target, expiresAt) = signer.Verify(token);
        var request = context.Request;
        var headersOnly = HttpMethods.IsHead(request.Method);
        var range = request.Headers.Range.ToString();
        var ifRange = request.Headers.IfRange.ToString();

        switch (target)
        {
            case { Kind: MediaTarget.Audio or MediaTarget.Video, Service: "yandex" or "youtube" }:
                await StreamTrackAsync(context, target, headersOnly, range, ifRange, expiresAt);
                break;
            case { Kind: MediaTarget.File, Name: not null, Caller: not null }:
                await StreamFileAsync(context, target, headersOnly, range, ifRange, expiresAt);
                break;
            default:
                throw ShelfException.NotFound(MediaErrorCodes.Invalid);
        }
    }

    private async Task StreamTrackAsync(HttpContext context, MediaTarget target, bool headersOnly, string range, string ifRange, DateTimeOffset expiresAt)
    {
        HttpResponseMessage answer;
        try
        {
            answer = await audio.OpenAsync(headersOnly ? HttpMethod.Head : HttpMethod.Get, $"{target.Service}/tracks/{Uri.EscapeDataString(target.Id)}/{target.Kind}",
                AudioActing.Service, range, ifRange, context.RequestAborted);
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

            Expire(response, expiresAt);

            if (!headersOnly)
            {
                await using var body = await answer.Content.ReadAsStreamAsync(context.RequestAborted);
                await body.CopyToAsync(response.Body, context.RequestAborted);
            }
        }
    }

    private async Task StreamFileAsync(HttpContext context, MediaTarget target, bool headersOnly, string range, string ifRange, DateTimeOffset expiresAt)
    {
        SourceFile? file;
        try
        {
            file = await index.Sources.GetFile(target.Caller!, target.Id, target.Name!, target.Address, headersOnly, range, ifRange, context.RequestAborted);
        }
        catch (SourceUnavailableException)
        {
            throw new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);
        }

        if (file == null)
            throw ShelfException.NotFound();

        using (file)
        {
            var response = context.Response;
            response.StatusCode = file.StatusCode;
            foreach (var (name, values) in file.Headers)
                response.Headers[name] = values;

            Expire(response, expiresAt);

            if (!headersOnly)
            {
                await using var body = await file.ReadBodyAsync(context.RequestAborted);
                await body.CopyToAsync(response.Body, context.RequestAborted);
            }
        }
    }

    /// <summary>
    /// What is streamed may be kept by the browser until the address expires, and by it alone.
    /// </summary>
    private static void Expire(HttpResponse response, DateTimeOffset expiresAt)
    {
        var seconds = Math.Max(0, (long)(expiresAt - DateTimeOffset.UtcNow).TotalSeconds);
        response.Headers.CacheControl = $"private, max-age={seconds}";
        response.Headers.Expires = expiresAt.ToString("R");
    }

    /// <summary>
    /// What an address of this sound opens: a track of a music service the shelf plays, or a file of the item for the
    /// caller; null for a track of a service the shelf does not know (it is passed over).
    /// </summary>
    private static MediaTarget? Target(Caller caller, string id, Uri? address, AudioRef audioRef) => audioRef switch
    {
        { Track: { Service: "yandex" or "youtube" } track } => new() { Kind = MediaTarget.Audio, Service = track.Service, Id = track.Id },
        { Track: not null } => null,
        { File: { } file } => new() { Kind = MediaTarget.File, Id = id, Name = file.Name, Caller = caller, Address = address },
        _ => throw new ShelfException(StatusCodes.Status502BadGateway, ErrorCodes.Unavailable),
    };

    private static string ServiceName(Model.Me.MusicService service)
        => Enum.IsDefined(service) ? MusicServiceNames.NameOf(service) : throw ShelfException.BadRequest();
}
