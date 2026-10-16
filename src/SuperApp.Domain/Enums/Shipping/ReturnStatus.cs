namespace SuperApp.Domain.Enums;

public enum ReturnStatus : byte
{
    Requested = 0,
    Approved = 1,
    Received = 2,
    Refunded = 3,
    Exchanged = 4,
    Reprinting = 5,
    Rejected = 6,
}
