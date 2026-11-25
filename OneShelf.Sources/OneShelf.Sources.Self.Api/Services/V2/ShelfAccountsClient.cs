using System.Net.Http.Json;
using System.Text.Json;
using HarmonyDB.Common.ServiceAuth;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Sources.Self.Api.Models;

namespace OneShelf.Sources.Self.Api.Services.V2;

/// <summary>
/// Asks the shelf which Telegram users an account is: the own chords belong to Telegram users, the callers of the
/// source contract are accounts of the shelf. The shelf answers
/// <c>GET {ShelfAccountsEndpoint}accounts/{accountId}/logins/telegram</c> with <c>{ "subjects": ["&lt;telegram user id&gt;", …] }</c>
/// (empty for an account without a Telegram sign-in, or one it does not have), to an internal client with its key.
/// </summary>
public class ShelfAccountsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly SelfV2Options _options;
    private readonly ILogger<ShelfAccountsClient> _logger;

    public ShelfAccountsClient(HttpClient httpClient, IMemoryCache cache, IOptions<SelfV2Options> options, ILogger<ShelfAccountsClient> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    /// <returns>The Telegram user ids of the account, as the shelf told them within <see cref="SelfV2Options.ShelfAccountsCacheTime"/>.</returns>
    /// <exception cref="ShelfUnavailableException">The shelf is not configured, or did not answer.</exception>
    public async Task<IReadOnlySet<long>> GetTelegramUserIds(Guid accountId, CancellationToken cancellationToken)
    {
        var key = (nameof(ShelfAccountsClient), accountId);
        if (_cache.TryGetValue(key, out IReadOnlySet<long>? cached)) return cached!;

        var ids = await Ask(accountId, cancellationToken);
        _cache.Set(key, ids, _options.ShelfAccountsCacheTime);
        return ids;
    }

    private async Task<IReadOnlySet<long>> Ask(Guid accountId, CancellationToken cancellationToken)
    {
        if (_options.ShelfAccountsEndpoint == null || string.IsNullOrEmpty(_options.ShelfAccountsKey))
        {
            _logger.LogError("The shelf's accounts are not configured, the account {accountId} cannot be told.", accountId);
            throw new ShelfUnavailableException();
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.ShelfAccountsTimeout);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_options.ShelfAccountsEndpoint, $"accounts/{accountId}/logins/telegram"));
            request.Headers.Add(InternalKeyDefaults.HeaderName, _options.ShelfAccountsKey);
            using var response = await _httpClient.SendAsync(request, timeout.Token);
            response.EnsureSuccessStatusCode();
            var answer = await response.Content.ReadFromJsonAsync<TelegramLogins>(JsonOptions, timeout.Token)
                         ?? throw new JsonException("The answer is empty.");

            return (answer.Subjects ?? throw new JsonException("The answer has no subjects.")).Select(long.Parse).ToHashSet();
        }
        catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(e, "The shelf did not tell the account {accountId} in time.", accountId);
            throw new ShelfUnavailableException(e);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or JsonException or FormatException or OverflowException)
        {
            _logger.LogError(e, "The shelf did not tell the account {accountId}.", accountId);
            throw new ShelfUnavailableException(e);
        }
    }

    private record TelegramLogins(IReadOnlyList<string> Subjects);
}

public class ShelfUnavailableException(Exception? innerException = null)
    : Exception("The shelf could not tell who the caller is.", innerException);
