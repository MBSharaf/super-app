using SuperApp.Domain.Common;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Fantasy;

/// <summary>A fantasy sponsor. Table <c>fantasy.Sponsors</c>.</summary>
public class Sponsor : BaseEntity, ISoftDelete
{
    public string Name { get; set; } = string.Empty;
    public SponsorTier Tier { get; set; }
    public string? ContactEmail { get; set; }
    public decimal ContractValue { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public string Color { get; set; } = "#0B6E4F";
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
