using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HarmonyDB.Common.ServiceAuth;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Model;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Clients.Audio;

/// <summary>
/// The music service's store of files (<c>/internal/blobs/{key}</c>), called with the shelf's internal key: files of
/// the shelf's own, streamed in, described and forgotten.
/// </summary>
public sealed class AudioBlobsClient(HttpClient http, IOptionsMonitor<AudioClientOptions> options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Stores the content under the key (<c>PUT /internal/blobs/{key}</c>), replacing what was there once all of it is
    /// received.
    /// </summary>
    public async Task<AudioBlob> PutAsync(string key, string contentType, Stream content, long? length, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        if (!settings.IsConfigured)
            throw new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);

        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri(new Uri(settings.BaseUrl!.TrimEnd('/') + "/"), $"internal/blobs/{key}"));
        request.Headers.Add(InternalKeyDefaults.HeaderName, settings.Key);
        request.Content = new StreamContent(content);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        if (length.HasValue)
            request.Content.Headers.ContentLength = length;

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new AudioException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable, exception);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<AudioBlob>(Json, cancellationToken))!;

            var problem = await ReadProblemAsync(response, cancellationToken);
            throw new AudioException((int)response.StatusCode, problem?.Code ?? ErrorCodes.InternalError, null);
        }
    }

    /// <summary>
    /// What is stored under the key (<c>HEAD /internal/blobs/{key}</c>): its size and SHA-256; null when nothing is.
    /// </summary>
    public async Task<AudioBlob?> GetInfoAsync(string key, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Head, key, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, cancellationToken);
        var digest = response.Headers.TryGetValues("Repr-Digest", out var values) ? values.Single() : null;
        const string prefix = "sha-256=:";
        if (digest == null || !digest.StartsWith(prefix, StringComparison.Ordinal) || !digest.EndsWith(':'))
            throw new AudioException(StatusCodes.Status502BadGateway, ErrorCodes.Unavailable, null);

        return new()
        {
            Key = key,
            Mime = response.Content.Headers.ContentType?.MediaType ?? string.Empty,
            Bytes = response.Content.Headers.ContentLength ?? 0,
            Sha256 = Convert.ToHexString(Convert.FromBase64String(digest[prefix.Length..^1])).ToLowerInvariant(),
        };
    }

    /// <summary>
    /// Forgets what is stored under the key (<c>DELETE /internal/blobs/{key}</c>); nothing stored there is not an error.
    /// </summary>
    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Delete, key, cancellationToken);
        if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
            await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string key, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        if (!settings.IsConfigured)
            throw new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);

        using var request = new HttpRequestMessage(method, new Uri(new Uri(settings.BaseUrl!.TrimEnd('/') + "/"), $"internal/blobs/{key}"));
        request.Headers.Add(InternalKeyDefaults.HeaderName, settings.Key);
        try
        {
            return await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new AudioException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable, exception);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var problem = response.Content.Headers.ContentLength > 0 ? await ReadProblemAsync(response, cancellationToken) : null;
        throw new AudioException((int)response.StatusCode, problem?.Code ?? ErrorCodes.InternalError, null);
    }

    private static async Task<AudioProblem?> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<AudioProblem>(Json, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record AudioProblem(string Code, string Message);
}

/// <summary>
/// A file stored in the music service.
/// </summary>
public sealed record AudioBlob
{
    public required string Key { get; init; }

    public required string Mime { get; init; }

    public long Bytes { get; init; }

    /// <summary>
    /// The SHA-256 of the content, hex.
    /// </summary>
    public required string Sha256 { get; init; }
}

public static class AudioBlobsClientExtensions
{
    /// <summary>
    /// The client of the music service's store of files (with the music service's configuration,
    /// <see cref="AudioClientExtensions.AddAudioClient"/>). Safe to call from every area that needs it.
    /// </summary>
    public static IServiceCollection AddAudioBlobsClient(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(x => x.ServiceType == typeof(AudioBlobsClient)))
            return services;

        services.AddAudioClient(configuration);

        // A file is streamed through whole: a recording or a separate instrument takes its time.
        services.AddHttpClient<AudioBlobsClient>(x => x.Timeout = TimeSpan.FromMinutes(10));
        return services;
    }
}
