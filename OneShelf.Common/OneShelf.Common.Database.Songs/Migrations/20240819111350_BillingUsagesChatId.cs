using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Common.Database.Songs.Migrations
{
    /// <inheritdoc />
    public partial class BillingUsagesChatId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BillingUsages_DomainId",
                table: "BillingUsages");

            migrationBuilder.AddColumn<long>(
                name: "ChatId",
                table: "BillingUsages",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillingUsages_DomainId_ChatId_UserId",
                table: "BillingUsages",
                columns: new[] { "DomainId", "ChatId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_BillingUsages_DomainId_UserId",
                table: "BillingUsages",
                columns: new[] { "DomainId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BillingUsages_DomainId_ChatId_UserId",
                table: "BillingUsages");

            migrationBuilder.DropIndex(
                name: "IX_BillingUsages_DomainId_UserId",
                table: "BillingUsages");

            migrationBuilder.DropColumn(
                name: "ChatId",
                table: "BillingUsages");

            migrationBuilder.CreateIndex(
                name: "IX_BillingUsages_DomainId",
                table: "BillingUsages",
                column: "DomainId");
        }
    }
}
