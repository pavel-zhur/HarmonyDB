using Microsoft.EntityFrameworkCore.Migrations;

namespace HarmonyDB.Shelf.Database.Migrations;

/// <summary>
/// The version of <see cref="Model.IVersioned"/> rows is written by the database. A migration that adds a versioned
/// table calls <see cref="AddVersionTrigger"/> for it.
/// </summary>
public static class VersionTriggerMigrationExtensions
{
    private const string FunctionName = "set_row_version";

    /// <summary>
    /// The function the triggers call: the row's version becomes the id of the transaction writing it.
    /// </summary>
    public static void CreateVersionFunction(this MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(
            $"""
             CREATE FUNCTION {FunctionName}() RETURNS trigger LANGUAGE plpgsql AS $$
             BEGIN
                 NEW.version := pg_current_xact_id()::text::bigint;
                 RETURN NEW;
             END
             $$;
             """);

    public static void DropVersionFunction(this MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql($"DROP FUNCTION {FunctionName}();");

    public static void AddVersionTrigger(this MigrationBuilder migrationBuilder, string table)
        => migrationBuilder.Sql(
            $"""
             CREATE TRIGGER {table}_version BEFORE INSERT OR UPDATE ON {table}
             FOR EACH ROW EXECUTE FUNCTION {FunctionName}();
             """);

    public static void DropVersionTrigger(this MigrationBuilder migrationBuilder, string table)
        => migrationBuilder.Sql($"DROP TRIGGER {table}_version ON {table};");
}
