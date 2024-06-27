using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Api.Client;

namespace OneShelf.Collectives.Api.Client;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCollectivesApiClient(this IServiceCollection services, IConfiguration configuration)
        => services.AddApiClient<CollectivesApiClient>(configuration);
}