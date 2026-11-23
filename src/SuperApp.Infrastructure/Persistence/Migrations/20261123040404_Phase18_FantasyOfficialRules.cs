using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase18_FantasyOfficialRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Players_Price",
                schema: "fantasy",
                table: "Players");

            migrationBuilder.AddColumn<byte>(
                name: "ChanceOfPlaying",
                schema: "fantasy",
                table: "Players",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "News",
                schema: "fantasy",
                table: "Players",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "DefensiveContribution",
                schema: "fantasy",
                table: "PlayerFixtureStats",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchasePrice",
                schema: "fantasy",
                table: "Picks",
                type: "decimal(4,1)",
                precision: 4,
                scale: 1,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ChipsUsed",
                schema: "fantasy",
                table: "FantasyTeams",
                type: "varchar(120)",
                unicode: false,
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(60)",
                oldUnicode: false,
                oldMaxLength: 60,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Players_Price",
                schema: "fantasy",
                table: "Players",
                sql: "[Price] BETWEEN 3.0 AND 25.0");

            // Picks already in squads were bought at today's price (selling prices count from here)
            migrationBuilder.Sql("UPDATE pk SET pk.PurchasePrice = p.Price FROM fantasy.Picks pk JOIN fantasy.Players p ON p.Id = pk.PlayerId WHERE pk.PurchasePrice IS NULL;");
            // The next sync reads every finished match again, so the season is re-scored with the official rules
            // (goalkeeper goal 10, goals conceded, defensive contribution, official bonus)
            migrationBuilder.Sql("UPDATE s SET s.UpdatedAt = f.KickoffAt FROM fantasy.PlayerFixtureStats s JOIN fantasy.Fixtures f ON f.Id = s.FixtureId;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Players_Price",
                schema: "fantasy",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "ChanceOfPlaying",
                schema: "fantasy",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "News",
                schema: "fantasy",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "DefensiveContribution",
                schema: "fantasy",
                table: "PlayerFixtureStats");

            migrationBuilder.DropColumn(
                name: "PurchasePrice",
                schema: "fantasy",
                table: "Picks");

            migrationBuilder.AlterColumn<string>(
                name: "ChipsUsed",
                schema: "fantasy",
                table: "FantasyTeams",
                type: "varchar(60)",
                unicode: false,
                maxLength: 60,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(120)",
                oldUnicode: false,
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Players_Price",
                schema: "fantasy",
                table: "Players",
                sql: "[Price] BETWEEN 3.5 AND 15.0");
        }
    }
}
