using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase16_Padel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SlotHolds_Hour",
                schema: "booking",
                table: "SlotHolds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Pitches_Format",
                schema: "pitch",
                table: "Pitches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BookingSlots_Hour",
                schema: "booking",
                table: "BookingSlots");

            migrationBuilder.AddColumn<decimal>(
                name: "BallsPrice",
                schema: "pitch",
                table: "Pitches",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Durations",
                schema: "pitch",
                table: "Pitches",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<bool>(
                name: "IsPanoramic",
                schema: "pitch",
                table: "Pitches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "RacketPrice",
                schema: "pitch",
                table: "Pitches",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Sport",
                schema: "pitch",
                table: "Pitches",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "Balls",
                schema: "booking",
                table: "Bookings",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<decimal>(
                name: "ExtrasAmount",
                schema: "booking",
                table: "Bookings",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<short>(
                name: "Minutes",
                schema: "booking",
                table: "Bookings",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<byte>(
                name: "Rackets",
                schema: "booking",
                table: "Bookings",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "StartMinute",
                schema: "booking",
                table: "Bookings",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SlotHolds_Hour",
                schema: "booking",
                table: "SlotHolds",
                sql: "[Hour] <= 47");

            migrationBuilder.CreateIndex(
                name: "IX_Pitches_Sport_Status",
                schema: "pitch",
                table: "Pitches",
                columns: new[] { "Sport", "Status" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Pitches_Durations",
                schema: "pitch",
                table: "Pitches",
                sql: "([Sport] = 0 AND [Durations] = 0) OR ([Sport] = 1 AND [Durations] BETWEEN 1 AND 7)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Pitches_Extras",
                schema: "pitch",
                table: "Pitches",
                sql: "([RacketPrice] IS NULL OR [RacketPrice] >= 0) AND ([BallsPrice] IS NULL OR [BallsPrice] >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Pitches_Format",
                schema: "pitch",
                table: "Pitches",
                sql: "([Sport] = 0 AND [Format] IN (5, 7, 11)) OR ([Sport] = 1 AND [Format] = 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Pitches_Sport",
                schema: "pitch",
                table: "Pitches",
                sql: "[Sport] IN (0, 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BookingSlots_Hour",
                schema: "booking",
                table: "BookingSlots",
                sql: "[Hour] <= 47");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_Extras",
                schema: "booking",
                table: "Bookings",
                sql: "[ExtrasAmount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_Minutes",
                schema: "booking",
                table: "Bookings",
                sql: "[Minutes] IN (0, 60, 90, 120)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_StartMinute",
                schema: "booking",
                table: "Bookings",
                sql: "[StartMinute] IN (0, 30)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SlotHolds_Hour",
                schema: "booking",
                table: "SlotHolds");

            migrationBuilder.DropIndex(
                name: "IX_Pitches_Sport_Status",
                schema: "pitch",
                table: "Pitches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Pitches_Durations",
                schema: "pitch",
                table: "Pitches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Pitches_Extras",
                schema: "pitch",
                table: "Pitches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Pitches_Format",
                schema: "pitch",
                table: "Pitches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Pitches_Sport",
                schema: "pitch",
                table: "Pitches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BookingSlots_Hour",
                schema: "booking",
                table: "BookingSlots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_Extras",
                schema: "booking",
                table: "Bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_Minutes",
                schema: "booking",
                table: "Bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_StartMinute",
                schema: "booking",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "BallsPrice",
                schema: "pitch",
                table: "Pitches");

            migrationBuilder.DropColumn(
                name: "Durations",
                schema: "pitch",
                table: "Pitches");

            migrationBuilder.DropColumn(
                name: "IsPanoramic",
                schema: "pitch",
                table: "Pitches");

            migrationBuilder.DropColumn(
                name: "RacketPrice",
                schema: "pitch",
                table: "Pitches");

            migrationBuilder.DropColumn(
                name: "Sport",
                schema: "pitch",
                table: "Pitches");

            migrationBuilder.DropColumn(
                name: "Balls",
                schema: "booking",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ExtrasAmount",
                schema: "booking",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "Minutes",
                schema: "booking",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "Rackets",
                schema: "booking",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "StartMinute",
                schema: "booking",
                table: "Bookings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SlotHolds_Hour",
                schema: "booking",
                table: "SlotHolds",
                sql: "[Hour] <= 23");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Pitches_Format",
                schema: "pitch",
                table: "Pitches",
                sql: "[Format] IN (5, 7, 11)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BookingSlots_Hour",
                schema: "booking",
                table: "BookingSlots",
                sql: "[Hour] <= 23");
        }
    }
}
