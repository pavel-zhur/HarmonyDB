using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HarmonyDB.Index.SourcesApiClient;

// ReSharper disable once InconsistentNaming
public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddSourcesApiClient(this IServiceCollection services, IConfiguration configuration) => services
        .AddScoped<SourcesApiClient>()
        .AddHttpClient()
        .Configure<SourcesApiClientOptions>(options => configuration.Bind(nameof(SourcesApiClientOptions), options));
}