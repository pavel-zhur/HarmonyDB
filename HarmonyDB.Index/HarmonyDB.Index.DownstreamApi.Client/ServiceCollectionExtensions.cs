using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HarmonyDB.Index.DownstreamApi.Client;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDownstreamApiClient(this IServiceCollection services, IConfiguration configuration) => services
        .AddScoped<DownstreamApiClient>()
        .AddHttpClient()
        .Configure<DownstreamApiClientOptions>(options => configuration.Bind(nameof(DownstreamApiClientOptions), options));
}