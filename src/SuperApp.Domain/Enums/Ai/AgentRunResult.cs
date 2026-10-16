namespace SuperApp.Domain.Enums;

public enum AgentRunResult : byte
{
    Running = 0,
    Done = 1,
    WaitingForApproval = 2,
    Failed = 3,
    Cancelled = 4,
}
