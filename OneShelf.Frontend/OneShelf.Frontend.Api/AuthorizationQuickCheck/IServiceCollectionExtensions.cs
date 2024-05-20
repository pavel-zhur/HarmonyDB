using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OneShelf.Frontend.Api.AuthorizationQuickCheck;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddAuthorizationQuickCheckOptions(this IServiceCollection services, IConfiguration configuration) => services
        .AddScoped<AuthorizationQuickChecker>()
        .Configure<AuthorizationQuickCheckOptions>(options => configuration.Bind(nameof(AuthorizationQuickCheckOptions), options));
}