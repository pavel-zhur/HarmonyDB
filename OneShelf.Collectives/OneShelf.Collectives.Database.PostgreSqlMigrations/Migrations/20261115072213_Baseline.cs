using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Collectives.Database.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class Baseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Collectives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    Versions = table.Column<string>(type: "jsonb", nullable: false),
                    LatestVisibility = table.Column<int>(type: "integer", nullable: false),
                    DerivedFromUri = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collectives", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Collectives_CreatedByUserId",
                table: "Collectives",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Collectives_LatestVisibility",
                table: "Collectives",
                column: "LatestVisibility");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Collectives");
        }
    }
}
