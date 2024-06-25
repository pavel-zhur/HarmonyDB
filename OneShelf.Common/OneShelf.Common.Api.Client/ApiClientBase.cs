using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using System.Text.Json;
using OneShelf.Authorization.Api.Model;
using System.Net.Http.Json;

namespace OneShelf.Common.Api.Client;

public class ApiClientBase<TClient>
    where TClient : ApiClientBase<TClient>
{
    private readonly ApiClientOptions<TClient> _options;
    private readonly IHttpClientFactory _httpClientFactory;

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public ApiClientBase(IOptions<ApiClientOptions<TClient>> options, IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
    }

    protected ApiClientBase(ApiClientOptions<TClient> options, IHttpClientFactory httpClientFactory)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
    }

    protected async Task<TResponse> PostWithCode<TRequest, TResponse>(string url, TRequest request, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient();
        using var response = await client.PostAsJsonAsync(new Uri(_options.Endpoint, WithCode(url)), request, _jsonSerializerOptions, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedException(await response.Content.ReadAsStringAsync(cancellationToken));
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(_jsonSerializerOptions, cancellationToken) ?? throw new("Empty response.");
    }

    protected async Task<byte[]> PostWithCode<TRequest>(string url, TRequest request)
    {
        using var client = _httpClientFactory.CreateClient();
        using var response = await client.PostAsJsonAsync(new Uri(_options.Endpoint, WithCode(url)), request, _jsonSerializerOptions);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedException(await response.Content.ReadAsStringAsync());
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }

    protected async Task<TResponse> Post<TRequest, TResponse>(string url, TRequest request, Action? unauthorized = null, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient();
        using var response = await client.PostAsJsonAsync(new Uri(_options.Endpoint, url), request, _jsonSerializerOptions, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            unauthorized?.Invoke();
            throw new UnauthorizedException(await response.Content.ReadAsStringAsync(cancellationToken));
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(_jsonSerializerOptions, cancellationToken: cancellationToken) ?? throw new("Empty response.");
    }

    protected async Task<TResponse> Get<TResponse>(string url, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient();
        using var response = await client.GetAsync(new Uri(_options.Endpoint, url), cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(_jsonSerializerOptions, cancellationToken: cancellationToken) ?? throw new("Empty response.");
    }

    protected async Task<string> PostDirect(string url, string request, Action? unauthorized = null)
    {
        using var client = _httpClientFactory.CreateClient();
        using var response = await client.PostAsync(new Uri(_options.Endpoint, url), new StringContent(request, MediaTypeHeaderValue.Parse("application/json")));
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            unauthorized?.Invoke();
            throw new UnauthorizedException(await response.Content.ReadAsStringAsync());
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    protected async Task Ping(string url)
    {
        using var client = _httpClientFactory.CreateClient();
        using var response = await client.GetAsync(new Uri(_options.Endpoint, url));
        response.EnsureSuccessStatusCode();
    }

    private string WithCode(string url)
    {
        return $"{url}?code={_options.MasterCode}";
    }
}