namespace SuperApp.Domain.Enums;

public enum QrScanResult : byte
{
    Accepted = 0,
    BadSignature = 1,
    OtherVenue = 2,
    WrongDay = 3,
    WrongHour = 4,
    Cancelled = 5,
    AlreadyUsed = 6,
    PackageFinished = 7,
    AccountBlocked = 8,
}
