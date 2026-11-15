using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Database;

namespace OneShelf.OneDog.Database.PostgreSqlMigrations;

/// <summary>
/// For dotnet ef: the context on PostgreSQL, with no database behind it.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DogDatabase>
{
    public DogDatabase CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DogDatabase>()
            .UseDatabase(DatabaseProvider.PostgreSql, null, DogDatabase.PostgreSqlMigrationsAssembly)
            .Options;

        return ActivatorUtilities.CreateInstance<DogDatabase>(new ServiceCollection().BuildServiceProvider(), options);
    }
}
