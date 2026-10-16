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
    public decimal PitchAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public long? PromoCodeId { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal Total { get; set; }
    public decimal CommissionRate { get; set; }
    public long? PaymentId { get; set; }
    public bool PayAtVenue { get; set; }
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
}
