using System.Text;
using System.Text.Json;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Billing.Api.Model;
using OneShelf.Common.Api.Client;

namespace OneShelf.Billing.Api.Client;

public class BillingApiClient : ApiClientBase<BillingApiClient>
{
    private readonly BillingApiClientOptions _options;
    private readonly ILogger<BillingApiClient> _logger;

    public BillingApiClient(IHttpClientFactory httpClientFactory, IOptions<BillingApiClientOptions> options, ILogger<BillingApiClient> logger)
        : base(options, httpClientFactory)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task<AllResponse> All(int? domainId = null, TimeSpan? window = null)
        => await PostWithCode<AllRequest, AllResponse>(BillingApiUrls.All, new()
        {
            DomainId = domainId,
            Window = window,
        });

    public async Task Add(Usage usage)
    {
        try
        {
            var serialized = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(usage)));
            var client = new QueueClient(_options.QueueClientConnectionString, BillingApiUrls.InputQueueName);
            await client.CreateIfNotExistsAsync();
            await client.SendMessageAsync(serialized);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error writing the billing info, {usage}", JsonSerializer.Serialize(usage));
        }
    }
}