using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Api.Client;

namespace OneShelf.Billing.Api.Client;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddBillingApiClient(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddApiClient<BillingApiClient, BillingApiClientOptions>(configuration);
}