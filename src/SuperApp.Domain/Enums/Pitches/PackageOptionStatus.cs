namespace SuperApp.Domain.Enums;

public enum PackageOptionStatus : byte
{
    WaitingForApproval = 0,
    Approved = 1,
    Rejected = 2,
    Disabled = 3,
}
