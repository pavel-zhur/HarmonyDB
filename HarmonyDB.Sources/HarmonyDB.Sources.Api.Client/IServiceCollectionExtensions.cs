using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HarmonyDB.Source.Api.Client;

// ReSharper disable once InconsistentNaming
public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddSourcesApiClient(this IServiceCollection services, IConfiguration configuration) => services
        .AddScoped<SourcesApiClient>()
        .AddHttpClient()
        .Configure<SourcesApiClientOptions>(options => configuration.GetSection(nameof(SourcesApiClientOptions)).Bind(options, binderOptions =>
        {
            binderOptions.BindNonPublicProperties = true;
        }));
}