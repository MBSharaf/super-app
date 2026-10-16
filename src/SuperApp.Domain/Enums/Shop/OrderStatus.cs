namespace SuperApp.Domain.Enums;

public enum OrderStatus : byte
{
    Placed = 0,
    Paid = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
    Refunded = 5,
}
