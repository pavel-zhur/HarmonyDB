using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace OneShelf.OneDog.Database.PostgreSqlMigrations;

/// <summary>
/// For dotnet ef: the context on PostgreSQL, with no database behind it.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DogDatabase>
{
    public DogDatabase CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DogDatabase>()
            .UseNpgsql(o => o.MigrationsAssembly(DogDatabase.PostgreSqlMigrationsAssembly))
            .Options;

        return ActivatorUtilities.CreateInstance<DogDatabase>(new ServiceCollection().BuildServiceProvider(), options);
    }
}
