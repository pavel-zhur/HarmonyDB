using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarmonyDB.Common.ServiceAuth;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using Microsoft.Extensions.Logging;

namespace HarmonyDB.Source.Api.Client.V2;

/// <summary>
/// A client of one server of the source contract, version 2: a source, or the index that relays to the sources.
/// It calls as an internal client, by its key, and reads and writes <see cref="SourceV2Json.Options"/> only.
/// When the server cannot be reached, answers with an error or out of the contract, or does not answer in time, the
/// failure is logged and becomes the contract's "unavailable" where the answer has a place for it (a search failure,
/// <see cref="ItemState.Unavailable"/>, <see cref="ResolveState.Unavailable"/>); otherwise
/// (<see cref="GetFile"/>, <see cref="GetIcon"/>, <see cref="Ping"/>) <see cref="SourceUnavailableException"/> is thrown.
/// A cancellation by the caller is not a failure of the server: it is thrown as it is.
/// </summary>
public class SourceV2Client
{
    private readonly HttpClient _httpClient;
    private readonly SourceV2ClientOptions _options;
    private readonly ILogger<SourceV2Client> _logger;

    public SourceV2Client(HttpClient httpClient, SourceV2ClientOptions options, ILogger<SourceV2Client> logger)
    {
        Validate(options);

        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// The name the server is configured with.
    /// </summary>
    public string Name => _options.Name;

    /// <exception cref="ArgumentException">The options do not describe a server to call.</exception>
    public static void Validate(SourceV2ClientOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Name)) throw new ArgumentException("The server has no name.", nameof(options));
        if (options.Endpoint is not { IsAbsoluteUri: true } || !options.Endpoint.AbsolutePath.EndsWith('/'))
            throw new ArgumentException($"The endpoint of {options.Name} is not an absolute address ending with a slash.", nameof(options));
        if (string.IsNullOrEmpty(options.Key)) throw new ArgumentException($"The key for {options.Name} is empty.", nameof(options));
        if (options.Timeout <= TimeSpan.Zero) throw new ArgumentException($"The timeout for {options.Name} is not positive.", nameof(options));
    }

    public async Task<SearchResponse> Search(Caller caller, string query, CancellationToken cancellationToken = default)
    {
        try
        {
            return await PostJson<SearchRequest, SearchResponse>(SourceV2Urls.V2Search, new()
            {
                Caller = caller,
                Query = query,
            }, response =>
            {
                Check(response.Headers != null && response.Failures != null, "a search answer has no list");
                foreach (var header in response.Headers!) CheckHeader(header);
                Check(response.Failures!.All(x => x is { Source: not null }), "a search failure is empty");
            }, cancellationToken);
        }
        catch (CallFailedException e)
        {
            return new()
            {
                Headers = [],
                Failures =
                [
                    new()
                    {
                        Source = Name,
                        Reason = e.IsTimeout ? SourceFailureReason.Timeout : SourceFailureReason.Unavailable,
                    },
                ],
            };
        }
    }

    public async Task<GetHeadersResponse> GetHeaders(Caller caller, IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
    {
        try
        {
            return await PostJson<GetHeadersRequest, GetHeadersResponse>(SourceV2Urls.V2GetHeaders, new()
            {
                Caller = caller,
                Ids = ids,
            }, response =>
            {
                Check(response.Headers != null && response.Headers.Count == ids.Count, "the number of the headers is not the number of the ids");
                for (var i = 0; i < ids.Count; i++)
                {
                    var answer = response.Headers![i];
                    Check(answer != null && answer.Id == ids[i], "the headers are not the answers for the ids asked, in their order");
                    Check((answer!.State == ItemState.Open) == (answer.Header != null), "a header is given exactly for an open item");
                    if (answer.Header != null)
                    {
                        CheckHeader(answer.Header);
                        Check(answer.Header.Id == answer.Id, "a header has another id");
                    }
                }
            }, cancellationToken);
        }
        catch (CallFailedException)
        {
            return new()
            {
                Headers = ids.Select(id => new HeaderAnswer
                {
                    Id = id,
                    State = ItemState.Unavailable,
                }).ToList(),
            };
        }
    }

    /// <param name="address">The address the person came by, when they came by one.</param>
    public async Task<GetContentResponse> GetContent(Caller caller, string id, IReadOnlyList<ContentKind> kinds, Uri? address, CancellationToken cancellationToken = default)
    {
        try
        {
            return await PostJson<GetContentRequest, GetContentResponse>(SourceV2Urls.V2GetContent, new()
            {
                Caller = caller,
                Id = id,
                Kinds = kinds,
                Address = address,
            }, response =>
            {
                Check(response.Id == id, "the content is of another id");
                CheckContent(response);
            }, cancellationToken);
        }
        catch (CallFailedException)
        {
            return new()
            {
                Id = id,
                State = ItemState.Unavailable,
            };
        }
    }

    /// <summary>
    /// The sources' labels this caller may see; a server that did not answer is in the failures.
    /// </summary>
    public async Task<GetSourcesResponse> GetSources(Caller caller, CancellationToken cancellationToken = default)
    {
        try
        {
            return await PostJson<GetSourcesRequest, GetSourcesResponse>(SourceV2Urls.V2GetSources, new()
            {
                Caller = caller,
            }, response =>
            {
                Check(response.Sources != null && response.Failures != null, "a sources' answer has no list");
                Check(response.Sources!.All(x => x is { Key: not null, Title: not null }), "a source's label is empty");
                Check(response.Failures!.All(x => x is { Source: not null }), "a failure is empty");
            }, cancellationToken);
        }
        catch (CallFailedException e)
        {
            return new()
            {
                Sources = [],
                Failures =
                [
                    new()
                    {
                        Source = Name,
                        Reason = e.IsTimeout ? SourceFailureReason.Timeout : SourceFailureReason.Unavailable,
                    },
                ],
            };
        }
    }

    /// <param name="ids">Each once, at most <see cref="GetContentsRequest.MaxIds"/>.</param>
    public async Task<GetContentsResponse> GetContents(Caller caller, IReadOnlyList<string> ids, IReadOnlyList<ContentKind> kinds, CancellationToken cancellationToken = default)
    {
        try
        {
            return await PostJson<GetContentsRequest, GetContentsResponse>(SourceV2Urls.V2GetContents, new()
            {
                Caller = caller,
                Ids = ids,
                Kinds = kinds,
            }, response =>
            {
                Check(response.Items != null && response.Items.Count == ids.Count, "the number of the contents is not the number of the ids");
                for (var i = 0; i < ids.Count; i++)
                {
                    var item = response.Items![i];
                    Check(item != null && item.Id == ids[i], "the contents are not the answers for the ids asked, in their order");
                    CheckContent(item!);
                }
            }, cancellationToken);
        }
        catch (CallFailedException)
        {
            return new()
            {
                Items = ids.Select(id => new GetContentResponse
                {
                    Id = id,
                    State = ItemState.Unavailable,
                }).ToList(),
            };
        }
    }

    public async Task<ResolveResponse> Resolve(Caller caller, IReadOnlyList<Uri> uris, CancellationToken cancellationToken = default)
    {
        try
        {
            return await PostJson<ResolveRequest, ResolveResponse>(SourceV2Urls.V2Resolve, new()
            {
                Caller = caller,
                Uris = uris,
            }, response =>
            {
                Check(response.Items != null && response.Items.Count == uris.Count, "the number of the answers is not the number of the addresses");
                for (var i = 0; i < uris.Count; i++)
                {
                    var item = response.Items![i];
                    Check(item != null && item.Uri == uris[i], "the answers are not for the addresses asked, in their order");
                    Check((item!.State == ResolveState.Resolved) == !string.IsNullOrEmpty(item.Id), "an id is given exactly for a resolved address");
                }
            }, cancellationToken);
        }
        catch (CallFailedException)
        {
            return new()
            {
                Items = uris.Select(uri => new ResolvedUri
                {
                    Uri = uri,
                    State = ResolveState.Unavailable,
                }).ToList(),
            };
        }
    }

    /// <summary>
    /// A file of an item, for the caller: its headers once the server has sent them (the timeout covers only the wait
    /// for them), and its body as a stream that is never cut by the timeout.
    /// </summary>
    /// <param name="headersOnly">Ask with <c>HEAD</c>: the headers alone.</param>
    /// <param name="range">The <c>Range</c> asked, as HTTP gives it; null for the whole file.</param>
    /// <param name="ifRange">The <c>If-Range</c> asked, as HTTP gives it.</param>
    /// <returns>The file's answer, to be disposed; null when there is no such file open to the caller.</returns>
    /// <exception cref="SourceUnavailableException">The server did not answer.</exception>
    /// <param name="address">The address the person came by, when they came by one.</param>
    public async Task<SourceFile?> GetFile(Caller caller, string id, string name, Uri? address, bool headersOnly, string? range, string? ifRange, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);

        HttpResponseMessage? response = null;
        try
        {
            using var request = new HttpRequestMessage(headersOnly ? HttpMethod.Head : HttpMethod.Get, new Uri(_options.Endpoint!, SourceV2Urls.V2GetFile));
            request.Headers.Add(InternalKeyDefaults.HeaderName, _options.Key);
            request.Headers.Add(GetFileRequest.HeaderName, GetFileRequest.Encode(new()
            {
                Caller = caller,
                Id = id,
                Name = name,
                Address = address,
            }));
            SourceFile.AddRange(request.Headers, range, ifRange);

            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            switch (response.StatusCode)
            {
                case HttpStatusCode.NotFound:
                    response.Dispose();
                    return null;
                case HttpStatusCode.OK or HttpStatusCode.PartialContent or HttpStatusCode.RequestedRangeNotSatisfiable:
                    var file = new SourceFile(response);
                    Check(file.Headers.ContainsKey("Content-Type"), "the file has no content type");
                    return file;
                default:
                    throw new InvalidAnswerException($"the answer is {(int)response.StatusCode} {response.StatusCode}");
            }
        }
        catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested)
        {
            response?.Dispose();
            _logger.LogWarning(e, "The source {source} did not answer {operation} in {timeout}.", Name, SourceV2Urls.V2GetFile, _options.Timeout);
            throw new SourceUnavailableException(Name, true, e);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidAnswerException)
        {
            response?.Dispose();
            _logger.LogError(e, "The source {source} failed {operation}.", Name, SourceV2Urls.V2GetFile);
            throw new SourceUnavailableException(Name, false, e);
        }
    }

    /// <returns>The picture, or null when the server has none of that name.</returns>
    /// <exception cref="SourceUnavailableException">The server did not answer.</exception>
    public async Task<SourceIcon?> GetIcon(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            return await Call(SourceV2Urls.V2GetIcon, Serialize(new GetIconRequest { Name = name }), async (response, token) =>
            {
                if (response.StatusCode == HttpStatusCode.NotFound) return null;

                EnsureSuccess(response);
                var contentType = response.Content.Headers.ContentType?.ToString();
                Check(!string.IsNullOrEmpty(contentType), "the picture has no content type");
                return new SourceIcon(await response.Content.ReadAsByteArrayAsync(token), contentType!);
            }, cancellationToken);
        }
        catch (CallFailedException e)
        {
            throw new SourceUnavailableException(Name, e.IsTimeout, e.InnerException);
        }
    }

    /// <exception cref="SourceUnavailableException">The server did not answer.</exception>
    public async Task Ping(CancellationToken cancellationToken = default)
    {
        try
        {
            await Call<object?>(SourceV2Urls.V2Ping, null, (response, _) =>
            {
                EnsureSuccess(response);
                return Task.FromResult<object?>(null);
            }, cancellationToken);
        }
        catch (CallFailedException e)
        {
            throw new SourceUnavailableException(Name, e.IsTimeout, e.InnerException);
        }
    }

    /// <summary>
    /// Posts the request and reads the answer; <paramref name="validate"/> throws (through <see cref="Check"/>) when the
    /// answer is out of the contract, which counts as a failure of the server.
    /// </summary>
    private async Task<TResponse> PostJson<TRequest, TResponse>(string url, TRequest request, Action<TResponse> validate, CancellationToken cancellationToken)
        where TResponse : class
        => await Call(url, Serialize(request), async (response, token) =>
        {
            EnsureSuccess(response);
            var answer = await response.Content.ReadFromJsonAsync<TResponse>(SourceV2Json.Options, token)
                         ?? throw new InvalidAnswerException("the answer is empty");
            validate(answer);
            return answer;
        }, cancellationToken);

    private static HttpContent Serialize<TRequest>(TRequest request) => JsonContent.Create(request, options: SourceV2Json.Options);

    /// <summary>
    /// Makes the call within the timeout. Every failure of the server is logged and thrown as
    /// <see cref="CallFailedException"/>.
    /// </summary>
    private async Task<T> Call<T>(string url, HttpContent? content, Func<HttpResponseMessage, CancellationToken, Task<T>> read, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_options.Endpoint!, url));
            request.Content = content;
            request.Headers.Add(InternalKeyDefaults.HeaderName, _options.Key);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            return await read(response, timeout.Token);
        }
        catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(e, "The source {source} did not answer {operation} in {timeout}.", Name, url, _options.Timeout);
            throw new CallFailedException(true, e);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or JsonException or InvalidAnswerException)
        {
            _logger.LogError(e, "The source {source} failed {operation}.", Name, url);
            throw new CallFailedException(false, e);
        }
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new InvalidAnswerException($"the answer is {(int)response.StatusCode} {response.StatusCode}");
    }

    /// <summary>
    /// A header has everything the contract requires of it: the wire format lets a required list or object be null.
    /// </summary>
    private static void CheckHeader(Header? header)
    {
        Check(header != null, "a header is empty");
        Check(!string.IsNullOrEmpty(header!.Id), "a header has no id");
        Check(header.Source is { Key: not null, Title: not null }, "a header has no source");
        Check(header.Artists != null && header.Artists.All(x => x != null), "a header has no artists list");
        Check(header.Kinds != null, "a header has no kinds");
        Check(header.Attributes != null, "a header has no attributes");
    }

    /// <summary>
    /// An item's content answer: the header and the content exactly for an open item, the header of that item.
    /// </summary>
    private static void CheckContent(GetContentResponse response)
    {
        Check((response.State == ItemState.Open) == (response.Header != null) && (response.Header == null) == (response.Content == null),
            "the header and the content are given exactly for an open item");
        if (response.Header != null)
        {
            CheckHeader(response.Header);
            Check(response.Header.Id == response.Id, "the header has another id");
        }
    }

    private static void Check(bool condition, string what)
    {
        if (!condition) throw new InvalidAnswerException(what);
    }

    private sealed class CallFailedException(bool isTimeout, Exception innerException) : Exception(null, innerException)
    {
        public bool IsTimeout { get; } = isTimeout;
    }

    private sealed class InvalidAnswerException(string what) : Exception($"The answer is out of the contract: {what}.");
}
