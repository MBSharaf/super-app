using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Fantasy;

/// <summary>A fantasy competition (EGY, EPL). Table <c>fantasy.Competitions</c>.</summary>
public class Competition : LookupEntity
{
    public string Key { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int ExternalLeagueId { get; set; }
    public string Country { get; set; } = string.Empty;
    public decimal SquadBudget { get; set; } = 100.0m;
    public byte MaxPerClub { get; set; } = (byte)3;
}
