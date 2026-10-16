using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Domain.Entities.Fantasy;

/// <summary>A customer's fantasy squad for a season. Table <c>fantasy.FantasyTeams</c>.</summary>
public class FantasyTeam : BaseEntity, IHasRowVersion
{
    public Guid UserId { get; set; }
    public long SeasonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Bank { get; set; }
    public byte FreeTransfers { get; set; } = (byte)1;
    public int TotalPoints { get; set; }
    public int? OverallRank { get; set; }
    public string? ChipsUsed { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public User User { get; set; } = null!;
    public Season Season { get; set; } = null!;
    public ICollection<Pick> Picks { get; set; } = [];
    public ICollection<Transfer> Transfers { get; set; } = [];
    public ICollection<TeamGameweekPoint> GameweekPoints { get; set; } = [];
}
