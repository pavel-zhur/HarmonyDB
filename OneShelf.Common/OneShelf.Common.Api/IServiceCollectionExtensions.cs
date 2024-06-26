using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OneShelf.Common.Api;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddConcurrencyLimiter(this IServiceCollection services,
        IConfiguration configuration)
        => services
            .Configure<ConcurrencyLimiterOptions>(o => configuration.GetSection(nameof(ConcurrencyLimiterOptions)).Bind(o))
            .AddSingleton<ConcurrencyLimiter>();
}