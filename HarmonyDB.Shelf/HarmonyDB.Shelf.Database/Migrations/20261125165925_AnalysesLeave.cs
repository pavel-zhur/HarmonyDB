using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmonyDB.Shelf.Database.Migrations
{
    /// <inheritdoc />
    public partial class AnalysesLeave : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // the analyses live in their own source now; the deletions of their files in the music service's store, which
            // nobody sends any more, go with them (the files there are dropped by hand)
            migrationBuilder.Sql("DELETE FROM outbox WHERE kind = 'analyses.delete_blob'");

            migrationBuilder.DropTable(
                name: "analysis_contents");

            migrationBuilder.DropTable(
                name: "analysis_files");

            migrationBuilder.DropTable(
                name: "analyses");

            migrationBuilder.DropIndex(
                name: "ix_accounts_system_name",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "system_name",
                table: "accounts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "system_name",
                table: "accounts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "analyses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recording_id = table.Column<Guid>(type: "uuid", nullable: true),
                    artists = table.Column<List<string>>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    duration_seconds = table.Column<double>(type: "double precision", nullable: true),
                    source_id = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_analyses", x => x.id);
                    table.ForeignKey(
                        name: "fk_analyses_accounts_owner_id",
                        column: x => x.owner_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_analyses_recordings_recording_id",
                        column: x => x.recording_id,
                        principalTable: "recordings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "analysis_contents",
                columns: table => new
                {
                    analysis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_analysis_contents", x => new { x.analysis_id, x.kind });
                    table.ForeignKey(
                        name: "fk_analysis_contents_analyses_analysis_id",
                        column: x => x.analysis_id,
                        principalTable: "analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "analysis_files",
                columns: table => new
                {
                    analysis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    blob_key = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_analysis_files", x => new { x.analysis_id, x.name });
                    table.ForeignKey(
                        name: "fk_analysis_files_analyses_analysis_id",
                        column: x => x.analysis_id,
                        principalTable: "analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_system_name",
                table: "accounts",
                column: "system_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_analyses_owner_id",
                table: "analyses",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_analyses_recording_id",
                table: "analyses",
                column: "recording_id");

            migrationBuilder.CreateIndex(
                name: "ix_analyses_source_id",
                table: "analyses",
                column: "source_id",
                unique: true);
        }
    }
}
