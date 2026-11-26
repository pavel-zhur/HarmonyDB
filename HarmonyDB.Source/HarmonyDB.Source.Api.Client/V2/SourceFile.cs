using System.Net.Http.Headers;

namespace HarmonyDB.Source.Api.Client.V2;

/// <summary>
/// A file as its source answered it (<see cref="SourceV2Client.GetFile"/>): the status, the headers of the answer that
/// pass on (<see cref="PassedHeaders"/>), and the body as a stream, read for as long as the reader reads. Disposed, it
/// lets the answer go.
/// </summary>
public sealed class SourceFile : IDisposable
{
    /// <summary>
    /// The headers of a file's answer that are passed on to whoever asked for it.
    /// </summary>
    public static readonly IReadOnlyList<string> PassedHeaders = ["Content-Type", "Content-Length", "Content-Range", "Accept-Ranges", "ETag", "Last-Modified"];

    private readonly HttpResponseMessage _response;

    internal SourceFile(HttpResponseMessage response)
    {
        _response = response;
        StatusCode = (int)response.StatusCode;
        Headers = PassedHeaders
            .Select(name => (name, values: Values(response, name)))
            .Where(x => x.values != null)
            .ToDictionary(x => x.name, x => x.values!, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 200, 206 or 416.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// The headers of <see cref="PassedHeaders"/> that the answer has, each with its values.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> Headers { get; }

    /// <summary>
    /// The body (empty for a <c>HEAD</c>), streamed from the source as it is read.
    /// </summary>
    public Task<Stream> ReadBodyAsync(CancellationToken cancellationToken) => _response.Content.ReadAsStreamAsync(cancellationToken);

    public void Dispose() => _response.Dispose();

    private static string[]? Values(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values))
            return values.ToArray();

        // the content's length is a header of its own kind: known only when the answer gave it
        return name == "Content-Length" && response.Content.Headers.ContentLength is { } length ? [length.ToString()] : null;
    }

    internal static void AddRange(HttpRequestHeaders headers, string? range, string? ifRange)
    {
        if (!string.IsNullOrEmpty(range)) headers.TryAddWithoutValidation("Range", range);
        if (!string.IsNullOrEmpty(ifRange)) headers.TryAddWithoutValidation("If-Range", ifRange);
    }
}
