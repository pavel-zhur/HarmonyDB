using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Database;
using OneShelf.Illustrations.Database.Services;

namespace OneShelf.Illustrations.Database.PostgreSqlMigrations;

/// <summary>
/// For dotnet ef: the context on PostgreSQL, with no database behind it.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<IllustrationsDatabase>
{
    public IllustrationsDatabase CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IllustrationsDatabase>()
            .UseDatabase(DatabaseProvider.PostgreSql, null, IllustrationsDatabase.PostgreSqlMigrationsAssembly)
            .Options;

        return ActivatorUtilities.CreateInstance<IllustrationsDatabase>(new ServiceCollection().AddSingleton<ImagesProcessor>().BuildServiceProvider(), options);
    }
}
