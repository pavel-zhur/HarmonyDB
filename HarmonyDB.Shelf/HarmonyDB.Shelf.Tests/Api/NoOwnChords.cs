using System.Net;
using System.Text;

namespace HarmonyDB.Shelf.Tests.Api;

/// <summary>
/// The own chords' store of a person who has none: every list is empty, a removal removes nothing, and nothing else is
/// found. For the tests whose work asks the store along the way (deleting a profile removes the person's private own
/// chords).
/// </summary>
public sealed class NoOwnChords : HttpMessageHandler
{
    public const string Endpoint = "http://own-chords.test/collectives/api/";
    public const string Key = "an-internal-key-of-the-tests-0123456789";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(request.RequestUri!.AbsolutePath.Split('/').Last() switch
        {
            "OwnList" => Json("""{"items":[]}"""),
            "OwnRemovePrivate" => Json("""{"removed":0}"""),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}