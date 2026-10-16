namespace SuperApp.Domain.Enums;

public enum ApprovalKind : byte
{
    Payout = 0,
    Refund = 1,
    Credit = 2,
    Settlement = 3,
    PackageDiscountOverPolicy = 4,
    Other = 5,
}
