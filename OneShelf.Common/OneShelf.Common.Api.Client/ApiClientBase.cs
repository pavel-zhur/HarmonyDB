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

    public Identity GetServiceIdentity()
        => new()
        {
            Hash = _options.ServiceCode,
        };

    protected async Task<TResponse> PostWithCode<TRequest, TResponse>(string url, TRequest request, CancellationToken cancellationToken = default, ApiTraceBag? apiTraceBag = null)
    {
        var started = DateTime.Now;
        using var client = _httpClientFactory.CreateClient();
        using var httpResponseMessage = await client.PostAsJsonAsync(new Uri(_options.Endpoint, WithCode(url)), request, _jsonSerializerOptions, cancellationToken);
        if (httpResponseMessage.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedException(await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken));
        }

        if (httpResponseMessage.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new ConcurrencyException();
        }

        httpResponseMessage.EnsureSuccessStatusCode();
        var response = await httpResponseMessage.Content.ReadFromJsonAsync<TResponse>(_jsonSerializerOptions, cancellationToken) ?? throw new("Empty response.");

        apiTraceBag?.Requests.Add(new()
        {
            Method = "POST",
            Request = MaskIdentity(request),
            Response = response,
            Url = new Uri(_options.Endpoint, $"{url}?code={{code}}").ToString(),
            TimeTaken = DateTime.Now - started,
        });

        return response;
    }

    protected async Task<byte[]> PostWithCode<TRequest>(string url, TRequest request)
    {
        using var client = _httpClientFactory.CreateClient();
        using var response = await client.PostAsJsonAsync(new Uri(_options.Endpoint, WithCode(url)), request, _jsonSerializerOptions);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedException(await response.Content.ReadAsStringAsync());
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new ConcurrencyException();
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }

    protected async Task<TResponse> Post<TRequest, TResponse>(string url, TRequest request, Action? unauthorized = null, CancellationToken cancellationToken = default, ApiTraceBag? apiTraceBag = null)
    {
        var started = DateTime.Now;
        using var client = _httpClientFactory.CreateClient();
        var uri = new Uri(_options.Endpoint, url);
        using var httpResponseMessage = await client.PostAsJsonAsync(uri, request, _jsonSerializerOptions, cancellationToken);
        if (httpResponseMessage.StatusCode == HttpStatusCode.Unauthorized)
        {
            unauthorized?.Invoke();
            throw new UnauthorizedException(await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken));
        }

        if (httpResponseMessage.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new ConcurrencyException();
        }

        httpResponseMessage.EnsureSuccessStatusCode();
        var response = await httpResponseMessage.Content.ReadFromJsonAsync<TResponse>(_jsonSerializerOptions, cancellationToken: cancellationToken) ?? throw new("Empty response.");

        apiTraceBag?.Requests.Add(new()
        {
            Method = "POST",
            Request = MaskIdentity(request),
            Response = response,
            Url = uri.ToString(),
            TimeTaken = DateTime.Now - started,
        });

        return response;
    }

    protected async Task<TResponse> Get<TResponse>(string url, CancellationToken cancellationToken = default, ApiTraceBag? apiTraceBag = null)
    {
        var started = DateTime.Now;
        using var client = _httpClientFactory.CreateClient();
        var uri = new Uri(_options.Endpoint, url);
        using var httpResponseMessage = await client.GetAsync(uri, cancellationToken);

        if (httpResponseMessage.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new ConcurrencyException();
        }

        httpResponseMessage.EnsureSuccessStatusCode();
        var response = await httpResponseMessage.Content.ReadFromJsonAsync<TResponse>(_jsonSerializerOptions, cancellationToken: cancellationToken) ?? throw new("Empty response.");

        apiTraceBag?.Requests.Add(new()
        {
            Method = "GET",
            Response = response,
            Url = uri.ToString(),
            TimeTaken = DateTime.Now - started,
        });

        return response;
    }

    protected async Task<string> PostDirect(string url, string request, Action? unauthorized = null, ApiTraceBag? apiTraceBag = null)
    {
        var started = DateTime.Now;
        using var client = _httpClientFactory.CreateClient();
        using var httpResponseMessage = await client.PostAsync(new Uri(_options.Endpoint, url), new StringContent(request, MediaTypeHeaderValue.Parse("application/json")));
        if (httpResponseMessage.StatusCode == HttpStatusCode.Unauthorized)
        {
            unauthorized?.Invoke();
            throw new UnauthorizedException(await httpResponseMessage.Content.ReadAsStringAsync());
        }

        if (httpResponseMessage.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new ConcurrencyException();
        }

        httpResponseMessage.EnsureSuccessStatusCode();
        var response = await httpResponseMessage.Content.ReadAsStringAsync();

        apiTraceBag?.Requests.Add(new()
        {
            Method = "POST",
            Request = MaskIdentity(request),
            Response = response,
            Url = new Uri(_options.Endpoint, $"{url}?code={{code}}").ToString(),
            TimeTaken = DateTime.Now - started,
        });

        return response;
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

    private static TRequest MaskIdentity<TRequest>(TRequest request)
    {
        if (request != null)
        {
            request = JsonSerializer.Deserialize<TRequest>(JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                Converters =
                {
                    new MaskedIdentityConverter(),
                },
            }))!;
        }

        return request;
    }
}