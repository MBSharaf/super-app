namespace SuperApp.Domain.Enums;

public enum PointKind : byte
{
    Booking = 0,
    Order = 1,
    Tournament = 2,
    Review = 3,
    VoucherRedemption = 4,
    AdminAdjustment = 5,
    /// <summary>1 point per EGP paid in the app (owner's rule, 2026-11-01).</summary>
    Payment = 6,
    /// <summary>A refund takes back the points of the money refunded.</summary>
    Refund = 7,
    /// <summary>Top 3 of a gameweek in the Super App League: 1,500 / 1,000 / 500 by default (owner's rule, 2026-11-03).</summary>
    FantasyWeekly = 8,
}
