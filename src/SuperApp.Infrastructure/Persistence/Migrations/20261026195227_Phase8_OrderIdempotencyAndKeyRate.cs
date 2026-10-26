using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8_OrderIdempotencyAndKeyRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "shop",
                table: "Orders",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RateLimitPerMinute",
                schema: "ai",
                table: "ApiKeys",
                type: "int",
                nullable: false,
                defaultValue: 600);

            // Keys made before this column kept their limit in Scopes as {"rate":n}; move it over and clear the old value
            migrationBuilder.Sql("""
                UPDATE ai.ApiKeys
                SET RateLimitPerMinute = CASE WHEN TRY_CAST(JSON_VALUE(Scopes, '$.rate') AS int) BETWEEN 1 AND 6000
                                              THEN TRY_CAST(JSON_VALUE(Scopes, '$.rate') AS int) ELSE 600 END,
                    Scopes = NULL
                WHERE Scopes IS NOT NULL AND JSON_VALUE(Scopes, '$.rate') IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "UQ_Orders_CustomerId_IdempotencyKey",
                schema: "shop",
                table: "Orders",
                columns: new[] { "CustomerId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApiKeys_RateLimitPerMinute",
                schema: "ai",
                table: "ApiKeys",
                sql: "[RateLimitPerMinute] BETWEEN 1 AND 6000");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Orders_CustomerId_IdempotencyKey",
                schema: "shop",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApiKeys_RateLimitPerMinute",
                schema: "ai",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "shop",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RateLimitPerMinute",
                schema: "ai",
                table: "ApiKeys");
        }
    }
}
