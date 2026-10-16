using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Bookings;

/// <summary>A match of a tournament. Table <c>booking.TournamentMatches</c>.</summary>
public class TournamentMatch : BaseEntity
{
    public long TournamentId { get; set; }
    public byte Round { get; set; }
    public long? HomeTeamId { get; set; }
    public long? AwayTeamId { get; set; }
    public long? BookingId { get; set; }
    public byte? HomeGoals { get; set; }
    public byte? AwayGoals { get; set; }
    public byte? HomePens { get; set; }
    public byte? AwayPens { get; set; }
    public long? WinnerTeamId { get; set; }

    // Navigation
    public Tournament Tournament { get; set; } = null!;
    public TournamentTeam? HomeTeam { get; set; }
    public TournamentTeam? AwayTeam { get; set; }
    public Booking? Booking { get; set; }
    public TournamentTeam? WinnerTeam { get; set; }
}
