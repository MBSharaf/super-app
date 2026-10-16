namespace SuperApp.Domain.Enums;

public enum JobStage : byte
{
    New = 0,
    Printing = 1,
    QualityCheck = 2,
    Packing = 3,
    Ready = 4,
    Shipped = 5,
    Cancelled = 6,
}
