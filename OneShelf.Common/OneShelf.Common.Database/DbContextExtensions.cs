using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace OneShelf.Common.Database;

public static class DbContextExtensions
{
    /// <summary>
    /// Inserts the entity's row or, when a row with its key exists, replaces that row's values, in one statement
    /// (INSERT ... ON CONFLICT DO UPDATE), so that concurrent writers of the same key do not fail. PostgreSQL only.
    /// The values are converted as the model maps them; the entity is not tracked.
    /// </summary>
    public static async Task UpsertAsync<T>(this DbContext context, T entity, CancellationToken cancellationToken = default)
        where T : class
    {
        var entityType = context.Model.FindEntityType(typeof(T)) ?? throw new ArgumentException($"{typeof(T).Name} is not in the model.");
        var table = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());
        var sql = context.GetService<ISqlGenerationHelper>();

        var properties = entityType.GetProperties().ToList();
        var key = entityType.FindPrimaryKey()!.Properties;
        string Column(IProperty property) => sql.DelimitIdentifier(property.GetColumnName(table)!);

        await using var command = context.Database.GetDbConnection().CreateCommand();
        var parameters = properties
            .Select((property, i) => (DbParameter)property.GetRelationalTypeMapping()
                .CreateParameter(command, $"p{i}", property.GetGetter().GetClrValueUsingContainingEntity(entity), property.IsNullable))
            .ToList();

        var update = properties.Except(key).Select(x => $"{Column(x)} = EXCLUDED.{Column(x)}").ToList();
        await context.Database.ExecuteSqlRawAsync(
            $"""
             INSERT INTO {sql.DelimitIdentifier(table.Name, table.Schema)} ({string.Join(", ", properties.Select(Column))})
             VALUES ({string.Join(", ", parameters.Select(x => $"@{x.ParameterName}"))})
             ON CONFLICT ({string.Join(", ", key.Select(Column))}) DO {(update.Any() ? $"UPDATE SET {string.Join(", ", update)}" : "NOTHING")}
             """,
            parameters,
            cancellationToken);
    }
}
