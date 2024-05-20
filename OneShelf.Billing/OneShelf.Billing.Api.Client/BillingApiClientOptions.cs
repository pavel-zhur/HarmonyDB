using OneShelf.Common.Api.Client;

namespace OneShelf.Billing.Api.Client;

public class BillingApiClientOptions : ApiClientOptions<BillingApiClient>
{
    public required string QueueClientConnectionString { get; set; }
}