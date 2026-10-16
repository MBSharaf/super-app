using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Bookings;

/// <summary>A player's request to join a game. Table <c>booking.GameRequests</c>.</summary>
public class GameRequest : BaseEntity
{
    public long GameId { get; set; }
    public Guid PlayerId { get; set; }
    public string? Position { get; set; }
    public string? Message { get; set; }
    public long? PaymentId { get; set; }
    public GameRequestStatus Status { get; set; }

    // Navigation
    public Game Game { get; set; } = null!;
    public User Player { get; set; } = null!;
    public Payment? Payment { get; set; }
}
