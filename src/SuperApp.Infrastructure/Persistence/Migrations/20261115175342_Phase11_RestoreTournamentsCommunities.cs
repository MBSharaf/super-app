using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase11_RestoreTournamentsCommunities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "community");

            migrationBuilder.CreateTable(
                name: "BannedWords",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Word = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Language = table.Column<string>(type: "char(2)", unicode: false, fixedLength: true, maxLength: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BannedWords", x => x.Id);
                    table.CheckConstraint("CK_BannedWords_Language", "[Language] IN ('ar', 'en')");
                });

            migrationBuilder.CreateTable(
                name: "Communities",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Slug = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    About = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Audience = table.Column<byte>(type: "tinyint", nullable: false),
                    RulesText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SlowModeSeconds = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Color = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    Icon = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Communities", x => x.Id);
                    table.CheckConstraint("CK_Communities_Audience", "[Audience] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_Communities_SlowModeSeconds", "[SlowModeSeconds] BETWEEN 0 AND 3600");
                    table.ForeignKey(
                        name: "FK_Communities_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CommunityMembers",
                schema: "community",
                columns: table => new
                {
                    CommunityId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    MutedUntil = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    MuteReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityMembers", x => new { x.CommunityId, x.UserId });
                    table.CheckConstraint("CK_CommunityMembers_Role", "[Role] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_CommunityMembers_Communities_CommunityId",
                        column: x => x.CommunityId,
                        principalSchema: "community",
                        principalTable: "Communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommunityMembers_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Posts",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommunityId = table.Column<long>(type: "bigint", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AsSuperAppAdmin = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ImageFileId = table.Column<long>(type: "bigint", nullable: true),
                    HasPoll = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsPinned = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsAnnouncement = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CommentsClosed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsHidden = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CommentsCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ReactionsCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Posts", x => x.Id);
                    table.CheckConstraint("CK_Posts_Text", "LEN([Text]) >= 1");
                    table.ForeignKey(
                        name: "FK_Posts_Communities_CommunityId",
                        column: x => x.CommunityId,
                        principalSchema: "community",
                        principalTable: "Communities",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Posts_Files_ImageFileId",
                        column: x => x.ImageFileId,
                        principalSchema: "ops",
                        principalTable: "Files",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Posts_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Comments",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostId = table.Column<long>(type: "bigint", nullable: false),
                    ParentCommentId = table.Column<long>(type: "bigint", nullable: true),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AsSuperAppAdmin = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Text = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsHidden = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comments_Comments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalSchema: "community",
                        principalTable: "Comments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Comments_Posts_PostId",
                        column: x => x.PostId,
                        principalSchema: "community",
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Comments_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PollOptions",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostId = table.Column<long>(type: "bigint", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    VotesCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollOptions", x => x.Id);
                    table.CheckConstraint("CK_PollOptions_SortOrder", "[SortOrder] <= 5");
                    table.ForeignKey(
                        name: "FK_PollOptions_Posts_PostId",
                        column: x => x.PostId,
                        principalSchema: "community",
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentReports",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReporterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostId = table.Column<long>(type: "bigint", nullable: true),
                    CommentId = table.Column<long>(type: "bigint", nullable: true),
                    Reason = table.Column<byte>(type: "tinyint", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    HandledById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentReports", x => x.Id);
                    table.CheckConstraint("CK_ContentReports_OneTarget", "([PostId] IS NULL AND [CommentId] IS NOT NULL) OR ([PostId] IS NOT NULL AND [CommentId] IS NULL)");
                    table.CheckConstraint("CK_ContentReports_Reason", "[Reason] BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_ContentReports_Status", "[Status] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_ContentReports_Comments_CommentId",
                        column: x => x.CommentId,
                        principalSchema: "community",
                        principalTable: "Comments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContentReports_Posts_PostId",
                        column: x => x.PostId,
                        principalSchema: "community",
                        principalTable: "Posts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContentReports_Users_HandledById",
                        column: x => x.HandledById,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContentReports_Users_ReporterId",
                        column: x => x.ReporterId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Reactions",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostId = table.Column<long>(type: "bigint", nullable: true),
                    CommentId = table.Column<long>(type: "bigint", nullable: true),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reactions", x => x.Id);
                    table.CheckConstraint("CK_Reactions_OneTarget", "([PostId] IS NULL AND [CommentId] IS NOT NULL) OR ([PostId] IS NOT NULL AND [CommentId] IS NULL)");
                    table.CheckConstraint("CK_Reactions_Type", "[Type] BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_Reactions_Comments_CommentId",
                        column: x => x.CommentId,
                        principalSchema: "community",
                        principalTable: "Comments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reactions_Posts_PostId",
                        column: x => x.PostId,
                        principalSchema: "community",
                        principalTable: "Posts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reactions_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PollVotes",
                schema: "community",
                columns: table => new
                {
                    OptionId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollVotes", x => new { x.OptionId, x.UserId });
                    table.ForeignKey(
                        name: "FK_PollVotes_PollOptions_OptionId",
                        column: x => x.OptionId,
                        principalSchema: "community",
                        principalTable: "PollOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PollVotes_Posts_PostId",
                        column: x => x.PostId,
                        principalSchema: "community",
                        principalTable: "Posts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PollVotes_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TournamentMatches",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<long>(type: "bigint", nullable: false),
                    Round = table.Column<byte>(type: "tinyint", nullable: false),
                    HomeTeamId = table.Column<long>(type: "bigint", nullable: true),
                    AwayTeamId = table.Column<long>(type: "bigint", nullable: true),
                    BookingId = table.Column<long>(type: "bigint", nullable: true),
                    HomeGoals = table.Column<byte>(type: "tinyint", nullable: true),
                    AwayGoals = table.Column<byte>(type: "tinyint", nullable: true),
                    HomePens = table.Column<byte>(type: "tinyint", nullable: true),
                    AwayPens = table.Column<byte>(type: "tinyint", nullable: true),
                    WinnerTeamId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentMatches", x => x.Id);
                    table.CheckConstraint("CK_TournamentMatches_Round", "[Round] >= 1");
                    table.CheckConstraint("CK_TournamentMatches_Teams", "[HomeTeamId] IS NULL OR [AwayTeamId] IS NULL OR [HomeTeamId] <> [AwayTeamId]");
                    table.ForeignKey(
                        name: "FK_TournamentMatches_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "booking",
                        principalTable: "Bookings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Tournaments",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerId = table.Column<long>(type: "bigint", nullable: false),
                    PitchId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    TeamCount = table.Column<byte>(type: "tinyint", nullable: false),
                    EntryFee = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    PrizeText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    MatchHours = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    RulesText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    ChampionTeamId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournaments", x => x.Id);
                    table.CheckConstraint("CK_Tournaments_Dates", "[EndsOn] >= [StartsOn]");
                    table.CheckConstraint("CK_Tournaments_EntryFee", "[EntryFee] >= 0");
                    table.CheckConstraint("CK_Tournaments_Status", "[Status] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_Tournaments_TeamCount", "([Type] = 0 AND [TeamCount] IN (4, 8, 16)) OR ([Type] = 1 AND [TeamCount] BETWEEN 3 AND 10)");
                    table.CheckConstraint("CK_Tournaments_Type", "[Type] BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_Tournaments_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalSchema: "pitch",
                        principalTable: "Partners",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tournaments_Pitches_PitchId",
                        column: x => x.PitchId,
                        principalSchema: "pitch",
                        principalTable: "Pitches",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TournamentTeams",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CaptainId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Phone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    PaymentId = table.Column<long>(type: "bigint", nullable: true),
                    WaiverVersionId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentTeams", x => x.Id);
                    table.CheckConstraint("CK_TournamentTeams_Status", "[Status] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_TournamentTeams_LegalVersions_WaiverVersionId",
                        column: x => x.WaiverVersionId,
                        principalSchema: "content",
                        principalTable: "LegalVersions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TournamentTeams_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "money",
                        principalTable: "Payments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TournamentTeams_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalSchema: "booking",
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TournamentTeams_Users_CaptainId",
                        column: x => x.CaptainId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "UQ_BannedWords_Word",
                schema: "community",
                table: "BannedWords",
                column: "Word",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Comments_AuthorId",
                schema: "community",
                table: "Comments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ParentCommentId",
                schema: "community",
                table: "Comments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_PostId",
                schema: "community",
                table: "Comments",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_Communities_CreatedById",
                schema: "community",
                table: "Communities",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "UQ_Communities_Slug",
                schema: "community",
                table: "Communities",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityMembers_UserId",
                schema: "community",
                table: "CommunityMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_CommentId",
                schema: "community",
                table: "ContentReports",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_HandledById",
                schema: "community",
                table: "ContentReports",
                column: "HandledById");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_PostId",
                schema: "community",
                table: "ContentReports",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_ReporterId",
                schema: "community",
                table: "ContentReports",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_PollOptions_PostId",
                schema: "community",
                table: "PollOptions",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_PollVotes_UserId",
                schema: "community",
                table: "PollVotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_PollVotes_PostId_UserId",
                schema: "community",
                table: "PollVotes",
                columns: new[] { "PostId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Posts_AuthorId",
                schema: "community",
                table: "Posts",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_CommunityId_CreatedAt",
                schema: "community",
                table: "Posts",
                columns: new[] { "CommunityId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_ImageFileId",
                schema: "community",
                table: "Posts",
                column: "ImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Reactions_CommentId",
                schema: "community",
                table: "Reactions",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_Reactions_PostId",
                schema: "community",
                table: "Reactions",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "UQ_Reactions_Comment",
                schema: "community",
                table: "Reactions",
                columns: new[] { "UserId", "CommentId" },
                unique: true,
                filter: "[CommentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_Reactions_Post",
                schema: "community",
                table: "Reactions",
                columns: new[] { "UserId", "PostId" },
                unique: true,
                filter: "[PostId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_AwayTeamId",
                schema: "booking",
                table: "TournamentMatches",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_BookingId",
                schema: "booking",
                table: "TournamentMatches",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_HomeTeamId",
                schema: "booking",
                table: "TournamentMatches",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_TournamentId",
                schema: "booking",
                table: "TournamentMatches",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_WinnerTeamId",
                schema: "booking",
                table: "TournamentMatches",
                column: "WinnerTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_ChampionTeamId",
                schema: "booking",
                table: "Tournaments",
                column: "ChampionTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_PartnerId",
                schema: "booking",
                table: "Tournaments",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_PitchId",
                schema: "booking",
                table: "Tournaments",
                column: "PitchId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentTeams_CaptainId",
                schema: "booking",
                table: "TournamentTeams",
                column: "CaptainId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentTeams_PaymentId",
                schema: "booking",
                table: "TournamentTeams",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentTeams_WaiverVersionId",
                schema: "booking",
                table: "TournamentTeams",
                column: "WaiverVersionId");

            migrationBuilder.CreateIndex(
                name: "UQ_TournamentTeams_TournamentId_Name",
                schema: "booking",
                table: "TournamentTeams",
                columns: new[] { "TournamentId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentMatches_TournamentTeams_AwayTeamId",
                schema: "booking",
                table: "TournamentMatches",
                column: "AwayTeamId",
                principalSchema: "booking",
                principalTable: "TournamentTeams",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentMatches_TournamentTeams_HomeTeamId",
                schema: "booking",
                table: "TournamentMatches",
                column: "HomeTeamId",
                principalSchema: "booking",
                principalTable: "TournamentTeams",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentMatches_TournamentTeams_WinnerTeamId",
                schema: "booking",
                table: "TournamentMatches",
                column: "WinnerTeamId",
                principalSchema: "booking",
                principalTable: "TournamentTeams",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentMatches_Tournaments_TournamentId",
                schema: "booking",
                table: "TournamentMatches",
                column: "TournamentId",
                principalSchema: "booking",
                principalTable: "Tournaments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tournaments_TournamentTeams_ChampionTeamId",
                schema: "booking",
                table: "Tournaments",
                column: "ChampionTeamId",
                principalSchema: "booking",
                principalTable: "TournamentTeams",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tournaments_TournamentTeams_ChampionTeamId",
                schema: "booking",
                table: "Tournaments");

            migrationBuilder.DropTable(
                name: "BannedWords",
                schema: "community");

            migrationBuilder.DropTable(
                name: "CommunityMembers",
                schema: "community");

            migrationBuilder.DropTable(
                name: "ContentReports",
                schema: "community");

            migrationBuilder.DropTable(
                name: "PollVotes",
                schema: "community");

            migrationBuilder.DropTable(
                name: "Reactions",
                schema: "community");

            migrationBuilder.DropTable(
                name: "TournamentMatches",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "PollOptions",
                schema: "community");

            migrationBuilder.DropTable(
                name: "Comments",
                schema: "community");

            migrationBuilder.DropTable(
                name: "Posts",
                schema: "community");

            migrationBuilder.DropTable(
                name: "Communities",
                schema: "community");

            migrationBuilder.DropTable(
                name: "TournamentTeams",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "Tournaments",
                schema: "booking");
        }
    }
}
