namespace SuperApp.Domain.Enums;

/// <summary>
/// The sport played on a pitch (owner, 2026-11-21: padel is a second sport in the same app). A padel <c>Court</c> is a
/// <see cref="Entities.Pitches.Pitch"/> with <c>Sport = Padel</c>, <c>Format = 2</c> (2 vs 2) and half-hour start times.
/// </summary>
public enum Sport : byte
{
    Football = 0,
    Padel = 1,
}
