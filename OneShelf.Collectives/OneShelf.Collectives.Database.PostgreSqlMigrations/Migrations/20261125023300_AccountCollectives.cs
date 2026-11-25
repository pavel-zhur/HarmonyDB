using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Collectives.Database.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AccountCollectives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountCollectives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Versions = table.Column<string>(type: "jsonb", nullable: false),
                    LatestVisibility = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountCollectives", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountCollectives_AccountId",
                table: "AccountCollectives",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountCollectives_LatestVisibility",
                table: "AccountCollectives",
                column: "LatestVisibility");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountCollectives");
        }
    }
}
