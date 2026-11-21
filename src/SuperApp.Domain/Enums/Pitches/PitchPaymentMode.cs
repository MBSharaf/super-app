namespace SuperApp.Domain.Enums;

/// <summary>
/// How players pay for a pitch, chosen by its owner (owner's rule, 2026-11-21):
/// <c>Deposit</c> — a deposit in the app, the rest in cash at the pitch; <c>FullOnline</c> — everything in the app,
/// and Super Malaab settles with the venue (its share, less the commission, after the match).
/// </summary>
public enum PitchPaymentMode : byte
{
    Deposit = 0,
    FullOnline = 1,
}
