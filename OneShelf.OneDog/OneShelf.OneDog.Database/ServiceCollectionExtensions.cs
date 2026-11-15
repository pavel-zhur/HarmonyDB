using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Database;

namespace OneShelf.OneDog.Database;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDogDatabase(this IServiceCollection serviceCollection)
        => serviceCollection
            .AddDbContext<DogDatabase>((services, o) => o.UseConfiguredDatabase(
                services.GetRequiredService<IConfiguration>(),
                nameof(DogDatabase),
                DogDatabase.PostgreSqlMigrationsAssembly));
}
