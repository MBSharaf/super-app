using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase20_PushDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PushDevices",
                schema: "notify",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "varchar(512)", unicode: false, maxLength: 512, nullable: false),
                    Platform = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushDevices", x => x.Id);
                    table.CheckConstraint("CK_PushDevices_Platform", "[Platform] IN ('android','ios')");
                    table.ForeignKey(
                        name: "FK_PushDevices_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PushDevices_UserId",
                schema: "notify",
                table: "PushDevices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_PushDevices_Token",
                schema: "notify",
                table: "PushDevices",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PushDevices",
                schema: "notify");
        }
    }
}
