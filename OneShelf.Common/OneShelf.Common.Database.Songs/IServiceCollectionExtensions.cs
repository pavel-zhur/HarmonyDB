using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Database.Songs.Services;

namespace OneShelf.Common.Database.Songs;

// ReSharper disable once InconsistentNaming
public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddSongsDatabase(this IServiceCollection serviceCollection)
        => serviceCollection
            .AddDbContext<SongsDatabase>((services, o) => o.UseSqlServer(
                services.GetRequiredService<IConfiguration>().GetConnectionString(nameof(SongsDatabase)),
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)))
            .AddSingleton<SongsDatabaseMemory>()
            .AddScoped<CategoriesCatalog>()
            .AddScoped<SongsOperations>();
}