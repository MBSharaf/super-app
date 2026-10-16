using SuperApp.Domain.Common;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Ops;

/// <summary>An Egyptian public holiday (Islamic dates start as estimated). Table <c>ops.PublicHolidays</c>.</summary>
public class PublicHoliday : IntBaseEntity
{
    public string Country { get; set; } = "EG";
    public DateOnly Date { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public HolidayKind Kind { get; set; }
    public bool IsDayOff { get; set; } = true;
    public bool IsEstimated { get; set; }
    public Guid? ConfirmedById { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string Source { get; set; } = string.Empty;
}
