using SuperApp.Domain.Common;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Fantasy;

/// <summary>A real match. Table <c>fantasy.Fixtures</c>.</summary>
public class Fixture : BaseEntity
{
    public int ExternalId { get; set; }
    public long GameweekId { get; set; }
    public long HomeClubId { get; set; }
    public long AwayClubId { get; set; }
    public DateTime KickoffAt { get; set; }
    public FixtureStatus Status { get; set; }
    public byte? HomeGoals { get; set; }
    public byte? AwayGoals { get; set; }
    public byte? Minute { get; set; }
    public string? Venue { get; set; }

    // Navigation
    public Gameweek Gameweek { get; set; } = null!;
    public Club HomeClub { get; set; } = null!;
    public Club AwayClub { get; set; } = null!;
    public ICollection<PlayerFixtureStat> PlayerStats { get; set; } = [];
}
