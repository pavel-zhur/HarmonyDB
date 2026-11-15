using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Database;
using OneShelf.Illustrations.Database.Services;

namespace OneShelf.Illustrations.Database;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIllustrationsDatabase(this IServiceCollection serviceCollection, IConfiguration configuration)
        => serviceCollection
            .AddSingleton<ImagesProcessor>()
            .AddDbContext<IllustrationsDatabase>(o => o.UseDatabase(
                DatabaseProvider.PostgreSql,
                configuration.GetConnectionString(nameof(IllustrationsDatabase)),
                IllustrationsDatabase.PostgreSqlMigrationsAssembly));
}
