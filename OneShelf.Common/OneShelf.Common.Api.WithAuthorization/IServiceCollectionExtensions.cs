using Microsoft.Extensions.DependencyInjection;

namespace OneShelf.Common.Api.WithAuthorization;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddSecurityContext(this IServiceCollection services) => services
        .AddScoped<SecurityContext>();
}