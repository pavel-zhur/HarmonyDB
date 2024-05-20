using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OneShelf.Common.Api.Client;

// ReSharper disable once InconsistentNaming
public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddApiClient<TClient>(this IServiceCollection services, IConfiguration configuration)
        where TClient : ApiClientBase<TClient>
        => services
            .AddScoped<TClient>()
            .AddHttpClient()
            .Configure<ApiClientOptions<TClient>>(options => configuration.Bind($"{typeof(TClient).Name}Options", options));

    public static IServiceCollection AddApiClient<TClient, TOptions>(this IServiceCollection services, IConfiguration configuration)
        where TOptions : ApiClientOptions<TClient>
        where TClient : ApiClientBase<TClient>
        => services
            .AddScoped<TClient>()
            .AddHttpClient()
            .Configure<TOptions>(options => configuration.Bind($"{typeof(TOptions).Name}", options))
            .Configure<ApiClientOptions<TClient>>(options => configuration.Bind($"{typeof(TOptions).Name}", options));
}