using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase17_PadelOpenMatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Games_Level",
                schema: "booking",
                table: "Games");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Games_Level",
                schema: "booking",
                table: "Games",
                sql: "[Level] BETWEEN 0 AND 3 OR [Level] BETWEEN 11 AND 17");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Games_Level",
                schema: "booking",
                table: "Games");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Games_Level",
                schema: "booking",
                table: "Games",
                sql: "[Level] BETWEEN 0 AND 3");
        }
    }
}
