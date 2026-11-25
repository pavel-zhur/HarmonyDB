using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmonyDB.Shelf.Database.Migrations
{
    /// <inheritdoc />
    public partial class JobsNotBefore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "not_before",
                table: "jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_jobs_not_before",
                table: "jobs",
                column: "not_before",
                filter: "state = 'queued' AND not_before IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_jobs_not_before",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "not_before",
                table: "jobs");
        }
    }
}
