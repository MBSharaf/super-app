using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Fantasy;

/// <summary>Clubs playing in a season. Table <c>fantasy.SeasonClubs</c>.</summary>
public class SeasonClub
{
    public long SeasonId { get; set; }
    public long ClubId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Season Season { get; set; } = null!;
    public Club Club { get; set; } = null!;
}
