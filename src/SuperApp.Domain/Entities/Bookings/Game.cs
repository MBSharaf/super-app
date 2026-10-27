using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Bookings;

/// <summary>A game looking for players (on one of the host's bookings). Table <c>booking.Games</c>.</summary>
public class Game : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Guid HostId { get; set; }
    public long BookingId { get; set; }
    public string? Title { get; set; }
    public byte PlayersNeeded { get; set; }
    public string? Positions { get; set; }
    public string? Notes { get; set; }
    public GameLevel Level { get; set; }
    public decimal SharePrice { get; set; }
    public bool AutoAccept { get; set; }
    public bool RequestsClosed { get; set; }
    public GameStatus Status { get; set; }
    public bool HiddenByAdmin { get; set; }

    // Navigation
    public User Host { get; set; } = null!;
    public Booking Booking { get; set; } = null!;
    public ICollection<GameRequest> Requests { get; set; } = [];
    public ICollection<PlayerRating> Ratings { get; set; } = [];
}
