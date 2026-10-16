"""Money (ERD B53–B55, B85–B94)."""
from generate import Table as T, E

U = "auth.Users"
NOW = "sql:SYSUTCDATETIME()"

T("money", "Wallets", "Wallet", "B", "V", doc="A wallet; balances are cached from the ledger.") \
    .c("OwnerUserId", "uniqueidentifier", null=True, fk=U) \
    .c("Type", E("WalletType", "Customer PartnerBookings PartnerStore Seller Factory PlatformRevenue PlatformClearing")) \
    .c("Currency", "char(3)", default="EGP") \
    .c("PendingBalance", "decimal(14,2)", default=0).c("AvailableBalance", "decimal(14,2)", default=0) \
    .c("SettledTotal", "decimal(14,2)", default=0) \
    .ck("AvailableBalance", "[AvailableBalance] >= 0 OR [Type] >= 5") \
    .ix("OwnerUserId Type", unique=True)

T("money", "PromoCodes", "PromoCode", "B", "S A", doc="A discount code funded by an owner or by Super App.") \
    .c("Code", "varchar(30)", uq=True, check="[Code] NOT LIKE '%[^A-Z0-9]%'") \
    .c("Funding", E("CodeFunding", "OwnerFunded SuperAppFunded")) \
    .c("OwnerUserId", "uniqueidentifier", null=True, fk=U).c("StoreId", "bigint", null=True, fk="shop.Stores") \
    .c("Scope", E("CodeScope", "Bookings Shop Both")).c("DiscountType", E("DiscountType", "Percent Fixed FreeDelivery")) \
    .c("Value", "decimal(12,2)", check="[Value] >= 0").c("MinSpend", "decimal(12,2)", default=0, check="[MinSpend] >= 0") \
    .c("MaxUses", "int", null=True, check="[MaxUses] IS NULL OR [MaxUses] > 0") \
    .c("PerCustomer", "int", null=True, check="[PerCustomer] IS NULL OR [PerCustomer] > 0") \
    .c("UsesCount", "int", default=0, check="[UsesCount] >= 0").c("StartsOn", "date").c("EndsOn", "date", null=True) \
    .c("Status", E("CodeStatus", "Active Paused"), default=0) \
    .c("Label", "nvarchar(120)", null=True).c("Note", "nvarchar(120)", null=True) \
    .ck("Owner", "[Funding] <> 0 OR [OwnerUserId] IS NOT NULL") \
    .ck("Percent", "[DiscountType] <> 0 OR [Value] <= 100") \
    .ck("Dates", "[EndsOn] IS NULL OR [EndsOn] >= [StartsOn]")

T("money", "Payments", "Payment", "B", "V", doc="A payment through the gateway (or cash / wallet).") \
    .c("Code", "varchar(16)", uq=True).c("PayerUserId", "uniqueidentifier", null=True, fk=U) \
    .c("Provider", "varchar(40)").c("ProviderRef", "varchar(100)", null=True, uq=True) \
    .c("IdempotencyKey", "varchar(64)", uq=True) \
    .c("Purpose", E("PaymentPurpose", "Booking Package GameShare TournamentFee ShopOrder FantasyPass Sample WalletTopUp")) \
    .c("Method", E("PaymentMethod", "Card WalletApp InstaPay FawryCash CashAtVenueOrCod SuperAppWallet")) \
    .c("Amount", "decimal(12,2)", check="[Amount] > 0").c("VatAmount", "decimal(12,2)", default=0) \
    .c("Currency", "char(3)", default="EGP") \
    .c("Status", E("PaymentStatus", "Created Pending Paid Failed Refunded PartlyRefunded"), default=0) \
    .c("PaidAt", "datetime2(0)", null=True).c("RawWebhookJson", "json", null=True) \
    .ck("Vat", "[VatAmount] >= 0 AND [VatAmount] <= [Amount]") \
    .ix("PayerUserId CreatedAt").ix("Status")

T("money", "LedgerEntries", "LedgerEntry", "P", doc="Append-only money movement; balances are sums of these rows.") \
    .c("WalletId", "bigint", fk="money.Wallets").c("Amount", "decimal(14,2)", check="[Amount] <> 0") \
    .c("Bucket", E("LedgerBucket", "Pending Available Settled")) \
    .c("Kind", E("LedgerKind", "BookingEarning StoreEarning PodProfit Commission ServiceFee Refund Payout Adjustment ReleaseFromHold FactoryEarning Deduction Voucher PointsRedemption WalletTopUp WalletPayment")) \
    .c("RefType", "varchar(30)").c("RefId", "bigint").c("AvailableAt", "datetime2(0)", null=True) \
    .c("GroupId", "uniqueidentifier").c("CreatedAt", "datetime2(0)", default=NOW) \
    .ix("WalletId CreatedAt").ix("Bucket AvailableAt").ix("RefType RefId")

T("money", "Approvals", "Approval", "B", doc="Something over a money limit waiting for a human decision.") \
    .c("Kind", E("ApprovalKind", "Payout Refund Credit Settlement PackageDiscountOverPolicy Other")) \
    .c("RefType", "varchar(30)").c("RefId", "bigint") \
    .c("Amount", "decimal(14,2)", default=0, check="[Amount] >= 0").c("Summary", "nvarchar(300)") \
    .c("RequestedById", "uniqueidentifier", fk=U) \
    .c("RequiredApprovals", "tinyint", default=1, check="[RequiredApprovals] BETWEEN 1 AND 2") \
    .c("Status", E("ApprovalStatus", "Waiting Approved Rejected Cancelled"), default=0) \
    .c("DecidedAt", "datetime2(0)", null=True).ix("Status CreatedAt")

