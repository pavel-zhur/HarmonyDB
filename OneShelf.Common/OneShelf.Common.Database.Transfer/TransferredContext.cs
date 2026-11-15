using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace OneShelf.Common.Database.Transfer;

/// <summary>
/// A context whose data is transferred, and how it is made on either side.
/// </summary>
public abstract class TransferredContext(string name)
{
    /// <summary>
    /// The name of its connection string. Its settings are Transfer:{Name}:Source, Transfer:{Name}:SourceUser and Transfer:{Name}:Target.
    /// </summary>
    public string Name { get; } = name;

    internal abstract DbContext CreateSource(IServiceProvider services, DbConnection connection);

    internal abstract DbContext CreateTarget(IServiceProvider services, string connectionString);
}

/// <param name="postgreSqlMigrationsAssembly">The assembly of the context's PostgreSQL migrations; the tool must reference it.</param>
public class TransferredContext<TContext>(string name, string postgreSqlMigrationsAssembly) : TransferredContext(name)
    where TContext : DbContext
{
    internal override DbContext CreateSource(IServiceProvider services, DbConnection connection)
        => ActivatorUtilities.CreateInstance<TContext>(services, new DbContextOptionsBuilder<TContext>()
            .UseSqlServer(connection)
            .Options);

    internal override DbContext CreateTarget(IServiceProvider services, string connectionString)
        => ActivatorUtilities.CreateInstance<TContext>(services, new DbContextOptionsBuilder<TContext>()
            .UseDatabase(DatabaseProvider.PostgreSql, connectionString, postgreSqlMigrationsAssembly)
            .Options);
}
