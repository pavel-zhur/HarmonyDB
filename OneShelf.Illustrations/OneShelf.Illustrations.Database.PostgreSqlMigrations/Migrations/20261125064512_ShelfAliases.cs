using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Illustrations.Database.PostgreSqlMigrations.Migrations
{
    /// <summary>
    /// The aliases of the new shelf's keys (chords ids) to the old addresses the illustrations are kept under: while both
    /// shelves run, an item has two names. This goes when the old shelf is retired: the rows are then re-keyed to the ids
    /// and the table dropped.
    /// </summary>
    public partial class ShelfAliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShelfAliases",
                columns: table => new
                {
                    Key = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShelfAliases", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShelfAliases_Url",
                table: "ShelfAliases",
                column: "Url");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShelfAliases");
        }
    }
}
