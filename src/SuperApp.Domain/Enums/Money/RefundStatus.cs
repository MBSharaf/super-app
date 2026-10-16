namespace SuperApp.Domain.Enums;

public enum RefundStatus : byte
{
    Requested = 0,
    WaitingForApproval = 1,
    Done = 2,
    Failed = 3,
}
