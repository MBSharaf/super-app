namespace SuperApp.Domain.Enums;

public enum GameLevel : byte
{
    Any = 0,
    Casual = 1,
    Intermediate = 2,
    Competitive = 3,

    // Padel Open Matches use the 1–7 padel scale (1–2 new, 3–4 club, 5–6 strong, 7 pro)
    Level1 = 11,
    Level2 = 12,
    Level3 = 13,
    Level4 = 14,
    Level5 = 15,
    Level6 = 16,
    Level7 = 17,
}
