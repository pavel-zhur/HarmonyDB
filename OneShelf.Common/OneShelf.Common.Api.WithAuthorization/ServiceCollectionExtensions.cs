using Microsoft.Extensions.DependencyInjection;

namespace OneShelf.Common.Api.WithAuthorization;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSecurityContext(this IServiceCollection services) => services
        .AddScoped<SecurityContext>();
}