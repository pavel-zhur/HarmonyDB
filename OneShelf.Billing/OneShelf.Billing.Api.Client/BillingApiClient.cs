using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Billing.Api.Model;
using OneShelf.Common.Api.Client;

namespace OneShelf.Billing.Api.Client;

public class BillingApiClient : ApiClientBase<BillingApiClient>
{
    private readonly ILogger<BillingApiClient> _logger;

    public BillingApiClient(IHttpClientFactory httpClientFactory, IOptions<BillingApiClientOptions> options, ILogger<BillingApiClient> logger)
        : base(options, httpClientFactory)
    {
        _logger = logger;
    }

    public async Task<AllResponse> All(int? domainId = null, long? userId = null, long? chatId = null)
        => await PostWithCode<AllRequest, AllResponse>(BillingApiUrls.All, new()
        {
            DomainId = domainId,
            ChatId = chatId,
            UserId = userId,
        });

    public async Task Add(Usage usage)
    {
        try
        {
            await PostWithCode(BillingApiUrls.Add, usage);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error writing the billing info, {usage}", JsonSerializer.Serialize(usage));
        }
    }
}