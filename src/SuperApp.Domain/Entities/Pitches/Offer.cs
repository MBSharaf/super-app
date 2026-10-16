using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Pitches;

/// <summary>A quiet-hour discount on a pitch. Table <c>pitch.Offers</c>.</summary>
public class Offer : BaseEntity, IHasAuthor
{
    public long PitchId { get; set; }
    public byte DaysMask { get; set; }
    public byte FromHour { get; set; }
    public byte ToHour { get; set; }
    public decimal DiscountPercent { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CreatedById { get; set; }

    // Navigation
    public Pitch Pitch { get; set; } = null!;
}
