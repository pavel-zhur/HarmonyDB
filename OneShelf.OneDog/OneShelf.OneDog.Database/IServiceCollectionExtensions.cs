using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OneShelf.OneDog.Database;

// ReSharper disable once InconsistentNaming
public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddDogDatabase(this IServiceCollection serviceCollection)
        => serviceCollection
            .AddDbContext<DogDatabase>((services, o) => o.UseSqlServer(
                services.GetRequiredService<IConfiguration>().GetConnectionString(nameof(DogDatabase)),
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
}