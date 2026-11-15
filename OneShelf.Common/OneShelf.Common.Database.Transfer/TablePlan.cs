using System.Collections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace OneShelf.Common.Database.Transfer;

/// <summary>
/// How the rows of one target table are made from the entities of the source, as the target model maps them:
/// the table's entity type with the owned ones split into the same table, their properties, shadow ones included,
/// converted to the target column types by the target model's converters.
/// </summary>
internal class TablePlan
{
    private TablePlan(ITable table, IEntityType sourceType, IEntityType targetType, IReadOnlyList<ColumnPlan> columns)
    {
        Table = table;
        SourceType = sourceType;
        TargetType = targetType;
        Columns = columns;
    }

    public ITable Table { get; }

    /// <summary>
    /// The entity type whose entities are read from the source, one per row.
    /// </summary>
    public IEntityType SourceType { get; }

    public IEntityType TargetType { get; }

    public IReadOnlyList<ColumnPlan> Columns { get; }

    public static TablePlan Create(ITable table, IModel sourceModel)
    {
        var roots = table.EntityTypeMappings
            .Select(x => x.TypeBase)
            .OfType<IEntityType>()
            .Where(x => !x.IsOwned())
            .Select(x => x.GetRootType())
            .Distinct()
            .ToList();

        if (roots.Count != 1)
            throw new NotSupportedException($"The table {table.Name} is expected to hold one entity type with its owned types, found {roots.Count}.");

        var columns = table.Columns
            .Where(x => x.ComputedColumnSql == null)
            .Select(column =>
            {
                var mappings = column.PropertyMappings.Select(x => new PropertySource(
                    x.TableMapping.TypeBase.Name,
                    sourceModel.FindEntityType(x.TableMapping.TypeBase.Name)?.FindProperty(x.Property.Name)
                    ?? throw new($"The source model has no {x.TableMapping.TypeBase.Name}.{x.Property.Name}."),
                    x.Property)).ToList();

                var typeMapping = column.PropertyMappings.First().TypeMapping;
                return new ColumnPlan(column.Name, typeMapping.StoreTypeNameBase, typeMapping.Converter, mappings);
            })
            .ToList();

        return new(table, sourceModel.FindEntityType(roots[0].Name)!, roots[0], columns);
    }

    /// <summary>
    /// The entries of one row: the entity and its owned entities that are present, by entity type name.
    /// </summary>
    public static Dictionary<string, EntityEntry> GetRowEntries(EntityEntry entry)
    {
        var result = new Dictionary<string, EntityEntry>();
        Add(entry);
        return result;

        void Add(EntityEntry x)
        {
            result.Add(x.Metadata.Name, x);
            foreach (var navigation in x.Navigations)
            {
                if (navigation.Metadata is INavigation { IsCollection: false, IsOnDependent: false } metadata
                    && metadata.TargetEntityType.IsOwned()
                    && ((ReferenceEntry)navigation).TargetEntry is { } owned)
                {
                    Add(owned);
                }
            }
        }
    }

    /// <summary>
    /// The target values of a row, ready to be written.
    /// </summary>
    public IEnumerable<(object? value, string dataTypeName)> GetTargetValues(Dictionary<string, EntityEntry> sourceRow)
        => Columns.Select(x =>
        {
            var value = x.GetSourceValue(sourceRow);
            return (value == null || x.Converter == null ? value : x.Converter.ConvertToProvider(value), x.DataTypeName);
        });

    /// <summary>
    /// The columns whose model values differ between a row read from the source and the same row read from the target.
    /// The target keeps times to the microsecond, SQL Server to 100 nanoseconds: that much difference is not counted.
    /// </summary>
    public IEnumerable<string> GetDifferences(Dictionary<string, EntityEntry> sourceRow, Dictionary<string, EntityEntry> targetRow)
        => Columns
            .Where(x => !Same(x.GetSourceValue(sourceRow), x.GetTargetValue(targetRow)))
            .Select(x => $"{x.Name}: {Show(x.GetSourceValue(sourceRow))} -> {Show(x.GetTargetValue(targetRow))}");

    private static bool Same(object? x, object? y)
        => (x, y) switch
        {
            (null, _) or (_, null) => x == null && y == null,
            (DateTime a, DateTime b) => a.Ticks / 10 == b.Ticks / 10,
            (string a, string b) => a == b,
            (IEnumerable a, IEnumerable b) => a.Cast<object?>().Count() == b.Cast<object?>().Count()
                                              && a.Cast<object?>().Zip(b.Cast<object?>()).All(z => Same(z.First, z.Second)),
            _ => x.Equals(y),
        };

    private static string Show(object? value)
        => value switch
        {
            null => "null",
            DateTime x => x.ToString("O"),
            string x => $"\"{x}\"",
            IEnumerable x => $"[{string.Join(", ", x.Cast<object?>().Select(Show))}]",
            _ => value.ToString() ?? string.Empty,
        };
}

/// <param name="Name">The column name.</param>
/// <param name="DataTypeName">The PostgreSQL type of the column, for the binary COPY.</param>
/// <param name="Converter">The target model's conversion of the property value to the column value.</param>
/// <param name="Sources">The properties mapped to the column (several when owned types share the key column).</param>
internal record ColumnPlan(string Name, string DataTypeName, ValueConverter? Converter, IReadOnlyList<PropertySource> Sources)
{
    public object? GetSourceValue(Dictionary<string, EntityEntry> row)
        => Sources.FirstOrDefault(x => row.ContainsKey(x.EntityTypeName)) is { } source
            ? row[source.EntityTypeName].CurrentValues[source.SourceProperty]
            : null;

    public object? GetTargetValue(Dictionary<string, EntityEntry> row)
        => Sources.FirstOrDefault(x => row.ContainsKey(x.EntityTypeName)) is { } source
            ? row[source.EntityTypeName].CurrentValues[source.TargetProperty]
            : null;
}

internal record PropertySource(string EntityTypeName, IProperty SourceProperty, IProperty TargetProperty);
