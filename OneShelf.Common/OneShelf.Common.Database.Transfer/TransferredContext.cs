using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OneShelf.Common.Database.Transfer;

/// <summary>
/// A context of the target PostgreSQL: made there by its migrations, then filled.
/// </summary>
public abstract class TargetContext(string name)
{
    /// <summary>
    /// The name of its connection string. Its settings are under Transfer:{Name}, the target's connection string is Transfer:{Name}:Target.
    /// </summary>
    public string Name { get; } = name;

    internal abstract DbContext CreateTarget(IServiceProvider services, string connectionString);
}

/// <param name="postgreSqlMigrationsAssembly">The assembly of the context's PostgreSQL migrations; the tool must reference it.</param>
public abstract class TargetContext<TContext>(string name, string postgreSqlMigrationsAssembly) : TargetContext(name)
    where TContext : DbContext
{
    internal override DbContext CreateTarget(IServiceProvider services, string connectionString)
        => ActivatorUtilities.CreateInstance<TContext>(services, new DbContextOptionsBuilder<TContext>()
            .UseDatabase(DatabaseProvider.PostgreSql, connectionString, postgreSqlMigrationsAssembly)
            .Options);
}

/// <summary>
/// A context copied from SQL Server. Its settings Transfer:{Name}:Source and Transfer:{Name}:SourceUser say where from.
/// A table of the target the SQL Server model does not have is left empty.
/// </summary>
public class TransferredContext<TContext>(string name, string postgreSqlMigrationsAssembly)
    : TargetContext<TContext>(name, postgreSqlMigrationsAssembly), ITransferredContext
    where TContext : DbContext
{
    DbContext ITransferredContext.CreateSource(IServiceProvider services, DbConnection connection)
        => ActivatorUtilities.CreateInstance<TContext>(services, new DbContextOptionsBuilder<TContext>()
            .UseSqlServer(connection)
            .Options);
}

/// <summary>
/// A context filled by a loader of its own, or left empty when it has none.
/// The loader gets the target context and the settings under Transfer:{Name}; it says whether what it loaded checks out.
/// </summary>
public class LoadedContext<TContext>(string name, string postgreSqlMigrationsAssembly, Func<TContext, IConfigurationSection, Task<bool>>? load = null)
    : TargetContext<TContext>(name, postgreSqlMigrationsAssembly), ILoadedContext
    where TContext : DbContext
{
    bool ILoadedContext.HasLoader => load != null;

    Task<bool> ILoadedContext.Load(DbContext target, IConfigurationSection settings) => load!((TContext)target, settings);
}

internal interface ITransferredContext
{
    DbContext CreateSource(IServiceProvider services, DbConnection connection);
}

internal interface ILoadedContext
{
    bool HasLoader { get; }

    Task<bool> Load(DbContext target, IConfigurationSection settings);
}
