namespace SuperApp.Domain.Enums;

public enum LedgerBucket : byte
{
    Pending = 0,
    Available = 1,
    Settled = 2,
}
