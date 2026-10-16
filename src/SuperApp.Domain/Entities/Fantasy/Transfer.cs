using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Fantasy;

/// <summary>A fantasy transfer. Table <c>fantasy.Transfers</c>.</summary>
public class Transfer : BaseEntity
{
    public long TeamId { get; set; }
    public long GameweekId { get; set; }
    public long PlayerOutId { get; set; }
    public long PlayerInId { get; set; }
    public decimal PriceOut { get; set; }
    public decimal PriceIn { get; set; }
    public byte PointsHit { get; set; }

    // Navigation
    public FantasyTeam Team { get; set; } = null!;
    public Gameweek Gameweek { get; set; } = null!;
    public Player PlayerOut { get; set; } = null!;
    public Player PlayerIn { get; set; } = null!;
}
