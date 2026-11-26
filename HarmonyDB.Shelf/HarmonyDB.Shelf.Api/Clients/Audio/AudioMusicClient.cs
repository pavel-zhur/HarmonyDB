using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HarmonyDB.Common.ServiceAuth;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Model;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Clients.Audio;

/// <summary>
/// The navigation of the music services (<c>/internal/{service}/...</c>), called with the shelf's internal key and the
/// way to act given in each call. The answers are read into <see cref="AudioShapes"/>: only the fields used here.
/// </summary>
public sealed class AudioMusicClient(HttpClient http, IOptionsMonitor<AudioClientOptions> options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// <c>GET /internal/{path}</c>, the answer's <c>data</c>.
    /// </summary>
    /// <param name="path">After <c>/internal/</c>, with its query, its parts escaped by the caller.</param>
    public Task<T> GetAsync<T>(string path, AudioActing acting, CancellationToken cancellationToken)
        => SendAsync<T>(HttpMethod.Get, path, null, acting, cancellationToken);

    /// <summary>
    /// <c>POST /internal/{path}</c> with the body as JSON, the answer's <c>data</c>.
    /// </summary>
    public Task<T> PostAsync<T>(string path, object body, AudioActing acting, CancellationToken cancellationToken)
        => SendAsync<T>(HttpMethod.Post, path, JsonContent.Create(body, options: Json), acting, cancellationToken);

    public static string Segment(AudioServiceName service) => service switch
    {
        AudioServiceName.Yandex => "yandex",
        AudioServiceName.Youtube => "youtube",
        _ => throw new ArgumentOutOfRangeException(nameof(service)),
    };

    private async Task<T> SendAsync<T>(HttpMethod method, string path, HttpContent? content, AudioActing acting, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;

        using var request = new HttpRequestMessage(method, new Uri(new Uri(settings.BaseUrl!.TrimEnd('/') + "/"), "internal/" + path));
        request.Headers.Add(InternalKeyDefaults.HeaderName, settings.Key);
        request.Headers.Add(AudioClient.ModeHeader, acting.Mode);
        if (acting.Subject != null)
            request.Headers.Add(AudioClient.SubjectHeader, acting.Subject);
        if (acting.Credentials != null)
            request.Headers.Add(AudioClient.CredentialsHeader, Convert.ToBase64String(Encoding.UTF8.GetBytes(acting.Credentials)));
        request.Content = content;

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new AudioException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable, exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AudioException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable, exception);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var answer = await response.Content.ReadFromJsonAsync<Envelope<T>>(Json, cancellationToken);
                    return answer is { Data: not null } ? answer.Data : throw new JsonException("The answer has no data.");
                }
                catch (JsonException exception)
                {
                    throw new AudioException(StatusCodes.Status502BadGateway, ErrorCodes.Unavailable, exception);
                }
            }

            string? code = null;
            try
            {
                code = (await response.Content.ReadFromJsonAsync<Problem>(Json, cancellationToken))?.Code;
            }
            catch (JsonException)
            {
            }

            throw new AudioException((int)response.StatusCode, code ?? ErrorCodes.InternalError, null);
        }
    }

    private sealed record Envelope<T>(T? Data);

    private sealed record Problem(string? Code);
}

public static class AudioMusicClientExtensions
{
    /// <summary>
    /// The clients of the music service's navigation and of its sound and video
    /// (<see cref="AudioMusicClient"/>, <see cref="AudioMediaClient"/>); safe to call from every area that needs them.
    /// </summary>
    public static IServiceCollection AddAudioMusicClients(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(x => x.ServiceType == typeof(AudioMusicClient)))
            return services;

        services.AddAudioClient(configuration);

        // a search or a page of a service may take its time when it is not kept yet
        services.AddHttpClient<AudioMusicClient>(x => x.Timeout = TimeSpan.FromSeconds(60));

        // sound and video are streamed for as long as they play
        services.AddHttpClient<AudioMediaClient>(x => x.Timeout = Timeout.InfiniteTimeSpan);
        return services;
    }
}
