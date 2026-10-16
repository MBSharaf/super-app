using SuperApp.Domain.Common;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Fantasy;

/// <summary>A real football player (from API-FOOTBALL). Table <c>fantasy.Players</c>.</summary>
public class Player : BaseEntity
{
    public int ExternalId { get; set; }
    public long ClubId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public PlayerPosition Position { get; set; }
    public decimal Price { get; set; }
    public string? PhotoUrl { get; set; }
    public PlayerStatus Status { get; set; }
    public decimal SelectedByPercent { get; set; }

    // Navigation
    public Club Club { get; set; } = null!;
}
