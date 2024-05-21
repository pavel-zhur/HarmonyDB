using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Common.Database.Songs.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SameSongGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SameSongGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrivateDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LatestUsedIndex = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Artists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CategoryOverride = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Artists_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsAuthorizedToUseIllustrations = table.Column<bool>(type: "bit", nullable: false),
                    AuthorizedToUseIllustrationAlterationsTemporarilySince = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsAuthorizedToUseIllustrationAlterationsPermanently = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ArtistSynonyms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArtistId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtistSynonyms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArtistSynonyms_Artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "Artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BillingUsages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UseCase = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    InputTokens = table.Column<int>(type: "int", nullable: true),
                    OutputTokens = table.Column<int>(type: "int", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdditionalInfo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DomainId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillingUsages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Interactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ShortInfoSerialized = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Serialized = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InteractionType = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Interactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Interactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LikeCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CssColor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CssIcon = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    PrivateWeight = table.Column<float>(type: "real", nullable: false),
                    Access = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LikeCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LikeCategories_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LikeCategories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Songs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Index = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdditionalKeywords = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceUniqueIdentifier = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UnparsedTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CategoryOverride = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SameSongGroupId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RedirectToSongId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Songs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Songs_SameSongGroups_SameSongGroupId",
                        column: x => x.SameSongGroupId,
                        principalTable: "SameSongGroups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Songs_Songs_RedirectToSongId",
                        column: x => x.RedirectToSongId,
                        principalTable: "Songs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Songs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Songs_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VisitedSearches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SearchedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Query = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitedSearches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisitedSearches_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArtistSong",
                columns: table => new
                {
                    ArtistsId = table.Column<int>(type: "int", nullable: false),
                    SongsId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtistSong", x => new { x.ArtistsId, x.SongsId });
                    table.ForeignKey(
                        name: "FK_ArtistSong_Artists_ArtistsId",
                        column: x => x.ArtistsId,
                        principalTable: "Artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArtistSong_Songs_SongsId",
                        column: x => x.SongsId,
                        principalTable: "Songs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    MessageId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ArtistId = table.Column<int>(type: "int", nullable: true),
                    Hash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Part = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    SongId = table.Column<int>(type: "int", nullable: true),
                    FileId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "Artists",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Messages_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Messages_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SongContents",
                columns: table => new
                {
                    SongId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SongContents", x => x.SongId);
                    table.ForeignKey(
                        name: "FK_SongContents_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Versions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SongId = table.Column<int>(type: "int", nullable: false),
                    Uri = table.Column<string>(type: "nvarchar(1000)", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedSettings = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CollectiveType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CollectiveSearchTag = table.Column<int>(type: "int", nullable: true),
                    CollectiveId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Versions", x => x.Id);
                    table.UniqueConstraint("AK_Versions_Id_SongId", x => new { x.Id, x.SongId });
                    table.ForeignKey(
                        name: "FK_Versions_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Versions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VisitedChords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ViewedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Uri = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SongId = table.Column<int>(type: "int", nullable: true),
                    ExternalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SearchQuery = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Transpose = table.Column<int>(type: "int", nullable: true),
                    Artists = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitedChords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisitedChords_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VisitedChords_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Comments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    SongId = table.Column<int>(type: "int", nullable: false),
                    VersionId = table.Column<int>(type: "int", nullable: true),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comments_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Comments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Comments_Versions_VersionId_SongId",
                        columns: x => new { x.VersionId, x.SongId },
                        principalTable: "Versions",
                        principalColumns: new[] { "Id", "SongId" });
                });

            migrationBuilder.CreateTable(
                name: "Likes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    SongId = table.Column<int>(type: "int", nullable: false),
                    VersionId = table.Column<int>(type: "int", nullable: true),
                    Level = table.Column<byte>(type: "tinyint", nullable: false),
                    Transpose = table.Column<int>(type: "int", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LikeCategoryId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Likes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Likes_LikeCategories_LikeCategoryId",
                        column: x => x.LikeCategoryId,
                        principalTable: "LikeCategories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Likes_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Likes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Likes_Versions_VersionId_SongId",
                        columns: x => new { x.VersionId, x.SongId },
                        principalTable: "Versions",
                        principalColumns: new[] { "Id", "SongId" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_Artists_Name",
                table: "Artists",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Artists_TenantId",
                table: "Artists",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtistSong_SongsId",
                table: "ArtistSong",
                column: "SongsId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtistSynonyms_ArtistId_Title",
                table: "ArtistSynonyms",
                columns: new[] { "ArtistId", "Title" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillingUsages_DomainId",
                table: "BillingUsages",
                column: "DomainId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingUsages_UserId",
                table: "BillingUsages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_SongId",
                table: "Comments",
                column: "SongId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_UserId_SongId_VersionId",
                table: "Comments",
                columns: new[] { "UserId", "SongId", "VersionId" },
                unique: true,
                filter: "[VersionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_VersionId_SongId",
                table: "Comments",
                columns: new[] { "VersionId", "SongId" });

            migrationBuilder.CreateIndex(
                name: "IX_Interactions_InteractionType_CreatedOn",
                table: "Interactions",
                columns: new[] { "InteractionType", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Interactions_UserId",
                table: "Interactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LikeCategories_TenantId",
                table: "LikeCategories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LikeCategories_UserId_Name",
                table: "LikeCategories",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Likes_LikeCategoryId",
                table: "Likes",
                column: "LikeCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Likes_SongId",
                table: "Likes",
                column: "SongId");

            migrationBuilder.CreateIndex(
                name: "IX_Likes_SongId_UserId_LikeCategoryId",
                table: "Likes",
                columns: new[] { "SongId", "UserId", "LikeCategoryId" },
                unique: true,
                filter: "[VersionId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Likes_UserId_SongId_VersionId_LikeCategoryId",
                table: "Likes",
                columns: new[] { "UserId", "SongId", "VersionId", "LikeCategoryId" },
                unique: true,
                filter: "[VersionId] IS NOT NULL AND [LikeCategoryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Likes_VersionId_SongId",
                table: "Likes",
                columns: new[] { "VersionId", "SongId" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ArtistId",
                table: "Messages",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SongId",
                table: "Messages",
                column: "SongId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_TenantId",
                table: "Messages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_Type_ArtistId_Category_Part_SongId",
                table: "Messages",
                columns: new[] { "Type", "ArtistId", "Category", "Part", "SongId" },
                unique: true,
                filter: "[ArtistId] IS NOT NULL AND [Category] IS NOT NULL AND [Part] IS NOT NULL AND [SongId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Songs_CreatedByUserId",
                table: "Songs",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Songs_Index",
                table: "Songs",
                column: "Index");

            migrationBuilder.CreateIndex(
                name: "IX_Songs_RedirectToSongId",
                table: "Songs",
                column: "RedirectToSongId");

            migrationBuilder.CreateIndex(
                name: "IX_Songs_SameSongGroupId",
                table: "Songs",
                column: "SameSongGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Songs_SourceUniqueIdentifier",
                table: "Songs",
                column: "SourceUniqueIdentifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Songs_TenantId",
                table: "Songs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId",
                table: "Users",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Versions_CollectiveId",
                table: "Versions",
                column: "CollectiveId",
                unique: true,
                filter: "[CollectiveId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Versions_CollectiveSearchTag",
                table: "Versions",
                column: "CollectiveSearchTag");

            migrationBuilder.CreateIndex(
                name: "IX_Versions_SongId_Uri",
                table: "Versions",
                columns: new[] { "SongId", "Uri" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Versions_Uri",
                table: "Versions",
                column: "Uri");

            migrationBuilder.CreateIndex(
                name: "IX_Versions_UserId",
                table: "Versions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitedChords_SongId",
                table: "VisitedChords",
                column: "SongId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitedChords_UserId",
                table: "VisitedChords",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitedSearches_UserId",
                table: "VisitedSearches",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArtistSong");

            migrationBuilder.DropTable(
                name: "ArtistSynonyms");

            migrationBuilder.DropTable(
                name: "BillingUsages");

            migrationBuilder.DropTable(
                name: "Comments");

            migrationBuilder.DropTable(
                name: "Interactions");

            migrationBuilder.DropTable(
                name: "Likes");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "SongContents");

            migrationBuilder.DropTable(
                name: "VisitedChords");

            migrationBuilder.DropTable(
                name: "VisitedSearches");

            migrationBuilder.DropTable(
                name: "LikeCategories");

            migrationBuilder.DropTable(
                name: "Versions");

            migrationBuilder.DropTable(
                name: "Artists");

            migrationBuilder.DropTable(
                name: "Songs");

            migrationBuilder.DropTable(
                name: "SameSongGroups");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
