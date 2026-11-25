using System.Net.Http.Json;
using System.Text.Json;
using HarmonyDB.Common.ServiceAuth;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Source.Api.Client.V2;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Clients.Index;

/// <summary>
/// The index, called as an internal client with the shelf's key: the source contract, version 2, which it relays to
/// the sources (<see cref="Sources"/>), and its analysis tools for an item by its id (the same ids).
/// Not configured: what needs it answers 503 <c>unavailable</c>.
/// </summary>
public sealed class IndexClient
{
    private const int NotInTheIndex = 460;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IndexClientOptions _options;
    private readonly SourceV2Client? _sources;

    public IndexClient(HttpClient http, IOptions<IndexClientOptions> options, ILogger<SourceV2Client> logger)
    {
        _http = http;
        _options = options.Value;
        if (_options.IsConfigured)
        {
            _sources = new(http, new()
            {
                Name = "index",
                Endpoint = _options.Endpoint,
                Key = _options.Key!,
                Timeout = _options.Timeout,
            }, logger);
        }
    }

    /// <summary>
    /// The source contract, version 2, through the index's relay.
    /// </summary>
    public SourceV2Client Sources => _sources ?? throw Unavailable();

    /// <summary>
    /// The index's analysis of an item: its detected keys, its loops. Null when the index does not know the item.
    /// </summary>
    public async Task<IndexSongResponse?> GetSongAsync(string id, CancellationToken cancellationToken)
        => await PostAsync<IndexSongResponse>("VExternal1TonalitiesSong", new { externalId = id }, cancellationToken);

    /// <summary>
    /// The items whose chords have the progression (chord names, space-separated), best first.
    /// </summary>
    public async Task<IndexSongsByChordsResponse> SongsByChordsAsync(string progression, int page, int perPage, CancellationToken cancellationToken)
        => await PostAsync<IndexSongsByChordsResponse>("VExternal1SongsByChords", new
           {
               query = progression,
               pageNumber = page,
               itemsPerPage = perPage,
           }, cancellationToken)
           ?? throw Unavailable();

    private async Task<T?> PostAsync<T>(string operation, object request, CancellationToken cancellationToken)
        where T : class
    {
        if (!_options.IsConfigured) throw Unavailable();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(_options.Endpoint!, operation));
            message.Headers.Add(InternalKeyDefaults.HeaderName, _options.Key);
            message.Content = JsonContent.Create(request, options: Json);
            using var response = await _http.SendAsync(message, timeout.Token);
            if ((int)response.StatusCode == NotInTheIndex) return null;
            if (!response.IsSuccessStatusCode) throw Unavailable();
            return await response.Content.ReadFromJsonAsync<T>(Json, timeout.Token) ?? throw Unavailable();
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
                                          || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw Unavailable();
        }
    }

    private static ShelfException Unavailable() => new(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);
}

/// <summary>
/// The configuration section <c>Clients:Index</c>.
/// </summary>
public sealed class IndexClientOptions
{
    public const string SectionName = "Clients:Index";

    /// <summary>
    /// The base address of the index's operations, ending with a slash (<c>http://host/index/api/</c>).
    /// </summary>
    public Uri? Endpoint { get; set; }

    /// <summary>
    /// The shelf's internal key at the index. Secret.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// How long an operation may take.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);

    public bool IsConfigured => Endpoint != null;
}

internal sealed class IndexClientOptionsValidator : IValidateOptions<IndexClientOptions>
{
    public ValidateOptionsResult Validate(string? name, IndexClientOptions options)
    {
        if (options.Endpoint == null && string.IsNullOrWhiteSpace(options.Key))
            return ValidateOptionsResult.Success;

        if (options.Endpoint is not { IsAbsoluteUri: true } || !options.Endpoint.AbsolutePath.EndsWith('/'))
            return ValidateOptionsResult.Fail("Clients:Index:Endpoint is not an absolute address ending with a slash.");

        if (options.Timeout <= TimeSpan.Zero)
            return ValidateOptionsResult.Fail("Clients:Index:Timeout is not positive.");

        return string.IsNullOrWhiteSpace(options.Key) || options.Key.Length < InternalClientsOptions.MinimumKeyLength
            ? ValidateOptionsResult.Fail($"Clients:Index:Key is shorter than {InternalClientsOptions.MinimumKeyLength} characters.")
            : ValidateOptionsResult.Success;
    }
}

public static class IndexClientExtensions
{
    /// <summary>
    /// Adds <see cref="IndexClient"/>; safe to call from every area that needs it.
    /// </summary>
    public static IServiceCollection AddIndexClient(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(x => x.ServiceType == typeof(IndexClient)))
            return services;

        services.AddOptions<IndexClientOptions>().Bind(configuration.GetSection(IndexClientOptions.SectionName)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<IndexClientOptions>, IndexClientOptionsValidator>();

        // the client keeps its own timeout per operation
        services.AddHttpClient<IndexClient>(x => x.Timeout = Timeout.InfiniteTimeSpan);
        return services;
    }
}

/// <summary>
/// The index's analysis of one item (its answer, the fields read here).
/// </summary>
public sealed record IndexSongResponse
{
    public required IndexSong Song { get; init; }

    public required List<IndexLoop> Loops { get; init; }

    public required List<IndexLink> Links { get; init; }
}

/// <param name="Probabilities">The probabilities of the 24 keys, by index: tonic × 2 + (minor ? 1 : 0).</param>
/// <param name="KnownTonalityIndex">The key the source states, by the same index, when it does.</param>
public sealed record IndexSong(string ExternalId, int TotalLoops, float[] Probabilities, float TonicScore, float ScaleScore, int? KnownTonalityIndex);

/// <param name="Normalized">The loop, in the index's own serialized form of a normalized progression.</param>
public sealed record IndexLoop(
    string Normalized,
    int Length,
    float TotalOccurrences,
    float TotalSuccessions,
    float AverageCoverage,
    int TotalSongs,
    float[] Probabilities,
    float ScaleScore,
    float TonicScore);

/// <summary>
/// A loop as it occurs in one item.
/// </summary>
public sealed record IndexLink(string Normalized, string ExternalId, byte NormalizationRoot, float Occurrences, float Successions, float Coverage);

public sealed record IndexSongsByChordsResponse
{
    public required List<IndexFoundSong> Songs { get; init; }

    public required int Total { get; init; }

    public required int TotalPages { get; init; }

    public required int CurrentPageNumber { get; init; }
}

/// <summary>
/// An item found by its chords. Only the id of its header is read: what is shown of it is asked of the sources for
/// the person.
/// </summary>
public sealed record IndexFoundSong
{
    public required IndexFoundHeader Header { get; init; }

    public required float Coverage { get; init; }

    public int? PredictedTonalityIndex { get; init; }
}

public sealed record IndexFoundHeader
{
    public required string ExternalId { get; init; }
}
