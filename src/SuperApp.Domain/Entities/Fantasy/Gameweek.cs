using SuperApp.Domain.Common;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Fantasy;

/// <summary>A gameweek of a season. Table <c>fantasy.Gameweeks</c>.</summary>
public class Gameweek : BaseEntity
{
    public long SeasonId { get; set; }
    public byte Number { get; set; }
    public DateTime Deadline { get; set; }
    public GameweekStatus Status { get; set; }
    public DateTime? ScoredAt { get; set; }

    // Navigation
    public Season Season { get; set; } = null!;
    public ICollection<Fixture> Fixtures { get; set; } = [];
}
