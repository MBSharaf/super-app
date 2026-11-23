using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase19_PlayerProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Area",
                schema: "auth",
                table: "Users",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Foot",
                schema: "auth",
                table: "Users",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Level",
                schema: "auth",
                table: "Users",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Position",
                schema: "auth",
                table: "Users",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Area",
                schema: "auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Foot",
                schema: "auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Level",
                schema: "auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Position",
                schema: "auth",
                table: "Users");
        }
    }
}
