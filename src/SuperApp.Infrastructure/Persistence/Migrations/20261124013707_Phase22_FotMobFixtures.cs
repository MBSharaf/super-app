using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperApp.Infrastructure.Persistence.Migrations
{
    /// <summary>Leagues and cups (not the Premier League) read their fixtures from FotMob: whole seasons, cups' group stages and moved matches.</summary>
    public partial class Phase22_FotMobFixtures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Competitions_Provider",
                schema: "fantasy",
                table: "Competitions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Competitions_Provider",
                schema: "fantasy",
                table: "Competitions",
                sql: "[Provider] IN ('fpl', 'fotmob', 'thesportsdb', 'api-football')");

            // Every followed league and cup except the Premier League (FPL) now reads its fixtures from FotMob
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'fotmob', [ExternalLeagueId] = 519 WHERE [Key] = 'egy' AND [Provider] = 'thesportsdb' AND [ExternalLeagueId] = 4829;");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'fotmob', [ExternalLeagueId] = 42 WHERE [Key] = 'ucl' AND [Provider] = 'thesportsdb' AND [ExternalLeagueId] = 4480;");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'fotmob', [ExternalLeagueId] = 87 WHERE [Key] = 'laliga' AND [Provider] = 'thesportsdb' AND [ExternalLeagueId] = 4335;");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'fotmob', [ExternalLeagueId] = 55 WHERE [Key] = 'seriea' AND [Provider] = 'thesportsdb' AND [ExternalLeagueId] = 4332;");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'fotmob', [ExternalLeagueId] = 54 WHERE [Key] = 'bundesliga' AND [Provider] = 'thesportsdb' AND [ExternalLeagueId] = 4331;");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'fotmob', [ExternalLeagueId] = 53 WHERE [Key] = 'ligue1' AND [Provider] = 'thesportsdb' AND [ExternalLeagueId] = 4334;");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'fotmob', [ExternalLeagueId] = 71 WHERE [Key] = 'superlig' AND [Provider] = 'thesportsdb' AND [ExternalLeagueId] = 4339;");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'fotmob', [ExternalLeagueId] = 73 WHERE [Key] = 'uel' AND [Provider] = 'thesportsdb' AND [ExternalLeagueId] = 4481;");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'fotmob', [ExternalLeagueId] = 10216 WHERE [Key] = 'uecl' AND [Provider] = 'thesportsdb' AND [ExternalLeagueId] = 5071;");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'fotmob', [ExternalLeagueId] = 289 WHERE [Key] = 'afcon' AND [Provider] = 'thesportsdb' AND [ExternalLeagueId] = 4496;");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'fotmob', [ExternalLeagueId] = 526 WHERE [Key] = 'cafcl' AND [Provider] = 'thesportsdb' AND [ExternalLeagueId] = 4720;");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'fotmob', [ExternalLeagueId] = 9468 WHERE [Key] = 'cafcc' AND [Provider] = 'thesportsdb' AND [ExternalLeagueId] = 5057;");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'fotmob', [ExternalLeagueId] = 50 WHERE [Key] = 'euro' AND [Provider] = 'thesportsdb' AND [ExternalLeagueId] = 4502;");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'fotmob', [ExternalLeagueId] = 77 WHERE [Key] = 'wc' AND [Provider] = 'thesportsdb' AND [ExternalLeagueId] = 4429;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'thesportsdb', [ExternalLeagueId] = 4829 WHERE [Key] = 'egy' AND [Provider] = 'fotmob';");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'thesportsdb', [ExternalLeagueId] = 4480 WHERE [Key] = 'ucl' AND [Provider] = 'fotmob';");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'thesportsdb', [ExternalLeagueId] = 4335 WHERE [Key] = 'laliga' AND [Provider] = 'fotmob';");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'thesportsdb', [ExternalLeagueId] = 4332 WHERE [Key] = 'seriea' AND [Provider] = 'fotmob';");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'thesportsdb', [ExternalLeagueId] = 4331 WHERE [Key] = 'bundesliga' AND [Provider] = 'fotmob';");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'thesportsdb', [ExternalLeagueId] = 4334 WHERE [Key] = 'ligue1' AND [Provider] = 'fotmob';");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'thesportsdb', [ExternalLeagueId] = 4339 WHERE [Key] = 'superlig' AND [Provider] = 'fotmob';");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'thesportsdb', [ExternalLeagueId] = 4481 WHERE [Key] = 'uel' AND [Provider] = 'fotmob';");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'thesportsdb', [ExternalLeagueId] = 5071 WHERE [Key] = 'uecl' AND [Provider] = 'fotmob';");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'thesportsdb', [ExternalLeagueId] = 4496 WHERE [Key] = 'afcon' AND [Provider] = 'fotmob';");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'thesportsdb', [ExternalLeagueId] = 4720 WHERE [Key] = 'cafcl' AND [Provider] = 'fotmob';");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'thesportsdb', [ExternalLeagueId] = 5057 WHERE [Key] = 'cafcc' AND [Provider] = 'fotmob';");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'thesportsdb', [ExternalLeagueId] = 4502 WHERE [Key] = 'euro' AND [Provider] = 'fotmob';");
            migrationBuilder.Sql("UPDATE [fantasy].[Competitions] SET [Provider] = 'thesportsdb', [ExternalLeagueId] = 4429 WHERE [Key] = 'wc' AND [Provider] = 'fotmob';");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Competitions_Provider",
                schema: "fantasy",
                table: "Competitions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Competitions_Provider",
                schema: "fantasy",
                table: "Competitions",
                sql: "[Provider] IN ('fpl', 'thesportsdb', 'api-football')");
        }
    }
}
