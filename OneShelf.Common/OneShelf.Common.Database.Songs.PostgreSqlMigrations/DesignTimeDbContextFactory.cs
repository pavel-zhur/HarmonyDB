using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Database.Songs.Services;

namespace OneShelf.Common.Database.Songs.PostgreSqlMigrations;

/// <summary>
/// For dotnet ef: the context on PostgreSQL, with no database behind it.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SongsDatabase>
{
    public SongsDatabase CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SongsDatabase>()
            .UseNpgsql(o => o.MigrationsAssembly(SongsDatabase.PostgreSqlMigrationsAssembly))
            .Options;

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<SongsDatabaseMemory>()
            .BuildServiceProvider();

        return ActivatorUtilities.CreateInstance<SongsDatabase>(services, options);
    }
}
