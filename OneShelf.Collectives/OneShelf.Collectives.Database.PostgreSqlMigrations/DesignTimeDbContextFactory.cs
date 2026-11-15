using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Database;

namespace OneShelf.Collectives.Database.PostgreSqlMigrations;

/// <summary>
/// For dotnet ef: the context on PostgreSQL, with no database behind it.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CollectivesDatabase>
{
    public CollectivesDatabase CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CollectivesDatabase>()
            .UseDatabase(DatabaseProvider.PostgreSql, null, CollectivesDatabase.PostgreSqlMigrationsAssembly)
            .Options;

        return ActivatorUtilities.CreateInstance<CollectivesDatabase>(new ServiceCollection().BuildServiceProvider(), options);
    }
}
