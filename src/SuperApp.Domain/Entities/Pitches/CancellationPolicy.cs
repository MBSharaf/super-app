using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Pitches;

/// <summary>Flexible, Standard, Moderate or Strict. Table <c>pitch.CancellationPolicies</c>.</summary>
public class CancellationPolicy : LookupEntity
{
    public string Key { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public short FullRefundHours { get; set; }
    public short HalfRefundHours { get; set; }
    public string SummaryEn { get; set; } = string.Empty;
    public string SummaryAr { get; set; } = string.Empty;
}
