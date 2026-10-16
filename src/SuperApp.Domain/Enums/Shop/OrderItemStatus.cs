namespace SuperApp.Domain.Enums;

public enum OrderItemStatus : byte
{
    Received = 0,
    Printing = 1,
    Packed = 2,
    Shipped = 3,
    Delivered = 4,
    Refused = 5,
    Cancelled = 6,
    Returned = 7,
}
