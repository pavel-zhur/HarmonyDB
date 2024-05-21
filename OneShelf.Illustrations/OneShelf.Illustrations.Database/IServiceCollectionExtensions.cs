using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Illustrations.Database.Options;

namespace OneShelf.Illustrations.Database;

// ReSharper disable once InconsistentNaming
public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddIllustrationsDatabase(this IServiceCollection serviceCollection, IConfiguration configuration)
        => serviceCollection
            .AddSingleton<IllustrationsCosmosDatabase>()
            .Configure<IllustrationsCosmosDatabaseOptions>(options => configuration.Bind(nameof(IllustrationsCosmosDatabaseOptions), options));
}