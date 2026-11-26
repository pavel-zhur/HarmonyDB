using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarmonyDB.Common.ServiceAuth;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Contents;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Clients.OwnChords;

/// <summary>
/// The store of own chords (the old services' collectives) and its editor's preview, called as an internal client with
/// the shelf's keys. Own chords are the person's when one of the names the person is known by is their author
/// (<see cref="OwnChordsAuthor"/>).
/// Not answering: 503 <c>unavailable</c>.
/// </summary>
public sealed class OwnChordsClient(HttpClient http, IOptions<OwnChordsClientOptions> options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly OwnChordsClientOptions _options = options.Value;

    public Task<IReadOnlyList<StoredOwnChords>> ListAsync(OwnChordsAuthor author, CancellationToken cancellationToken)
        => PostAsync<OwnListResponse, IReadOnlyList<StoredOwnChords>>("OwnList", new { author }, x => x.Items, cancellationToken)!;

    /// <returns>With every version; null when they are not the author's, not there, or deleted.</returns>
    public Task<StoredOwnChords?> GetAsync(OwnChordsAuthor author, Guid collectiveId, CancellationToken cancellationToken)
        => PostAsync<StoredOwnChords, StoredOwnChords>("OwnGet", new { author, collectiveId }, x => x, cancellationToken);

    /// <exception cref="ShelfException">400 when the store refuses the text (an empty title, artists or text).</exception>
    public async Task<StoredOwnChords> InsertAsync(OwnChordsAuthor author, StoredText collective, CancellationToken cancellationToken)
        => (await PostAsync<StoredOwnChords, StoredOwnChords>("OwnInsert", new { author, collective }, x => x, cancellationToken))!;

    /// <returns>Null when they are not the author's, not there, or deleted.</returns>
    /// <exception cref="ShelfException">409 when they are not changed here (<see cref="StoredOwnChords.Editable"/>).</exception>
    public Task<StoredOwnChords?> UpdateAsync(OwnChordsAuthor author, Guid collectiveId, StoredText collective, CancellationToken cancellationToken)
        => PostAsync<StoredOwnChords, StoredOwnChords>("OwnUpdate", new { author, collectiveId, collective }, x => x, cancellationToken);

    /// <returns>False when they are not the author's, not there, or deleted already.</returns>
    /// <exception cref="ShelfException">409 when they are not deleted here (<see cref="StoredOwnChords.Editable"/>).</exception>
    public async Task<bool> DeleteAsync(OwnChordsAuthor author, Guid collectiveId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(new Uri(_options.Endpoint!, "OwnDelete"), _options.Key!, JsonContent.Create(new { author, collectiveId }, options: Json), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        if (response.StatusCode == HttpStatusCode.Conflict) throw NotEditable();
        if (!response.IsSuccessStatusCode) throw Unavailable();
        return true;
    }

    /// <summary>
    /// Removes for good the person's private own chords written here (those deleted while private too), every version.
    /// Done again, it removes what is left.
    /// </summary>
    public async Task RemovePrivateAsync(OwnChordsAuthor author, CancellationToken cancellationToken)
        => await PostAsync<RemovePrivateResponse, RemovePrivateResponse>("OwnRemovePrivate", new { author }, x => x, cancellationToken);

    /// <summary>
    /// The text in the editor's markup shown as a sheet, as the source will give it once saved.
    /// </summary>
    public async Task<SheetContent> PreviewAsync(string contents, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(new Uri(_options.PreviewEndpoint!, "V2Preview"), _options.PreviewKey!,
            JsonContent.Create(new { contents }, options: SourceV2Json.Options), cancellationToken);
        return await ReadAsync<SheetContent>(response, SourceV2Json.Options, cancellationToken);
    }

    private async Task<TResult?> PostAsync<TAnswer, TResult>(string operation, object request, Func<TAnswer, TResult> result, CancellationToken cancellationToken)
        where TResult : class
    {
        using var response = await SendAsync(new Uri(_options.Endpoint!, operation), _options.Key!, JsonContent.Create(request, options: Json), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (response.StatusCode == HttpStatusCode.BadRequest) throw ShelfException.BadRequest(OwnChordsClientErrors.Refused);
        if (response.StatusCode == HttpStatusCode.Conflict) throw NotEditable();

        return result(await ReadAsync<TAnswer>(response, Json, cancellationToken));
    }

    private async Task<HttpResponseMessage> SendAsync(Uri uri, string key, HttpContent content, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, uri);
            message.Headers.Add(InternalKeyDefaults.HeaderName, key);
            message.Content = content;
            return await http.SendAsync(message, timeout.Token);
        }
        catch (Exception exception) when (exception is HttpRequestException || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw Unavailable();
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, JsonSerializerOptions json, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode) throw Unavailable();
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(json, cancellationToken) ?? throw Unavailable();
        }
        catch (JsonException)
        {
            throw Unavailable();
        }
    }

    private static ShelfException Unavailable() => new(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);

    private static ShelfException NotEditable() => ShelfException.Conflict(HarmonyDB.Shelf.Model.OwnChords.OwnChordsErrorCodes.NotEditable);

    private sealed record OwnListResponse(IReadOnlyList<StoredOwnChords> Items);

    private sealed record RemovePrivateResponse(int Removed);
}

