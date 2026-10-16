namespace SuperApp.Domain.Enums;

public enum PayoutStatus : byte
{
    Requested = 0,
    Prepared = 1,
    WaitingForApproval = 2,
    Sent = 3,
    Rejected = 4,
    Failed = 5,
}
