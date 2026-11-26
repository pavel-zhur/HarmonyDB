using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmonyDB.Shelf.Database.Migrations
{
    /// <inheritdoc />
    public partial class AccountMerges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_merges",
                columns: table => new
                {
                    merged_id = table.Column<Guid>(type: "uuid", nullable: false),
                    into_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account_merges", x => x.merged_id);
                    table.ForeignKey(
                        name: "fk_account_merges_accounts_into_id",
                        column: x => x.into_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_account_merges_into_id",
                table: "account_merges",
                column: "into_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_merges");
        }
    }
}
