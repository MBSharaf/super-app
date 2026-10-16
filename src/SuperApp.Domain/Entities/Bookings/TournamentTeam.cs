using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Content;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Bookings;

/// <summary>A team registered in a tournament. Table <c>booking.TournamentTeams</c>.</summary>
public class TournamentTeam : BaseEntity
{
    public long TournamentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid CaptainId { get; set; }
    public string Phone { get; set; } = string.Empty;
    public long? PaymentId { get; set; }
    public long? WaiverVersionId { get; set; }
    public TournamentTeamStatus Status { get; set; }

    // Navigation
    public Tournament Tournament { get; set; } = null!;
    public User Captain { get; set; } = null!;
    public Payment? Payment { get; set; }
    public LegalVersion? WaiverVersion { get; set; }
}
