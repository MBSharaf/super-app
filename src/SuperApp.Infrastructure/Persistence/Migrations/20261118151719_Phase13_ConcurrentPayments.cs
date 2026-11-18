using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase13_ConcurrentPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "ReceiptNumbers_PA",
                schema: "money");

            migrationBuilder.CreateSequence(
                name: "ReceiptNumbers_RC",
                schema: "money");

            migrationBuilder.CreateSequence(
                name: "ReceiptNumbers_RF",
                schema: "money");

            migrationBuilder.CreateSequence(
                name: "ReceiptNumbers_ST",
                schema: "money");

            migrationBuilder.CreateSequence(
                name: "ReceiptNumbers_TU",
                schema: "money");

            // Existing databases already hold numbered receipts: start each sequence after the highest number of its prefix
            foreach (var prefix in new[] { "PA", "RC", "RF", "ST", "TU" })
            {
                migrationBuilder.Sql($@"
DECLARE @next bigint = 1 + ISNULL((SELECT MAX(TRY_CAST(SUBSTRING([Number], 9, 20) AS bigint))
    FROM [money].[Receipts] WHERE [Number] LIKE '{prefix}-____-%'), 0);
DECLARE @sql nvarchar(200) = N'ALTER SEQUENCE [money].[ReceiptNumbers_{prefix}] RESTART WITH ' + CAST(@next AS nvarchar(20));
EXEC sp_executesql @sql;");
            }

            // Two Super App wallets of one type (made by payments arriving together): fold them into the oldest one
            migrationBuilder.Sql(@"
SELECT [Id], MIN([Id]) OVER (PARTITION BY [Type]) AS [KeepId] INTO #dup FROM [money].[Wallets] WHERE [OwnerUserId] IS NULL;
DELETE FROM #dup WHERE [Id] = [KeepId];
UPDATE l SET [WalletId] = d.[KeepId] FROM [money].[LedgerEntries] l JOIN #dup d ON l.[WalletId] = d.[Id];
UPDATE a SET [WalletId] = d.[KeepId] FROM [money].[Adjustments] a JOIN #dup d ON a.[WalletId] = d.[Id];
UPDATE p SET [WalletId] = d.[KeepId] FROM [money].[PayoutRequests] p JOIN #dup d ON p.[WalletId] = d.[Id];
UPDATE k SET [PendingBalance] = k.[PendingBalance] + s.[P], [AvailableBalance] = k.[AvailableBalance] + s.[A], [SettledTotal] = k.[SettledTotal] + s.[S]
FROM [money].[Wallets] k JOIN (SELECT d.[KeepId], SUM(w.[PendingBalance]) P, SUM(w.[AvailableBalance]) A, SUM(w.[SettledTotal]) S
    FROM #dup d JOIN [money].[Wallets] w ON w.[Id] = d.[Id] GROUP BY d.[KeepId]) s ON s.[KeepId] = k.[Id];
DELETE w FROM [money].[Wallets] w JOIN #dup d ON w.[Id] = d.[Id];
DROP TABLE #dup;");

            migrationBuilder.CreateIndex(
                name: "UQ_Wallets_Platform_Type",
                schema: "money",
                table: "Wallets",
                column: "Type",
                unique: true,
                filter: "[OwnerUserId] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Wallets_Platform_Type",
                schema: "money",
                table: "Wallets");

            migrationBuilder.DropSequence(
                name: "ReceiptNumbers_PA",
                schema: "money");

            migrationBuilder.DropSequence(
                name: "ReceiptNumbers_RC",
                schema: "money");

            migrationBuilder.DropSequence(
                name: "ReceiptNumbers_RF",
                schema: "money");

            migrationBuilder.DropSequence(
                name: "ReceiptNumbers_ST",
                schema: "money");

            migrationBuilder.DropSequence(
                name: "ReceiptNumbers_TU",
                schema: "money");
        }
    }
}
