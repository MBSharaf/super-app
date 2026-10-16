namespace SuperApp.Domain.Enums;

public enum TicketStatus : byte
{
    Open = 0,
    WaitingForCustomer = 1,
    WaitingForTeam = 2,
    Resolved = 3,
    Closed = 4,
}
