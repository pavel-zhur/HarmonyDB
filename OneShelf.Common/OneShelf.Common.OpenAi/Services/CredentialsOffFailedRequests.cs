namespace OneShelf.Common.OpenAi.Services;

/// <summary>
/// Takes the credentials off a request once its answer is in: whoever writes out a failed request with its headers
/// (the OpenAI client does, to the console) does not write the key.
/// </summary>
internal sealed class CredentialsOffFailedRequests(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        request.Headers.Authorization = null;
        request.Headers.Remove("api-key");
        return response;
    }
}
