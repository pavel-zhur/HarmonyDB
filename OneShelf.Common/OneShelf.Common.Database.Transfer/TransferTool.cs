using System.Data.Common;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace OneShelf.Common.Database.Transfer;

/// <summary>
/// Fills an empty PostgreSQL, context by context. Every context is first made by its migrations, then filled:
/// - a <see cref="TransferredContext{TContext}"/> from SQL Server, through the EF Core models: the entities are read with
///   the SQL Server provider and their rows written with a binary COPY as the PostgreSQL model maps them.
///   Keys are kept; the identity sequences are moved past them. Then the row counts and some rows are compared.
/// - a <see cref="LoadedContext{TContext}"/> by its own loader, which checks what it loaded.
/// Each context is filled in one transaction.
///
/// Usage:
///   (no arguments)   fill; refuses when a target table has rows
///   --wipe           empty the target tables first
///   check {context} {table} {key}...   compare these rows of a table (single-column keys) between SQL Server and PostgreSQL
///
/// Settings (environment variables, ':' written as '__'), for every context:
///   Transfer:{Name}:Target       the PostgreSQL connection string, as the context's own role, whose search_path is its schema
/// and for a transferred context:
///   Transfer:{Name}:Source       the SQL Server connection string
///   Transfer:{Name}:SourceUser   optional: the database user to read as, whose default schema holds the context's tables
///                                (when the source connection is an administrator's)
/// A loaded context's loader reads its own settings under Transfer:{Name}.
/// </summary>
public static class TransferTool
{
    private const int TrackedEntitiesLimit = 2000;
    private const int SampleRowsPerTable = 5;

    public static async Task<int> RunAsync(string[] args, IServiceProvider services, params TargetContext[] contexts)
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        if (args is ["check", var contextName, var tableName, .. var keys] && keys.Any())
        {
            var context = contexts.SingleOrDefault(x => x.Name == contextName && x is ITransferredContext)
                          ?? throw new($"Unknown transferred context {contextName}.");
            return await Check(configuration, services, context, tableName, keys) ? 0 : 1;
        }

        if (args is not ([] or ["--wipe"]))
        {
            Log("Usage: [--wipe] | check {context} {table} {key}...");
            return 2;
        }

        var wipe = args is ["--wipe"];

        // all the targets are checked before anything is written
        foreach (var context in contexts)
        {
            await using var connections = await Connections.Open(configuration, services, context);
            if (connections.Source != null)
                await CheckSourceMigrations(connections.Source, context);

            Log($"{context.Name}: migrating the target.");
            await connections.Target.Database.MigrateAsync();

            var nonEmpty = new List<string>();
            foreach (var table in GetTables(connections.Target))
            {
                if (await Count(connections.Target, table, exists: true) > 0) nonEmpty.Add(table.Name);
            }

            if (nonEmpty.Any() && !wipe)
            {
                Log($"{context.Name}: the target is not empty ({string.Join(", ", nonEmpty)}). Run with --wipe to empty it first.");
                return 1;
            }
        }

        var success = true;
        foreach (var context in contexts)
        {
            success &= context is ILoadedContext loaded
                ? await Load(configuration, services, context, loaded, wipe)
                : await Transfer(configuration, services, context, wipe);
        }

