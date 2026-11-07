using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase10_CancellationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CancellationRequests",
                schema: "support",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    Reference = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PaidOnline = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    RefundIfApproved = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    DueAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    DecidedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Refunded = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CancellationRequests", x => x.Id);
                    table.CheckConstraint("CK_CancellationRequests_Kind", "[Kind] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_CancellationRequests_Money", "[PaidOnline] >= 0 AND [RefundIfApproved] >= 0 AND ([Refunded] IS NULL OR [Refunded] >= 0)");
                    table.CheckConstraint("CK_CancellationRequests_Status", "[Status] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_CancellationRequests_Users_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CancellationRequests_Users_DecidedById",
                        column: x => x.DecidedById,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CancellationRequests_CustomerId",
                schema: "support",
                table: "CancellationRequests",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CancellationRequests_DecidedById",
                schema: "support",
                table: "CancellationRequests",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_CancellationRequests_Status_DueAt",
                schema: "support",
                table: "CancellationRequests",
                columns: new[] { "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "UQ_CancellationRequests_Code",
                schema: "support",
                table: "CancellationRequests",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_CancellationRequests_Open",
                schema: "support",
                table: "CancellationRequests",
                columns: new[] { "Kind", "Reference" },
                unique: true,
                filter: "[Status] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CancellationRequests",
                schema: "support");
        }
    }
}
