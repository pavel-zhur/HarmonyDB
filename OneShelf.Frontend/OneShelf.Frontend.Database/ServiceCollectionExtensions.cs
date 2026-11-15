using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Database;

namespace OneShelf.Frontend.Database;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFrontendDatabase(this IServiceCollection serviceCollection, IConfiguration configuration)
        => serviceCollection
            .AddDbContext<FrontendDatabase>(o => o.UseDatabase(
                DatabaseProvider.PostgreSql,
                configuration.GetConnectionString(nameof(FrontendDatabase)),
                FrontendDatabase.PostgreSqlMigrationsAssembly));
}
