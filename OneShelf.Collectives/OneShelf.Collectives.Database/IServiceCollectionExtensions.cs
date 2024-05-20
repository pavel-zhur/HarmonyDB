using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Collectives.Database.Options;

namespace OneShelf.Collectives.Database;

// ReSharper disable once InconsistentNaming
public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddCollectivesDatabase(this IServiceCollection serviceCollection, IConfiguration configuration)
        => serviceCollection
            .AddSingleton<CollectivesCosmosDatabase>()
            .Configure<CollectivesCosmosDatabaseOptions>(options => configuration.Bind(nameof(CollectivesCosmosDatabaseOptions), options));
}