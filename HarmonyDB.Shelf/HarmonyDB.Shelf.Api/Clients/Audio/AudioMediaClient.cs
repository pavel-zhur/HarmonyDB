using HarmonyDB.Common.ServiceAuth;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Model;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Clients.Audio;

/// <summary>
/// Sound and video of the music service, streamed: a track's sound or video
/// (<c>/internal/{service}/tracks/{id}/audio|video</c>), never with a person's credentials, with Range.
/// </summary>
public sealed class AudioMediaClient(HttpClient http, IOptionsMonitor<AudioClientOptions> options)
{
    /// <param name="path">After <c>/internal/</c>.</param>
    /// <param name="acting">How to act for the track: anonymously or by the service account.</param>
    /// <param name="range">The <c>Range</c> header asked, if any.</param>
    /// <param name="ifRange">The <c>If-Range</c> header asked, if any.</param>
    /// <returns>The answer with its headers read and its body still to be streamed; the caller disposes of it.</returns>
    public async Task<HttpResponseMessage> OpenAsync(HttpMethod method, string path, AudioActing acting, string? range, string? ifRange, CancellationToken cancellationToken)
    {
        if (acting.Credentials != null)
            throw new InvalidOperationException("Sound and video are never fetched with a person's credentials.");

        var settings = options.CurrentValue;
        if (!settings.IsConfigured)
            throw new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);

        var request = new HttpRequestMessage(method, new Uri(new Uri(settings.BaseUrl!.TrimEnd('/') + "/"), "internal/" + path));
        request.Headers.Add(InternalKeyDefaults.HeaderName, settings.Key);
        request.Headers.Add(AudioClient.ModeHeader, acting.Mode);
        if (!string.IsNullOrEmpty(range))
            request.Headers.TryAddWithoutValidation("Range", range);
        if (!string.IsNullOrEmpty(ifRange))
            request.Headers.TryAddWithoutValidation("If-Range", ifRange);

        try
        {
            return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new AudioException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable, exception);
        }
        finally
        {
            request.Dispose();
        }
    }
}
