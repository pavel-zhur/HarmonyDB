using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace OneShelf.Common.Database;

public static class ModelBuilderExtensions
{
    /// <summary>
    /// The PostgreSQL collation of the <see cref="CaseInsensitiveAttribute"/> columns: case-insensitive, accents still count.
    /// It lives in the schema of the context, made by its migrations.
    /// </summary>
    public const string CaseInsensitiveCollation = "case_insensitive";

    private static readonly Regex NVarChar = new(@"^nvarchar\((\d+|max)\)$", RegexOptions.IgnoreCase);

    // SQL Server keeps any DateTime as it is, whatever its Kind; PostgreSQL's timestamp only takes Local and Unspecified.
    private static readonly ValueConverter<DateTime, DateTime> AnyKind = new(
        x => DateTime.SpecifyKind(x, DateTimeKind.Unspecified),
        x => x);

    /// <summary>
    /// Makes a model written for SQL Server fit PostgreSQL. Call at the end of OnModelCreating, for PostgreSQL only.
    /// - The column types given for SQL Server (nvarchar(n), nvarchar(max)) become character varying(n) and text.
    /// - DateTime is stored as timestamp without time zone, as datetime2 keeps it: the local time the code writes,
    ///   any DateTimeKind accepted, read back as Unspecified.
    /// - The <see cref="CaseInsensitiveAttribute"/> columns get the case-insensitive collation.
    /// </summary>
    public static void ApplyPostgreSqlStoreTypes(this ModelBuilder modelBuilder)
    {
        var properties = modelBuilder.Model.GetEntityTypes().SelectMany(x => x.GetDeclaredProperties()).ToList();

        foreach (var property in properties)
        {
            if (property.PropertyInfo?.GetCustomAttribute<CaseInsensitiveAttribute>() != null)
            {
                property.SetCollation(CaseInsensitiveCollation);
            }

            if (property.GetColumnType() is { } columnType)
            {
                if (NVarChar.Match(columnType) is { Success: true } match)
                {
                    property.SetColumnType(match.Groups[1].Value.Equals("max", StringComparison.OrdinalIgnoreCase)
                        ? "text"
                        : $"character varying({match.Groups[1].Value})");
                }

                continue;
            }

            if ((property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                && property.GetValueConverter() == null)
            {
                property.SetColumnType("timestamp without time zone");
                property.SetValueConverter(AnyKind);
            }
        }

        if (properties.Any(x => x.GetCollation() == CaseInsensitiveCollation))
        {
            modelBuilder.HasCollation(CaseInsensitiveCollation, locale: "und-u-ks-level2", provider: "icu", deterministic: false);
        }
    }
}
