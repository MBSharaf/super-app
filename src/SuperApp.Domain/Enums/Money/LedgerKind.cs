namespace SuperApp.Domain.Enums;

public enum LedgerKind : byte
{
    BookingEarning = 0,
    StoreEarning = 1,
    PodProfit = 2,
    Commission = 3,
    ServiceFee = 4,
    Refund = 5,
    Payout = 6,
    Adjustment = 7,
    ReleaseFromHold = 8,
    FactoryEarning = 9,
    Deduction = 10,
    Voucher = 11,
    PointsRedemption = 12,
    WalletTopUp = 13,
    WalletPayment = 14,
}
