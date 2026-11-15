using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace OneShelf.OneDragon.Database.PostgreSqlMigrations;

/// <summary>
/// For dotnet ef: the context on PostgreSQL, with no database behind it.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DragonDatabase>
{
    public DragonDatabase CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DragonDatabase>()
            .UseNpgsql(o => o.MigrationsAssembly(DragonDatabase.PostgreSqlMigrationsAssembly))
            .Options;

        return ActivatorUtilities.CreateInstance<DragonDatabase>(new ServiceCollection().BuildServiceProvider(), options);
    }
}
