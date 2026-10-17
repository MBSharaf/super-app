using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OvernightOpeningHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_OpeningHours_CloseHour",
                schema: "pitch",
                table: "OpeningHours");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OpeningHours_CloseHour",
                schema: "pitch",
                table: "OpeningHours",
                sql: "[CloseHour] BETWEEN 1 AND 30");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_OpeningHours_CloseHour",
                schema: "pitch",
                table: "OpeningHours");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OpeningHours_CloseHour",
                schema: "pitch",
                table: "OpeningHours",
                sql: "[CloseHour] BETWEEN 1 AND 24");
        }
    }
}
