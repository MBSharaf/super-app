using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Fantasy;

/// <summary>A season of a competition. Table <c>fantasy.Seasons</c>.</summary>
public class Season : BaseEntity
{
    public int CompetitionId { get; set; }
    public short Year { get; set; }
    public bool IsCurrent { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public DateTime? FullSyncedAt { get; set; }

    // Navigation
    public Competition Competition { get; set; } = null!;
    public ICollection<Gameweek> Gameweeks { get; set; } = [];
    public ICollection<SeasonClub> Clubs { get; set; } = [];
}
