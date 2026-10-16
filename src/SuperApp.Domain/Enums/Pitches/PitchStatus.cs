namespace SuperApp.Domain.Enums;

public enum PitchStatus : byte
{
    WaitingForReview = 0,
    Live = 1,
    Rejected = 2,
    ClosedByOwner = 3,
    ClosedByAdmin = 4,
}
