using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Common.Database.Songs.Migrations
{
    /// <inheritdoc />
    public partial class GetNextSongIndexAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"

CREATE PROCEDURE [GetNextSongIndex]
	@tenantid int
AS
BEGIN
	declare @nextindex int
    select @nextindex = latestusedindex + 1 from tenants with (xlock) where id = @tenantid
	update tenants set latestusedindex = @nextindex where id = @tenantid
	select @nextindex
END

");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("drop procedure [GetNextSongIndex]");
        }
    }
}
