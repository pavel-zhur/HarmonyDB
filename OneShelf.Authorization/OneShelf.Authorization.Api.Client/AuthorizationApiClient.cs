using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using OneShelf.Authorization.Api.Model;

namespace OneShelf.Authorization.Api.Client;

public class AuthorizationApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthorizationApiClientOptions _options;

    public AuthorizationApiClient(IHttpClientFactory httpClientFactory, IOptions<AuthorizationApiClientOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<CheckIdentityResponse?> CheckIdentityRespectingCode(Identity identity)
    {
        if (identity.Hash == _options.ServiceCode)
        {
            return null;
        }

        return await CheckIdentity(identity);
    }

    public async Task<CheckIdentityResponse> CheckIdentity(Identity identity)
    {
        using var client = _httpClientFactory.CreateClient();

        using var response = await client.PostAsJsonAsync(_options.CheckIdentityEndpoint, identity);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CheckIdentityResponse>())!;
    }

    public async Task Ping()
    {
        using var client = _httpClientFactory.CreateClient();

        using var response = await client.GetAsync(_options.PingEndpoint);
        response.EnsureSuccessStatusCode();
    }
}