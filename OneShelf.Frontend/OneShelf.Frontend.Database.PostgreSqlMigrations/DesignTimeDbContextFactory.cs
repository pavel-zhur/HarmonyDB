using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Database;

namespace OneShelf.Frontend.Database.PostgreSqlMigrations;

/// <summary>
/// For dotnet ef: the context on PostgreSQL, with no database behind it.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FrontendDatabase>
{
    public FrontendDatabase CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FrontendDatabase>()
            .UseDatabase(DatabaseProvider.PostgreSql, null, FrontendDatabase.PostgreSqlMigrationsAssembly)
            .Options;

        return ActivatorUtilities.CreateInstance<FrontendDatabase>(new ServiceCollection().BuildServiceProvider(), options);
    }
}
