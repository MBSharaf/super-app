using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8_BookingDeposit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PointEntries_Kind",
                schema: "money",
                table: "PointEntries");

            migrationBuilder.AddColumn<decimal>(
                name: "BalanceDue",
                schema: "booking",
                table: "Bookings",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "BalancePaidAt",
                schema: "booking",
                table: "Bookings",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Deposit",
                schema: "booking",
                table: "Bookings",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PointEntries_Kind",
                schema: "money",
                table: "PointEntries",
                sql: "[Kind] BETWEEN 0 AND 7");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_Deposit",
                schema: "booking",
                table: "Bookings",
                sql: "[Deposit] >= 0 AND [BalanceDue] >= 0 AND [Deposit] + [BalanceDue] <= [Total]");

            // Owner's rule (2026-11-01): 1 Super Point per 1 EGP paid (was 1 per 10). The seed never overwrites a setting.
            migrationBuilder.Sql("UPDATE [ops].[Settings] SET [Value] = '1', [UpdatedAt] = SYSUTCDATETIME() WHERE [Key] = 'loyalty.earnPer';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PointEntries_Kind",
                schema: "money",
                table: "PointEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_Deposit",
                schema: "booking",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "BalanceDue",
                schema: "booking",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "BalancePaidAt",
                schema: "booking",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "Deposit",
                schema: "booking",
                table: "Bookings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PointEntries_Kind",
                schema: "money",
                table: "PointEntries",
                sql: "[Kind] BETWEEN 0 AND 5");

            migrationBuilder.Sql("UPDATE [ops].[Settings] SET [Value] = '10', [UpdatedAt] = SYSUTCDATETIME() WHERE [Key] = 'loyalty.earnPer';");
        }
    }
}
