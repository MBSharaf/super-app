namespace SuperApp.Domain.Enums;

public enum WaitlistStatus : byte
{
    Waiting = 0,
    FreeNow = 1,
    Booked = 2,
    Expired = 3,
}