public static class OwnChordsClientErrors
{
    /// <summary>
    /// 400: the store refused the text: a title, artists and the text itself are needed, without spaces around them.
    /// </summary>
    public const string Refused = "own_chords_refused";
}

/// <summary>
/// Who acts on own chords: the names the person is known by (<see cref="HarmonyDB.Shelf.Database.AccountSubjects"/>), the
/// first being the one what they write is authored by.
/// </summary>
public sealed record OwnChordsAuthor(IReadOnlyList<string> Subjects);

/// <summary>
/// The store's visibility of own chords: private — the author only; club — anyone holding their id; public — also found
/// by everyone's search.
/// </summary>
public enum StoredVisibility
{
    Private = 0,
    Club = 1,
    Public = 2,
}

/// <summary>
/// One version of own chords, as the store keeps it.
/// </summary>
public sealed record StoredText(
    IReadOnlyList<string> Authors,
    string Title,
    string Contents,
    StoredVisibility Visibility,
    string? Description,
    string? Tonality,
    int? Bpm,
    string? VersionComment);

public sealed record StoredVersion(int VersionNumber, DateTimeOffset CreatedOn, StoredText Collective);

/// <param name="Editable">Whether they are changed and deleted here: those written in the old shelf are not.</param>
public sealed record StoredOwnChords(
    Guid CollectiveId,
    StoredText Collective,
    int VersionNumber,
    DateTimeOffset CreatedOn,
    DateTimeOffset UpdatedOn,
    Uri Uri,
    string Author,
    bool Editable,
    IReadOnlyList<StoredVersion>? History);

/// <summary>
/// The configuration section <c>Clients:OwnChords</c>: the store (<c>…/collectives/api/</c>) and the editor's preview
/// (<c>…/sourcesself/api/</c>), each with the shelf's key there.
/// </summary>
public sealed class OwnChordsClientOptions
{
    public const string SectionName = "Clients:OwnChords";

    public Uri? Endpoint { get; set; }

    public string? Key { get; set; }

    public Uri? PreviewEndpoint { get; set; }

    public string? PreviewKey { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);
}

internal sealed class OwnChordsClientOptionsValidator : IValidateOptions<OwnChordsClientOptions>
{
    public ValidateOptionsResult Validate(string? name, OwnChordsClientOptions options)
    {
        var failures = new List<string>();
        var section = OwnChordsClientOptions.SectionName;
        Check(options.Endpoint, options.Key, nameof(OwnChordsClientOptions.Endpoint), nameof(OwnChordsClientOptions.Key));
        Check(options.PreviewEndpoint, options.PreviewKey, nameof(OwnChordsClientOptions.PreviewEndpoint), nameof(OwnChordsClientOptions.PreviewKey));
        if (options.Timeout <= TimeSpan.Zero)
            failures.Add($"{section}:{nameof(OwnChordsClientOptions.Timeout)} is not positive.");

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;

        void Check(Uri? endpoint, string? key, string endpointName, string keyName)
        {
            if (endpoint is not { IsAbsoluteUri: true } || !endpoint.AbsolutePath.EndsWith('/'))
                failures.Add($"{section}:{endpointName} is not an absolute address ending with a slash.");
            if (string.IsNullOrEmpty(key) || key.Length < InternalClientsOptions.MinimumKeyLength)
                failures.Add($"{section}:{keyName} is shorter than {InternalClientsOptions.MinimumKeyLength} characters.");
        }
    }
}

public static class OwnChordsClientExtensions
{
    /// <summary>
    /// Adds <see cref="OwnChordsClient"/>; safe to call from every area that needs it.
    /// </summary>
    public static IServiceCollection AddOwnChordsClient(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(x => x.ServiceType == typeof(OwnChordsClient)))
            return services;

        services.AddOptions<OwnChordsClientOptions>().Bind(configuration.GetSection(OwnChordsClientOptions.SectionName)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<OwnChordsClientOptions>, OwnChordsClientOptionsValidator>();
        services.AddHttpClient<OwnChordsClient>(x => x.Timeout = Timeout.InfiniteTimeSpan);
        return services;
    }
}
