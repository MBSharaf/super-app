namespace SuperApp.Domain.Enums;

public enum DesignStatus : byte
{
    Draft = 0,
    InReview = 1,
    Live = 2,
    Rejected = 3,
    Paused = 4,
    Removed = 5,
}
