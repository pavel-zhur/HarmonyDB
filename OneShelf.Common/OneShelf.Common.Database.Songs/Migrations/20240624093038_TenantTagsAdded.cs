using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Common.Database.Songs.Migrations
{
    /// <inheritdoc />
    public partial class TenantTagsAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Tenants");
        }
    }
}
