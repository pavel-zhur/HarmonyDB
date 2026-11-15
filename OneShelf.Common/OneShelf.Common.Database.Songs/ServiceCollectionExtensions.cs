using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Database.Songs.Services;

namespace OneShelf.Common.Database.Songs;

// ReSharper disable once InconsistentNaming
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSongsDatabase(this IServiceCollection serviceCollection)
        => serviceCollection
            .AddDbContext<SongsDatabase>((services, o) => o.UseConfiguredDatabase(
                services.GetRequiredService<IConfiguration>(),
                nameof(SongsDatabase),
                SongsDatabase.PostgreSqlMigrationsAssembly))
            .AddSingleton<SongsDatabaseMemory>()
            .AddScoped<CategoriesCatalog>()
            .AddScoped<SongsOperations>();
}
