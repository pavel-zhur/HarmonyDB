using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace OneShelf.Common.Database;

public static class DbContextOptionsBuilderExtensions
{
    public const string DatabaseProviderKey = "DatabaseProvider";

    public static DatabaseProvider GetDatabaseProvider(this IConfiguration configuration)
        => configuration[DatabaseProviderKey] is { Length: > 0 } value
            ? Enum.Parse<DatabaseProvider>(value)
            : DatabaseProvider.SqlServer;

    /// <summary>
    /// Connects a context to its database the way the configuration says: the provider from the "DatabaseProvider" setting,
    /// the connection string by its name.
    /// No schema is set: a service connects as its own user, whose default schema (SQL Server) or search_path (PostgreSQL)
    /// holds the service's tables and its migrations history.
    /// </summary>
    /// <param name="postgreSqlMigrationsAssembly">
    /// The PostgreSQL migrations are kept in their own assembly, apart from the SQL Server ones in the context's assembly.
    /// </param>
    public static DbContextOptionsBuilder UseConfiguredDatabase(this DbContextOptionsBuilder options,
        IConfiguration configuration, string connectionStringName, string postgreSqlMigrationsAssembly)
        => options.UseDatabase(
            configuration.GetDatabaseProvider(),
            configuration.GetConnectionString(connectionStringName),
            postgreSqlMigrationsAssembly);

    public static DbContextOptionsBuilder UseDatabase(this DbContextOptionsBuilder options,
        DatabaseProvider provider, string? connectionString, string postgreSqlMigrationsAssembly)
        => provider switch
        {
            DatabaseProvider.SqlServer => options.UseSqlServer(connectionString,
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)),
            DatabaseProvider.PostgreSql => options.UseNpgsql(connectionString,
                o => o
                    .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                    .MigrationsAssembly(postgreSqlMigrationsAssembly)),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
        };
}
