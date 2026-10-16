namespace SuperApp.Domain.Enums;

public enum ReturnReason : byte
{
    WrongSize = 0,
    FaultyOrDamaged = 1,
    WrongItemOrPrint = 2,
    NotAsDescribed = 3,
    ChangedMyMind = 4,
}
