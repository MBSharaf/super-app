using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Fantasy;

/// <summary>A squad's points in a gameweek. Table <c>fantasy.TeamGameweekPoints</c>.</summary>
public class TeamGameweekPoint : BaseEntity
{
    public long TeamId { get; set; }
    public long GameweekId { get; set; }
    public short Points { get; set; }
    public byte TransfersCost { get; set; }
    public int? Rank { get; set; }
    public string? ChipPlayed { get; set; }

    // Navigation
    public FantasyTeam Team { get; set; } = null!;
    public Gameweek Gameweek { get; set; } = null!;
}
