namespace SuperApp.Domain.Enums;

public enum JobProblemKind : byte
{
    OutOfBlanks = 0,
    MachineDown = 1,
    BadArtwork = 2,
    Damaged = 3,
    Other = 4,
}
