using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Pitches;

namespace SuperApp.Domain.Entities.Money;

/// <summary>Pitches a booking code is limited to. Table <c>money.PromoCodePitches</c>.</summary>
public class PromoCodePitch
{
    public long PromoCodeId { get; set; }
    public long PitchId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public PromoCode PromoCode { get; set; } = null!;
    public Pitch Pitch { get; set; } = null!;
}
