using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Bookings;

/// <summary>A knockout cup or a league at a venue. Table <c>booking.Tournaments</c>.</summary>
public class Tournament : BaseEntity
{
    public long PartnerId { get; set; }
    public long PitchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public TournamentType Type { get; set; }
    public byte TeamCount { get; set; }
    public decimal EntryFee { get; set; }
    public string? PrizeText { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public string MatchHours { get; set; } = string.Empty;
    public string? RulesText { get; set; }
    public TournamentStatus Status { get; set; }
    public long? ChampionTeamId { get; set; }

    // Navigation
    public Partner Partner { get; set; } = null!;
    public Pitch Pitch { get; set; } = null!;
    public TournamentTeam? ChampionTeam { get; set; }
    public ICollection<TournamentTeam> Teams { get; set; } = [];
    public ICollection<TournamentMatch> Matches { get; set; } = [];
}
