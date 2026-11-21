using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Content;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Bookings;

/// <summary>A booking of one or more consecutive hours. Table <c>booking.Bookings</c>.</summary>
public class Booking : BaseEntity, IHasRowVersion
{
    public string Code { get; set; } = string.Empty;
    public long PitchId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? WalkInName { get; set; }
    public string? WalkInPhone { get; set; }
    public BookingSource Source { get; set; }
    public long? PackageId { get; set; }
    public long? SeriesId { get; set; }
    public DateOnly Date { get; set; }
    public byte StartHour { get; set; }
    public byte Duration { get; set; } = (byte)1;

    /// <summary>
    /// Padel courts start every half hour and last 60, 90 or 120 minutes: <c>StartMinute</c> is 0 or 30 and <c>Minutes</c>
    /// the length (then <c>Duration</c> is the length rounded up to whole hours). Football: 0 and 0 (whole hours).
    /// </summary>
    public byte StartMinute { get; set; }
    public short Minutes { get; set; }

    /// <summary>Padel extras paid at the club at check-in (not part of <c>Total</c>): rackets rented, cans of balls, their price.</summary>
    public byte Rackets { get; set; }
    public byte Balls { get; set; }
    public decimal ExtrasAmount { get; set; }
    public decimal PitchAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public long? PromoCodeId { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal Total { get; set; }
    public decimal CommissionRate { get; set; }
    public long? PaymentId { get; set; }
    public bool PayAtVenue { get; set; }

    /// <summary>
    /// App bookings pay only a deposit online (owner's rule, 2026-11-01: 20% of the total, at least 100 EGP, never more than the
    /// total). <c>BalanceDue</c> is the rest, paid in cash to the venue at the pitch and recorded at check-in (<c>BalancePaidAt</c>).
    /// Both are 0 for walk-ins, tournament hours and bookings made before the rule.
    /// </summary>
    public decimal Deposit { get; set; }
    public decimal BalanceDue { get; set; }
    public PitchPaymentMode PaymentMode { get; set; }
    public DateTime? BalancePaidAt { get; set; }
    public string PolicyKey { get; set; } = string.Empty;
    public long? WaiverVersionId { get; set; }
    public BookingStatus Status { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public decimal RefundAmount { get; set; }
    public byte[]? QrSecretHash { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public Pitch Pitch { get; set; } = null!;
    public User? Customer { get; set; }
    public Package? Package { get; set; }
    public Series? Series { get; set; }
    public PromoCode? PromoCode { get; set; }
    public Payment? Payment { get; set; }
    public LegalVersion? WaiverVersion { get; set; }
    public ICollection<BookingSlot> Slots { get; set; } = [];

    /// <summary>How long the booking lasts, in minutes.</summary>
    public int LengthMinutes => Minutes > 0 ? Minutes : Duration * 60;

    /// <summary>Kick-off, Cairo local time.</summary>
    public DateTime KickOff() => Date.ToDateTime(new TimeOnly(StartHour, StartMinute));

    /// <summary>The end of the booking, Cairo local time.</summary>
    public DateTime EndsAt() => KickOff().AddMinutes(LengthMinutes);
}
