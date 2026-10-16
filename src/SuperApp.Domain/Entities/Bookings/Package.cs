using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Bookings;

/// <summary>A prepaid weekly package: 1–12 matches with one QR. Table <c>booking.Packages</c>.</summary>
public class Package : BaseEntity, IHasRowVersion
{
    public string Code { get; set; } = string.Empty;
    public long PitchId { get; set; }
    public Guid CustomerId { get; set; }
    public long PackageOptionId { get; set; }
    public long? PaymentId { get; set; }
    public byte MatchCount { get; set; }
    public byte UsedCount { get; set; }
    public byte DayOfWeek { get; set; }
    public byte StartHour { get; set; }
    public byte Duration { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal TotalPrice { get; set; }
    public byte[] QrSecretHash { get; set; } = [];
    public bool ExceptionUsed { get; set; }
    public Guid? ExceptionById { get; set; }
    public PackageStatus Status { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public Pitch Pitch { get; set; } = null!;
    public User Customer { get; set; } = null!;
    public PackageOption PackageOption { get; set; } = null!;
    public Payment? Payment { get; set; }
    public User? ExceptionBy { get; set; }
}
