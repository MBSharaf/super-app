using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Bookings;

/// <summary>A QR check-in attempt and its result. Table <c>booking.QrScans</c>.</summary>
public class QrScan : Entity<long>
{
    public long? BookingId { get; set; }
    public long? PackageId { get; set; }
    public Guid ScannedById { get; set; }
    public long PitchId { get; set; }
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public QrScanResult Result { get; set; }
    public byte? MatchNumber { get; set; }
    public string? Device { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;

    // Navigation
    public Booking? Booking { get; set; }
    public Package? Package { get; set; }
    public User ScannedBy { get; set; } = null!;
    public Pitch Pitch { get; set; } = null!;
}
