using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Fantasy;

/// <summary>A player picked in a squad for a gameweek. Table <c>fantasy.Picks</c>.</summary>
public class Pick : Entity<long>
{
    public long TeamId { get; set; }
    public long GameweekId { get; set; }
    public long PlayerId { get; set; }
    public byte Slot { get; set; }
    public bool IsCaptain { get; set; }
    public bool IsViceCaptain { get; set; }
    public byte Multiplier { get; set; } = (byte)1;
    public short Points { get; set; }

    // Navigation
    public FantasyTeam Team { get; set; } = null!;
    public Gameweek Gameweek { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
