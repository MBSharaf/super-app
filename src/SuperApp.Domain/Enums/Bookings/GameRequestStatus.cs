namespace SuperApp.Domain.Enums;

public enum GameRequestStatus : byte
{
    Waiting = 0,
    Accepted = 1,
    Declined = 2,
    Withdrawn = 3,
    Removed = 4,
    Waitlisted = 5,
}
