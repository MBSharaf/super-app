namespace SuperApp.Domain.Enums;

public enum EmailStatus : byte
{
    Queued = 0,
    Sent = 1,
    Failed = 2,
    Cancelled = 3,
}
