using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.OneDog.Database.Migrations
{
    /// <inheritdoc />
    public partial class InteractionsIndexChanged : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Interactions_InteractionType_CreatedOn",
                table: "Interactions");

            migrationBuilder.CreateIndex(
                name: "IX_Interactions_InteractionType_DomainId_CreatedOn",
                table: "Interactions",
                columns: new[] { "InteractionType", "DomainId", "CreatedOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Interactions_InteractionType_DomainId_CreatedOn",
                table: "Interactions");

            migrationBuilder.CreateIndex(
                name: "IX_Interactions_InteractionType_CreatedOn",
                table: "Interactions",
                columns: new[] { "InteractionType", "CreatedOn" });
        }
    }
}
