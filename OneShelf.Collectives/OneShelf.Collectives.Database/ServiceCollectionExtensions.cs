using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Collectives.Database.Options;

namespace OneShelf.Collectives.Database;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCollectivesDatabase(this IServiceCollection serviceCollection, IConfiguration configuration)
        => serviceCollection
            .AddSingleton<CollectivesCosmosDatabase>()
            .Configure<CollectivesCosmosDatabaseOptions>(options => configuration.Bind(nameof(CollectivesCosmosDatabaseOptions), options));
}