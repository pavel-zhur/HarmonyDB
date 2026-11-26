using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmonyDB.Analyses.Database.Migrations
{
    /// <inheritdoc />
    public partial class NamedFilesWithTheirKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "stored_name",
                table: "analysis_files",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "stored_at",
                table: "analysis_files",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<long>(
                name: "size",
                table: "analysis_files",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "sha256",
                table: "analysis_files",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            // the kind that names each kept file, read once from the contents; then the files named and not yet kept
            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "analysis_files",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE analysis_files f
                SET kind = CASE WHEN EXISTS (
                        SELECT 1 FROM analysis_contents c
                        WHERE c.analysis_id = f.analysis_id AND c.kind = 'notes' AND c.content #>> '{midi,name}' = f.name)
                    THEN 'notes' ELSE 'recording' END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "kind",
                table: "analysis_files",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.Sql("""
                INSERT INTO analysis_files (analysis_id, name, kind, content_type)
                SELECT DISTINCT ON (c.analysis_id, named.file ->> 'name') c.analysis_id, named.file ->> 'name', c.kind, named.file ->> 'contentType'
                FROM analysis_contents c
                CROSS JOIN LATERAL (
                    SELECT jsonb_path_query(c.content, 'lax $.audio.file') AS file WHERE c.kind = 'recording'
                    UNION ALL SELECT jsonb_path_query(c.content, 'lax $.stems[*].audio.file') WHERE c.kind = 'recording'
                    UNION ALL SELECT jsonb_path_query(c.content, 'lax $.midi') WHERE c.kind = 'notes') named
                WHERE jsonb_typeof(named.file) = 'object'
                  AND NOT EXISTS (SELECT 1 FROM analysis_files f WHERE f.analysis_id = c.analysis_id AND f.name = named.file ->> 'name');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM analysis_files WHERE stored_name IS NULL;");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "analysis_files");

            migrationBuilder.AlterColumn<string>(
                name: "stored_name",
                table: "analysis_files",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "stored_at",
                table: "analysis_files",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "size",
                table: "analysis_files",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "sha256",
                table: "analysis_files",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
