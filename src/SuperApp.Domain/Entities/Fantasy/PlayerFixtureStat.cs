using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Fantasy;

/// <summary>A player's statistics in one match. Table <c>fantasy.PlayerFixtureStats</c>.</summary>
public class PlayerFixtureStat : BaseEntity
{
    public long PlayerId { get; set; }
    public long FixtureId { get; set; }
    public byte Minutes { get; set; }
    public byte Goals { get; set; }
    public byte Assists { get; set; }
    public byte Saves { get; set; }
    public byte OwnGoals { get; set; }
    public byte PenaltiesSaved { get; set; }
    public byte PenaltiesMissed { get; set; }
    public byte YellowCards { get; set; }
    public bool RedCard { get; set; }
    public bool CleanSheet { get; set; }
    public byte GoalsConceded { get; set; }
    public byte Bonus { get; set; }
    public short Points { get; set; }

    // Navigation
    public Player Player { get; set; } = null!;
    public Fixture Fixture { get; set; } = null!;
}
