using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Api.Client;

namespace OneShelf.Sources.Self.Api.Client;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSelfApiClient(this IServiceCollection services, IConfiguration configuration) =>
        services
            .AddApiClient<SelfApiClient>(configuration);
}