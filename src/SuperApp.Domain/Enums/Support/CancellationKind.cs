namespace SuperApp.Domain.Enums;

/// <summary>What a cancellation request is about: a pitch booking, a weekly package, a shop order or a paid place in a game.</summary>
public enum CancellationKind : byte
{
    Booking = 0,
    Package = 1,
    Order = 2,
    Game = 3,
}
