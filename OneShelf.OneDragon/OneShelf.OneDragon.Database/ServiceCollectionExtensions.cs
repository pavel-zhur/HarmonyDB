using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Database;

namespace OneShelf.OneDragon.Database;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDragonDatabase(this IServiceCollection serviceCollection)
        => serviceCollection
            .AddDbContext<DragonDatabase>((services, o) => o.UseConfiguredDatabase(
                services.GetRequiredService<IConfiguration>(),
                nameof(DragonDatabase),
                DragonDatabase.PostgreSqlMigrationsAssembly));
}
