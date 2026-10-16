namespace SuperApp.Domain.Enums;

public enum ContentReportStatus : byte
{
    Open = 0,
    Removed = 1,
    Kept = 2,
    RemovedAndMuted = 3,
}
