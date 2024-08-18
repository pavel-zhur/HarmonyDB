using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.OneDragon.Database.Migrations
{
    /// <inheritdoc />
    public partial class AllUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UpdateId",
                table: "Interactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Updates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Json = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Updates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Interactions_UpdateId",
                table: "Interactions",
                column: "UpdateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Interactions_Updates_UpdateId",
                table: "Interactions",
                column: "UpdateId",
                principalTable: "Updates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Interactions_Updates_UpdateId",
                table: "Interactions");

            migrationBuilder.DropTable(
                name: "Updates");

            migrationBuilder.DropIndex(
                name: "IX_Interactions_UpdateId",
                table: "Interactions");

            migrationBuilder.DropColumn(
                name: "UpdateId",
                table: "Interactions");
        }
    }
}
