using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Api.Client;

namespace OneShelf.Frontend.Api.Client;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFrontendApiClient(this IServiceCollection services,
        IConfiguration configuration) => services
        .AddApiClient<FrontendApiClient>(configuration);
}