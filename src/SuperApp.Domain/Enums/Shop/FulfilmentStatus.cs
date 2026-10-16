namespace SuperApp.Domain.Enums;

public enum FulfilmentStatus : byte
{
    New = 0,
    Packed = 1,
    Shipped = 2,
    Delivered = 3,
    Cancelled = 4,
}
