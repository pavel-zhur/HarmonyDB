using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OneShelf.OneDragon.Database;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDragonDatabase(this IServiceCollection serviceCollection)
        => serviceCollection
            .AddDbContext<DragonDatabase>((services, o) => o.UseSqlServer(
                services.GetRequiredService<IConfiguration>().GetConnectionString(nameof(DragonDatabase)),
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
}