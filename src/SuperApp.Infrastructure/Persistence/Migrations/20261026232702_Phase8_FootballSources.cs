using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8_FootballSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Gameweeks_Number",
                schema: "fantasy",
                table: "Gameweeks");

            migrationBuilder.DropIndex(
                name: "UQ_Competitions_ExternalLeagueId",
                schema: "fantasy",
                table: "Competitions");

            migrationBuilder.AddColumn<DateTime>(
                name: "FullSyncedAt",
                schema: "fantasy",
                table: "Seasons",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Venue",
                schema: "fantasy",
                table: "Fixtures",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Color",
                schema: "fantasy",
                table: "Competitions",
                type: "varchar(9)",
                unicode: false,
                maxLength: 9,
                nullable: false,
                defaultValue: "#0B6E4F");

            migrationBuilder.AddColumn<bool>(
                name: "FantasyEnabled",
                schema: "fantasy",
                table: "Competitions",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                schema: "fantasy",
                table: "Competitions",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                defaultValue: "api-football");

            migrationBuilder.AddColumn<string>(
                name: "Region",
                schema: "fantasy",
                table: "Competitions",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                schema: "fantasy",
                table: "Competitions",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Gameweeks_Number",
                schema: "fantasy",
                table: "Gameweeks",
                sql: "[Number] BETWEEN 1 AND 255");

            // Owner's decision 2026-10-27: free sources. The English league comes from FPL with fantasy; the Egyptian league
            // from TheSportsDB (league 4829) with fantasy paused. New competitions are added by the seed.
            migrationBuilder.Sql("""
                UPDATE fantasy.Competitions SET Provider = 'fpl', FantasyEnabled = 1, ShortName = N'Premier League', Region = N'Europe', Color = '#3D195B'
                WHERE [Key] = 'epl';
                UPDATE fantasy.Competitions SET Provider = 'thesportsdb', ExternalLeagueId = 4829, FantasyEnabled = 0, ShortName = N'EPL Egypt', Region = N'Egypt', Color = '#0B6E4F'
                WHERE [Key] = 'egy';
                UPDATE s SET LastSyncedAt = NULL FROM fantasy.Seasons s JOIN fantasy.Competitions c ON c.Id = s.CompetitionId
                WHERE c.[Key] IN ('egy', 'epl');   -- read from the new sources at the next scheduler tick
                """);

            // The simulated provider's rows (ids from 9,000,000 up) would sit next to the real ones: remove them, except
            // anything a fantasy team still points to.
            migrationBuilder.Sql("""
                DELETE s FROM fantasy.PlayerFixtureStats s JOIN fantasy.Fixtures f ON f.Id = s.FixtureId WHERE f.ExternalId >= 9000000;
                DELETE FROM fantasy.Fixtures WHERE ExternalId >= 9000000;
                DELETE p FROM fantasy.Players p
                WHERE p.ExternalId >= 9000000
                  AND NOT EXISTS (SELECT 1 FROM fantasy.Picks k WHERE k.PlayerId = p.Id)
                  AND NOT EXISTS (SELECT 1 FROM fantasy.Transfers t WHERE t.PlayerInId = p.Id OR t.PlayerOutId = p.Id)
                  AND NOT EXISTS (SELECT 1 FROM fantasy.PlayerFixtureStats s WHERE s.PlayerId = p.Id);
                DELETE sc FROM fantasy.SeasonClubs sc JOIN fantasy.Clubs c ON c.Id = sc.ClubId
                WHERE c.ExternalId >= 9000000 AND NOT EXISTS (SELECT 1 FROM fantasy.Players p WHERE p.ClubId = c.Id);
                DELETE c FROM fantasy.Clubs c
                WHERE c.ExternalId >= 9000000 AND NOT EXISTS (SELECT 1 FROM fantasy.Players p WHERE p.ClubId = c.Id)
                  AND NOT EXISTS (SELECT 1 FROM fantasy.SeasonClubs sc WHERE sc.ClubId = c.Id);
                """);

            migrationBuilder.CreateIndex(
                name: "UQ_Competitions_Provider_ExternalLeagueId",
                schema: "fantasy",
                table: "Competitions",
                columns: new[] { "Provider", "ExternalLeagueId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Competitions_Color",
                schema: "fantasy",
                table: "Competitions",
                sql: "[Color] LIKE '#%'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Competitions_Provider",
                schema: "fantasy",
                table: "Competitions",
                sql: "[Provider] IN ('fpl', 'thesportsdb', 'api-football')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Gameweeks_Number",
                schema: "fantasy",
                table: "Gameweeks");

            migrationBuilder.DropIndex(
                name: "UQ_Competitions_Provider_ExternalLeagueId",
                schema: "fantasy",
                table: "Competitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Competitions_Color",
                schema: "fantasy",
                table: "Competitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Competitions_Provider",
                schema: "fantasy",
                table: "Competitions");

            migrationBuilder.DropColumn(
                name: "FullSyncedAt",
                schema: "fantasy",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "Venue",
                schema: "fantasy",
                table: "Fixtures");

            migrationBuilder.DropColumn(
                name: "Color",
                schema: "fantasy",
                table: "Competitions");

            migrationBuilder.DropColumn(
                name: "FantasyEnabled",
                schema: "fantasy",
                table: "Competitions");

            migrationBuilder.DropColumn(
                name: "Provider",
                schema: "fantasy",
                table: "Competitions");

            migrationBuilder.DropColumn(
                name: "Region",
                schema: "fantasy",
                table: "Competitions");

            migrationBuilder.DropColumn(
                name: "ShortName",
                schema: "fantasy",
                table: "Competitions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Gameweeks_Number",
                schema: "fantasy",
                table: "Gameweeks",
                sql: "[Number] BETWEEN 1 AND 50");

            migrationBuilder.CreateIndex(
                name: "UQ_Competitions_ExternalLeagueId",
                schema: "fantasy",
                table: "Competitions",
                column: "ExternalLeagueId",
                unique: true);
        }
    }
}
