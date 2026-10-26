namespace SuperApp.Domain.Enums;

public enum BookingStatus : byte
{
    Confirmed = 0,
    CheckedIn = 1,
    Played = 2,
    NoShow = 3,
    CancelledByCustomer = 4,
    CancelledByVenue = 5,

    /// <summary>Waiting for the gateway's webhook; the hours are kept for 15 minutes.</summary>
    AwaitingPayment = 6,
}
