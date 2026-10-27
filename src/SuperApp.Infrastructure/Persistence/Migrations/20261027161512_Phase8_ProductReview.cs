using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8_ProductReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Status",
                schema: "shop",
                table: "Products");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                schema: "shop",
                table: "Products",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                schema: "shop",
                table: "Products",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            // Products already on sale were checked before review existed: they count as approved
            migrationBuilder.Sql("UPDATE [shop].[Products] SET [ApprovedAt] = COALESCE([UpdatedAt], [CreatedAt]) WHERE [Status] = 1;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Status",
                schema: "shop",
                table: "Products",
                sql: "[Status] BETWEEN 0 AND 4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Status",
                schema: "shop",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                schema: "shop",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                schema: "shop",
                table: "Products");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Status",
                schema: "shop",
                table: "Products",
                sql: "[Status] BETWEEN 0 AND 2");
        }
    }
}
