namespace SuperApp.Domain.Enums;

public enum OutboxStatus : byte
{
    Pending = 0,
    Delivered = 1,
    Failed = 2,
    Skipped = 3,
}
