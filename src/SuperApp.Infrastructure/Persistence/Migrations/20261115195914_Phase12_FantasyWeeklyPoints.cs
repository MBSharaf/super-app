using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase12_FantasyWeeklyPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PointEntries_Kind",
                schema: "money",
                table: "PointEntries");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PointEntries_Kind",
                schema: "money",
                table: "PointEntries",
                sql: "[Kind] BETWEEN 0 AND 8");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PointEntries_Kind",
                schema: "money",
                table: "PointEntries");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PointEntries_Kind",
                schema: "money",
                table: "PointEntries",
                sql: "[Kind] BETWEEN 0 AND 7");
        }
    }
}
