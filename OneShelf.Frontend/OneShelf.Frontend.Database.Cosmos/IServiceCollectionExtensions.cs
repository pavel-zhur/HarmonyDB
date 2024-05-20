using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Frontend.Database.Cosmos.Options;

namespace OneShelf.Frontend.Database.Cosmos;

// ReSharper disable once InconsistentNaming
public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddFrontendCosmosDatabase(this IServiceCollection serviceCollection, IConfiguration configuration)
        => serviceCollection
            .AddSingleton<FrontendCosmosDatabase>()
            .Configure<FrontendCosmosDatabaseOptions>(options => configuration.Bind(nameof(FrontendCosmosDatabaseOptions), options));
}