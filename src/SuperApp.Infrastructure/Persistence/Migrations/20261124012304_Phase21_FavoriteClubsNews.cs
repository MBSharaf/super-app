using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase21_FavoriteClubsNews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_Category",
                schema: "notify",
                table: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NotificationPreferences_Category",
                schema: "notify",
                table: "NotificationPreferences");

            migrationBuilder.CreateTable(
                name: "FavoriteClubs",
                schema: "fantasy",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClubId = table.Column<long>(type: "bigint", nullable: false),
                    Rank = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FavoriteClubs", x => new { x.UserId, x.ClubId });
                    table.CheckConstraint("CK_FavoriteClubs_Rank", "[Rank] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_FavoriteClubs_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalSchema: "fantasy",
                        principalTable: "Clubs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FavoriteClubs_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NewsPosts",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 6000, nullable: false),
                    Language = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    SourceUrl = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    ClubId = table.Column<long>(type: "bigint", nullable: true),
                    Important = table.Column<bool>(type: "bit", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsPosts", x => x.Id);
                    table.CheckConstraint("CK_NewsPosts_Language", "[Language] IN ('ar','en')");
                    table.CheckConstraint("CK_NewsPosts_SourceUrl", "[SourceUrl] IS NULL OR [SourceUrl] LIKE 'https://%'");
                    table.ForeignKey(
                        name: "FK_NewsPosts_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalSchema: "fantasy",
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_Category",
                schema: "notify",
                table: "Notifications",
                sql: "[Category] BETWEEN 0 AND 12");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationPreferences_Category",
                schema: "notify",
                table: "NotificationPreferences",
                sql: "[Category] BETWEEN 0 AND 12");

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteClubs_ClubId",
                schema: "fantasy",
                table: "FavoriteClubs",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "UX_FavoriteClubs_UserRank",
                schema: "fantasy",
                table: "FavoriteClubs",
                columns: new[] { "UserId", "Rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NewsPosts_ClubId",
                schema: "content",
                table: "NewsPosts",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsPosts_Published",
                schema: "content",
                table: "NewsPosts",
                columns: new[] { "IsPublished", "PublishedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FavoriteClubs",
                schema: "fantasy");

            migrationBuilder.DropTable(
                name: "NewsPosts",
                schema: "content");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_Category",
                schema: "notify",
                table: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NotificationPreferences_Category",
                schema: "notify",
                table: "NotificationPreferences");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_Category",
                schema: "notify",
                table: "Notifications",
                sql: "[Category] BETWEEN 0 AND 10");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationPreferences_Category",
                schema: "notify",
                table: "NotificationPreferences",
                sql: "[Category] BETWEEN 0 AND 10");
        }
    }
}
