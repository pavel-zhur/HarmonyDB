using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarmonyDB.Common.ServiceAuth;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Model;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Clients.Illustrations;

/// <summary>
/// The illustrations service, called as an internal client with the shelf's key: its presets, the illustrations kept for
/// a key (the chords' id), drawing a preset for a key, and the images' copies. Not answering: 503 <c>unavailable</c>.
/// </summary>
public sealed partial class IllustrationsClient(HttpClient http, IOptions<IllustrationsClientOptions> options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IllustrationsClientOptions _options = options.Value;

    public Task<IllustrationPresets> GetPresetsAsync(CancellationToken cancellationToken)
        => PostAsync<IllustrationPresets>("ShelfPresets", new { }, _options.Timeout, cancellationToken);

    public async Task<IReadOnlyList<StoredIllustration>> ReadAsync(string key, CancellationToken cancellationToken)
        => (await PostAsync<ReadAnswer>("ShelfRead", new { key }, _options.Timeout, cancellationToken)).Images;

    /// <summary>
    /// Draws the preset for the key, by the lyrics when no prompts are kept for it yet. Takes from half a minute to a
    /// few minutes; nothing is drawn again when the preset is drawn for the key already.
    /// </summary>
    public Task<GenerateAnswer> GenerateAsync(string key, string lyrics, string preset, string billingInfo, CancellationToken cancellationToken)
        => PostAsync<GenerateAnswer>("ShelfGenerate", new { key, lyrics, preset, additionalBillingInfo = billingInfo }, _options.GenerationTimeout, cancellationToken);

    /// <summary>
    /// A copy of an image, a square JPEG; null when there is no such image or size.
    /// </summary>
    public async Task<byte[]?> GetImageAsync(Guid id, int size, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, new Uri(_options.Endpoint!, $"images/{id}/{size}"), null, _options.Timeout, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode) throw Unavailable();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private async Task<T> PostAsync<T>(string operation, object request, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, new Uri(_options.Endpoint!, operation), JsonContent.Create(request, options: Json), timeout, cancellationToken);
        if (!response.IsSuccessStatusCode) throw Unavailable();
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken) ?? throw Unavailable();
        }
        catch (JsonException)
        {
            throw Unavailable();
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, Uri uri, HttpContent? content, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            using var message = new HttpRequestMessage(method, uri);
            message.Headers.Add(InternalKeyDefaults.HeaderName, _options.Key);
            message.Content = content;
            return await http.SendAsync(message, limit.Token);
        }
        catch (Exception exception) when (exception is HttpRequestException || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw Unavailable();
        }
    }

    private static ShelfException Unavailable() => new(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);

    private sealed record ReadAnswer(IReadOnlyList<StoredIllustration> Images);
}

/// <param name="Presets">The presets, the plain one first.</param>
/// <param name="Sizes">The sides of the square copies every image has.</param>
public sealed record IllustrationPresets(IReadOnlyList<StoredPreset> Presets, IReadOnlyList<int> Sizes);

/// <param name="Mood">What it draws in, a stable code.</param>
public sealed record StoredPreset(string Key, string Mood);

/// <param name="Preset">The preset it was drawn by; null for one drawn otherwise.</param>
/// <param name="Mood">That preset's mood; null with it, or for a preset no longer offered.</param>
public sealed record StoredIllustration(Guid Id, string? Preset, string? Mood, DateTimeOffset CreatedOn);

public enum GenerateOutcome
{
    Made = 0,
    AlreadyMade = 1,
    NoLyrics = 2,
    NoPrompts = 3,
}

public sealed record GenerateAnswer(GenerateOutcome Outcome, int Made);

/// <summary>
/// The configuration section <c>Clients:Illustrations</c>.
/// </summary>
public sealed class IllustrationsClientOptions
{
    public const string SectionName = "Clients:Illustrations";

    /// <summary>
    /// The base address of the service's operations, ending with a slash (<c>http://host/illustrations/api/</c>).
    /// </summary>
    public Uri? Endpoint { get; set; }

    /// <summary>
    /// The shelf's internal key there. Secret.
    /// </summary>
    public string? Key { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// How long drawing a preset may take.
    /// </summary>
    public TimeSpan GenerationTimeout { get; set; } = TimeSpan.FromMinutes(5);
}

internal sealed class IllustrationsClientOptionsValidator : IValidateOptions<IllustrationsClientOptions>
{
    public ValidateOptionsResult Validate(string? name, IllustrationsClientOptions options)
    {
        var section = IllustrationsClientOptions.SectionName;
        if (options.Timeout <= TimeSpan.Zero || options.GenerationTimeout <= TimeSpan.Zero)
            return ValidateOptionsResult.Fail($"{section}: the timeouts are not positive.");
        if (options.Endpoint is not { IsAbsoluteUri: true } || !options.Endpoint.AbsolutePath.EndsWith('/'))
            return ValidateOptionsResult.Fail($"{section}:Endpoint is not an absolute address ending with a slash.");

        return string.IsNullOrEmpty(options.Key) || options.Key.Length < InternalClientsOptions.MinimumKeyLength
            ? ValidateOptionsResult.Fail($"{section}:Key is shorter than {InternalClientsOptions.MinimumKeyLength} characters.")
            : ValidateOptionsResult.Success;
    }
}

public static class IllustrationsClientExtensions
{
    /// <summary>
    /// Adds <see cref="IllustrationsClient"/>; safe to call from every area that needs it.
    /// </summary>
    public static IServiceCollection AddIllustrationsClient(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(x => x.ServiceType == typeof(IllustrationsClient)))
            return services;

        services.AddOptions<IllustrationsClientOptions>().Bind(configuration.GetSection(IllustrationsClientOptions.SectionName)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<IllustrationsClientOptions>, IllustrationsClientOptionsValidator>();
        services.AddHttpClient<IllustrationsClient>(x => x.Timeout = Timeout.InfiniteTimeSpan);
        return services;
    }
}
