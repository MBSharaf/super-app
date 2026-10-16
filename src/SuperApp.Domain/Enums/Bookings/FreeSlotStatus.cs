namespace SuperApp.Domain.Enums;

public enum FreeSlotStatus : byte
{
    Live = 0,
    Booked = 1,
    Expired = 2,
    HiddenByAdmin = 3,
}
