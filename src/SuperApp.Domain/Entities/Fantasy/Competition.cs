using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Fantasy;

/// <summary>
/// A competition the site follows (leagues and cups). Its matches come from <see cref="Provider"/> ("fpl", "thesportsdb",
/// "api-football"); only competitions with <see cref="FantasyEnabled"/> have squads, prices and fantasy teams.
/// Table <c>fantasy.Competitions</c>.
/// </summary>
public class Competition : LookupEntity
{
    public string Key { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int ExternalLeagueId { get; set; }
    public string Country { get; set; } = string.Empty;
    public decimal SquadBudget { get; set; } = 100.0m;
    public byte MaxPerClub { get; set; } = (byte)3;
    public string Provider { get; set; } = "api-football";
    public bool FantasyEnabled { get; set; } = true;
    public string ShortName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Color { get; set; } = "#0B6E4F";
}
