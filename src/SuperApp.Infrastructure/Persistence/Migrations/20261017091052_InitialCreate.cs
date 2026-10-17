using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "money");

            migrationBuilder.EnsureSchema(
                name: "ai");

            migrationBuilder.EnsureSchema(
                name: "auth");

            migrationBuilder.EnsureSchema(
                name: "geo");

            migrationBuilder.EnsureSchema(
                name: "ops");

            migrationBuilder.EnsureSchema(
                name: "community");

            migrationBuilder.EnsureSchema(
                name: "content");

            migrationBuilder.EnsureSchema(
                name: "booking");

            migrationBuilder.EnsureSchema(
                name: "notify");

            migrationBuilder.EnsureSchema(
                name: "pitch");

            migrationBuilder.EnsureSchema(
                name: "shop");

            migrationBuilder.EnsureSchema(
                name: "pod");

            migrationBuilder.EnsureSchema(
                name: "fantasy");

            migrationBuilder.EnsureSchema(
                name: "factory");

            migrationBuilder.EnsureSchema(
                name: "ship");

            migrationBuilder.EnsureSchema(
                name: "support");

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
                name: "Banners",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(70)", maxLength: 70, nullable: false),
                    Badge = table.Column<string>(type: "nvarchar(22)", maxLength: 22, nullable: true),
                    CtaText = table.Column<string>(type: "nvarchar(22)", maxLength: 22, nullable: true),
                    Link = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ImageKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ShowFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ShowUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banners", x => x.Id);
                    table.CheckConstraint("CK_Banners_Dates", "[ShowUntil] IS NULL OR [ShowFrom] IS NULL OR [ShowUntil] >= [ShowFrom]");
                    table.CheckConstraint("CK_Banners_Link", "[Link] LIKE '#/%'");
                    table.CheckConstraint("CK_Banners_Title", "LEN([Title]) >= 5");
                });

            migrationBuilder.CreateTable(
                name: "CancellationPolicies",
                schema: "pitch",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FullRefundHours = table.Column<short>(type: "smallint", nullable: false),
                    HalfRefundHours = table.Column<short>(type: "smallint", nullable: false),
                    SummaryEn = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SummaryAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CancellationPolicies", x => x.Id);
                    table.CheckConstraint("CK_CancellationPolicies_FullRefundHours", "[FullRefundHours] >= 0");
                    table.CheckConstraint("CK_CancellationPolicies_HalfRefundHours", "[HalfRefundHours] >= 0 AND [HalfRefundHours] <= [FullRefundHours]");
                });

            migrationBuilder.CreateTable(
                name: "CatalogProducts",
                schema: "pod",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    DescriptionEn = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DescriptionAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    FactoryCost = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    BackPrintExtra = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    BigSizeExtra = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    MaxPrice = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    HasBackPrint = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Model3dUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogProducts", x => x.Id);
                    table.CheckConstraint("CK_CatalogProducts_BackPrintExtra", "[BackPrintExtra] >= 0");
                    table.CheckConstraint("CK_CatalogProducts_BasePrice", "[BasePrice] > 0");
                    table.CheckConstraint("CK_CatalogProducts_BigSizeExtra", "[BigSizeExtra] >= 0");
                    table.CheckConstraint("CK_CatalogProducts_FactoryCost", "[FactoryCost] > 0 AND [FactoryCost] < [BasePrice]");
                    table.CheckConstraint("CK_CatalogProducts_Kind", "[Kind] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_CatalogProducts_MaxPrice", "[MaxPrice] > [BasePrice]");
                    table.CheckConstraint("CK_CatalogProducts_Status", "[Status] BETWEEN 0 AND 2");
                });

            migrationBuilder.CreateTable(
                name: "Clubs",
                schema: "fantasy",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExternalId = table.Column<int>(type: "int", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ShortName = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: false),
                    KitColor = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: false, defaultValue: "#0D1411"),
                    KitColor2 = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: false, defaultValue: "#FFFFFF"),
                    LogoUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clubs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Competitions",
                schema: "fantasy",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ExternalLeagueId = table.Column<int>(type: "int", nullable: false),
                    Country = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SquadBudget = table.Column<decimal>(type: "decimal(6,1)", precision: 6, scale: 1, nullable: false, defaultValue: 100.0m),
                    MaxPerClub = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)3),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Competitions", x => x.Id);
                    table.CheckConstraint("CK_Competitions_MaxPerClub", "[MaxPerClub] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_Competitions_SquadBudget", "[SquadBudget] > 0");
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Color = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: false, defaultValue: "#0B6E4F"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                    table.CheckConstraint("CK_Departments_Color", "[Color] LIKE '#%'");
                });

            migrationBuilder.CreateTable(
                name: "DesignCategories",
                schema: "pod",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesignCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Facilities",
                schema: "pitch",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Icon = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Facilities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeatureFlags",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Reason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ChangedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureFlags", x => x.Id);
                    table.CheckConstraint("CK_FeatureFlags_Kind", "[Kind] BETWEEN 0 AND 1");
                });

            migrationBuilder.CreateTable(
                name: "Governorates",
                schema: "geo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Governorates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HelpTopics",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Icon = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpTopics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Integrations",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    Category = table.Column<byte>(type: "tinyint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Mode = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecretName = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: true),
                    SecretSetAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    LastCallAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    LastErrorAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DailyQuota = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Integrations", x => x.Id);
                    table.CheckConstraint("CK_Integrations_Category", "[Category] BETWEEN 0 AND 11");
                    table.CheckConstraint("CK_Integrations_ConfigJson_IsJson", "[ConfigJson] IS NULL OR ISJSON([ConfigJson]) = 1");
                    table.CheckConstraint("CK_Integrations_Mode", "[Mode] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_Integrations_Status", "[Status] BETWEEN 0 AND 3");
                });

            migrationBuilder.CreateTable(
                name: "LegalDocuments",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RequiresAcceptance = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxEvents",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Type = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    Attempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxEvents", x => x.Id);
                    table.CheckConstraint("CK_OutboxEvents_PayloadJson_IsJson", "ISJSON([PayloadJson]) = 1");
                    table.CheckConstraint("CK_OutboxEvents_Status", "[Status] BETWEEN 0 AND 3");
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    Area = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DescriptionEn = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                    table.CheckConstraint("CK_Permissions_Code", "[Code] LIKE '%.%'");
                });

            migrationBuilder.CreateTable(
                name: "PopupAds",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TitleEn = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    TextEn = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    TextAr = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    CtaEn = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    CtaAr = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Link = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Badge = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    Color = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: false),
                    Shape = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    Icons = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    HideFromSellers = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ShownCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ClickedCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PopupAds", x => x.Id);
                    table.CheckConstraint("CK_PopupAds_ClickedCount", "[ClickedCount] >= 0");
                    table.CheckConstraint("CK_PopupAds_Color", "[Color] LIKE '#%'");
                    table.CheckConstraint("CK_PopupAds_Link", "[Link] LIKE '#/%'");
                    table.CheckConstraint("CK_PopupAds_Shape", "[Shape] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_PopupAds_ShownCount", "[ShownCount] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "ProductCategories",
                schema: "shop",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Icon = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    IsHidden = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PublicHolidays",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Country = table.Column<string>(type: "char(2)", unicode: false, fixedLength: true, maxLength: 2, nullable: false, defaultValue: "EG"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    IsDayOff = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsEstimated = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ConfirmedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    Source = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicHolidays", x => x.Id);
                    table.CheckConstraint("CK_PublicHolidays_Kind", "[Kind] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_PublicHolidays_Source", "[Source] IN ('nagerdate', 'calendarific', 'admin')");
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    DescriptionEn = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                    table.CheckConstraint("CK_Roles_Kind", "[Kind] BETWEEN 0 AND 2");
                });

            migrationBuilder.CreateTable(
                name: "SellerLevels",
                schema: "pod",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Rank = table.Column<byte>(type: "tinyint", nullable: false),
                    SalesNeeded = table.Column<int>(type: "int", nullable: false),
                    MinRating = table.Column<decimal>(type: "decimal(2,1)", precision: 2, scale: 1, nullable: false, defaultValue: 0m),
                    ActiveDesigns = table.Column<int>(type: "int", nullable: true),
                    ActiveCodes = table.Column<int>(type: "int", nullable: true),
                    BaseDiscount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    StoreCommission = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ReviewLane = table.Column<byte>(type: "tinyint", nullable: false),
                    SearchBoost = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    SampleDiscount = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                    FreeSamplesPerMonth = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    FantasyDiscount = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                    BulkUpload = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    TeamKits = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EarlyAccess = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    AnalyticsTier = table.Column<byte>(type: "tinyint", nullable: false),
                    CalendarTier = table.Column<byte>(type: "tinyint", nullable: false),
                    SupportTier = table.Column<byte>(type: "tinyint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellerLevels", x => x.Id);
                    table.CheckConstraint("CK_SellerLevels_ActiveCodes", "[ActiveCodes] IS NULL OR [ActiveCodes] > 0");
                    table.CheckConstraint("CK_SellerLevels_ActiveDesigns", "[ActiveDesigns] IS NULL OR [ActiveDesigns] > 0");
                    table.CheckConstraint("CK_SellerLevels_AnalyticsTier", "[AnalyticsTier] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_SellerLevels_BaseDiscount", "[BaseDiscount] >= 0");
                    table.CheckConstraint("CK_SellerLevels_CalendarTier", "[CalendarTier] <= 3");
                    table.CheckConstraint("CK_SellerLevels_FantasyDiscount", "[FantasyDiscount] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_SellerLevels_MinRating", "[MinRating] BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_SellerLevels_Rank", "[Rank] <= 9");
                    table.CheckConstraint("CK_SellerLevels_ReviewLane", "[ReviewLane] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_SellerLevels_SalesNeeded", "[SalesNeeded] >= 0");
                    table.CheckConstraint("CK_SellerLevels_SampleDiscount", "[SampleDiscount] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_SellerLevels_SearchBoost", "[SearchBoost] <= 5");
                    table.CheckConstraint("CK_SellerLevels_StoreCommission", "[StoreCommission] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_SellerLevels_SupportTier", "[SupportTier] BETWEEN 0 AND 2");
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                schema: "ops",
                columns: table => new
                {
                    Key = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Group = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Key);
                    table.CheckConstraint("CK_Settings_Value_IsJson", "ISJSON([Value]) = 1");
                });

            migrationBuilder.CreateTable(
                name: "Sponsors",
                schema: "fantasy",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Tier = table.Column<byte>(type: "tinyint", nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ContractValue = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Color = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: false, defaultValue: "#0B6E4F"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sponsors", x => x.Id);
                    table.CheckConstraint("CK_Sponsors_ContractValue", "[ContractValue] >= 0");
                    table.CheckConstraint("CK_Sponsors_EndsOn", "[EndsOn] >= [StartsOn]");
                    table.CheckConstraint("CK_Sponsors_Tier", "[Tier] BETWEEN 0 AND 2");
                });

            migrationBuilder.CreateTable(
                name: "WebhookSubscriptions",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Events = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SecretHash = table.Column<byte[]>(type: "varbinary(64)", maxLength: 64, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FailureCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookSubscriptions", x => x.Id);
                    table.CheckConstraint("CK_WebhookSubscriptions_Events_IsJson", "ISJSON([Events]) = 1");
                    table.CheckConstraint("CK_WebhookSubscriptions_FailureCount", "[FailureCount] >= 0");
                    table.CheckConstraint("CK_WebhookSubscriptions_Url", "[Url] LIKE 'https://%' OR [Url] LIKE 'http://localhost%'");
                });

            migrationBuilder.CreateTable(
                name: "CatalogColors",
                schema: "pod",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CatalogProductId = table.Column<int>(type: "int", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Hex = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogColors", x => x.Id);
                    table.CheckConstraint("CK_CatalogColors_Hex", "[Hex] LIKE '#%'");
                    table.ForeignKey(
                        name: "FK_CatalogColors_CatalogProducts_CatalogProductId",
                        column: x => x.CatalogProductId,
                        principalSchema: "pod",
                        principalTable: "CatalogProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatalogDetails",
                schema: "pod",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CatalogProductId = table.Column<int>(type: "int", nullable: false),
                    LabelEn = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LabelAr = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ValueEn = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ValueAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogDetails_CatalogProducts_CatalogProductId",
                        column: x => x.CatalogProductId,
                        principalSchema: "pod",
                        principalTable: "CatalogProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatalogPrintAreas",
                schema: "pod",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CatalogProductId = table.Column<int>(type: "int", nullable: false),
                    Side = table.Column<byte>(type: "tinyint", nullable: false),
                    X = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    Y = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    Width = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    Height = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    PrintWidthCm = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: false, defaultValue: 30m),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogPrintAreas", x => x.Id);
                    table.CheckConstraint("CK_CatalogPrintAreas_Height", "[Height] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_CatalogPrintAreas_PrintWidthCm", "[PrintWidthCm] > 0");
                    table.CheckConstraint("CK_CatalogPrintAreas_Side", "[Side] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_CatalogPrintAreas_Width", "[Width] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_CatalogPrintAreas_X", "[X] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_CatalogPrintAreas_Y", "[Y] BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_CatalogPrintAreas_CatalogProducts_CatalogProductId",
                        column: x => x.CatalogProductId,
                        principalSchema: "pod",
                        principalTable: "CatalogProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatalogSizes",
                schema: "pod",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CatalogProductId = table.Column<int>(type: "int", nullable: false),
                    Size = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: false),
                    IsBigSize = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ChestCm = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: false),
                    LengthCm = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: false),
                    SleeveCm = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogSizes", x => x.Id);
                    table.CheckConstraint("CK_CatalogSizes_ChestCm", "[ChestCm] > 0");
                    table.CheckConstraint("CK_CatalogSizes_LengthCm", "[LengthCm] > 0");
                    table.CheckConstraint("CK_CatalogSizes_SleeveCm", "[SleeveCm] > 0");
                    table.ForeignKey(
                        name: "FK_CatalogSizes_CatalogProducts_CatalogProductId",
                        column: x => x.CatalogProductId,
                        principalSchema: "pod",
                        principalTable: "CatalogProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                schema: "fantasy",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExternalId = table.Column<int>(type: "int", nullable: false),
                    ClubId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Position = table.Column<byte>(type: "tinyint", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: false),
                    PhotoUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    SelectedByPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                    table.CheckConstraint("CK_Players_Position", "[Position] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_Players_Price", "[Price] BETWEEN 3.5 AND 15.0");
                    table.CheckConstraint("CK_Players_Status", "[Status] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_Players_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalSchema: "fantasy",
                        principalTable: "Clubs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Seasons",
                schema: "fantasy",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<short>(type: "smallint", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seasons", x => x.Id);
                    table.CheckConstraint("CK_Seasons_Year", "[Year] BETWEEN 2020 AND 2100");
                    table.ForeignKey(
                        name: "FK_Seasons_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalSchema: "fantasy",
                        principalTable: "Competitions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Budgets",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Budgets", x => x.Id);
                    table.CheckConstraint("CK_Budgets_Amount", "[Amount] >= 0");
                    table.ForeignKey(
                        name: "FK_Budgets_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "ops",
                        principalTable: "Departments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Areas",
                schema: "geo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GovernorateId = table.Column<int>(type: "int", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Lat = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Lng = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Areas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Areas_Governorates_GovernorateId",
                        column: x => x.GovernorateId,
                        principalSchema: "geo",
                        principalTable: "Governorates",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "HelpArticles",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TopicId = table.Column<int>(type: "int", nullable: false),
                    QuestionEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    QuestionAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AnswerEn = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AnswerAr = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    HelpfulCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    NotHelpfulCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpArticles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpArticles_HelpTopics_TopicId",
                        column: x => x.TopicId,
                        principalSchema: "content",
                        principalTable: "HelpTopics",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "IntegrationUsage",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IntegrationId = table.Column<int>(type: "int", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Calls = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Errors = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    QuotaUsed = table.Column<int>(type: "int", nullable: true),
                    QuotaLimit = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationUsage", x => x.Id);
                    table.CheckConstraint("CK_IntegrationUsage_Calls", "[Calls] >= 0");
                    table.CheckConstraint("CK_IntegrationUsage_Errors", "[Errors] >= 0");
                    table.ForeignKey(
                        name: "FK_IntegrationUsage_Integrations_IntegrationId",
                        column: x => x.IntegrationId,
                        principalSchema: "ops",
                        principalTable: "Integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "auth",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                schema: "auth",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalSchema: "auth",
                        principalTable: "Permissions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "auth",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WebhookDeliveries",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubscriptionId = table.Column<int>(type: "int", nullable: false),
                    OutboxEventId = table.Column<long>(type: "bigint", nullable: false),
                    Attempt = table.Column<byte>(type: "tinyint", nullable: false),
                    StatusCode = table.Column<short>(type: "smallint", nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    At = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookDeliveries", x => x.Id);
                    table.CheckConstraint("CK_WebhookDeliveries_Attempt", "[Attempt] >= 1");
                    table.ForeignKey(
                        name: "FK_WebhookDeliveries_OutboxEvents_OutboxEventId",
                        column: x => x.OutboxEventId,
                        principalSchema: "ops",
                        principalTable: "OutboxEvents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WebhookDeliveries_WebhookSubscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalSchema: "ops",
                        principalTable: "WebhookSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Gameweeks",
                schema: "fantasy",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeasonId = table.Column<long>(type: "bigint", nullable: false),
                    Number = table.Column<byte>(type: "tinyint", nullable: false),
                    Deadline = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    ScoredAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gameweeks", x => x.Id);
                    table.CheckConstraint("CK_Gameweeks_Number", "[Number] BETWEEN 1 AND 50");
                    table.CheckConstraint("CK_Gameweeks_Status", "[Status] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_Gameweeks_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalSchema: "fantasy",
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeasonClubs",
                schema: "fantasy",
                columns: table => new
                {
                    SeasonId = table.Column<long>(type: "bigint", nullable: false),
                    ClubId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonClubs", x => new { x.SeasonId, x.ClubId });
                    table.ForeignKey(
                        name: "FK_SeasonClubs_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalSchema: "fantasy",
                        principalTable: "Clubs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SeasonClubs_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalSchema: "fantasy",
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Fixtures",
                schema: "fantasy",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExternalId = table.Column<int>(type: "int", nullable: false),
                    GameweekId = table.Column<long>(type: "bigint", nullable: false),
                    HomeClubId = table.Column<long>(type: "bigint", nullable: false),
                    AwayClubId = table.Column<long>(type: "bigint", nullable: false),
                    KickoffAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    HomeGoals = table.Column<byte>(type: "tinyint", nullable: true),
                    AwayGoals = table.Column<byte>(type: "tinyint", nullable: true),
                    Minute = table.Column<byte>(type: "tinyint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fixtures", x => x.Id);
                    table.CheckConstraint("CK_Fixtures_Clubs", "[HomeClubId] <> [AwayClubId]");
                    table.CheckConstraint("CK_Fixtures_Status", "[Status] BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_Fixtures_Clubs_AwayClubId",
                        column: x => x.AwayClubId,
                        principalSchema: "fantasy",
                        principalTable: "Clubs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Fixtures_Clubs_HomeClubId",
                        column: x => x.HomeClubId,
                        principalSchema: "fantasy",
                        principalTable: "Clubs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Fixtures_Gameweeks_GameweekId",
                        column: x => x.GameweekId,
                        principalSchema: "fantasy",
                        principalTable: "Gameweeks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerFixtureStats",
                schema: "fantasy",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    FixtureId = table.Column<long>(type: "bigint", nullable: false),
                    Minutes = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    Goals = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    Assists = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    Saves = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    OwnGoals = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    PenaltiesSaved = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    PenaltiesMissed = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    YellowCards = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    RedCard = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CleanSheet = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    GoalsConceded = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    Bonus = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    Points = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerFixtureStats", x => x.Id);
                    table.CheckConstraint("CK_PlayerFixtureStats_Bonus", "[Bonus] <= 3");
                    table.CheckConstraint("CK_PlayerFixtureStats_Minutes", "[Minutes] <= 130");
                    table.CheckConstraint("CK_PlayerFixtureStats_YellowCards", "[YellowCards] <= 2");
                    table.ForeignKey(
                        name: "FK_PlayerFixtureStats_Fixtures_FixtureId",
                        column: x => x.FixtureId,
                        principalSchema: "fantasy",
                        principalTable: "Fixtures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerFixtureStats_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "fantasy",
                        principalTable: "Players",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Adjustments",
                schema: "money",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Adjustments", x => x.Id);
                    table.CheckConstraint("CK_Adjustments_Amount", "[Amount] <> 0");
                });

            migrationBuilder.CreateTable(
                name: "AgentPermissions",
                schema: "ai",
                columns: table => new
                {
                    AgentId = table.Column<long>(type: "bigint", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentPermissions", x => new { x.AgentId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_AgentPermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalSchema: "auth",
                        principalTable: "Permissions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AgentRuns",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentId = table.Column<long>(type: "bigint", nullable: false),
                    TaskId = table.Column<long>(type: "bigint", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    FinishedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    Result = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    InputTokens = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    OutputTokens = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CostUsd = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRuns", x => x.Id);
                    table.CheckConstraint("CK_AgentRuns_CostUsd", "[CostUsd] >= 0");
                    table.CheckConstraint("CK_AgentRuns_InputTokens", "[InputTokens] >= 0");
                    table.CheckConstraint("CK_AgentRuns_OutputTokens", "[OutputTokens] >= 0");
                    table.CheckConstraint("CK_AgentRuns_Result", "[Result] BETWEEN 0 AND 4");
                });

            migrationBuilder.CreateTable(
                name: "Agents",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PositionId = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Model = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    Platform = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    MoneyLimit = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    MonthlyBudget = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)1),
                    PausedReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agents", x => x.Id);
                    table.CheckConstraint("CK_Agents_MoneyLimit", "[MoneyLimit] >= 0");
                    table.CheckConstraint("CK_Agents_MonthlyBudget", "[MonthlyBudget] >= 0");
                    table.CheckConstraint("CK_Agents_Platform", "[Platform] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_Agents_Status", "[Status] BETWEEN 0 AND 2");
                });

            migrationBuilder.CreateTable(
                name: "AgentTasks",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Trigger = table.Column<byte>(type: "tinyint", nullable: false),
                    EventType = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true),
                    Cron = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: true),
                    ExpectedOutput = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastRunAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentTasks", x => x.Id);
                    table.CheckConstraint("CK_AgentTasks_Trigger", "[Trigger] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_AgentTasks_TriggerDetails", "([Trigger] <> 0 OR [EventType] IS NOT NULL) AND ([Trigger] <> 1 OR [Cron] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_AgentTasks_Agents_AgentId",
                        column: x => x.AgentId,
                        principalSchema: "ai",
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApiKeys",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentId = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Prefix = table.Column<string>(type: "char(8)", unicode: false, fixedLength: true, maxLength: 8, nullable: false),
                    KeyHash = table.Column<byte[]>(type: "varbinary(64)", maxLength: 64, nullable: false),
                    Scopes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                    table.CheckConstraint("CK_ApiKeys_Scopes_IsJson", "[Scopes] IS NULL OR ISJSON([Scopes]) = 1");
                    table.ForeignKey(
                        name: "FK_ApiKeys_Agents_AgentId",
                        column: x => x.AgentId,
                        principalSchema: "ai",
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationFiles",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationId = table.Column<long>(type: "bigint", nullable: false),
                    FileId = table.Column<long>(type: "bigint", nullable: false),
                    Purpose = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationFiles", x => x.Id);
                    table.CheckConstraint("CK_ApplicationFiles_Purpose", "[Purpose] BETWEEN 0 AND 3");
                });

            migrationBuilder.CreateTable(
                name: "Applications",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    ApplicantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    AnswersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AgreementVersionId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RejectReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                    table.CheckConstraint("CK_Applications_AnswersJson_IsJson", "ISJSON([AnswersJson]) = 1");
                    table.CheckConstraint("CK_Applications_RejectReason", "[Status] <> 2 OR [RejectReason] IS NOT NULL");
                    table.CheckConstraint("CK_Applications_Status", "[Status] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_Applications_Type", "[Type] BETWEEN 0 AND 2");
                });

            migrationBuilder.CreateTable(
                name: "ApprovalDecisions",
                schema: "money",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApprovalId = table.Column<long>(type: "bigint", nullable: false),
                    DeciderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<byte>(type: "tinyint", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalDecisions", x => x.Id);
                    table.CheckConstraint("CK_ApprovalDecisions_Decision", "[Decision] BETWEEN 0 AND 1");
                });

            migrationBuilder.CreateTable(
                name: "Approvals",
                schema: "money",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    RefType = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    RefId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    Summary = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequiredApprovals = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)1),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    DecidedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Approvals", x => x.Id);
                    table.CheckConstraint("CK_Approvals_Amount", "[Amount] >= 0");
                    table.CheckConstraint("CK_Approvals_Kind", "[Kind] BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_Approvals_RequiredApprovals", "[RequiredApprovals] BETWEEN 1 AND 2");
                    table.CheckConstraint("CK_Approvals_Status", "[Status] BETWEEN 0 AND 3");
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    At = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorAgentId = table.Column<long>(type: "bigint", nullable: true),
                    System = table.Column<byte>(type: "tinyint", nullable: false),
                    Action = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    EntityType = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: true),
                    EntityId = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ip = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    CorrelationId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.CheckConstraint("CK_AuditLogs_DataJson_IsJson", "[DataJson] IS NULL OR ISJSON([DataJson]) = 1");
                    table.CheckConstraint("CK_AuditLogs_System", "[System] BETWEEN 0 AND 6");
                    table.ForeignKey(
                        name: "FK_AuditLogs_Agents_ActorAgentId",
                        column: x => x.ActorAgentId,
                        principalSchema: "ai",
                        principalTable: "Agents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    PitchId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WalkInName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    WalkInPhone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Source = table.Column<byte>(type: "tinyint", nullable: false),
                    PackageId = table.Column<long>(type: "bigint", nullable: true),
                    SeriesId = table.Column<long>(type: "bigint", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StartHour = table.Column<byte>(type: "tinyint", nullable: false),
                    Duration = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)1),
                    PitchAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    PromoCodeId = table.Column<long>(type: "bigint", nullable: true),
                    ServiceFee = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    Total = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    CommissionRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    PaymentId = table.Column<long>(type: "bigint", nullable: true),
                    PayAtVenue = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PolicyKey = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    WaiverVersionId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    CheckedInAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RefundAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    QrSecretHash = table.Column<byte[]>(type: "varbinary(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.CheckConstraint("CK_Bookings_CommissionRate", "[CommissionRate] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_Bookings_DiscountAmount", "[DiscountAmount] >= 0");
                    table.CheckConstraint("CK_Bookings_Duration", "[Duration] BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_Bookings_PitchAmount", "[PitchAmount] >= 0");
                    table.CheckConstraint("CK_Bookings_Refund", "[RefundAmount] >= 0 AND [RefundAmount] <= [Total]");
                    table.CheckConstraint("CK_Bookings_ServiceFee", "[ServiceFee] >= 0");
                    table.CheckConstraint("CK_Bookings_Source", "[Source] BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_Bookings_StartHour", "[StartHour] <= 23");
                    table.CheckConstraint("CK_Bookings_Status", "[Status] BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_Bookings_Total", "[Total] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "BookingSlots",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<long>(type: "bigint", nullable: false),
                    PitchId = table.Column<long>(type: "bigint", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Hour = table.Column<byte>(type: "tinyint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingSlots", x => x.Id);
                    table.CheckConstraint("CK_BookingSlots_Hour", "[Hour] <= 23");
                    table.ForeignKey(
                        name: "FK_BookingSlots_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "booking",
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Broadcasts",
                schema: "notify",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Link = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Segment = table.Column<byte>(type: "tinyint", nullable: false),
                    SendEmail = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RecipientsCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Broadcasts", x => x.Id);
                    table.CheckConstraint("CK_Broadcasts_Segment", "[Segment] BETWEEN 0 AND 5");
                });

            migrationBuilder.CreateTable(
                name: "CartItems",
                schema: "shop",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductVariantId = table.Column<long>(type: "bigint", nullable: true),
                    DesignId = table.Column<long>(type: "bigint", nullable: true),
                    Size = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Color = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Qty = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartItems", x => x.Id);
                    table.CheckConstraint("CK_CartItems_OneItem", "([ProductVariantId] IS NULL AND [DesignId] IS NOT NULL) OR ([ProductVariantId] IS NOT NULL AND [DesignId] IS NULL)");
                    table.CheckConstraint("CK_CartItems_Qty", "[Qty] BETWEEN 1 AND 20");
                });

            migrationBuilder.CreateTable(
                name: "CatalogChangeRequests",
                schema: "pod",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CatalogProductId = table.Column<int>(type: "int", nullable: false),
                    FactoryId = table.Column<long>(type: "bigint", nullable: false),
                    RequestedByStaffId = table.Column<long>(type: "bigint", nullable: true),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    ChangeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogChangeRequests", x => x.Id);
                    table.CheckConstraint("CK_CatalogChangeRequests_ChangeJson_IsJson", "ISJSON([ChangeJson]) = 1");
                    table.CheckConstraint("CK_CatalogChangeRequests_Kind", "[Kind] BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_CatalogChangeRequests_Status", "[Status] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_CatalogChangeRequests_CatalogProducts_CatalogProductId",
                        column: x => x.CatalogProductId,
                        principalSchema: "pod",
                        principalTable: "CatalogProducts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Closures",
                schema: "pitch",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PitchId = table.Column<long>(type: "bigint", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FromHour = table.Column<byte>(type: "tinyint", nullable: true),
                    ToHour = table.Column<byte>(type: "tinyint", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Closures", x => x.Id);
                    table.CheckConstraint("CK_Closures_Dates", "[ToDate] >= [FromDate]");
                    table.CheckConstraint("CK_Closures_Hours", "([FromHour] IS NULL AND [ToHour] IS NULL) OR ([FromHour] <= 24 AND [ToHour] <= 24 AND [ToHour] > [FromHour])");
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
                });

            migrationBuilder.CreateTable(
                name: "ComplianceItems",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Repeat = table.Column<byte>(type: "tinyint", nullable: false),
                    DoneAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceItems", x => x.Id);
                    table.CheckConstraint("CK_ComplianceItems_Repeat", "[Repeat] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_ComplianceItems_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "ops",
                        principalTable: "Departments",
                        principalColumn: "Id");
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
                });

            migrationBuilder.CreateTable(
                name: "CookieConsents",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VisitorId = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Analytics = table.Column<bool>(type: "bit", nullable: false),
                    PolicyVersion = table.Column<short>(type: "smallint", nullable: false),
                    At = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CookieConsents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DesignColors",
                schema: "pod",
                columns: table => new
                {
                    DesignId = table.Column<long>(type: "bigint", nullable: false),
                    CatalogColorId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesignColors", x => new { x.DesignId, x.CatalogColorId });
                    table.ForeignKey(
                        name: "FK_DesignColors_CatalogColors_CatalogColorId",
                        column: x => x.CatalogColorId,
                        principalSchema: "pod",
                        principalTable: "CatalogColors",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DesignReports",
                schema: "pod",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DesignId = table.Column<long>(type: "bigint", nullable: false),
                    ReporterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<byte>(type: "tinyint", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    Contact = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    HandledById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HandledAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesignReports", x => x.Id);
                    table.CheckConstraint("CK_DesignReports_Reason", "[Reason] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_DesignReports_Status", "[Status] BETWEEN 0 AND 2");
                });

            migrationBuilder.CreateTable(
                name: "Designs",
                schema: "pod",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SellerId = table.Column<long>(type: "bigint", nullable: false),
                    CatalogProductId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false, defaultValue: ""),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    FrontArtFileId = table.Column<long>(type: "bigint", nullable: true),
                    BackArtFileId = table.Column<long>(type: "bigint", nullable: true),
                    FrontX = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    FrontY = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    FrontW = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    BackX = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    BackY = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    BackW = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    RejectReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SpotCheck = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Views = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Sales = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Designs", x => x.Id);
                    table.CheckConstraint("CK_Designs_BackPlacement", "([BackX] IS NULL AND [BackY] IS NULL AND [BackW] IS NULL) OR ([BackX] BETWEEN 0 AND 1 AND [BackY] BETWEEN 0 AND 1 AND [BackW] BETWEEN 0 AND 1)");
                    table.CheckConstraint("CK_Designs_FrontPlacement", "([FrontX] IS NULL AND [FrontY] IS NULL AND [FrontW] IS NULL) OR ([FrontX] BETWEEN 0 AND 1 AND [FrontY] BETWEEN 0 AND 1 AND [FrontW] BETWEEN 0 AND 1)");
                    table.CheckConstraint("CK_Designs_Price", "[Price] >= 0");
                    table.CheckConstraint("CK_Designs_Sales", "[Sales] >= 0");
                    table.CheckConstraint("CK_Designs_Status", "[Status] BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_Designs_Views", "[Views] >= 0");
                    table.ForeignKey(
                        name: "FK_Designs_CatalogProducts_CatalogProductId",
                        column: x => x.CatalogProductId,
                        principalSchema: "pod",
                        principalTable: "CatalogProducts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Designs_DesignCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "pod",
                        principalTable: "DesignCategories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DesignTags",
                schema: "pod",
                columns: table => new
                {
                    DesignId = table.Column<long>(type: "bigint", nullable: false),
                    Tag = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesignTags", x => new { x.DesignId, x.Tag });
                    table.ForeignKey(
                        name: "FK_DesignTags_Designs_DesignId",
                        column: x => x.DesignId,
                        principalSchema: "pod",
                        principalTable: "Designs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailOutbox",
                schema: "notify",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Template = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    ModelJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Language = table.Column<string>(type: "char(2)", unicode: false, fixedLength: true, maxLength: 2, nullable: false, defaultValue: "ar"),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    Attempts = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    SentAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailOutbox", x => x.Id);
                    table.CheckConstraint("CK_EmailOutbox_ModelJson_IsJson", "ISJSON([ModelJson]) = 1");
                    table.CheckConstraint("CK_EmailOutbox_Status", "[Status] BETWEEN 0 AND 3");
                });

            migrationBuilder.CreateTable(
                name: "Factories",
                schema: "factory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    AreaId = table.Column<int>(type: "int", nullable: false),
                    CapacityPerDay = table.Column<int>(type: "int", nullable: false),
                    SlaHours = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)48),
                    Techniques = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false, defaultValue: "DTF"),
                    IntakePaused = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    PayoutDetail = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factories", x => x.Id);
                    table.CheckConstraint("CK_Factories_CapacityPerDay", "[CapacityPerDay] > 0");
                    table.CheckConstraint("CK_Factories_SlaHours", "[SlaHours] BETWEEN 1 AND 240");
                    table.CheckConstraint("CK_Factories_Status", "[Status] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_Factories_Areas_AreaId",
                        column: x => x.AreaId,
                        principalSchema: "geo",
                        principalTable: "Areas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FactoryDevices",
                schema: "factory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FactoryId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    TokenHash = table.Column<byte[]>(type: "varbinary(64)", maxLength: 64, nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactoryDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FactoryDevices_Factories_FactoryId",
                        column: x => x.FactoryId,
                        principalSchema: "factory",
                        principalTable: "Factories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FactoryPrices",
                schema: "factory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FactoryId = table.Column<long>(type: "bigint", nullable: false),
                    CatalogProductId = table.Column<int>(type: "int", nullable: false),
                    PricePerPiece = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    BackPrintPrice = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactoryPrices", x => x.Id);
                    table.CheckConstraint("CK_FactoryPrices_BackPrintPrice", "[BackPrintPrice] >= 0");
                    table.CheckConstraint("CK_FactoryPrices_PricePerPiece", "[PricePerPiece] > 0");
                    table.ForeignKey(
                        name: "FK_FactoryPrices_CatalogProducts_CatalogProductId",
                        column: x => x.CatalogProductId,
                        principalSchema: "pod",
                        principalTable: "CatalogProducts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FactoryPrices_Factories_FactoryId",
                        column: x => x.FactoryId,
                        principalSchema: "factory",
                        principalTable: "Factories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FactoryStaff",
                schema: "factory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FactoryId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Role = table.Column<byte>(type: "tinyint", nullable: false),
                    PinHash = table.Column<byte[]>(type: "varbinary(64)", maxLength: 64, nullable: false),
                    FailedPins = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    LockedUntil = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactoryStaff", x => x.Id);
                    table.CheckConstraint("CK_FactoryStaff_Role", "[Role] BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_FactoryStaff_Factories_FactoryId",
                        column: x => x.FactoryId,
                        principalSchema: "factory",
                        principalTable: "Factories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FantasyGroups",
                schema: "fantasy",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeasonId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    InviteCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    IsHidden = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MonthlyPrice = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SponsorId = table.Column<long>(type: "bigint", nullable: true),
                    PrizeText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PrizeValue = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FantasyGroups", x => x.Id);
                    table.CheckConstraint("CK_FantasyGroups_Price", "([Type] <> 2 AND [MonthlyPrice] IS NULL) OR ([Type] = 2 AND [MonthlyPrice] > 0)");
                    table.CheckConstraint("CK_FantasyGroups_Type", "[Type] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_FantasyGroups_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalSchema: "fantasy",
                        principalTable: "Seasons",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FantasyGroups_Sponsors_SponsorId",
                        column: x => x.SponsorId,
                        principalSchema: "fantasy",
                        principalTable: "Sponsors",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FantasyPasses",
                schema: "fantasy",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompetitionId = table.Column<int>(type: "int", nullable: true),
                    PaymentId = table.Column<long>(type: "bigint", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    StartsAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FantasyPasses", x => x.Id);
                    table.CheckConstraint("CK_FantasyPasses_Dates", "[EndsAt] > [StartsAt]");
                    table.CheckConstraint("CK_FantasyPasses_Price", "[Price] >= 0");
                    table.ForeignKey(
                        name: "FK_FantasyPasses_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalSchema: "fantasy",
                        principalTable: "Competitions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FantasyTeams",
                schema: "fantasy",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeasonId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Bank = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: false, defaultValue: 0m),
                    FreeTransfers = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)1),
                    TotalPoints = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    OverallRank = table.Column<int>(type: "int", nullable: true),
                    ChipsUsed = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FantasyTeams", x => x.Id);
                    table.CheckConstraint("CK_FantasyTeams_Bank", "[Bank] >= 0");
                    table.CheckConstraint("CK_FantasyTeams_FreeTransfers", "[FreeTransfers] <= 5");
                    table.ForeignKey(
                        name: "FK_FantasyTeams_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalSchema: "fantasy",
                        principalTable: "Seasons",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GroupMembers",
                schema: "fantasy",
                columns: table => new
                {
                    GroupId = table.Column<long>(type: "bigint", nullable: false),
                    TeamId = table.Column<long>(type: "bigint", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: true),
                    PaidUntil = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupMembers", x => new { x.GroupId, x.TeamId });
                    table.ForeignKey(
                        name: "FK_GroupMembers_FantasyGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "fantasy",
                        principalTable: "FantasyGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupMembers_FantasyTeams_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "fantasy",
                        principalTable: "FantasyTeams",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Picks",
                schema: "fantasy",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeamId = table.Column<long>(type: "bigint", nullable: false),
                    GameweekId = table.Column<long>(type: "bigint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    Slot = table.Column<byte>(type: "tinyint", nullable: false),
                    IsCaptain = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsViceCaptain = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Multiplier = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)1),
                    Points = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Picks", x => x.Id);
                    table.CheckConstraint("CK_Picks_Multiplier", "[Multiplier] <= 3");
                    table.CheckConstraint("CK_Picks_Slot", "[Slot] BETWEEN 1 AND 15");
                    table.ForeignKey(
                        name: "FK_Picks_FantasyTeams_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "fantasy",
                        principalTable: "FantasyTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Picks_Gameweeks_GameweekId",
                        column: x => x.GameweekId,
                        principalSchema: "fantasy",
                        principalTable: "Gameweeks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Picks_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "fantasy",
                        principalTable: "Players",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TeamGameweekPoints",
                schema: "fantasy",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeamId = table.Column<long>(type: "bigint", nullable: false),
                    GameweekId = table.Column<long>(type: "bigint", nullable: false),
                    Points = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    TransfersCost = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    Rank = table.Column<int>(type: "int", nullable: true),
                    ChipPlayed = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamGameweekPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamGameweekPoints_FantasyTeams_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "fantasy",
                        principalTable: "FantasyTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamGameweekPoints_Gameweeks_GameweekId",
                        column: x => x.GameweekId,
                        principalSchema: "fantasy",
                        principalTable: "Gameweeks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Transfers",
                schema: "fantasy",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeamId = table.Column<long>(type: "bigint", nullable: false),
                    GameweekId = table.Column<long>(type: "bigint", nullable: false),
                    PlayerOutId = table.Column<long>(type: "bigint", nullable: false),
                    PlayerInId = table.Column<long>(type: "bigint", nullable: false),
                    PriceOut = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: false),
                    PriceIn = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: false),
                    PointsHit = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transfers", x => x.Id);
                    table.CheckConstraint("CK_Transfers_Players", "[PlayerOutId] <> [PlayerInId]");
                    table.ForeignKey(
                        name: "FK_Transfers_FantasyTeams_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "fantasy",
                        principalTable: "FantasyTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Transfers_Gameweeks_GameweekId",
                        column: x => x.GameweekId,
                        principalSchema: "fantasy",
                        principalTable: "Gameweeks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Transfers_Players_PlayerInId",
                        column: x => x.PlayerInId,
                        principalSchema: "fantasy",
                        principalTable: "Players",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Transfers_Players_PlayerOutId",
                        column: x => x.PlayerOutId,
                        principalSchema: "fantasy",
                        principalTable: "Players",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Files",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StorageKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Container = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "int", nullable: true),
                    Height = table.Column<int>(type: "int", nullable: true),
                    Sha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    IsPrivate = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Files", x => x.Id);
                    table.CheckConstraint("CK_Files_ContentType", "[ContentType] IN ('image/png', 'image/jpeg', 'image/webp', 'image/svg+xml', 'application/pdf', 'model/gltf-binary')");
                    table.CheckConstraint("CK_Files_SizeBytes", "[SizeBytes] > 0 AND [SizeBytes] <= 20971520");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    FullName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AvatarFileId = table.Column<long>(type: "bigint", nullable: true),
                    Language = table.Column<string>(type: "char(2)", unicode: false, fixedLength: true, maxLength: 2, nullable: false, defaultValue: "ar"),
                    AccountType = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    SuspendedReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    MustPrepay = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    CodRefusals = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    AdminNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_Users_AccountType", "[AccountType] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_Users_CodRefusals", "[CodRefusals] >= 0");
                    table.CheckConstraint("CK_Users_FullName", "LEN([FullName]) >= 2");
                    table.CheckConstraint("CK_Users_Language", "[Language] IN ('ar', 'en')");
                    table.CheckConstraint("CK_Users_MustPrepay", "[MustPrepay] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_Users_PhoneNumber", "[PhoneNumber] IS NULL OR [PhoneNumber] LIKE '+20%'");
                    table.CheckConstraint("CK_Users_Status", "[Status] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_Users_Files_AvatarFileId",
                        column: x => x.AvatarFileId,
                        principalSchema: "ops",
                        principalTable: "Files",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Games",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    HostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PlayersNeeded = table.Column<byte>(type: "tinyint", nullable: false),
                    Positions = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    Level = table.Column<byte>(type: "tinyint", nullable: false),
                    SharePrice = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    AutoAccept = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RequestsClosed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    HiddenByAdmin = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                    table.CheckConstraint("CK_Games_Level", "[Level] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_Games_PlayersNeeded", "[PlayersNeeded] BETWEEN 1 AND 21");
                    table.CheckConstraint("CK_Games_SharePrice", "[SharePrice] >= 0");
                    table.CheckConstraint("CK_Games_Status", "[Status] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_Games_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "booking",
                        principalTable: "Bookings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Games_Users_HostId",
                        column: x => x.HostId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Goals",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quarter = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Goals", x => x.Id);
                    table.CheckConstraint("CK_Goals_Quarter", "[Quarter] LIKE '[0-9][0-9][0-9][0-9]-Q[1-4]'");
                    table.ForeignKey(
                        name: "FK_Goals_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "ops",
                        principalTable: "Departments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Goals_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "HelpVotes",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticleId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VisitorId = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true),
                    Helpful = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpVotes_HelpArticles_ArticleId",
                        column: x => x.ArticleId,
                        principalSchema: "content",
                        principalTable: "HelpArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HelpVotes_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "KycDocuments",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NationalId = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    FrontFileId = table.Column<long>(type: "bigint", nullable: false),
                    BackFileId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RejectReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KycDocuments", x => x.Id);
                    table.CheckConstraint("CK_KycDocuments_RejectReason", "[Status] <> 2 OR [RejectReason] IS NOT NULL");
                    table.CheckConstraint("CK_KycDocuments_Status", "[Status] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_KycDocuments_Files_BackFileId",
                        column: x => x.BackFileId,
                        principalSchema: "ops",
                        principalTable: "Files",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KycDocuments_Files_FrontFileId",
                        column: x => x.FrontFileId,
                        principalSchema: "ops",
                        principalTable: "Files",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KycDocuments_Users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KycDocuments_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LegalVersions",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<short>(type: "smallint", nullable: false),
                    BodyEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SummaryEn = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    SummaryAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    LawyerReviewedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    LawyerNote = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalVersions", x => x.Id);
                    table.CheckConstraint("CK_LegalVersions_Version", "[Version] >= 1");
                    table.ForeignKey(
                        name: "FK_LegalVersions_LegalDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "content",
                        principalTable: "LegalDocuments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LegalVersions_Users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "NewsPosts",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(110)", maxLength: 110, nullable: false),
                    Excerpt = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImageFileId = table.Column<long>(type: "bigint", nullable: true),
                    IsBreaking = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsHidden = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsPosts", x => x.Id);
                    table.CheckConstraint("CK_NewsPosts_Excerpt", "LEN([Excerpt]) >= 20");
                    table.CheckConstraint("CK_NewsPosts_Title", "LEN([Title]) >= 10");
                    table.ForeignKey(
                        name: "FK_NewsPosts_Files_ImageFileId",
                        column: x => x.ImageFileId,
                        principalSchema: "ops",
                        principalTable: "Files",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_NewsPosts_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                schema: "notify",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<byte>(type: "tinyint", nullable: false),
                    InApp = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Email = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Push = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => new { x.UserId, x.Category });
                    table.CheckConstraint("CK_NotificationPreferences_Category", "[Category] BETWEEN 0 AND 10");
                    table.ForeignKey(
                        name: "FK_NotificationPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "notify",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<byte>(type: "tinyint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Link = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Tone = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    ReadAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.CheckConstraint("CK_Notifications_Category", "[Category] BETWEEN 0 AND 10");
                    table.CheckConstraint("CK_Notifications_Link", "[Link] IS NULL OR [Link] LIKE '#/%'");
                    table.CheckConstraint("CK_Notifications_Tone", "[Tone] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrgPositions",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    ReportsToId = table.Column<long>(type: "bigint", nullable: true),
                    TitleEn = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    HolderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DutiesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgPositions", x => x.Id);
                    table.CheckConstraint("CK_OrgPositions_DutiesJson_IsJson", "[DutiesJson] IS NULL OR ISJSON([DutiesJson]) = 1");
                    table.ForeignKey(
                        name: "FK_OrgPositions_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "ops",
                        principalTable: "Departments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OrgPositions_OrgPositions_ReportsToId",
                        column: x => x.ReportsToId,
                        principalSchema: "ops",
                        principalTable: "OrgPositions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OrgPositions_Users_HolderUserId",
                        column: x => x.HolderUserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Partners",
                schema: "pitch",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    BookingCommission = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    StoreCommission = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    PayoutMethod = table.Column<byte>(type: "tinyint", nullable: false),
                    PayoutDetail = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    SuspendedReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SlotPostingBlocked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Partners", x => x.Id);
                    table.CheckConstraint("CK_Partners_BookingCommission", "[BookingCommission] IS NULL OR [BookingCommission] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_Partners_PayoutMethod", "[PayoutMethod] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_Partners_Status", "[Status] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_Partners_StoreCommission", "[StoreCommission] IS NULL OR [StoreCommission] BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_Partners_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                schema: "money",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    PayerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Provider = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    ProviderRef = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Purpose = table.Column<byte>(type: "tinyint", nullable: false),
                    Method = table.Column<byte>(type: "tinyint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    Currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false, defaultValue: "EGP"),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    PaidAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RawWebhookJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.CheckConstraint("CK_Payments_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_Payments_Method", "[Method] BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_Payments_Purpose", "[Purpose] BETWEEN 0 AND 7");
                    table.CheckConstraint("CK_Payments_RawWebhookJson_IsJson", "[RawWebhookJson] IS NULL OR ISJSON([RawWebhookJson]) = 1");
                    table.CheckConstraint("CK_Payments_Status", "[Status] BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_Payments_Vat", "[VatAmount] >= 0 AND [VatAmount] <= [Amount]");
                    table.ForeignKey(
                        name: "FK_Payments_Users_PayerUserId",
                        column: x => x.PayerUserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PointEntries",
                schema: "money",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    RefType = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    RefId = table.Column<long>(type: "bigint", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointEntries", x => x.Id);
                    table.CheckConstraint("CK_PointEntries_Kind", "[Kind] BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_PointEntries_Points", "[Points] <> 0");
                    table.ForeignKey(
                        name: "FK_PointEntries_Users_UserId",
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
                name: "Receipts",
                schema: "money",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    RefType = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    RefId = table.Column<long>(type: "bigint", nullable: false),
                    FileId = table.Column<long>(type: "bigint", nullable: true),
                    EtaUuid = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipts", x => x.Id);
                    table.CheckConstraint("CK_Receipts_Amount", "[Amount] >= 0");
                    table.CheckConstraint("CK_Receipts_Kind", "[Kind] BETWEEN 0 AND 6");
                    table.ForeignKey(
                        name: "FK_Receipts_Files_FileId",
                        column: x => x.FileId,
                        principalSchema: "ops",
                        principalTable: "Files",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Receipts_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StaffLimits",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RefundLimit = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    CreditLimit = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    PayoutLimit = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    CanSecondApprove = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffLimits", x => x.Id);
                    table.CheckConstraint("CK_StaffLimits_CreditLimit", "[CreditLimit] >= 0");
                    table.CheckConstraint("CK_StaffLimits_PayoutLimit", "[PayoutLimit] >= 0");
                    table.CheckConstraint("CK_StaffLimits_RefundLimit", "[RefundLimit] >= 0");
                    table.ForeignKey(
                        name: "FK_StaffLimits_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Stores",
                schema: "shop",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    Handle = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Tagline = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    About = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LogoFileId = table.Column<long>(type: "bigint", nullable: true),
                    CoverFileId = table.Column<long>(type: "bigint", nullable: true),
                    CoverPositionY = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)50),
                    AccentColor = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: false, defaultValue: "#17795A"),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    BreakUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    BreakNote = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.Id);
                    table.CheckConstraint("CK_Stores_CoverPositionY", "[CoverPositionY] <= 100");
                    table.CheckConstraint("CK_Stores_Handle", "[Handle] NOT LIKE '%[^a-z0-9-]%'");
                    table.CheckConstraint("CK_Stores_Status", "[Status] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_Stores_Type", "[Type] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_Stores_Files_CoverFileId",
                        column: x => x.CoverFileId,
                        principalSchema: "ops",
                        principalTable: "Files",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Stores_Files_LogoFileId",
                        column: x => x.LogoFileId,
                        principalSchema: "ops",
                        principalTable: "Files",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Stores_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserAddresses",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AreaId = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, defaultValue: "Home"),
                    Street = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Building = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Floor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Apartment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Landmark = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Phone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAddresses", x => x.Id);
                    table.CheckConstraint("CK_UserAddresses_Phone", "[Phone] LIKE '+20%'");
                    table.ForeignKey(
                        name: "FK_UserAddresses_Areas_AreaId",
                        column: x => x.AreaId,
                        principalSchema: "geo",
                        principalTable: "Areas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserAddresses_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserClaims",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClaims_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLogins",
                schema: "auth",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPermissions",
                schema: "auth",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false),
                    GrantedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermissions", x => new { x.UserId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_UserPermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalSchema: "auth",
                        principalTable: "Permissions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserPermissions_Users_GrantedById",
                        column: x => x.GrantedById,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserPermissions_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "auth",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "auth",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                schema: "auth",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vouchers",
                schema: "money",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Source = table.Column<byte>(type: "tinyint", nullable: false),
                    PointsUsed = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    UsedOnType = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    UsedOnId = table.Column<long>(type: "bigint", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vouchers", x => x.Id);
                    table.CheckConstraint("CK_Vouchers_Amount", "[Amount] >= 100");
                    table.CheckConstraint("CK_Vouchers_PointsUsed", "[PointsUsed] >= 0");
                    table.CheckConstraint("CK_Vouchers_Source", "[Source] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_Vouchers_Status", "[Status] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_Vouchers_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Wallets",
                schema: "money",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    Currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false, defaultValue: "EGP"),
                    PendingBalance = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    AvailableBalance = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    SettledTotal = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wallets", x => x.Id);
                    table.CheckConstraint("CK_Wallets_AvailableBalance", "[AvailableBalance] >= 0 OR [Type] >= 5");
                    table.CheckConstraint("CK_Wallets_Type", "[Type] BETWEEN 0 AND 6");
                    table.ForeignKey(
                        name: "FK_Wallets_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerRatings",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<long>(type: "bigint", nullable: false),
                    RaterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stars = table.Column<byte>(type: "tinyint", nullable: true),
                    ShowedUp = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerRatings", x => x.Id);
                    table.CheckConstraint("CK_PlayerRatings_Stars", "[Stars] IS NULL OR [Stars] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_PlayerRatings_Games_GameId",
                        column: x => x.GameId,
                        principalSchema: "booking",
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerRatings_Users_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerRatings_Users_RaterId",
                        column: x => x.RaterId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "KeyResults",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GoalId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Metric = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: true),
                    Target = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Current = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeyResults", x => x.Id);
                    table.CheckConstraint("CK_KeyResults_Target", "[Target] > 0");
                    table.ForeignKey(
                        name: "FK_KeyResults_Goals_GoalId",
                        column: x => x.GoalId,
                        principalSchema: "ops",
                        principalTable: "Goals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LegalAcceptances",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VersionId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Context = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    ContextId = table.Column<long>(type: "bigint", nullable: true),
                    Ip = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalAcceptances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegalAcceptances_LegalVersions_VersionId",
                        column: x => x.VersionId,
                        principalSchema: "content",
                        principalTable: "LegalVersions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LegalAcceptances_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PartnerStaff",
                schema: "pitch",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<byte>(type: "tinyint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerStaff", x => x.Id);
                    table.CheckConstraint("CK_PartnerStaff_Role", "[Role] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_PartnerStaff_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalSchema: "pitch",
                        principalTable: "Partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartnerStaff_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Venues",
                schema: "pitch",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AreaId = table.Column<int>(type: "int", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Lat = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    Lng = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    Phone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Venues", x => x.Id);
                    table.CheckConstraint("CK_Venues_Lat", "[Lat] BETWEEN 22 AND 32");
                    table.CheckConstraint("CK_Venues_Lng", "[Lng] BETWEEN 24 AND 37");
                    table.ForeignKey(
                        name: "FK_Venues_Areas_AreaId",
                        column: x => x.AreaId,
                        principalSchema: "geo",
                        principalTable: "Areas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Venues_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalSchema: "pitch",
                        principalTable: "Partners",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GameRequests",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<long>(type: "bigint", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PaymentId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameRequests", x => x.Id);
                    table.CheckConstraint("CK_GameRequests_Status", "[Status] BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_GameRequests_Games_GameId",
                        column: x => x.GameId,
                        principalSchema: "booking",
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameRequests_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "money",
                        principalTable: "Payments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GameRequests_Users_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                schema: "money",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Destination = table.Column<byte>(type: "tinyint", nullable: false),
                    PaidBy = table.Column<byte>(type: "tinyint", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ProviderRef = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.Id);
                    table.CheckConstraint("CK_Refunds_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_Refunds_Destination", "[Destination] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_Refunds_PaidBy", "[PaidBy] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_Refunds_Status", "[Status] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_Refunds_Approvals_ApprovalId",
                        column: x => x.ApprovalId,
                        principalSchema: "money",
                        principalTable: "Approvals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Refunds_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "money",
                        principalTable: "Payments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Refunds_Users_RequestedById",
                        column: x => x.RequestedById,
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
                name: "Products",
                schema: "shop",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<long>(type: "bigint", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Slug = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    CompareAtPrice = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    LowStockAt = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    HiddenByAdmin = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    HiddenReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Rating = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: false, defaultValue: 0m),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.CheckConstraint("CK_Products_CompareAtPrice", "[CompareAtPrice] IS NULL OR [CompareAtPrice] > [Price]");
                    table.CheckConstraint("CK_Products_HiddenReason", "[HiddenByAdmin] = 0 OR [HiddenReason] IS NOT NULL");
                    table.CheckConstraint("CK_Products_LowStockAt", "[LowStockAt] >= 0");
                    table.CheckConstraint("CK_Products_Price", "[Price] > 0");
                    table.CheckConstraint("CK_Products_Status", "[Status] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_Products_ProductCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "shop",
                        principalTable: "ProductCategories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Products_Stores_StoreId",
                        column: x => x.StoreId,
                        principalSchema: "shop",
                        principalTable: "Stores",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Products_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PromoCodes",
                schema: "money",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Funding = table.Column<byte>(type: "tinyint", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StoreId = table.Column<long>(type: "bigint", nullable: true),
                    Scope = table.Column<byte>(type: "tinyint", nullable: false),
                    DiscountType = table.Column<byte>(type: "tinyint", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    MinSpend = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    MaxUses = table.Column<int>(type: "int", nullable: true),
                    PerCustomer = table.Column<int>(type: "int", nullable: true),
                    UsesCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    Label = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoCodes", x => x.Id);
                    table.CheckConstraint("CK_PromoCodes_Code", "[Code] NOT LIKE '%[^A-Z0-9]%'");
                    table.CheckConstraint("CK_PromoCodes_Dates", "[EndsOn] IS NULL OR [EndsOn] >= [StartsOn]");
                    table.CheckConstraint("CK_PromoCodes_DiscountType", "[DiscountType] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_PromoCodes_Funding", "[Funding] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_PromoCodes_MaxUses", "[MaxUses] IS NULL OR [MaxUses] > 0");
                    table.CheckConstraint("CK_PromoCodes_MinSpend", "[MinSpend] >= 0");
                    table.CheckConstraint("CK_PromoCodes_Owner", "[Funding] <> 0 OR [OwnerUserId] IS NOT NULL");
                    table.CheckConstraint("CK_PromoCodes_Percent", "[DiscountType] <> 0 OR [Value] <= 100");
                    table.CheckConstraint("CK_PromoCodes_PerCustomer", "[PerCustomer] IS NULL OR [PerCustomer] > 0");
                    table.CheckConstraint("CK_PromoCodes_Scope", "[Scope] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_PromoCodes_Status", "[Status] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_PromoCodes_UsesCount", "[UsesCount] >= 0");
                    table.CheckConstraint("CK_PromoCodes_Value", "[Value] >= 0");
                    table.ForeignKey(
                        name: "FK_PromoCodes_Stores_StoreId",
                        column: x => x.StoreId,
                        principalSchema: "shop",
                        principalTable: "Stores",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PromoCodes_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PromoCodes_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Sellers",
                schema: "pod",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<long>(type: "bigint", nullable: false),
                    LevelId = table.Column<int>(type: "int", nullable: false),
                    Mode = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    FrozenUntil = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    GraceUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    LifetimeSales = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Rating = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: false, defaultValue: 0m),
                    FeaturedDesignId = table.Column<long>(type: "bigint", nullable: true),
                    FeaturedByAdmin = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Tags = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PayoutMethod = table.Column<byte>(type: "tinyint", nullable: false),
                    PayoutDetail = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sellers", x => x.Id);
                    table.CheckConstraint("CK_Sellers_LifetimeSales", "[LifetimeSales] >= 0");
                    table.CheckConstraint("CK_Sellers_Mode", "[Mode] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_Sellers_PayoutMethod", "[PayoutMethod] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_Sellers_Rating", "[Rating] BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_Sellers_Status", "[Status] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_Sellers_Designs_FeaturedDesignId",
                        column: x => x.FeaturedDesignId,
                        principalSchema: "pod",
                        principalTable: "Designs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Sellers_SellerLevels_LevelId",
                        column: x => x.LevelId,
                        principalSchema: "pod",
                        principalTable: "SellerLevels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Sellers_Stores_StoreId",
                        column: x => x.StoreId,
                        principalSchema: "shop",
                        principalTable: "Stores",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Sellers_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LedgerEntries",
                schema: "money",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Bucket = table.Column<byte>(type: "tinyint", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    RefType = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    RefId = table.Column<long>(type: "bigint", nullable: false),
                    AvailableAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerEntries", x => x.Id);
                    table.CheckConstraint("CK_LedgerEntries_Amount", "[Amount] <> 0");
                    table.CheckConstraint("CK_LedgerEntries_Bucket", "[Bucket] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_LedgerEntries_Kind", "[Kind] BETWEEN 0 AND 14");
                    table.ForeignKey(
                        name: "FK_LedgerEntries_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalSchema: "money",
                        principalTable: "Wallets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PayoutRequests",
                schema: "money",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(14)", unicode: false, maxLength: 14, nullable: false),
                    WalletId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Method = table.Column<byte>(type: "tinyint", nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    PreparedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SentById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TransferRef = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    RejectReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ApprovalId = table.Column<long>(type: "bigint", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutRequests", x => x.Id);
                    table.CheckConstraint("CK_PayoutRequests_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_PayoutRequests_Method", "[Method] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_PayoutRequests_RejectReason", "[Status] <> 4 OR [RejectReason] IS NOT NULL");
                    table.CheckConstraint("CK_PayoutRequests_Status", "[Status] BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_PayoutRequests_TransferRef", "[Status] <> 3 OR [TransferRef] IS NOT NULL");
                    table.CheckConstraint("CK_PayoutRequests_TwoPeople", "[SentById] IS NULL OR [PreparedById] IS NULL OR [SentById] <> [PreparedById]");
                    table.ForeignKey(
                        name: "FK_PayoutRequests_Approvals_ApprovalId",
                        column: x => x.ApprovalId,
                        principalSchema: "money",
                        principalTable: "Approvals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PayoutRequests_Users_PreparedById",
                        column: x => x.PreparedById,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PayoutRequests_Users_SentById",
                        column: x => x.SentById,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PayoutRequests_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalSchema: "money",
                        principalTable: "Wallets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Pitches",
                schema: "pitch",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VenueId = table.Column<long>(type: "bigint", nullable: false),
                    Slug = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Format = table.Column<byte>(type: "tinyint", nullable: false),
                    Surface = table.Column<byte>(type: "tinyint", nullable: false),
                    IsIndoor = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PricePerHour = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    PeakPricePerHour = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    PeakFromHour = table.Column<byte>(type: "tinyint", nullable: true),
                    DescriptionEn = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DescriptionAr = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CancellationPolicyId = table.Column<int>(type: "int", nullable: false),
                    AllowCash = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Rating = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: false, defaultValue: 0m),
                    ReviewsCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pitches", x => x.Id);
                    table.CheckConstraint("CK_Pitches_Format", "[Format] IN (5, 7, 11)");
                    table.CheckConstraint("CK_Pitches_PeakFromHour", "[PeakFromHour] IS NULL OR [PeakFromHour] <= 23");
                    table.CheckConstraint("CK_Pitches_PeakPricePerHour", "[PeakPricePerHour] IS NULL OR [PeakPricePerHour] > 0");
                    table.CheckConstraint("CK_Pitches_PricePerHour", "[PricePerHour] > 0");
                    table.CheckConstraint("CK_Pitches_Rating", "[Rating] BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_Pitches_Status", "[Status] BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_Pitches_Surface", "[Surface] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_Pitches_CancellationPolicies_CancellationPolicyId",
                        column: x => x.CancellationPolicyId,
                        principalSchema: "pitch",
                        principalTable: "CancellationPolicies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Pitches_Users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Pitches_Venues_VenueId",
                        column: x => x.VenueId,
                        principalSchema: "pitch",
                        principalTable: "Venues",
                        principalColumn: "Id");
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
                name: "ProductImages",
                schema: "shop",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    FileId = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductImages_Files_FileId",
                        column: x => x.FileId,
                        principalSchema: "ops",
                        principalTable: "Files",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductImages_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "shop",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductSpecs",
                schema: "shop",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSpecs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductSpecs_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "shop",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductVariants",
                schema: "shop",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    Size = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "One size"),
                    ColorName = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, defaultValue: "Standard"),
                    ColorHex = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: true),
                    Sku = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Stock = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Reserved = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    PriceOverride = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVariants", x => x.Id);
                    table.CheckConstraint("CK_ProductVariants_PriceOverride", "[PriceOverride] IS NULL OR [PriceOverride] > 0");
                    table.CheckConstraint("CK_ProductVariants_Reserved", "[Reserved] >= 0 AND [Reserved] <= [Stock]");
                    table.CheckConstraint("CK_ProductVariants_Stock", "[Stock] >= 0");
                    table.ForeignKey(
                        name: "FK_ProductVariants_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "shop",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WishlistItems",
                schema: "shop",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: true),
                    DesignId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WishlistItems", x => x.Id);
                    table.CheckConstraint("CK_WishlistItems_OneItem", "([ProductId] IS NULL AND [DesignId] IS NOT NULL) OR ([ProductId] IS NOT NULL AND [DesignId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_WishlistItems_Designs_DesignId",
                        column: x => x.DesignId,
                        principalSchema: "pod",
                        principalTable: "Designs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WishlistItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "shop",
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WishlistItems_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                schema: "shop",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    DeliveryFee = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    Total = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    PromoCodeId = table.Column<long>(type: "bigint", nullable: true),
                    PaymentMethod = table.Column<byte>(type: "tinyint", nullable: false),
                    PaymentId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    DeliverySpeed = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.CheckConstraint("CK_Orders_AddressJson_IsJson", "ISJSON([AddressJson]) = 1");
                    table.CheckConstraint("CK_Orders_DeliveryFee", "[DeliveryFee] >= 0");
                    table.CheckConstraint("CK_Orders_DeliverySpeed", "[DeliverySpeed] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_Orders_DiscountAmount", "[DiscountAmount] >= 0");
                    table.CheckConstraint("CK_Orders_PaymentMethod", "[PaymentMethod] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_Orders_Status", "[Status] BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_Orders_Subtotal", "[Subtotal] >= 0");
                    table.CheckConstraint("CK_Orders_Total", "[Total] >= 0");
                    table.ForeignKey(
                        name: "FK_Orders_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "money",
                        principalTable: "Payments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Orders_PromoCodes_PromoCodeId",
                        column: x => x.PromoCodeId,
                        principalSchema: "money",
                        principalTable: "PromoCodes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Orders_Users_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LevelReviews",
                schema: "pod",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SellerId = table.Column<long>(type: "bigint", nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    FromLevelId = table.Column<int>(type: "int", nullable: false),
                    ToLevelId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<byte>(type: "tinyint", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LevelReviews", x => x.Id);
                    table.CheckConstraint("CK_LevelReviews_Action", "[Action] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_LevelReviews_SellerLevels_FromLevelId",
                        column: x => x.FromLevelId,
                        principalSchema: "pod",
                        principalTable: "SellerLevels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LevelReviews_SellerLevels_ToLevelId",
                        column: x => x.ToLevelId,
                        principalSchema: "pod",
                        principalTable: "SellerLevels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LevelReviews_Sellers_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "pod",
                        principalTable: "Sellers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SellerFollowers",
                schema: "pod",
                columns: table => new
                {
                    SellerId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellerFollowers", x => new { x.SellerId, x.UserId });
                    table.ForeignKey(
                        name: "FK_SellerFollowers_Sellers_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "pod",
                        principalTable: "Sellers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SellerFollowers_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SellerStrikes",
                schema: "pod",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SellerId = table.Column<long>(type: "bigint", nullable: false),
                    DesignId = table.Column<long>(type: "bigint", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    RemovedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RemovedReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellerStrikes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SellerStrikes_Designs_DesignId",
                        column: x => x.DesignId,
                        principalSchema: "pod",
                        principalTable: "Designs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SellerStrikes_Sellers_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "pod",
                        principalTable: "Sellers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SellerStrikes_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Offers",
                schema: "pitch",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PitchId = table.Column<long>(type: "bigint", nullable: false),
                    DaysMask = table.Column<byte>(type: "tinyint", nullable: false),
                    FromHour = table.Column<byte>(type: "tinyint", nullable: false),
                    ToHour = table.Column<byte>(type: "tinyint", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Offers", x => x.Id);
                    table.CheckConstraint("CK_Offers_Dates", "[EndsOn] >= [StartsOn]");
                    table.CheckConstraint("CK_Offers_DaysMask", "[DaysMask] BETWEEN 1 AND 127");
                    table.CheckConstraint("CK_Offers_DiscountPercent", "[DiscountPercent] > 0 AND [DiscountPercent] <= 90");
                    table.CheckConstraint("CK_Offers_FromHour", "[FromHour] <= 24");
                    table.CheckConstraint("CK_Offers_Hours", "[ToHour] > [FromHour]");
                    table.CheckConstraint("CK_Offers_ToHour", "[ToHour] <= 24");
                    table.ForeignKey(
                        name: "FK_Offers_Pitches_PitchId",
                        column: x => x.PitchId,
                        principalSchema: "pitch",
                        principalTable: "Pitches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Offers_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OpeningHours",
                schema: "pitch",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PitchId = table.Column<long>(type: "bigint", nullable: false),
                    DayOfWeek = table.Column<byte>(type: "tinyint", nullable: false),
                    OpenHour = table.Column<byte>(type: "tinyint", nullable: false),
                    CloseHour = table.Column<byte>(type: "tinyint", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningHours", x => x.Id);
                    table.CheckConstraint("CK_OpeningHours_CloseHour", "[CloseHour] BETWEEN 1 AND 24");
                    table.CheckConstraint("CK_OpeningHours_DayOfWeek", "[DayOfWeek] <= 6");
                    table.CheckConstraint("CK_OpeningHours_Hours", "[CloseHour] > [OpenHour]");
                    table.CheckConstraint("CK_OpeningHours_OpenHour", "[OpenHour] <= 23");
                    table.ForeignKey(
                        name: "FK_OpeningHours_Pitches_PitchId",
                        column: x => x.PitchId,
                        principalSchema: "pitch",
                        principalTable: "Pitches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PackageOptions",
                schema: "pitch",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PitchId = table.Column<long>(type: "bigint", nullable: false),
                    MatchCount = table.Column<byte>(type: "tinyint", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    ApprovedDiscount = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RejectReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageOptions", x => x.Id);
                    table.CheckConstraint("CK_PackageOptions_ApprovedDiscount", "[ApprovedDiscount] IS NULL OR [ApprovedDiscount] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_PackageOptions_DiscountPercent", "[DiscountPercent] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_PackageOptions_MatchCount", "[MatchCount] BETWEEN 1 AND 12");
                    table.CheckConstraint("CK_PackageOptions_Status", "[Status] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_PackageOptions_Pitches_PitchId",
                        column: x => x.PitchId,
                        principalSchema: "pitch",
                        principalTable: "Pitches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PackageOptions_Users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PitchFacilities",
                schema: "pitch",
                columns: table => new
                {
                    PitchId = table.Column<long>(type: "bigint", nullable: false),
                    FacilityId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PitchFacilities", x => new { x.PitchId, x.FacilityId });
                    table.ForeignKey(
                        name: "FK_PitchFacilities_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalSchema: "pitch",
                        principalTable: "Facilities",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PitchFacilities_Pitches_PitchId",
                        column: x => x.PitchId,
                        principalSchema: "pitch",
                        principalTable: "Pitches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PitchPhotos",
                schema: "pitch",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PitchId = table.Column<long>(type: "bigint", nullable: false),
                    FileId = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PitchPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PitchPhotos_Files_FileId",
                        column: x => x.FileId,
                        principalSchema: "ops",
                        principalTable: "Files",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PitchPhotos_Pitches_PitchId",
                        column: x => x.PitchId,
                        principalSchema: "pitch",
                        principalTable: "Pitches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PromoCodePitches",
                schema: "money",
                columns: table => new
                {
                    PromoCodeId = table.Column<long>(type: "bigint", nullable: false),
                    PitchId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoCodePitches", x => new { x.PromoCodeId, x.PitchId });
                    table.ForeignKey(
                        name: "FK_PromoCodePitches_Pitches_PitchId",
                        column: x => x.PitchId,
                        principalSchema: "pitch",
                        principalTable: "Pitches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PromoCodePitches_PromoCodes_PromoCodeId",
                        column: x => x.PromoCodeId,
                        principalSchema: "money",
                        principalTable: "PromoCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                schema: "pitch",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PitchId = table.Column<long>(type: "bigint", nullable: false),
                    BookingId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stars = table.Column<byte>(type: "tinyint", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Reply = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RepliedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    IsHidden = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                    table.CheckConstraint("CK_Reviews_Stars", "[Stars] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_Reviews_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "booking",
                        principalTable: "Bookings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reviews_Pitches_PitchId",
                        column: x => x.PitchId,
                        principalSchema: "pitch",
                        principalTable: "Pitches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reviews_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Series",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PitchId = table.Column<long>(type: "bigint", nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ContactUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContactPhone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    DayOfWeek = table.Column<byte>(type: "tinyint", nullable: false),
                    StartHour = table.Column<byte>(type: "tinyint", nullable: false),
                    Duration = table.Column<byte>(type: "tinyint", nullable: false),
                    AutoRenew = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Series", x => x.Id);
                    table.CheckConstraint("CK_Series_DayOfWeek", "[DayOfWeek] <= 6");
                    table.CheckConstraint("CK_Series_Duration", "[Duration] BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_Series_StartHour", "[StartHour] <= 23");
                    table.CheckConstraint("CK_Series_Status", "[Status] BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_Series_Pitches_PitchId",
                        column: x => x.PitchId,
                        principalSchema: "pitch",
                        principalTable: "Pitches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Series_Users_ContactUserId",
                        column: x => x.ContactUserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SlotAlerts",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PitchId = table.Column<long>(type: "bigint", nullable: true),
                    AreaId = table.Column<int>(type: "int", nullable: true),
                    FromHour = table.Column<byte>(type: "tinyint", nullable: true),
                    ToHour = table.Column<byte>(type: "tinyint", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlotAlerts", x => x.Id);
                    table.CheckConstraint("CK_SlotAlerts_FromHour", "[FromHour] IS NULL OR [FromHour] <= 24");
                    table.CheckConstraint("CK_SlotAlerts_Target", "[PitchId] IS NOT NULL OR [AreaId] IS NOT NULL");
                    table.CheckConstraint("CK_SlotAlerts_ToHour", "[ToHour] IS NULL OR [ToHour] <= 24");
                    table.ForeignKey(
                        name: "FK_SlotAlerts_Areas_AreaId",
                        column: x => x.AreaId,
                        principalSchema: "geo",
                        principalTable: "Areas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SlotAlerts_Pitches_PitchId",
                        column: x => x.PitchId,
                        principalSchema: "pitch",
                        principalTable: "Pitches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SlotAlerts_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SlotHolds",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PitchId = table.Column<long>(type: "bigint", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Hour = table.Column<byte>(type: "tinyint", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlotHolds", x => x.Id);
                    table.CheckConstraint("CK_SlotHolds_Hour", "[Hour] <= 23");
                    table.ForeignKey(
                        name: "FK_SlotHolds_Pitches_PitchId",
                        column: x => x.PitchId,
                        principalSchema: "pitch",
                        principalTable: "Pitches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SlotHolds_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WaitlistEntries",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PitchId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Hour = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    NotifiedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitlistEntries", x => x.Id);
                    table.CheckConstraint("CK_WaitlistEntries_Hour", "[Hour] <= 23");
                    table.CheckConstraint("CK_WaitlistEntries_Status", "[Status] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_WaitlistEntries_Pitches_PitchId",
                        column: x => x.PitchId,
                        principalSchema: "pitch",
                        principalTable: "Pitches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WaitlistEntries_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Fulfilments",
                schema: "shop",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    StoreId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fulfilments", x => x.Id);
                    table.CheckConstraint("CK_Fulfilments_Status", "[Status] BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_Fulfilments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "shop",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Fulfilments_Stores_StoreId",
                        column: x => x.StoreId,
                        principalSchema: "shop",
                        principalTable: "Stores",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                schema: "shop",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    StoreId = table.Column<long>(type: "bigint", nullable: false),
                    ProductVariantId = table.Column<long>(type: "bigint", nullable: true),
                    DesignId = table.Column<long>(type: "bigint", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Size = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Qty = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    CommissionAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    SellerEarning = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.CheckConstraint("CK_OrderItems_Kind", "[Kind] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_OrderItems_Qty", "[Qty] BETWEEN 1 AND 20");
                    table.CheckConstraint("CK_OrderItems_Status", "[Status] BETWEEN 0 AND 7");
                    table.CheckConstraint("CK_OrderItems_UnitPrice", "[UnitPrice] >= 0");
                    table.ForeignKey(
                        name: "FK_OrderItems_Designs_DesignId",
                        column: x => x.DesignId,
                        principalSchema: "pod",
                        principalTable: "Designs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "shop",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItems_ProductVariants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalSchema: "shop",
                        principalTable: "ProductVariants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OrderItems_Stores_StoreId",
                        column: x => x.StoreId,
                        principalSchema: "shop",
                        principalTable: "Stores",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PromoCodeUses",
                schema: "money",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PromoCodeId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: true),
                    BookingId = table.Column<long>(type: "bigint", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    RevenueAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoCodeUses", x => x.Id);
                    table.CheckConstraint("CK_PromoCodeUses_DiscountAmount", "[DiscountAmount] > 0");
                    table.CheckConstraint("CK_PromoCodeUses_Target", "[OrderId] IS NOT NULL OR [BookingId] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_PromoCodeUses_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "booking",
                        principalTable: "Bookings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PromoCodeUses_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "shop",
                        principalTable: "Orders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PromoCodeUses_PromoCodes_PromoCodeId",
                        column: x => x.PromoCodeId,
                        principalSchema: "money",
                        principalTable: "PromoCodes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PromoCodeUses_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Returns",
                schema: "ship",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(14)", unicode: false, maxLength: 14, nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<byte>(type: "tinyint", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    DecidedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RefundAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    CustomerPaysDelivery = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Returns", x => x.Id);
                    table.CheckConstraint("CK_Returns_Reason", "[Reason] BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_Returns_RefundAmount", "[RefundAmount] >= 0");
                    table.CheckConstraint("CK_Returns_Status", "[Status] BETWEEN 0 AND 6");
                    table.ForeignKey(
                        name: "FK_Returns_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "shop",
                        principalTable: "Orders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Returns_Users_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Returns_Users_DecidedById",
                        column: x => x.DecidedById,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Samples",
                schema: "pod",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SellerId = table.Column<long>(type: "bigint", nullable: false),
                    DesignId = table.Column<long>(type: "bigint", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Size = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    IsFree = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Samples", x => x.Id);
                    table.CheckConstraint("CK_Samples_Price", "[Price] >= 0");
                    table.CheckConstraint("CK_Samples_Status", "[Status] BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_Samples_Designs_DesignId",
                        column: x => x.DesignId,
                        principalSchema: "pod",
                        principalTable: "Designs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Samples_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "shop",
                        principalTable: "Orders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Samples_Sellers_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "pod",
                        principalTable: "Sellers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                schema: "support",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    OpenedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpenedFrom = table.Column<byte>(type: "tinyint", nullable: false),
                    Topic = table.Column<byte>(type: "tinyint", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    BookingId = table.Column<long>(type: "bigint", nullable: true),
                    OrderId = table.Column<long>(type: "bigint", nullable: true),
                    AgainstUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDispute = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DisputeAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Priority = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)1),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedToAgentId = table.Column<long>(type: "bigint", nullable: true),
                    FirstResponseAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.CheckConstraint("CK_Tickets_DisputeAmount", "[DisputeAmount] IS NULL OR [DisputeAmount] > 0");
                    table.CheckConstraint("CK_Tickets_OpenedFrom", "[OpenedFrom] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_Tickets_Priority", "[Priority] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_Tickets_Status", "[Status] BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_Tickets_Topic", "[Topic] BETWEEN 0 AND 7");
                    table.ForeignKey(
                        name: "FK_Tickets_Agents_AssignedToAgentId",
                        column: x => x.AssignedToAgentId,
                        principalSchema: "ai",
                        principalTable: "Agents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tickets_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "booking",
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Tickets_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "shop",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Tickets_Users_AgainstUserId",
                        column: x => x.AgainstUserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tickets_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tickets_Users_OpenedById",
                        column: x => x.OpenedById,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FreeSlots",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PitchId = table.Column<long>(type: "bigint", nullable: false),
                    PostedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Hour = table.Column<byte>(type: "tinyint", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                    OfferId = table.Column<long>(type: "bigint", nullable: true),
                    BookingId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FreeSlots", x => x.Id);
                    table.CheckConstraint("CK_FreeSlots_DiscountPercent", "[DiscountPercent] BETWEEN 0 AND 90");
                    table.CheckConstraint("CK_FreeSlots_Hour", "[Hour] <= 23");
                    table.CheckConstraint("CK_FreeSlots_Status", "[Status] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_FreeSlots_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "booking",
                        principalTable: "Bookings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FreeSlots_Offers_OfferId",
                        column: x => x.OfferId,
                        principalSchema: "pitch",
                        principalTable: "Offers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FreeSlots_Pitches_PitchId",
                        column: x => x.PitchId,
                        principalSchema: "pitch",
                        principalTable: "Pitches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FreeSlots_Users_PostedById",
                        column: x => x.PostedById,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Packages",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    PitchId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackageOptionId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentId = table.Column<long>(type: "bigint", nullable: true),
                    MatchCount = table.Column<byte>(type: "tinyint", nullable: false),
                    UsedCount = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    DayOfWeek = table.Column<byte>(type: "tinyint", nullable: false),
                    StartHour = table.Column<byte>(type: "tinyint", nullable: false),
                    Duration = table.Column<byte>(type: "tinyint", nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TotalPrice = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    QrSecretHash = table.Column<byte[]>(type: "varbinary(64)", maxLength: 64, nullable: false),
                    ExceptionUsed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ExceptionById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Packages", x => x.Id);
                    table.CheckConstraint("CK_Packages_Dates", "[EndsOn] >= [StartsOn]");
                    table.CheckConstraint("CK_Packages_DayOfWeek", "[DayOfWeek] <= 6");
                    table.CheckConstraint("CK_Packages_DiscountPercent", "[DiscountPercent] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_Packages_Duration", "[Duration] BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_Packages_MatchCount", "[MatchCount] BETWEEN 1 AND 12");
                    table.CheckConstraint("CK_Packages_StartHour", "[StartHour] <= 23");
                    table.CheckConstraint("CK_Packages_Status", "[Status] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_Packages_TotalPrice", "[TotalPrice] > 0");
                    table.CheckConstraint("CK_Packages_Used", "[UsedCount] <= [MatchCount]");
                    table.ForeignKey(
                        name: "FK_Packages_PackageOptions_PackageOptionId",
                        column: x => x.PackageOptionId,
                        principalSchema: "pitch",
                        principalTable: "PackageOptions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Packages_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "money",
                        principalTable: "Payments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Packages_Pitches_PitchId",
                        column: x => x.PitchId,
                        principalSchema: "pitch",
                        principalTable: "Pitches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Packages_Users_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Packages_Users_ExceptionById",
                        column: x => x.ExceptionById,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ReviewReports",
                schema: "pitch",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReviewId = table.Column<long>(type: "bigint", nullable: false),
                    ReporterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewReports", x => x.Id);
                    table.CheckConstraint("CK_ReviewReports_Status", "[Status] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_ReviewReports_Reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalSchema: "pitch",
                        principalTable: "Reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReviewReports_Users_ReporterId",
                        column: x => x.ReporterId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Shipments",
                schema: "ship",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    FulfilmentId = table.Column<long>(type: "bigint", nullable: true),
                    FactoryId = table.Column<long>(type: "bigint", nullable: true),
                    Provider = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Mode = table.Column<byte>(type: "tinyint", nullable: false),
                    TrackingNumber = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    CodAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    LabelFileId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    PickupAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CodRemittedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    IsReturn = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
                    table.CheckConstraint("CK_Shipments_CodAmount", "[CodAmount] >= 0");
                    table.CheckConstraint("CK_Shipments_Mode", "[Mode] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_Shipments_Status", "[Status] BETWEEN 0 AND 6");
                    table.ForeignKey(
                        name: "FK_Shipments_Factories_FactoryId",
                        column: x => x.FactoryId,
                        principalSchema: "factory",
                        principalTable: "Factories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Shipments_Files_LabelFileId",
                        column: x => x.LabelFileId,
                        principalSchema: "ops",
                        principalTable: "Files",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Shipments_Fulfilments_FulfilmentId",
                        column: x => x.FulfilmentId,
                        principalSchema: "shop",
                        principalTable: "Fulfilments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Shipments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "shop",
                        principalTable: "Orders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Jobs",
                schema: "factory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    OrderItemId = table.Column<long>(type: "bigint", nullable: false),
                    FactoryId = table.Column<long>(type: "bigint", nullable: false),
                    Stage = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    DueAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    AssignedStaffId = table.Column<long>(type: "bigint", nullable: true),
                    ReprintCount = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    IsSample = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeductionAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                    table.CheckConstraint("CK_Jobs_DeductionAmount", "[DeductionAmount] >= 0");
                    table.CheckConstraint("CK_Jobs_Stage", "[Stage] BETWEEN 0 AND 6");
                    table.ForeignKey(
                        name: "FK_Jobs_Factories_FactoryId",
                        column: x => x.FactoryId,
                        principalSchema: "factory",
                        principalTable: "Factories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Jobs_FactoryStaff_AssignedStaffId",
                        column: x => x.AssignedStaffId,
                        principalSchema: "factory",
                        principalTable: "FactoryStaff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Jobs_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalSchema: "shop",
                        principalTable: "OrderItems",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ReturnItems",
                schema: "ship",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnId = table.Column<long>(type: "bigint", nullable: false),
                    OrderItemId = table.Column<long>(type: "bigint", nullable: false),
                    Qty = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<byte>(type: "tinyint", nullable: false),
                    NewSize = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    NewColor = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnItems", x => x.Id);
                    table.CheckConstraint("CK_ReturnItems_Action", "[Action] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_ReturnItems_Qty", "[Qty] >= 1");
                    table.ForeignKey(
                        name: "FK_ReturnItems_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalSchema: "shop",
                        principalTable: "OrderItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReturnItems_Returns_ReturnId",
                        column: x => x.ReturnId,
                        principalSchema: "ship",
                        principalTable: "Returns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketMessages",
                schema: "support",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    SenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsInternalNote = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketMessages", x => x.Id);
                    table.CheckConstraint("CK_TicketMessages_Body", "LEN([Body]) >= 1");
                    table.ForeignKey(
                        name: "FK_TicketMessages_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "support",
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TicketMessages_Users_SenderId",
                        column: x => x.SenderId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "QrScans",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<long>(type: "bigint", nullable: true),
                    PackageId = table.Column<long>(type: "bigint", nullable: true),
                    ScannedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PitchId = table.Column<long>(type: "bigint", nullable: false),
                    ScannedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    Result = table.Column<byte>(type: "tinyint", nullable: false),
                    MatchNumber = table.Column<byte>(type: "tinyint", nullable: true),
                    Device = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QrScans", x => x.Id);
                    table.CheckConstraint("CK_QrScans_Result", "[Result] BETWEEN 0 AND 8");
                    table.CheckConstraint("CK_QrScans_Target", "[BookingId] IS NOT NULL OR [PackageId] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_QrScans_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "booking",
                        principalTable: "Bookings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QrScans_Packages_PackageId",
                        column: x => x.PackageId,
                        principalSchema: "booking",
                        principalTable: "Packages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QrScans_Pitches_PitchId",
                        column: x => x.PitchId,
                        principalSchema: "pitch",
                        principalTable: "Pitches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QrScans_Users_ScannedById",
                        column: x => x.ScannedById,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ShipmentEvents",
                schema: "ship",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShipmentId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RawJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProviderEventId = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true),
                    At = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentEvents", x => x.Id);
                    table.CheckConstraint("CK_ShipmentEvents_RawJson_IsJson", "[RawJson] IS NULL OR ISJSON([RawJson]) = 1");
                    table.CheckConstraint("CK_ShipmentEvents_Status", "[Status] <= 6");
                    table.ForeignKey(
                        name: "FK_ShipmentEvents_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalSchema: "ship",
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobEvents",
                schema: "factory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobId = table.Column<long>(type: "bigint", nullable: false),
                    FromStage = table.Column<byte>(type: "tinyint", nullable: false),
                    ToStage = table.Column<byte>(type: "tinyint", nullable: false),
                    StaffId = table.Column<long>(type: "bigint", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    At = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobEvents", x => x.Id);
                    table.CheckConstraint("CK_JobEvents_FromStage", "[FromStage] <= 6");
                    table.CheckConstraint("CK_JobEvents_ToStage", "[ToStage] <= 6");
                    table.ForeignKey(
                        name: "FK_JobEvents_FactoryStaff_StaffId",
                        column: x => x.StaffId,
                        principalSchema: "factory",
                        principalTable: "FactoryStaff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JobEvents_Jobs_JobId",
                        column: x => x.JobId,
                        principalSchema: "factory",
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobProblems",
                schema: "factory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobId = table.Column<long>(type: "bigint", nullable: false),
                    ReportedByStaffId = table.Column<long>(type: "bigint", nullable: true),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    ResolvedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Resolution = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobProblems", x => x.Id);
                    table.CheckConstraint("CK_JobProblems_Kind", "[Kind] BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_JobProblems_Status", "[Status] BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_JobProblems_FactoryStaff_ReportedByStaffId",
                        column: x => x.ReportedByStaffId,
                        principalSchema: "factory",
                        principalTable: "FactoryStaff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JobProblems_Jobs_JobId",
                        column: x => x.JobId,
                        principalSchema: "factory",
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobProblems_Users_ResolvedById",
                        column: x => x.ResolvedById,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TicketAttachments",
                schema: "support",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    FileId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketAttachments_Files_FileId",
                        column: x => x.FileId,
                        principalSchema: "ops",
                        principalTable: "Files",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TicketAttachments_TicketMessages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "support",
                        principalTable: "TicketMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "IX_Adjustments_ApprovalId",
                schema: "money",
                table: "Adjustments",
                column: "ApprovalId");

            migrationBuilder.CreateIndex(
                name: "IX_Adjustments_ById",
                schema: "money",
                table: "Adjustments",
                column: "ById");

            migrationBuilder.CreateIndex(
                name: "IX_Adjustments_WalletId",
                schema: "money",
                table: "Adjustments",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentPermissions_PermissionId",
                schema: "ai",
                table: "AgentPermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_AgentId_StartedAt",
                schema: "ai",
                table: "AgentRuns",
                columns: new[] { "AgentId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_TaskId",
                schema: "ai",
                table: "AgentRuns",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "UQ_Agents_Name",
                schema: "ai",
                table: "Agents",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UQ_Agents_PositionId",
                schema: "ai",
                table: "Agents",
                column: "PositionId",
                unique: true,
                filter: "[IsDeleted] = 0 AND [PositionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_Agents_ServiceUserId",
                schema: "ai",
                table: "Agents",
                column: "ServiceUserId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AgentTasks_AgentId",
                schema: "ai",
                table: "AgentTasks",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_AgentId",
                schema: "ai",
                table: "ApiKeys",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "UQ_ApiKeys_Prefix",
                schema: "ai",
                table: "ApiKeys",
                column: "Prefix",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationFiles_ApplicationId",
                schema: "auth",
                table: "ApplicationFiles",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationFiles_FileId",
                schema: "auth",
                table: "ApplicationFiles",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_AgreementVersionId",
                schema: "auth",
                table: "Applications",
                column: "AgreementVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_ReviewedById",
                schema: "auth",
                table: "Applications",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Status_Type_CreatedAt",
                schema: "auth",
                table: "Applications",
                columns: new[] { "Status", "Type", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UQ_Applications_Code",
                schema: "auth",
                table: "Applications",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Applications_OneOpenPerType",
                schema: "auth",
                table: "Applications",
                columns: new[] { "ApplicantId", "Type" },
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_DeciderId",
                schema: "money",
                table: "ApprovalDecisions",
                column: "DeciderId");

            migrationBuilder.CreateIndex(
                name: "UQ_ApprovalDecisions_ApprovalId_DeciderId",
                schema: "money",
                table: "ApprovalDecisions",
                columns: new[] { "ApprovalId", "DeciderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_RequestedById",
                schema: "money",
                table: "Approvals",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_Status_CreatedAt",
                schema: "money",
                table: "Approvals",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UQ_Areas_GovernorateId_NameEn",
                schema: "geo",
                table: "Areas",
                columns: new[] { "GovernorateId", "NameEn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorAgentId",
                schema: "ops",
                table: "AuditLogs",
                column: "ActorAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorUserId_At",
                schema: "ops",
                table: "AuditLogs",
                columns: new[] { "ActorUserId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_At",
                schema: "ops",
                table: "AuditLogs",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                schema: "ops",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "UQ_BannedWords_Word",
                schema: "community",
                table: "BannedWords",
                column: "Word",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CustomerId_Date",
                schema: "booking",
                table: "Bookings",
                columns: new[] { "CustomerId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PackageId",
                schema: "booking",
                table: "Bookings",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PaymentId",
                schema: "booking",
                table: "Bookings",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PitchId_Date",
                schema: "booking",
                table: "Bookings",
                columns: new[] { "PitchId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PromoCodeId",
                schema: "booking",
                table: "Bookings",
                column: "PromoCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_SeriesId",
                schema: "booking",
                table: "Bookings",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Status_Date",
                schema: "booking",
                table: "Bookings",
                columns: new[] { "Status", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_WaiverVersionId",
                schema: "booking",
                table: "Bookings",
                column: "WaiverVersionId");

            migrationBuilder.CreateIndex(
                name: "UQ_Bookings_Code",
                schema: "booking",
                table: "Bookings",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingSlots_BookingId",
                schema: "booking",
                table: "BookingSlots",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "UQ_BookingSlots_PitchId_Date_Hour",
                schema: "booking",
                table: "BookingSlots",
                columns: new[] { "PitchId", "Date", "Hour" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Broadcasts_CreatedById",
                schema: "notify",
                table: "Broadcasts",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "UQ_Budgets_DepartmentId_Month",
                schema: "ops",
                table: "Budgets",
                columns: new[] { "DepartmentId", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_CancellationPolicies_Key",
                schema: "pitch",
                table: "CancellationPolicies",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_DesignId",
                schema: "shop",
                table: "CartItems",
                column: "DesignId");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_ProductVariantId",
                schema: "shop",
                table: "CartItems",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "UQ_CartItems_Design",
                schema: "shop",
                table: "CartItems",
                columns: new[] { "UserId", "DesignId", "Size", "Color" },
                unique: true,
                filter: "[DesignId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_CartItems_Variant",
                schema: "shop",
                table: "CartItems",
                columns: new[] { "UserId", "ProductVariantId" },
                unique: true,
                filter: "[ProductVariantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogChangeRequests_CatalogProductId",
                schema: "pod",
                table: "CatalogChangeRequests",
                column: "CatalogProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogChangeRequests_FactoryId",
                schema: "pod",
                table: "CatalogChangeRequests",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogChangeRequests_RequestedByStaffId",
                schema: "pod",
                table: "CatalogChangeRequests",
                column: "RequestedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogChangeRequests_ReviewedById",
                schema: "pod",
                table: "CatalogChangeRequests",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogChangeRequests_Status",
                schema: "pod",
                table: "CatalogChangeRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UQ_CatalogColors_CatalogProductId_NameEn",
                schema: "pod",
                table: "CatalogColors",
                columns: new[] { "CatalogProductId", "NameEn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogDetails_CatalogProductId",
                schema: "pod",
                table: "CatalogDetails",
                column: "CatalogProductId");

            migrationBuilder.CreateIndex(
                name: "UQ_CatalogPrintAreas_CatalogProductId_Side",
                schema: "pod",
                table: "CatalogPrintAreas",
                columns: new[] { "CatalogProductId", "Side" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_CatalogProducts_Key",
                schema: "pod",
                table: "CatalogProducts",
                column: "Key",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UQ_CatalogSizes_CatalogProductId_Size",
                schema: "pod",
                table: "CatalogSizes",
                columns: new[] { "CatalogProductId", "Size" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Closures_CreatedById",
                schema: "pitch",
                table: "Closures",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Closures_PitchId_FromDate",
                schema: "pitch",
                table: "Closures",
                columns: new[] { "PitchId", "FromDate" });

            migrationBuilder.CreateIndex(
                name: "UQ_Clubs_ExternalId",
                schema: "fantasy",
                table: "Clubs",
                column: "ExternalId",
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
                name: "UQ_Competitions_ExternalLeagueId",
                schema: "fantasy",
                table: "Competitions",
                column: "ExternalLeagueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Competitions_Key",
                schema: "fantasy",
                table: "Competitions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceItems_DepartmentId",
                schema: "ops",
                table: "ComplianceItems",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceItems_OwnerUserId",
                schema: "ops",
                table: "ComplianceItems",
                column: "OwnerUserId");

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
                name: "IX_CookieConsents_UserId",
                schema: "content",
                table: "CookieConsents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_Departments_Key",
                schema: "ops",
                table: "Departments",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_DesignCategories_Key",
                schema: "pod",
                table: "DesignCategories",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_DesignCategories_NameEn",
                schema: "pod",
                table: "DesignCategories",
                column: "NameEn",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DesignColors_CatalogColorId",
                schema: "pod",
                table: "DesignColors",
                column: "CatalogColorId");

            migrationBuilder.CreateIndex(
                name: "IX_DesignReports_DesignId",
                schema: "pod",
                table: "DesignReports",
                column: "DesignId");

            migrationBuilder.CreateIndex(
                name: "IX_DesignReports_HandledById",
                schema: "pod",
                table: "DesignReports",
                column: "HandledById");

            migrationBuilder.CreateIndex(
                name: "IX_DesignReports_ReporterUserId",
                schema: "pod",
                table: "DesignReports",
                column: "ReporterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Designs_BackArtFileId",
                schema: "pod",
                table: "Designs",
                column: "BackArtFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Designs_CatalogProductId",
                schema: "pod",
                table: "Designs",
                column: "CatalogProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Designs_CategoryId",
                schema: "pod",
                table: "Designs",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Designs_FrontArtFileId",
                schema: "pod",
                table: "Designs",
                column: "FrontArtFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Designs_ReviewedById",
                schema: "pod",
                table: "Designs",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_Designs_SellerId_Status",
                schema: "pod",
                table: "Designs",
                columns: new[] { "SellerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Designs_Status_CategoryId",
                schema: "pod",
                table: "Designs",
                columns: new[] { "Status", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Designs_SubmittedAt",
                schema: "pod",
                table: "Designs",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DesignTags_Tag",
                schema: "pod",
                table: "DesignTags",
                column: "Tag");

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutbox_Status_CreatedAt",
                schema: "notify",
                table: "EmailOutbox",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutbox_UserId",
                schema: "notify",
                table: "EmailOutbox",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_Facilities_Key",
                schema: "pitch",
                table: "Facilities",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Factories_AreaId",
                schema: "factory",
                table: "Factories",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Factories_OwnerUserId",
                schema: "factory",
                table: "Factories",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "UQ_Factories_Name",
                schema: "factory",
                table: "Factories",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FactoryDevices_FactoryId",
                schema: "factory",
                table: "FactoryDevices",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "UQ_FactoryDevices_TokenHash",
                schema: "factory",
                table: "FactoryDevices",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FactoryPrices_CatalogProductId",
                schema: "factory",
                table: "FactoryPrices",
                column: "CatalogProductId");

            migrationBuilder.CreateIndex(
                name: "UQ_FactoryPrices_FactoryId_CatalogProductId",
                schema: "factory",
                table: "FactoryPrices",
                columns: new[] { "FactoryId", "CatalogProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_FactoryStaff_FactoryId_Name",
                schema: "factory",
                table: "FactoryStaff",
                columns: new[] { "FactoryId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FantasyGroups_OwnerUserId",
                schema: "fantasy",
                table: "FantasyGroups",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FantasyGroups_SeasonId",
                schema: "fantasy",
                table: "FantasyGroups",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_FantasyGroups_SponsorId",
                schema: "fantasy",
                table: "FantasyGroups",
                column: "SponsorId");

            migrationBuilder.CreateIndex(
                name: "UQ_FantasyGroups_InviteCode",
                schema: "fantasy",
                table: "FantasyGroups",
                column: "InviteCode",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FantasyPasses_CompetitionId",
                schema: "fantasy",
                table: "FantasyPasses",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_FantasyPasses_PaymentId",
                schema: "fantasy",
                table: "FantasyPasses",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_FantasyPasses_UserId_EndsAt",
                schema: "fantasy",
                table: "FantasyPasses",
                columns: new[] { "UserId", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FantasyTeams_SeasonId",
                schema: "fantasy",
                table: "FantasyTeams",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "UQ_FantasyTeams_UserId_SeasonId",
                schema: "fantasy",
                table: "FantasyTeams",
                columns: new[] { "UserId", "SeasonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_FeatureFlags_Key",
                schema: "ops",
                table: "FeatureFlags",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Files_OwnerUserId",
                schema: "ops",
                table: "Files",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "UQ_Files_StorageKey",
                schema: "ops",
                table: "Files",
                column: "StorageKey",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Fixtures_AwayClubId",
                schema: "fantasy",
                table: "Fixtures",
                column: "AwayClubId");

            migrationBuilder.CreateIndex(
                name: "IX_Fixtures_GameweekId",
                schema: "fantasy",
                table: "Fixtures",
                column: "GameweekId");

            migrationBuilder.CreateIndex(
                name: "IX_Fixtures_HomeClubId",
                schema: "fantasy",
                table: "Fixtures",
                column: "HomeClubId");

            migrationBuilder.CreateIndex(
                name: "IX_Fixtures_KickoffAt",
                schema: "fantasy",
                table: "Fixtures",
                column: "KickoffAt");

            migrationBuilder.CreateIndex(
                name: "UQ_Fixtures_ExternalId",
                schema: "fantasy",
                table: "Fixtures",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FreeSlots_BookingId",
                schema: "booking",
                table: "FreeSlots",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_FreeSlots_OfferId",
                schema: "booking",
                table: "FreeSlots",
                column: "OfferId");

            migrationBuilder.CreateIndex(
                name: "IX_FreeSlots_PostedById",
                schema: "booking",
                table: "FreeSlots",
                column: "PostedById");

            migrationBuilder.CreateIndex(
                name: "UQ_FreeSlots_LivePerHour",
                schema: "booking",
                table: "FreeSlots",
                columns: new[] { "PitchId", "Date", "Hour" },
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Fulfilments_StoreId",
                schema: "shop",
                table: "Fulfilments",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "UQ_Fulfilments_OrderId_StoreId",
                schema: "shop",
                table: "Fulfilments",
                columns: new[] { "OrderId", "StoreId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameRequests_PaymentId",
                schema: "booking",
                table: "GameRequests",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_GameRequests_PlayerId",
                schema: "booking",
                table: "GameRequests",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "UQ_GameRequests_GameId_PlayerId",
                schema: "booking",
                table: "GameRequests",
                columns: new[] { "GameId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_HostId",
                schema: "booking",
                table: "Games",
                column: "HostId");

            migrationBuilder.CreateIndex(
                name: "UQ_Games_BookingId",
                schema: "booking",
                table: "Games",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Games_Code",
                schema: "booking",
                table: "Games",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Gameweeks_SeasonId_Number",
                schema: "fantasy",
                table: "Gameweeks",
                columns: new[] { "SeasonId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Goals_DepartmentId",
                schema: "ops",
                table: "Goals",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Goals_OwnerUserId",
                schema: "ops",
                table: "Goals",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "UQ_Governorates_Code",
                schema: "geo",
                table: "Governorates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Governorates_NameAr",
                schema: "geo",
                table: "Governorates",
                column: "NameAr",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Governorates_NameEn",
                schema: "geo",
                table: "Governorates",
                column: "NameEn",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupMembers_TeamId",
                schema: "fantasy",
                table: "GroupMembers",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpArticles_TopicId",
                schema: "content",
                table: "HelpArticles",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "UQ_HelpTopics_Key",
                schema: "content",
                table: "HelpTopics",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpVotes_ArticleId",
                schema: "content",
                table: "HelpVotes",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpVotes_UserId",
                schema: "content",
                table: "HelpVotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_Integrations_Key",
                schema: "ops",
                table: "Integrations",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Integrations_PrimaryPerCategory",
                schema: "ops",
                table: "Integrations",
                column: "Category",
                unique: true,
                filter: "[IsPrimary] = 1");

            migrationBuilder.CreateIndex(
                name: "UQ_IntegrationUsage_IntegrationId_Day",
                schema: "ops",
                table: "IntegrationUsage",
                columns: new[] { "IntegrationId", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobEvents_JobId",
                schema: "factory",
                table: "JobEvents",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_JobEvents_StaffId",
                schema: "factory",
                table: "JobEvents",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_JobProblems_JobId",
                schema: "factory",
                table: "JobProblems",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_JobProblems_ReportedByStaffId",
                schema: "factory",
                table: "JobProblems",
                column: "ReportedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_JobProblems_ResolvedById",
                schema: "factory",
                table: "JobProblems",
                column: "ResolvedById");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_AssignedStaffId",
                schema: "factory",
                table: "Jobs",
                column: "AssignedStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_FactoryId_Stage_DueAt",
                schema: "factory",
                table: "Jobs",
                columns: new[] { "FactoryId", "Stage", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "UQ_Jobs_Code",
                schema: "factory",
                table: "Jobs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Jobs_OrderItemId",
                schema: "factory",
                table: "Jobs",
                column: "OrderItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KeyResults_GoalId",
                schema: "ops",
                table: "KeyResults",
                column: "GoalId");

            migrationBuilder.CreateIndex(
                name: "IX_KycDocuments_BackFileId",
                schema: "auth",
                table: "KycDocuments",
                column: "BackFileId");

            migrationBuilder.CreateIndex(
                name: "IX_KycDocuments_FrontFileId",
                schema: "auth",
                table: "KycDocuments",
                column: "FrontFileId");

            migrationBuilder.CreateIndex(
                name: "IX_KycDocuments_ReviewedById",
                schema: "auth",
                table: "KycDocuments",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_KycDocuments_UserId_Status",
                schema: "auth",
                table: "KycDocuments",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_Bucket_AvailableAt",
                schema: "money",
                table: "LedgerEntries",
                columns: new[] { "Bucket", "AvailableAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_RefType_RefId",
                schema: "money",
                table: "LedgerEntries",
                columns: new[] { "RefType", "RefId" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_WalletId_CreatedAt",
                schema: "money",
                table: "LedgerEntries",
                columns: new[] { "WalletId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalAcceptances_UserId_VersionId",
                schema: "content",
                table: "LegalAcceptances",
                columns: new[] { "UserId", "VersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalAcceptances_VersionId",
                schema: "content",
                table: "LegalAcceptances",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "UQ_LegalDocuments_Key",
                schema: "content",
                table: "LegalDocuments",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegalVersions_ReviewedById",
                schema: "content",
                table: "LegalVersions",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "UQ_LegalVersions_DocumentId_Version",
                schema: "content",
                table: "LegalVersions",
                columns: new[] { "DocumentId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LevelReviews_FromLevelId",
                schema: "pod",
                table: "LevelReviews",
                column: "FromLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_LevelReviews_ToLevelId",
                schema: "pod",
                table: "LevelReviews",
                column: "ToLevelId");

            migrationBuilder.CreateIndex(
                name: "UQ_LevelReviews_SellerId_Month",
                schema: "pod",
                table: "LevelReviews",
                columns: new[] { "SellerId", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NewsPosts_AuthorId",
                schema: "content",
                table: "NewsPosts",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsPosts_ImageFileId",
                schema: "content",
                table: "NewsPosts",
                column: "ImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_ReadAt_CreatedAt",
                schema: "notify",
                table: "Notifications",
                columns: new[] { "UserId", "ReadAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Offers_CreatedById",
                schema: "pitch",
                table: "Offers",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Offers_PitchId",
                schema: "pitch",
                table: "Offers",
                column: "PitchId");

            migrationBuilder.CreateIndex(
                name: "UQ_OpeningHours_PitchId_DayOfWeek",
                schema: "pitch",
                table: "OpeningHours",
                columns: new[] { "PitchId", "DayOfWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_DesignId",
                schema: "shop",
                table: "OrderItems",
                column: "DesignId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                schema: "shop",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductVariantId",
                schema: "shop",
                table: "OrderItems",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_StoreId_Status",
                schema: "shop",
                table: "OrderItems",
                columns: new[] { "StoreId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId_CreatedAt",
                schema: "shop",
                table: "Orders",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PaymentId",
                schema: "shop",
                table: "Orders",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PromoCodeId",
                schema: "shop",
                table: "Orders",
                column: "PromoCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_CreatedAt",
                schema: "shop",
                table: "Orders",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UQ_Orders_Code",
                schema: "shop",
                table: "Orders",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrgPositions_DepartmentId",
                schema: "ops",
                table: "OrgPositions",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgPositions_HolderUserId",
                schema: "ops",
                table: "OrgPositions",
                column: "HolderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgPositions_ReportsToId",
                schema: "ops",
                table: "OrgPositions",
                column: "ReportsToId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxEvents_Status_NextAttemptAt",
                schema: "ops",
                table: "OutboxEvents",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "UQ_OutboxEvents_EventId",
                schema: "ops",
                table: "OutboxEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageOptions_ReviewedById",
                schema: "pitch",
                table: "PackageOptions",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "UQ_PackageOptions_PitchId_MatchCount",
                schema: "pitch",
                table: "PackageOptions",
                columns: new[] { "PitchId", "MatchCount" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Packages_CustomerId",
                schema: "booking",
                table: "Packages",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_ExceptionById",
                schema: "booking",
                table: "Packages",
                column: "ExceptionById");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_PackageOptionId",
                schema: "booking",
                table: "Packages",
                column: "PackageOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_PaymentId",
                schema: "booking",
                table: "Packages",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_PitchId",
                schema: "booking",
                table: "Packages",
                column: "PitchId");

            migrationBuilder.CreateIndex(
                name: "UQ_Packages_Code",
                schema: "booking",
                table: "Packages",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Partners_UserId",
                schema: "pitch",
                table: "Partners",
                column: "UserId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerStaff_UserId",
                schema: "pitch",
                table: "PartnerStaff",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_PartnerStaff_PartnerId_UserId",
                schema: "pitch",
                table: "PartnerStaff",
                columns: new[] { "PartnerId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PayerUserId_CreatedAt",
                schema: "money",
                table: "Payments",
                columns: new[] { "PayerUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Status",
                schema: "money",
                table: "Payments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UQ_Payments_Code",
                schema: "money",
                table: "Payments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Payments_IdempotencyKey",
                schema: "money",
                table: "Payments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Payments_ProviderRef",
                schema: "money",
                table: "Payments",
                column: "ProviderRef",
                unique: true,
                filter: "[ProviderRef] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutRequests_ApprovalId",
                schema: "money",
                table: "PayoutRequests",
                column: "ApprovalId");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutRequests_PreparedById",
                schema: "money",
                table: "PayoutRequests",
                column: "PreparedById");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutRequests_SentById",
                schema: "money",
                table: "PayoutRequests",
                column: "SentById");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutRequests_WalletId",
                schema: "money",
                table: "PayoutRequests",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "UQ_PayoutRequests_Code",
                schema: "money",
                table: "PayoutRequests",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Area",
                schema: "auth",
                table: "Permissions",
                column: "Area");

            migrationBuilder.CreateIndex(
                name: "UQ_Permissions_Code",
                schema: "auth",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Picks_GameweekId",
                schema: "fantasy",
                table: "Picks",
                column: "GameweekId");

            migrationBuilder.CreateIndex(
                name: "IX_Picks_PlayerId",
                schema: "fantasy",
                table: "Picks",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "UQ_Picks_TeamId_GameweekId_PlayerId",
                schema: "fantasy",
                table: "Picks",
                columns: new[] { "TeamId", "GameweekId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Picks_TeamId_GameweekId_Slot",
                schema: "fantasy",
                table: "Picks",
                columns: new[] { "TeamId", "GameweekId", "Slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pitches_CancellationPolicyId",
                schema: "pitch",
                table: "Pitches",
                column: "CancellationPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_Pitches_Format_PricePerHour",
                schema: "pitch",
                table: "Pitches",
                columns: new[] { "Format", "PricePerHour" });

            migrationBuilder.CreateIndex(
                name: "IX_Pitches_ReviewedById",
                schema: "pitch",
                table: "Pitches",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_Pitches_Status_VenueId",
                schema: "pitch",
                table: "Pitches",
                columns: new[] { "Status", "VenueId" });

            migrationBuilder.CreateIndex(
                name: "IX_Pitches_VenueId",
                schema: "pitch",
                table: "Pitches",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "UQ_Pitches_Slug",
                schema: "pitch",
                table: "Pitches",
                column: "Slug",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PitchFacilities_FacilityId",
                schema: "pitch",
                table: "PitchFacilities",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_PitchPhotos_FileId",
                schema: "pitch",
                table: "PitchPhotos",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_PitchPhotos_PitchId",
                schema: "pitch",
                table: "PitchPhotos",
                column: "PitchId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerFixtureStats_FixtureId",
                schema: "fantasy",
                table: "PlayerFixtureStats",
                column: "FixtureId");

            migrationBuilder.CreateIndex(
                name: "UQ_PlayerFixtureStats_PlayerId_FixtureId",
                schema: "fantasy",
                table: "PlayerFixtureStats",
                columns: new[] { "PlayerId", "FixtureId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerRatings_PlayerId",
                schema: "booking",
                table: "PlayerRatings",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerRatings_RaterId",
                schema: "booking",
                table: "PlayerRatings",
                column: "RaterId");

            migrationBuilder.CreateIndex(
                name: "UQ_PlayerRatings_GameId_PlayerId",
                schema: "booking",
                table: "PlayerRatings",
                columns: new[] { "GameId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_ClubId_Position",
                schema: "fantasy",
                table: "Players",
                columns: new[] { "ClubId", "Position" });

            migrationBuilder.CreateIndex(
                name: "UQ_Players_ExternalId",
                schema: "fantasy",
                table: "Players",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointEntries_UserId_CreatedAt",
                schema: "money",
                table: "PointEntries",
                columns: new[] { "UserId", "CreatedAt" });

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
                name: "UQ_ProductCategories_Key",
                schema: "shop",
                table: "ProductCategories",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_FileId",
                schema: "shop",
                table: "ProductImages",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId",
                schema: "shop",
                table: "ProductImages",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId_Status_Price",
                schema: "shop",
                table: "Products",
                columns: new[] { "CategoryId", "Status", "Price" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_CreatedById",
                schema: "shop",
                table: "Products",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Products_StoreId_Status",
                schema: "shop",
                table: "Products",
                columns: new[] { "StoreId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UQ_Products_Slug",
                schema: "shop",
                table: "Products",
                column: "Slug",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSpecs_ProductId",
                schema: "shop",
                table: "ProductSpecs",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "UQ_ProductVariants_ProductId_Size_ColorName",
                schema: "shop",
                table: "ProductVariants",
                columns: new[] { "ProductId", "Size", "ColorName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_ProductVariants_Sku",
                schema: "shop",
                table: "ProductVariants",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodePitches_PitchId",
                schema: "money",
                table: "PromoCodePitches",
                column: "PitchId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_CreatedById",
                schema: "money",
                table: "PromoCodes",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_OwnerUserId",
                schema: "money",
                table: "PromoCodes",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_StoreId",
                schema: "money",
                table: "PromoCodes",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "UQ_PromoCodes_Code",
                schema: "money",
                table: "PromoCodes",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodeUses_BookingId",
                schema: "money",
                table: "PromoCodeUses",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodeUses_OrderId",
                schema: "money",
                table: "PromoCodeUses",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodeUses_PromoCodeId_UserId",
                schema: "money",
                table: "PromoCodeUses",
                columns: new[] { "PromoCodeId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodeUses_UserId",
                schema: "money",
                table: "PromoCodeUses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicHolidays_Date",
                schema: "ops",
                table: "PublicHolidays",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "UQ_PublicHolidays_Country_Date_NameEn",
                schema: "ops",
                table: "PublicHolidays",
                columns: new[] { "Country", "Date", "NameEn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QrScans_BookingId",
                schema: "booking",
                table: "QrScans",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_QrScans_PackageId_ScannedAt",
                schema: "booking",
                table: "QrScans",
                columns: new[] { "PackageId", "ScannedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QrScans_PitchId",
                schema: "booking",
                table: "QrScans",
                column: "PitchId");

            migrationBuilder.CreateIndex(
                name: "IX_QrScans_ScannedById",
                schema: "booking",
                table: "QrScans",
                column: "ScannedById");

            migrationBuilder.CreateIndex(
                name: "UQ_QrScans_IdempotencyKey",
                schema: "booking",
                table: "QrScans",
                column: "IdempotencyKey",
                unique: true);

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
                name: "IX_Receipts_FileId",
                schema: "money",
                table: "Receipts",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_UserId",
                schema: "money",
                table: "Receipts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_Receipts_Number",
                schema: "money",
                table: "Receipts",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_ApprovalId",
                schema: "money",
                table: "Refunds",
                column: "ApprovalId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_PaymentId",
                schema: "money",
                table: "Refunds",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_RequestedById",
                schema: "money",
                table: "Refunds",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnItems_OrderItemId",
                schema: "ship",
                table: "ReturnItems",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnItems_ReturnId",
                schema: "ship",
                table: "ReturnItems",
                column: "ReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_Returns_CustomerId",
                schema: "ship",
                table: "Returns",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Returns_DecidedById",
                schema: "ship",
                table: "Returns",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_Returns_OrderId",
                schema: "ship",
                table: "Returns",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "UQ_Returns_Code",
                schema: "ship",
                table: "Returns",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewReports_ReporterId",
                schema: "pitch",
                table: "ReviewReports",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewReports_ReviewId",
                schema: "pitch",
                table: "ReviewReports",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_PitchId_CreatedAt",
                schema: "pitch",
                table: "Reviews",
                columns: new[] { "PitchId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_UserId",
                schema: "pitch",
                table: "Reviews",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_Reviews_BookingId",
                schema: "pitch",
                table: "Reviews",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId",
                schema: "auth",
                table: "RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                schema: "auth",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "auth",
                table: "Roles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Samples_DesignId",
                schema: "pod",
                table: "Samples",
                column: "DesignId");

            migrationBuilder.CreateIndex(
                name: "IX_Samples_OrderId",
                schema: "pod",
                table: "Samples",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Samples_SellerId",
                schema: "pod",
                table: "Samples",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonClubs_ClubId",
                schema: "fantasy",
                table: "SeasonClubs",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "UQ_Seasons_CompetitionId_Year",
                schema: "fantasy",
                table: "Seasons",
                columns: new[] { "CompetitionId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Seasons_OneCurrent",
                schema: "fantasy",
                table: "Seasons",
                column: "CompetitionId",
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SellerFollowers_UserId",
                schema: "pod",
                table: "SellerFollowers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_SellerLevels_Key",
                schema: "pod",
                table: "SellerLevels",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_SellerLevels_Rank",
                schema: "pod",
                table: "SellerLevels",
                column: "Rank",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sellers_FeaturedDesignId",
                schema: "pod",
                table: "Sellers",
                column: "FeaturedDesignId");

            migrationBuilder.CreateIndex(
                name: "IX_Sellers_LevelId_Status",
                schema: "pod",
                table: "Sellers",
                columns: new[] { "LevelId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UQ_Sellers_StoreId",
                schema: "pod",
                table: "Sellers",
                column: "StoreId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UQ_Sellers_UserId",
                schema: "pod",
                table: "Sellers",
                column: "UserId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SellerStrikes_CreatedById",
                schema: "pod",
                table: "SellerStrikes",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_SellerStrikes_DesignId",
                schema: "pod",
                table: "SellerStrikes",
                column: "DesignId");

            migrationBuilder.CreateIndex(
                name: "IX_SellerStrikes_SellerId",
                schema: "pod",
                table: "SellerStrikes",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_Series_ContactUserId",
                schema: "booking",
                table: "Series",
                column: "ContactUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Series_PitchId",
                schema: "booking",
                table: "Series",
                column: "PitchId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentEvents_ShipmentId",
                schema: "ship",
                table: "ShipmentEvents",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "UQ_ShipmentEvents_ProviderEventId",
                schema: "ship",
                table: "ShipmentEvents",
                column: "ProviderEventId",
                unique: true,
                filter: "[ProviderEventId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_FactoryId",
                schema: "ship",
                table: "Shipments",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_FulfilmentId",
                schema: "ship",
                table: "Shipments",
                column: "FulfilmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_LabelFileId",
                schema: "ship",
                table: "Shipments",
                column: "LabelFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_OrderId",
                schema: "ship",
                table: "Shipments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "UQ_Shipments_Provider_TrackingNumber",
                schema: "ship",
                table: "Shipments",
                columns: new[] { "Provider", "TrackingNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlotAlerts_AreaId",
                schema: "booking",
                table: "SlotAlerts",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_SlotAlerts_PitchId",
                schema: "booking",
                table: "SlotAlerts",
                column: "PitchId");

            migrationBuilder.CreateIndex(
                name: "IX_SlotAlerts_UserId",
                schema: "booking",
                table: "SlotAlerts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SlotHolds_UserId",
                schema: "booking",
                table: "SlotHolds",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_SlotHolds_PitchId_Date_Hour",
                schema: "booking",
                table: "SlotHolds",
                columns: new[] { "PitchId", "Date", "Hour" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Sponsors_Name",
                schema: "fantasy",
                table: "Sponsors",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UQ_StaffLimits_UserId",
                schema: "auth",
                table: "StaffLimits",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stores_CoverFileId",
                schema: "shop",
                table: "Stores",
                column: "CoverFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_LogoFileId",
                schema: "shop",
                table: "Stores",
                column: "LogoFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_OwnerUserId",
                schema: "shop",
                table: "Stores",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "UQ_Stores_Handle",
                schema: "shop",
                table: "Stores",
                column: "Handle",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UQ_Stores_OneOfficialStore",
                schema: "shop",
                table: "Stores",
                column: "Type",
                unique: true,
                filter: "[IsDeleted] = 0 AND [Type] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TeamGameweekPoints_GameweekId_Points",
                schema: "fantasy",
                table: "TeamGameweekPoints",
                columns: new[] { "GameweekId", "Points" });

            migrationBuilder.CreateIndex(
                name: "UQ_TeamGameweekPoints_TeamId_GameweekId",
                schema: "fantasy",
                table: "TeamGameweekPoints",
                columns: new[] { "TeamId", "GameweekId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketAttachments_FileId",
                schema: "support",
                table: "TicketAttachments",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketAttachments_MessageId",
                schema: "support",
                table: "TicketAttachments",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_SenderId",
                schema: "support",
                table: "TicketMessages",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_TicketId_CreatedAt",
                schema: "support",
                table: "TicketMessages",
                columns: new[] { "TicketId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AgainstUserId",
                schema: "support",
                table: "Tickets",
                column: "AgainstUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AssignedToAgentId",
                schema: "support",
                table: "Tickets",
                column: "AssignedToAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AssignedToUserId",
                schema: "support",
                table: "Tickets",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_BookingId",
                schema: "support",
                table: "Tickets",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_OpenedById",
                schema: "support",
                table: "Tickets",
                column: "OpenedById");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_OrderId",
                schema: "support",
                table: "Tickets",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Status_Priority_CreatedAt",
                schema: "support",
                table: "Tickets",
                columns: new[] { "Status", "Priority", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UQ_Tickets_Code",
                schema: "support",
                table: "Tickets",
                column: "Code",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_GameweekId",
                schema: "fantasy",
                table: "Transfers",
                column: "GameweekId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_PlayerInId",
                schema: "fantasy",
                table: "Transfers",
                column: "PlayerInId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_PlayerOutId",
                schema: "fantasy",
                table: "Transfers",
                column: "PlayerOutId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_TeamId",
                schema: "fantasy",
                table: "Transfers",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAddresses_AreaId",
                schema: "auth",
                table: "UserAddresses",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "UQ_UserAddresses_OneDefault",
                schema: "auth",
                table: "UserAddresses",
                column: "UserId",
                unique: true,
                filter: "[IsDefault] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_UserClaims_UserId",
                schema: "auth",
                table: "UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId",
                schema: "auth",
                table: "UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_GrantedById",
                schema: "auth",
                table: "UserPermissions",
                column: "GrantedById");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_PermissionId",
                schema: "auth",
                table: "UserPermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                schema: "auth",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_AvatarFileId",
                schema: "auth",
                table: "Users",
                column: "AvatarFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CreatedAt",
                schema: "auth",
                table: "Users",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Status",
                schema: "auth",
                table: "Users",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UQ_Users_NormalizedEmail",
                schema: "auth",
                table: "Users",
                column: "NormalizedEmail",
                unique: true,
                filter: "[IsDeleted] = 0 AND [NormalizedEmail] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_Users_PhoneNumber",
                schema: "auth",
                table: "Users",
                column: "PhoneNumber",
                unique: true,
                filter: "[PhoneNumber] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "auth",
                table: "Users",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Venues_AreaId",
                schema: "pitch",
                table: "Venues",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Venues_PartnerId",
                schema: "pitch",
                table: "Venues",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_UserId",
                schema: "money",
                table: "Vouchers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_Vouchers_Code",
                schema: "money",
                table: "Vouchers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_UserId",
                schema: "booking",
                table: "WaitlistEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_WaitlistEntries_PitchId_Date_Hour_UserId",
                schema: "booking",
                table: "WaitlistEntries",
                columns: new[] { "PitchId", "Date", "Hour", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Wallets_OwnerUserId_Type",
                schema: "money",
                table: "Wallets",
                columns: new[] { "OwnerUserId", "Type" },
                unique: true,
                filter: "[OwnerUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_OutboxEventId",
                schema: "ops",
                table: "WebhookDeliveries",
                column: "OutboxEventId");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_SubscriptionId",
                schema: "ops",
                table: "WebhookDeliveries",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_WishlistItems_DesignId",
                schema: "shop",
                table: "WishlistItems",
                column: "DesignId");

            migrationBuilder.CreateIndex(
                name: "IX_WishlistItems_ProductId",
                schema: "shop",
                table: "WishlistItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "UQ_WishlistItems_Design",
                schema: "shop",
                table: "WishlistItems",
                columns: new[] { "UserId", "DesignId" },
                unique: true,
                filter: "[DesignId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_WishlistItems_Product",
                schema: "shop",
                table: "WishlistItems",
                columns: new[] { "UserId", "ProductId" },
                unique: true,
                filter: "[ProductId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Adjustments_Approvals_ApprovalId",
                schema: "money",
                table: "Adjustments",
                column: "ApprovalId",
                principalSchema: "money",
                principalTable: "Approvals",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Adjustments_Users_ById",
                schema: "money",
                table: "Adjustments",
                column: "ById",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Adjustments_Wallets_WalletId",
                schema: "money",
                table: "Adjustments",
                column: "WalletId",
                principalSchema: "money",
                principalTable: "Wallets",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AgentPermissions_Agents_AgentId",
                schema: "ai",
                table: "AgentPermissions",
                column: "AgentId",
                principalSchema: "ai",
                principalTable: "Agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AgentRuns_AgentTasks_TaskId",
                schema: "ai",
                table: "AgentRuns",
                column: "TaskId",
                principalSchema: "ai",
                principalTable: "AgentTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AgentRuns_Agents_AgentId",
                schema: "ai",
                table: "AgentRuns",
                column: "AgentId",
                principalSchema: "ai",
                principalTable: "Agents",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Agents_OrgPositions_PositionId",
                schema: "ai",
                table: "Agents",
                column: "PositionId",
                principalSchema: "ops",
                principalTable: "OrgPositions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Agents_Users_ServiceUserId",
                schema: "ai",
                table: "Agents",
                column: "ServiceUserId",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicationFiles_Applications_ApplicationId",
                schema: "auth",
                table: "ApplicationFiles",
                column: "ApplicationId",
                principalSchema: "auth",
                principalTable: "Applications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicationFiles_Files_FileId",
                schema: "auth",
                table: "ApplicationFiles",
                column: "FileId",
                principalSchema: "ops",
                principalTable: "Files",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_LegalVersions_AgreementVersionId",
                schema: "auth",
                table: "Applications",
                column: "AgreementVersionId",
                principalSchema: "content",
                principalTable: "LegalVersions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_Users_ApplicantId",
                schema: "auth",
                table: "Applications",
                column: "ApplicantId",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_Users_ReviewedById",
                schema: "auth",
                table: "Applications",
                column: "ReviewedById",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalDecisions_Approvals_ApprovalId",
                schema: "money",
                table: "ApprovalDecisions",
                column: "ApprovalId",
                principalSchema: "money",
                principalTable: "Approvals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalDecisions_Users_DeciderId",
                schema: "money",
                table: "ApprovalDecisions",
                column: "DeciderId",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Approvals_Users_RequestedById",
                schema: "money",
                table: "Approvals",
                column: "RequestedById",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Users_ActorUserId",
                schema: "ops",
                table: "AuditLogs",
                column: "ActorUserId",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_LegalVersions_WaiverVersionId",
                schema: "booking",
                table: "Bookings",
                column: "WaiverVersionId",
                principalSchema: "content",
                principalTable: "LegalVersions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Packages_PackageId",
                schema: "booking",
                table: "Bookings",
                column: "PackageId",
                principalSchema: "booking",
                principalTable: "Packages",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Payments_PaymentId",
                schema: "booking",
                table: "Bookings",
                column: "PaymentId",
                principalSchema: "money",
                principalTable: "Payments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Pitches_PitchId",
                schema: "booking",
                table: "Bookings",
                column: "PitchId",
                principalSchema: "pitch",
                principalTable: "Pitches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_PromoCodes_PromoCodeId",
                schema: "booking",
                table: "Bookings",
                column: "PromoCodeId",
                principalSchema: "money",
                principalTable: "PromoCodes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Series_SeriesId",
                schema: "booking",
                table: "Bookings",
                column: "SeriesId",
                principalSchema: "booking",
                principalTable: "Series",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Users_CustomerId",
                schema: "booking",
                table: "Bookings",
                column: "CustomerId",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingSlots_Pitches_PitchId",
                schema: "booking",
                table: "BookingSlots",
                column: "PitchId",
                principalSchema: "pitch",
                principalTable: "Pitches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Broadcasts_Users_CreatedById",
                schema: "notify",
                table: "Broadcasts",
                column: "CreatedById",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CartItems_Designs_DesignId",
                schema: "shop",
                table: "CartItems",
                column: "DesignId",
                principalSchema: "pod",
                principalTable: "Designs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CartItems_ProductVariants_ProductVariantId",
                schema: "shop",
                table: "CartItems",
                column: "ProductVariantId",
                principalSchema: "shop",
                principalTable: "ProductVariants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CartItems_Users_UserId",
                schema: "shop",
                table: "CartItems",
                column: "UserId",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CatalogChangeRequests_Factories_FactoryId",
                schema: "pod",
                table: "CatalogChangeRequests",
                column: "FactoryId",
                principalSchema: "factory",
                principalTable: "Factories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CatalogChangeRequests_FactoryStaff_RequestedByStaffId",
                schema: "pod",
                table: "CatalogChangeRequests",
                column: "RequestedByStaffId",
                principalSchema: "factory",
                principalTable: "FactoryStaff",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CatalogChangeRequests_Users_ReviewedById",
                schema: "pod",
                table: "CatalogChangeRequests",
                column: "ReviewedById",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Closures_Pitches_PitchId",
                schema: "pitch",
                table: "Closures",
                column: "PitchId",
                principalSchema: "pitch",
                principalTable: "Pitches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Closures_Users_CreatedById",
                schema: "pitch",
                table: "Closures",
                column: "CreatedById",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Posts_PostId",
                schema: "community",
                table: "Comments",
                column: "PostId",
                principalSchema: "community",
                principalTable: "Posts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Users_AuthorId",
                schema: "community",
                table: "Comments",
                column: "AuthorId",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Communities_Users_CreatedById",
                schema: "community",
                table: "Communities",
                column: "CreatedById",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CommunityMembers_Users_UserId",
                schema: "community",
                table: "CommunityMembers",
                column: "UserId",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ComplianceItems_Users_OwnerUserId",
                schema: "ops",
                table: "ComplianceItems",
                column: "OwnerUserId",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentReports_Posts_PostId",
                schema: "community",
                table: "ContentReports",
                column: "PostId",
                principalSchema: "community",
                principalTable: "Posts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ContentReports_Users_HandledById",
                schema: "community",
                table: "ContentReports",
                column: "HandledById",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ContentReports_Users_ReporterId",
                schema: "community",
                table: "ContentReports",
                column: "ReporterId",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CookieConsents_Users_UserId",
                schema: "content",
                table: "CookieConsents",
                column: "UserId",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DesignColors_Designs_DesignId",
                schema: "pod",
                table: "DesignColors",
                column: "DesignId",
                principalSchema: "pod",
                principalTable: "Designs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DesignReports_Designs_DesignId",
                schema: "pod",
                table: "DesignReports",
                column: "DesignId",
                principalSchema: "pod",
                principalTable: "Designs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DesignReports_Users_HandledById",
                schema: "pod",
                table: "DesignReports",
                column: "HandledById",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DesignReports_Users_ReporterUserId",
                schema: "pod",
                table: "DesignReports",
                column: "ReporterUserId",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Designs_Files_BackArtFileId",
                schema: "pod",
                table: "Designs",
                column: "BackArtFileId",
                principalSchema: "ops",
                principalTable: "Files",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Designs_Files_FrontArtFileId",
                schema: "pod",
                table: "Designs",
                column: "FrontArtFileId",
                principalSchema: "ops",
                principalTable: "Files",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Designs_Sellers_SellerId",
                schema: "pod",
                table: "Designs",
                column: "SellerId",
                principalSchema: "pod",
                principalTable: "Sellers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Designs_Users_ReviewedById",
                schema: "pod",
                table: "Designs",
                column: "ReviewedById",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EmailOutbox_Users_UserId",
                schema: "notify",
                table: "EmailOutbox",
                column: "UserId",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Factories_Users_OwnerUserId",
                schema: "factory",
                table: "Factories",
                column: "OwnerUserId",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FantasyGroups_Users_OwnerUserId",
                schema: "fantasy",
                table: "FantasyGroups",
                column: "OwnerUserId",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FantasyPasses_Payments_PaymentId",
                schema: "fantasy",
                table: "FantasyPasses",
                column: "PaymentId",
                principalSchema: "money",
                principalTable: "Payments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FantasyPasses_Users_UserId",
                schema: "fantasy",
                table: "FantasyPasses",
                column: "UserId",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FantasyTeams_Users_UserId",
                schema: "fantasy",
                table: "FantasyTeams",
                column: "UserId",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Users_OwnerUserId",
                schema: "ops",
                table: "Files",
                column: "OwnerUserId",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id");

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
                name: "FK_Designs_Users_ReviewedById",
                schema: "pod",
                table: "Designs");

            migrationBuilder.DropForeignKey(
                name: "FK_Files_Users_OwnerUserId",
                schema: "ops",
                table: "Files");

            migrationBuilder.DropForeignKey(
                name: "FK_LegalVersions_Users_ReviewedById",
                schema: "content",
                table: "LegalVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_Partners_Users_UserId",
                schema: "pitch",
                table: "Partners");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_PayerUserId",
                schema: "money",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Pitches_Users_ReviewedById",
                schema: "pitch",
                table: "Pitches");

            migrationBuilder.DropForeignKey(
                name: "FK_Sellers_Users_UserId",
                schema: "pod",
                table: "Sellers");

            migrationBuilder.DropForeignKey(
                name: "FK_Stores_Users_OwnerUserId",
                schema: "shop",
                table: "Stores");

            migrationBuilder.DropForeignKey(
                name: "FK_TournamentTeams_Users_CaptainId",
                schema: "booking",
                table: "TournamentTeams");

            migrationBuilder.DropForeignKey(
                name: "FK_Designs_Files_BackArtFileId",
                schema: "pod",
                table: "Designs");

            migrationBuilder.DropForeignKey(
                name: "FK_Designs_Files_FrontArtFileId",
                schema: "pod",
                table: "Designs");

            migrationBuilder.DropForeignKey(
                name: "FK_Stores_Files_CoverFileId",
                schema: "shop",
                table: "Stores");

            migrationBuilder.DropForeignKey(
                name: "FK_Stores_Files_LogoFileId",
                schema: "shop",
                table: "Stores");

            migrationBuilder.DropForeignKey(
                name: "FK_TournamentTeams_LegalVersions_WaiverVersionId",
                schema: "booking",
                table: "TournamentTeams");

            migrationBuilder.DropForeignKey(
                name: "FK_Areas_Governorates_GovernorateId",
                schema: "geo",
                table: "Areas");

            migrationBuilder.DropForeignKey(
                name: "FK_TournamentTeams_Payments_PaymentId",
                schema: "booking",
                table: "TournamentTeams");

            migrationBuilder.DropForeignKey(
                name: "FK_Tournaments_Pitches_PitchId",
                schema: "booking",
                table: "Tournaments");

            migrationBuilder.DropForeignKey(
                name: "FK_Sellers_Designs_FeaturedDesignId",
                schema: "pod",
                table: "Sellers");

            migrationBuilder.DropForeignKey(
                name: "FK_Tournaments_Partners_PartnerId",
                schema: "booking",
                table: "Tournaments");

            migrationBuilder.DropForeignKey(
                name: "FK_Tournaments_TournamentTeams_ChampionTeamId",
                schema: "booking",
                table: "Tournaments");

            migrationBuilder.DropTable(
                name: "Adjustments",
                schema: "money");

            migrationBuilder.DropTable(
                name: "AgentPermissions",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "AgentRuns",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "ApiKeys",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "ApplicationFiles",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "ApprovalDecisions",
                schema: "money");

            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "BannedWords",
                schema: "community");

            migrationBuilder.DropTable(
                name: "Banners",
                schema: "content");

            migrationBuilder.DropTable(
                name: "BookingSlots",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "Broadcasts",
                schema: "notify");

            migrationBuilder.DropTable(
                name: "Budgets",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "CartItems",
                schema: "shop");

            migrationBuilder.DropTable(
                name: "CatalogChangeRequests",
                schema: "pod");

            migrationBuilder.DropTable(
                name: "CatalogDetails",
                schema: "pod");

            migrationBuilder.DropTable(
                name: "CatalogPrintAreas",
                schema: "pod");

            migrationBuilder.DropTable(
                name: "CatalogSizes",
                schema: "pod");

            migrationBuilder.DropTable(
                name: "Closures",
                schema: "pitch");

            migrationBuilder.DropTable(
                name: "CommunityMembers",
                schema: "community");

            migrationBuilder.DropTable(
                name: "ComplianceItems",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "ContentReports",
                schema: "community");

            migrationBuilder.DropTable(
                name: "CookieConsents",
                schema: "content");

            migrationBuilder.DropTable(
                name: "DesignColors",
                schema: "pod");

            migrationBuilder.DropTable(
                name: "DesignReports",
                schema: "pod");

            migrationBuilder.DropTable(
                name: "DesignTags",
                schema: "pod");

            migrationBuilder.DropTable(
                name: "EmailOutbox",
                schema: "notify");

            migrationBuilder.DropTable(
                name: "FactoryDevices",
                schema: "factory");

            migrationBuilder.DropTable(
                name: "FactoryPrices",
                schema: "factory");

            migrationBuilder.DropTable(
                name: "FantasyPasses",
                schema: "fantasy");

            migrationBuilder.DropTable(
                name: "FeatureFlags",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "FreeSlots",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "GameRequests",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "GroupMembers",
                schema: "fantasy");

            migrationBuilder.DropTable(
                name: "HelpVotes",
                schema: "content");

            migrationBuilder.DropTable(
                name: "IntegrationUsage",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "JobEvents",
                schema: "factory");

            migrationBuilder.DropTable(
                name: "JobProblems",
                schema: "factory");

            migrationBuilder.DropTable(
                name: "KeyResults",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "KycDocuments",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "LedgerEntries",
                schema: "money");

            migrationBuilder.DropTable(
                name: "LegalAcceptances",
                schema: "content");

            migrationBuilder.DropTable(
                name: "LevelReviews",
                schema: "pod");

            migrationBuilder.DropTable(
                name: "NewsPosts",
                schema: "content");

            migrationBuilder.DropTable(
                name: "NotificationPreferences",
                schema: "notify");

            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "notify");

            migrationBuilder.DropTable(
                name: "OpeningHours",
                schema: "pitch");

            migrationBuilder.DropTable(
                name: "PartnerStaff",
                schema: "pitch");

            migrationBuilder.DropTable(
                name: "PayoutRequests",
                schema: "money");

            migrationBuilder.DropTable(
                name: "Picks",
                schema: "fantasy");

            migrationBuilder.DropTable(
                name: "PitchFacilities",
                schema: "pitch");

            migrationBuilder.DropTable(
                name: "PitchPhotos",
                schema: "pitch");

            migrationBuilder.DropTable(
                name: "PlayerFixtureStats",
                schema: "fantasy");

            migrationBuilder.DropTable(
                name: "PlayerRatings",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "PointEntries",
                schema: "money");

            migrationBuilder.DropTable(
                name: "PollVotes",
                schema: "community");

            migrationBuilder.DropTable(
                name: "PopupAds",
                schema: "content");

            migrationBuilder.DropTable(
                name: "ProductImages",
                schema: "shop");

            migrationBuilder.DropTable(
                name: "ProductSpecs",
                schema: "shop");

            migrationBuilder.DropTable(
                name: "PromoCodePitches",
                schema: "money");

            migrationBuilder.DropTable(
                name: "PromoCodeUses",
                schema: "money");

            migrationBuilder.DropTable(
                name: "PublicHolidays",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "QrScans",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "Reactions",
                schema: "community");

            migrationBuilder.DropTable(
                name: "Receipts",
                schema: "money");

            migrationBuilder.DropTable(
                name: "Refunds",
                schema: "money");

            migrationBuilder.DropTable(
                name: "ReturnItems",
                schema: "ship");

            migrationBuilder.DropTable(
                name: "ReviewReports",
                schema: "pitch");

            migrationBuilder.DropTable(
                name: "RoleClaims",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "RolePermissions",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "Samples",
                schema: "pod");

            migrationBuilder.DropTable(
                name: "SeasonClubs",
                schema: "fantasy");

            migrationBuilder.DropTable(
                name: "SellerFollowers",
                schema: "pod");

            migrationBuilder.DropTable(
                name: "SellerStrikes",
                schema: "pod");

            migrationBuilder.DropTable(
                name: "Settings",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "ShipmentEvents",
                schema: "ship");

            migrationBuilder.DropTable(
                name: "SlotAlerts",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "SlotHolds",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "StaffLimits",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "TeamGameweekPoints",
                schema: "fantasy");

            migrationBuilder.DropTable(
                name: "TicketAttachments",
                schema: "support");

            migrationBuilder.DropTable(
                name: "TournamentMatches",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "Transfers",
                schema: "fantasy");

            migrationBuilder.DropTable(
                name: "UserAddresses",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "UserClaims",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "UserLogins",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "UserPermissions",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "UserTokens",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "Vouchers",
                schema: "money");

            migrationBuilder.DropTable(
                name: "WaitlistEntries",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "WebhookDeliveries",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "WishlistItems",
                schema: "shop");

            migrationBuilder.DropTable(
                name: "AgentTasks",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "Applications",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "CatalogColors",
                schema: "pod");

            migrationBuilder.DropTable(
                name: "Offers",
                schema: "pitch");

            migrationBuilder.DropTable(
                name: "FantasyGroups",
                schema: "fantasy");

            migrationBuilder.DropTable(
                name: "HelpArticles",
                schema: "content");

            migrationBuilder.DropTable(
                name: "Integrations",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "Jobs",
                schema: "factory");

            migrationBuilder.DropTable(
                name: "Goals",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "Wallets",
                schema: "money");

            migrationBuilder.DropTable(
                name: "Facilities",
                schema: "pitch");

            migrationBuilder.DropTable(
                name: "Fixtures",
                schema: "fantasy");

            migrationBuilder.DropTable(
                name: "Games",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "PollOptions",
                schema: "community");

            migrationBuilder.DropTable(
                name: "Comments",
                schema: "community");

            migrationBuilder.DropTable(
                name: "Approvals",
                schema: "money");

            migrationBuilder.DropTable(
                name: "Returns",
                schema: "ship");

            migrationBuilder.DropTable(
                name: "Reviews",
                schema: "pitch");

            migrationBuilder.DropTable(
                name: "Shipments",
                schema: "ship");

            migrationBuilder.DropTable(
                name: "TicketMessages",
                schema: "support");

            migrationBuilder.DropTable(
                name: "FantasyTeams",
                schema: "fantasy");

            migrationBuilder.DropTable(
                name: "Players",
                schema: "fantasy");

            migrationBuilder.DropTable(
                name: "Permissions",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "OutboxEvents",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "WebhookSubscriptions",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "Sponsors",
                schema: "fantasy");

            migrationBuilder.DropTable(
                name: "HelpTopics",
                schema: "content");

            migrationBuilder.DropTable(
                name: "FactoryStaff",
                schema: "factory");

            migrationBuilder.DropTable(
                name: "OrderItems",
                schema: "shop");

            migrationBuilder.DropTable(
                name: "Gameweeks",
                schema: "fantasy");

            migrationBuilder.DropTable(
                name: "Posts",
                schema: "community");

            migrationBuilder.DropTable(
                name: "Fulfilments",
                schema: "shop");

            migrationBuilder.DropTable(
                name: "Tickets",
                schema: "support");

            migrationBuilder.DropTable(
                name: "Clubs",
                schema: "fantasy");

            migrationBuilder.DropTable(
                name: "Factories",
                schema: "factory");

            migrationBuilder.DropTable(
                name: "ProductVariants",
                schema: "shop");

            migrationBuilder.DropTable(
                name: "Seasons",
                schema: "fantasy");

            migrationBuilder.DropTable(
                name: "Communities",
                schema: "community");

            migrationBuilder.DropTable(
                name: "Agents",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "Bookings",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "Orders",
                schema: "shop");

            migrationBuilder.DropTable(
                name: "Products",
                schema: "shop");

            migrationBuilder.DropTable(
                name: "Competitions",
                schema: "fantasy");

            migrationBuilder.DropTable(
                name: "OrgPositions",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "Packages",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "Series",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "PromoCodes",
                schema: "money");

            migrationBuilder.DropTable(
                name: "ProductCategories",
                schema: "shop");

            migrationBuilder.DropTable(
                name: "Departments",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "PackageOptions",
                schema: "pitch");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "Files",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "LegalVersions",
                schema: "content");

            migrationBuilder.DropTable(
                name: "LegalDocuments",
                schema: "content");

            migrationBuilder.DropTable(
                name: "Governorates",
                schema: "geo");

            migrationBuilder.DropTable(
                name: "Payments",
                schema: "money");

            migrationBuilder.DropTable(
                name: "Pitches",
                schema: "pitch");

            migrationBuilder.DropTable(
                name: "CancellationPolicies",
                schema: "pitch");

            migrationBuilder.DropTable(
                name: "Venues",
                schema: "pitch");

            migrationBuilder.DropTable(
                name: "Areas",
                schema: "geo");

            migrationBuilder.DropTable(
                name: "Designs",
                schema: "pod");

            migrationBuilder.DropTable(
                name: "CatalogProducts",
                schema: "pod");

            migrationBuilder.DropTable(
                name: "DesignCategories",
                schema: "pod");

            migrationBuilder.DropTable(
                name: "Sellers",
                schema: "pod");

            migrationBuilder.DropTable(
                name: "SellerLevels",
                schema: "pod");

            migrationBuilder.DropTable(
                name: "Stores",
                schema: "shop");

            migrationBuilder.DropTable(
                name: "Partners",
                schema: "pitch");

            migrationBuilder.DropTable(
                name: "TournamentTeams",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "Tournaments",
                schema: "booking");
        }
    }
}
