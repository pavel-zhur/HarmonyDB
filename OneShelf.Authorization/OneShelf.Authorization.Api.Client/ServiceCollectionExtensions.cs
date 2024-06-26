using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OneShelf.Authorization.Api.Client
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAuthorizationApiClient(this IServiceCollection services, IConfiguration configuration) => services
            .AddScoped<AuthorizationApiClient>()
            .AddHttpClient()
            .Configure<AuthorizationApiClientOptions>(options => configuration.Bind(nameof(AuthorizationApiClientOptions), options));
    }
}