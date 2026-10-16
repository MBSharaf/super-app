namespace SuperApp.Domain.Enums;

public enum IntegrationStatus : byte
{
    Unknown = 0,
    Healthy = 1,
    Degraded = 2,
    Down = 3,
}
