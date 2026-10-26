using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5_BookingAwaitingPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_Status",
                schema: "booking",
                table: "Bookings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_Status",
                schema: "booking",
                table: "Bookings",
                sql: "[Status] BETWEEN 0 AND 6");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_Status",
                schema: "booking",
                table: "Bookings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_Status",
                schema: "booking",
                table: "Bookings",
                sql: "[Status] BETWEEN 0 AND 5");
        }
    }
}
