using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Database;

namespace OneShelf.Collectives.Database;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCollectivesDatabase(this IServiceCollection serviceCollection, IConfiguration configuration)
        => serviceCollection
            .AddDbContext<CollectivesDatabase>(o => o.UseDatabase(
                DatabaseProvider.PostgreSql,
                configuration.GetConnectionString(nameof(CollectivesDatabase)),
                CollectivesDatabase.PostgreSqlMigrationsAssembly));
}
