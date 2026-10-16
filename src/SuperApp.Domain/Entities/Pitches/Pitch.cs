using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Pitches;

/// <summary>A playable pitch. Table <c>pitch.Pitches</c>.</summary>
public class Pitch : BaseEntity, ISoftDelete, IHasRowVersion
{
    public long VenueId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public byte Format { get; set; }
    public PitchSurface Surface { get; set; }
    public bool IsIndoor { get; set; }
    public decimal PricePerHour { get; set; }
    public decimal? PeakPricePerHour { get; set; }
    public byte? PeakFromHour { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }
    public int CancellationPolicyId { get; set; }
    public bool AllowCash { get; set; } = true;
    public PitchStatus Status { get; set; }
    public Guid? ReviewedById { get; set; }
    public string? RejectReason { get; set; }
    public decimal Rating { get; set; }
    public int ReviewsCount { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public Venue Venue { get; set; } = null!;
    public CancellationPolicy CancellationPolicy { get; set; } = null!;
    public User? ReviewedBy { get; set; }
    public ICollection<PitchPhoto> Photos { get; set; } = [];
    public ICollection<OpeningHour> OpeningHours { get; set; } = [];
    public ICollection<Closure> Closures { get; set; } = [];
    public ICollection<PackageOption> PackageOptions { get; set; } = [];
    public ICollection<Offer> Offers { get; set; } = [];
    public ICollection<PitchFacility> Facilities { get; set; } = [];
}