        Log(success ? "Done, all equal." : "Done, WITH DIFFERENCES (see above).");
        return success ? 0 : 1;
    }

    private static async Task<bool> Transfer(IConfiguration configuration, IServiceProvider services, TargetContext context, bool wipe)
    {
        var started = Stopwatch.StartNew();
        await using var connections = await Connections.Open(configuration, services, context);
        var (source, target) = (connections.Source!, connections.Target);

        var tables = GetTables(target);
        var plans = tables.Where(x => TablePlan.IsInSource(x, source.Model)).Select(x => TablePlan.Create(x, source.Model)).ToList();
        foreach (var table in tables.Except(plans.Select(x => x.Table)))
        {
            Log($"{context.Name}.{table.Name}: not in the source, left empty.");
        }

        await target.Database.OpenConnectionAsync();
        var connection = (NpgsqlConnection)target.Database.GetDbConnection();
        var samples = new Dictionary<TablePlan, List<object?[]>>();

        await using (var transaction = await target.Database.BeginTransactionAsync())
        {
            if (wipe)
            {
                await Empty(target, context, tables);
            }

            foreach (var plan in plans)
            {
                samples[plan] = await Copy(source, target, connection, plan);
            }

            await MoveSequences(target, tables);
            await transaction.CommitAsync();
        }

        Log($"{context.Name}: copied in {started.Elapsed:hh\\:mm\\:ss}. Comparing.");

        var success = true;
        foreach (var plan in plans)
        {
            var (sourceCount, targetCount) = (await Count(source, plan.Table), await Count(target, plan.Table));
            var differences = await Compare(source, target, plan, samples[plan]);
            success &= sourceCount == targetCount && !differences.Any();

            Log($"{context.Name}.{plan.Table.Name}: source {sourceCount}, target {targetCount}{(sourceCount == targetCount ? "" : " NOT EQUAL")}, "
                + $"{samples[plan].Count} rows compared{(differences.Any() ? $", DIFFERENT: {string.Join("; ", differences)}" : ", equal")}.");
        }

        return success;
    }

    private static async Task<bool> Load(IConfiguration configuration, IServiceProvider services, TargetContext context, ILoadedContext loaded, bool wipe)
    {
        var started = Stopwatch.StartNew();
        await using var connections = await Connections.Open(configuration, services, context);
        var target = connections.Target;
        var tables = GetTables(target);

        bool success;
        await using (var transaction = await target.Database.BeginTransactionAsync())
        {
            if (wipe)
            {
                await Empty(target, context, tables);
            }

            if (loaded.HasLoader)
            {
                Log($"{context.Name}: loading.");
                success = await loaded.Load(target, configuration.GetSection($"Transfer:{context.Name}"));
            }
            else
            {
                Log($"{context.Name}: nothing to load, left empty.");
                success = true;
            }

            await transaction.CommitAsync();
        }

        foreach (var table in tables)
        {
            Log($"{context.Name}.{table.Name}: {await Count(target, table)} rows.");
        }

        Log($"{context.Name}: {(success ? "loaded" : "loaded WITH DIFFERENCES (see above)")} in {started.Elapsed:hh\\:mm\\:ss}.");
        return success;
    }

    private static async Task Empty(DbContext target, TargetContext context, IEnumerable<ITable> tables)
    {
        Log($"{context.Name}: emptying the target.");
        await target.Database.ExecuteSqlRawAsync($"TRUNCATE {string.Join(", ", tables.Select(x => Delimit(target, x)))}");
    }

    private static async Task<List<object?[]>> Copy(DbContext source, DbContext target, NpgsqlConnection connection, TablePlan plan)
    {
        var started = Stopwatch.StartNew();
        var count = await Count(source, plan.Table);
        var samplePositions = Enumerable.Range(0, SampleRowsPerTable).Select(x => count * x / Math.Max(1, SampleRowsPerTable - 1)).Select(x => Math.Min(x, count - 1)).ToHashSet();
        var samples = new List<object?[]>();
        var key = plan.SourceType.FindPrimaryKey()!.Properties;

        var columns = string.Join(", ", plan.Columns.Select(x => Delimit(target, x.Name)));
        await using var importer = await connection.BeginBinaryImportAsync($"COPY {Delimit(target, plan.Table)} ({columns}) FROM STDIN (FORMAT BINARY)");

        long position = 0;
        foreach (var entity in Read(source, plan.SourceType))
        {
            var entry = source.Entry(entity);
            var row = TablePlan.GetRowEntries(entry);

            await importer.StartRowAsync();
            foreach (var (value, dataTypeName) in plan.GetTargetValues(row))
            {
                if (value == null)
                    await importer.WriteNullAsync();
                else
                    await importer.WriteAsync(value, dataTypeName);
            }

            if (samplePositions.Contains(position))
                samples.Add(key.Select(x => entry.CurrentValues[x]).ToArray());

            if (++position % TrackedEntitiesLimit == 0)
                source.ChangeTracker.Clear();

            if (position % 200000 == 0)
                Log($"  {plan.Table.Name}: {position} of {count}, {position / started.Elapsed.TotalSeconds:N0} rows/s");
        }

        await importer.CompleteAsync();
        source.ChangeTracker.Clear();

        Log($"  {plan.Table.Name}: {position} rows in {started.Elapsed:hh\\:mm\\:ss}.");
        return samples;
    }

    private static async Task<bool> Check(IConfiguration configuration, IServiceProvider services, TargetContext context, string tableName, string[] keys)
    {
        await using var connections = await Connections.Open(configuration, services, context);
        var source = connections.Source!;
        var table = GetTables(connections.Target).SingleOrDefault(x => x.Name == tableName) ?? throw new($"Unknown table {tableName}.");
        var plan = TablePlan.Create(table, source.Model);
        var key = plan.SourceType.FindPrimaryKey()!.Properties.Single();
        var keyValues = keys.Select(x => new object?[] { Convert.ChangeType(x, key.ClrType) }).ToList();

        var differences = await Compare(source, connections.Target, plan, keyValues, show: true);
        Log(differences.Any() ? $"DIFFERENT: {string.Join("; ", differences)}" : "Equal.");
        return !differences.Any();
    }

    /// <summary>
    /// Reads the rows by their keys from both databases through the models and compares all their columns.
    /// </summary>
    private static async Task<List<string>> Compare(DbContext source, DbContext target, TablePlan plan, List<object?[]> keys, bool show = false)
    {
        var differences = new List<string>();
        foreach (var key in keys)
        {
            source.ChangeTracker.Clear();
            target.ChangeTracker.Clear();

            var sourceEntity = await Find(source, plan.SourceType, key);
            var targetEntity = await Find(target, plan.TargetType, key);
            var label = string.Join(",", key);

            if (sourceEntity == null || targetEntity == null)
            {
                differences.Add($"{label}: {(sourceEntity == null ? "not in the source" : "not in the target")}");
                continue;
            }

            var (sourceRow, targetRow) = (TablePlan.GetRowEntries(source.Entry(sourceEntity)), TablePlan.GetRowEntries(target.Entry(targetEntity)));
            if (show)
            {
                Log($"{plan.Table.Name} {label}: {plan.Columns.Count} columns, {plan.Columns.Count(x => x.GetSourceValue(sourceRow) != null)} not null; "
                    + $"entities in the source: {string.Join(", ", sourceRow.Keys.Select(x => x.Split('.')[^1]))}; in the target: {string.Join(", ", targetRow.Keys.Select(x => x.Split('.')[^1]))}");
            }

            differences.AddRange(plan.GetDifferences(sourceRow, targetRow).Select(x => $"{label}: {x}"));
        }

        source.ChangeTracker.Clear();
        target.ChangeTracker.Clear();
        return differences;
    }

    private static async Task CheckSourceMigrations(DbContext source, TargetContext context)
    {
        // applied migrations older than the model's last one may be unknown: the migrations were started anew at some point
        var last = source.Database.GetMigrations().Last();
        var pending = (await source.Database.GetPendingMigrationsAsync()).ToList();
        var newer = (await source.Database.GetAppliedMigrationsAsync()).Where(x => string.CompareOrdinal(x, last) > 0).ToList();
        if (pending.Any() || newer.Any())
            throw new($"{context.Name}: the source database is not at the model's migration {last} (pending: {string.Join(", ", pending)}; newer: {string.Join(", ", newer)}).");
    }

    private static async Task MoveSequences(DbContext target, IEnumerable<ITable> tables)
    {
        foreach (var table in tables)
        foreach (var column in table.Columns)
        {
            var delimitedTable = Delimit(target, table);
            var sequence = await target.Database
                .SqlQuery<string?>($"SELECT pg_get_serial_sequence({delimitedTable}, {column.Name}) AS \"Value\"")
                .SingleAsync();
            if (sequence == null) continue;

            var setval = $"SELECT setval({{0}}, COALESCE((SELECT max({Delimit(target, column.Name)}) FROM {delimitedTable}), 0) + 1, false)";
            await target.Database.ExecuteSqlRawAsync(setval, sequence);
        }
    }

    private static List<ITable> GetTables(DbContext target)
    {
        // a table comes after the tables it refers to; a table referring to itself is filled by one COPY, checked at its end
        var tables = target.Model.GetRelationalModel().Tables.ToList();
        var result = new List<ITable>();
        var visiting = new HashSet<ITable>();

        foreach (var table in tables) Visit(table);
        return result;

        void Visit(ITable table)
        {
            if (result.Contains(table)) return;
            if (!visiting.Add(table)) throw new NotSupportedException($"The foreign keys of {table.Name} make a cycle.");

            foreach (var principal in table.ForeignKeyConstraints.Select(x => x.PrincipalTable).Where(x => x != table))
                Visit(principal);

            visiting.Remove(table);
            result.Add(table);
        }
    }

    // read synchronously: SqlClient reads large values asynchronously many times slower
    private static IEnumerable<object> Read(DbContext context, IEntityType type)
        => (IEnumerable<object>)typeof(TransferTool)
            .GetMethod(nameof(ReadTyped), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(type.ClrType)
            .Invoke(null, [context, type])!;

    private static IEnumerable<T> ReadTyped<T>(DbContext context, IEntityType type) where T : class
        => type.HasSharedClrType ? context.Set<T>(type.Name) : context.Set<T>();

    private static async Task<object?> Find(DbContext context, IEntityType type, object?[] key)
        => await (ValueTask<object?>)typeof(TransferTool)
            .GetMethod(nameof(FindTyped), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(type.ClrType)
            .Invoke(null, [context, type, key])!;

    private static async ValueTask<object?> FindTyped<T>(DbContext context, IEntityType type, object?[] key) where T : class
        => await (type.HasSharedClrType ? context.Set<T>(type.Name) : context.Set<T>()).FindAsync(key);

    private static async Task<long> Count(DbContext context, ITable table, bool exists = false)
    {
        var sql = exists
            ? $"SELECT CASE WHEN EXISTS (SELECT 1 FROM {Delimit(context, table)}) THEN 1 ELSE 0 END"
            : context.Database.IsNpgsql()
                ? $"SELECT count(*) FROM {Delimit(context, table)}"
                : $"SELECT COUNT_BIG(*) FROM {Delimit(context, table)}";

        var connection = context.Database.GetDbConnection();
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 0;
            command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static string Delimit(DbContext context, ITable table)
        => context.GetService<ISqlGenerationHelper>().DelimitIdentifier(table.Name, table.Schema);

    private static string Delimit(DbContext context, string name)
        => context.GetService<ISqlGenerationHelper>().DelimitIdentifier(name);

    private static void Log(string message) => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {message}");

    /// <summary>
    /// The target context of one context and, for a transferred one, its source.
    /// </summary>
    private sealed class Connections : IAsyncDisposable
    {
        private readonly DbConnection? _sourceConnection;

        private Connections(DbConnection? sourceConnection, DbContext? source, DbContext target)
        {
            _sourceConnection = sourceConnection;
            Source = source;
            Target = target;
        }

        public DbContext? Source { get; }

        public DbContext Target { get; }

        public static async Task<Connections> Open(IConfiguration configuration, IServiceProvider services, TargetContext context)
        {
            var section = configuration.GetSection($"Transfer:{context.Name}");
            string Get(string key) => section[key] is { Length: > 0 } value ? value : throw new($"The setting Transfer:{context.Name}:{key} is missing.");

            var target = context.CreateTarget(services, new NpgsqlConnectionStringBuilder(Get("Target")) { CommandTimeout = 0 }.ConnectionString);
            if (context is not ITransferredContext transferred)
                return new(null, null, target);

            target.ChangeTracker.AutoDetectChangesEnabled = false;

            // the reading user's security context belongs to this one connection: it is not pooled
            var sourceConnection = new SqlConnection(new SqlConnectionStringBuilder(Get("Source")) { Pooling = false }.ConnectionString);
            await sourceConnection.OpenAsync();
            if (section["SourceUser"] is { Length: > 0 } user)
            {
                await using var command = sourceConnection.CreateCommand();
                command.CommandText = "EXECUTE AS USER = @user";
                command.Parameters.AddWithValue("@user", user);
                await command.ExecuteNonQueryAsync();
            }

            var source = transferred.CreateSource(services, sourceConnection);
            source.Database.SetCommandTimeout(TimeSpan.Zero);
            source.ChangeTracker.AutoDetectChangesEnabled = false;

            return new(sourceConnection, source, target);
        }

        public async ValueTask DisposeAsync()
        {
            if (Source != null) await Source.DisposeAsync();
            await Target.DisposeAsync();
            if (_sourceConnection != null) await _sourceConnection.DisposeAsync();
        }
    }
}
