namespace SuperApp.Domain.Enums;

public enum FixtureStatus : byte
{
    Scheduled = 0,
    Live = 1,
    Finished = 2,
    Postponed = 3,
    Cancelled = 4,
}
