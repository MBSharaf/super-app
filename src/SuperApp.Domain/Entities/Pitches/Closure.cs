using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Pitches;

/// <summary>Blocked hours or days of a pitch. Table <c>pitch.Closures</c>.</summary>
public class Closure : BaseEntity, IHasAuthor
{
    public long PitchId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public byte? FromHour { get; set; }
    public byte? ToHour { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? CreatedById { get; set; }

    // Navigation
    public Pitch Pitch { get; set; } = null!;
}
