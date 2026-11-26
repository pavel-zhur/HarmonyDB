using System.Net;
using System.Text;

namespace HarmonyDB.Shelf.Tests.Api;

/// <summary>
/// The music service's purge of a subject (<c>DELETE /internal/subjects/{subject}</c>) as the tests set it: the subjects
/// purged are in <see cref="Subjects"/>; while <see cref="Down"/>, it does not answer. Anything else is not found.
/// </summary>
public sealed class FakePurges : HttpMessageHandler
{
    private const string Path = "/internal/subjects/";

    public List<string> Subjects { get; } = [];

    public bool Down { get; set; }

    /// <summary>
    /// The answer to a purge, or null for a request that is not one.
    /// </summary>
    public HttpResponseMessage? Answer(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (request.Method != HttpMethod.Delete || !path.StartsWith(Path, StringComparison.Ordinal))
            return null;
        if (Down)
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        lock (Subjects)
            Subjects.Add(Uri.UnescapeDataString(path[Path.Length..]));
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"answers":0}""", Encoding.UTF8, "application/json") };
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(Answer(request) ?? new HttpResponseMessage(HttpStatusCode.NotFound));
}
