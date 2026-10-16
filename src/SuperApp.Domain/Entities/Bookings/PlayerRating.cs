using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Domain.Entities.Bookings;

/// <summary>A host's rating of a player after a game. Table <c>booking.PlayerRatings</c>.</summary>
public class PlayerRating : BaseEntity
{
    public long GameId { get; set; }
    public Guid RaterId { get; set; }
    public Guid PlayerId { get; set; }
    public byte? Stars { get; set; }
    public bool ShowedUp { get; set; }

    // Navigation
    public Game Game { get; set; } = null!;
    public User Rater { get; set; } = null!;
    public User Player { get; set; } = null!;
}
