namespace SuperApp.Domain.Enums;

public enum PaymentStatus : byte
{
    Created = 0,
    Pending = 1,
    Paid = 2,
    Failed = 3,
    Refunded = 4,
    PartlyRefunded = 5,
}