T("money", "ApprovalDecisions", "ApprovalDecision", "B", doc="One person's decision on an approval.") \
    .c("ApprovalId", "bigint", fk="money.Approvals", on="Cascade", collection="Decisions") \
    .c("DeciderId", "uniqueidentifier", fk=U).c("Decision", E("Decision", "Approve Reject")) \
    .c("Note", "nvarchar(400)", null=True).ix("ApprovalId DeciderId", unique=True)

T("money", "Refunds", "Refund", "B", doc="A refund of a payment.") \
    .c("PaymentId", "bigint", fk="money.Payments").c("Amount", "decimal(12,2)", check="[Amount] > 0") \
    .c("Destination", E("RefundDestination", "OriginalMethod SuperAppWallet")) \
    .c("PaidBy", E("RefundPayer", "SuperApp Partner Seller Factory")).c("Reason", "nvarchar(400)") \
    .c("ProviderRef", "varchar(100)", null=True) \
    .c("Status", E("RefundStatus", "Requested WaitingForApproval Done Failed"), default=0) \
    .c("RequestedById", "uniqueidentifier", fk=U).c("ApprovalId", "bigint", null=True, fk="money.Approvals")

T("money", "PayoutRequests", "PayoutRequest", "B", "V", doc="A withdrawal request: requested → prepared → sent.") \
    .c("Code", "varchar(14)", uq=True).c("WalletId", "bigint", fk="money.Wallets") \
    .c("Amount", "decimal(12,2)", check="[Amount] > 0").c("Method", E("PayoutMethod", "Bank InstaPay")) \
    .c("Destination", "nvarchar(400)", doc="Encrypted snapshot of the payout account.") \
    .c("Status", E("PayoutStatus", "Requested Prepared WaitingForApproval Sent Rejected Failed"), default=0) \
    .c("PreparedById", "uniqueidentifier", null=True, fk=U).c("SentById", "uniqueidentifier", null=True, fk=U) \
    .c("TransferRef", "varchar(100)", null=True).c("RejectReason", "nvarchar(400)", null=True) \
    .c("ApprovalId", "bigint", null=True, fk="money.Approvals").c("SentAt", "datetime2(0)", null=True) \
    .ck("TwoPeople", "[SentById] IS NULL OR [PreparedById] IS NULL OR [SentById] <> [PreparedById]") \
    .ck("TransferRef", "[Status] <> 3 OR [TransferRef] IS NOT NULL") \
    .ck("RejectReason", "[Status] <> 4 OR [RejectReason] IS NOT NULL")

T("money", "Adjustments", "Adjustment", "B", doc="A manual credit or debit of a wallet.") \
    .c("WalletId", "bigint", fk="money.Wallets").c("Amount", "decimal(12,2)", check="[Amount] <> 0") \
    .c("Reason", "nvarchar(400)").c("ById", "uniqueidentifier", fk=U, nav="By") \
    .c("ApprovalId", "bigint", null=True, fk="money.Approvals")

T("money", "Receipts", "Receipt", "B", doc="A PDF receipt, payout advice, statement or tax invoice.") \
    .c("Number", "varchar(20)", uq=True).c("UserId", "uniqueidentifier", null=True, fk=U) \
    .c("Kind", E("ReceiptKind", "Payment Refund PayoutAdvice TopUp Voucher Statement TaxInvoice")) \
    .c("Amount", "decimal(12,2)", check="[Amount] >= 0").c("VatAmount", "decimal(12,2)", default=0) \
    .c("RefType", "varchar(30)").c("RefId", "bigint").c("FileId", "bigint", null=True, fk="ops.Files") \
    .c("EtaUuid", "varchar(64)", null=True)

T("money", "PromoCodeUses", "PromoCodeUse", "B", doc="One use of a discount code.") \
    .c("PromoCodeId", "bigint", fk="money.PromoCodes").c("UserId", "uniqueidentifier", fk=U) \
    .c("OrderId", "bigint", null=True, fk="shop.Orders").c("BookingId", "bigint", null=True, fk="booking.Bookings") \
    .c("DiscountAmount", "decimal(12,2)", check="[DiscountAmount] > 0").c("RevenueAmount", "decimal(12,2)", default=0) \
    .ck("Target", "[OrderId] IS NOT NULL OR [BookingId] IS NOT NULL").ix("PromoCodeId UserId")

T("money", "PointEntries", "PointEntry", "P", doc="Points earned or spent.") \
    .c("UserId", "uniqueidentifier", fk=U).c("Points", "int", check="[Points] <> 0") \
    .c("Kind", E("PointKind", "Booking Order Tournament Review VoucherRedemption AdminAdjustment")) \
    .c("RefType", "varchar(30)", null=True).c("RefId", "bigint", null=True).c("Reason", "nvarchar(200)", null=True) \
    .c("CreatedAt", "datetime2(0)", default=NOW).ix("UserId CreatedAt")

T("money", "Vouchers", "Voucher", "B", doc="A voucher (from points, a fantasy prize or an admin).") \
    .c("Code", "varchar(20)", uq=True).c("UserId", "uniqueidentifier", fk=U) \
    .c("Amount", "decimal(12,2)", check="[Amount] >= 100").c("Source", E("VoucherSource", "Points FantasyPrize Admin")) \
    .c("PointsUsed", "int", default=0, check="[PointsUsed] >= 0") \
    .c("Status", E("VoucherStatus", "Active Used Expired"), default=0) \
    .c("UsedOnType", "varchar(30)", null=True).c("UsedOnId", "bigint", null=True).c("ExpiresAt", "datetime2(0)", null=True)
