using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmonyDB.Shelf.Database.Migrations
{
    /// <inheritdoc />
    public partial class AnalysesIdPrefix : Migration
    {
        // the analyses' ids as a source's items, where they are kept, and the source's picture, named as they are
        private static readonly (string Table, string Column)[] Columns =
        [
            ("analyses", "source_id"),
            ("chords", "source_id"),
            ("chords", "source_icon"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // the analyses' id prefix is "ha-", no longer "analysis-"
            foreach (var (table, column) in Columns)
                migrationBuilder.Sql($"UPDATE {table} SET {column} = 'ha-' || substr({column}, 10) WHERE left({column}, 9) = 'analysis-'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, column) in Columns)
                migrationBuilder.Sql($"UPDATE {table} SET {column} = 'analysis-' || substr({column}, 4) WHERE left({column}, 3) = 'ha-'");
        }
    }
}
