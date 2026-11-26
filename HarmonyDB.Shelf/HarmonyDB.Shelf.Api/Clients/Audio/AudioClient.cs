using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HarmonyDB.Common.ServiceAuth;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Model;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Clients.Audio;

/// <summary>
/// The music service (HarmonyDB.Audio), called with the shelf's internal key. Every call to a music service says how
/// it acts (<see cref="AudioActing"/>). Add the calls an area needs here, typed, in the same way.
/// </summary>
public sealed partial class AudioClient(HttpClient http, IOptionsMonitor<AudioClientOptions> options)
{
    public const string ModeHeader = "X-Audio-Mode";
    public const string SubjectHeader = "X-Audio-Subject";
    public const string CredentialsHeader = "X-Audio-Credentials";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Who the call's account is on the service, asked now (<c>GET /internal/{service}/me</c>).
    /// </summary>
    public Task<AudioWho> GetMeAsync(AudioServiceName service, AudioActing acting, CancellationToken cancellationToken)
        => SendAsync<AudioWho>(HttpMethod.Get, $"internal/{Segment(service)}/me", acting, cancellationToken);

    /// <summary>
    /// Forgets every personal answer the service keeps under each of the music links (a link's id is its subject there,
    /// <c>DELETE /internal/subjects/{subject}</c>). Called before the links are removed; done again, it does no harm.
    /// </summary>
    /// <exception cref="ShelfException">503 <c>unavailable</c>: the music service did not do it; the links are to stay.</exception>
    public async Task PurgeLinksAsync(IEnumerable<Guid> musicLinkIds, CancellationToken cancellationToken)
    {
        foreach (var id in musicLinkIds)
        {
            try
            {
                await SendAsync<AudioPurged>(HttpMethod.Delete, $"internal/subjects/{id}", null, cancellationToken);
            }
            catch (AudioException)
            {
                throw new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);
            }
        }
    }

    /// <summary>
    /// How long a call may take, unless it says otherwise.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private Task<T> SendAsync<T>(HttpMethod method, string path, AudioActing? acting, CancellationToken cancellationToken)
        => SendAsync<T>(method, path, acting, DefaultTimeout, cancellationToken);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, AudioActing? acting, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);

        using var request = new HttpRequestMessage(method, new Uri(new Uri(settings.BaseUrl!.TrimEnd('/') + "/"), path));
        request.Headers.Add(InternalKeyDefaults.HeaderName, settings.Key);
        if (acting != null)
        {
            request.Headers.Add(ModeHeader, acting.Mode);
            if (acting.Subject != null)
                request.Headers.Add(SubjectHeader, acting.Subject);
            if (acting.Credentials != null)
                request.Headers.Add(CredentialsHeader, Convert.ToBase64String(Encoding.UTF8.GetBytes(acting.Credentials)));
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, limit.Token);
        }
        catch (HttpRequestException exception)
        {
            throw new AudioException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable, exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The call's timeout: the music service hangs.
            throw new AudioException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable, exception);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    return await response.Content.ReadFromJsonAsync<T>(Json, limit.Token)
                           ?? throw new JsonException("The answer is null.");
                }
                catch (JsonException exception)
                {
                    throw new AudioException(StatusCodes.Status502BadGateway, ErrorCodes.Unavailable, exception);
                }
                catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new AudioException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable, exception);
                }
            }

            var problem = await ReadProblemAsync(response, cancellationToken);
            throw new AudioException((int)response.StatusCode, problem?.Code ?? ErrorCodes.InternalError, null);
        }
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

    private static string Segment(AudioServiceName service) => service switch
    {
        AudioServiceName.Yandex => "yandex",
        AudioServiceName.Youtube => "youtube",
        _ => throw new ArgumentOutOfRangeException(nameof(service)),
    };

    private sealed record AudioProblem(string Code, string Message);
}

public enum AudioServiceName
{
    Yandex,
    Youtube,
}

/// <summary>
/// How a call to a music service acts: anonymously, by the service account, or as a person with the credentials given
/// in this call (never stored by the music service) and the subject its personal answers are kept under.
/// </summary>
public sealed record AudioActing
{
    private AudioActing(string mode, string? subject, string? credentials)
    {
        Mode = mode;
        Subject = subject;
        Credentials = credentials;
    }

    public string Mode { get; }

    public string? Subject { get; }

    public string? Credentials { get; }

    public static AudioActing Anonymous { get; } = new("anonymous", null, null);

    public static AudioActing Service { get; } = new("service", null, null);

    /// <param name="subject">The id of the person's music link.</param>
    /// <param name="credentials">What the person copied from the browser, as text.</param>
    public static AudioActing Person(string subject, string credentials) => new("person", subject, credentials);

    /// <summary>
    /// Never shows the credentials (in a log, a debugger).
    /// </summary>
    public override string ToString() => Subject == null ? Mode : $"{Mode} ({Subject})";
}

/// <summary>
/// A refusal of the music service: its status and its stable code (<c>credentials_rejected</c> …).
/// </summary>
public sealed class AudioException(int statusCode, string code, Exception? inner)
    : Exception($"The music service answered {statusCode} {code}.", inner)
{
    public int StatusCode { get; } = statusCode;

    public string Code { get; } = code;
}

public sealed record AudioWho
{
    public bool Authed { get; init; }

    public string? Account { get; init; }

    public bool? Premium { get; init; }
}

public sealed record AudioPurged
{
    public int Answers { get; init; }
}

/// <summary>
/// The configuration section <c>Clients:Audio</c>.
/// </summary>
public sealed class AudioClientOptions
{
    public const string SectionName = "Clients:Audio";

    /// <summary>
    /// The music service's address (<c>http://audio-api:8080</c>).
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// The shelf's internal key at the music service. Secret.
    /// </summary>
    public string? Key { get; set; }
}

internal sealed class AudioClientOptionsValidator : IValidateOptions<AudioClientOptions>
{
    public ValidateOptionsResult Validate(string? name, AudioClientOptions options)
    {
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
            return ValidateOptionsResult.Fail("Clients:Audio:BaseUrl is not an absolute address.");

        return string.IsNullOrWhiteSpace(options.Key) || options.Key.Length < InternalClientsOptions.MinimumKeyLength
            ? ValidateOptionsResult.Fail($"Clients:Audio:Key is shorter than {InternalClientsOptions.MinimumKeyLength} characters.")
            : ValidateOptionsResult.Success;
    }
}
