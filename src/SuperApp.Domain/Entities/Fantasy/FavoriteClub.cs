using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Domain.Entities.Fantasy;

/// <summary>A club a person follows (up to three, in order): its match alerts reach them. Table <c>fantasy.FavoriteClubs</c>.</summary>
public class FavoriteClub
{
    public Guid UserId { get; set; }
    public long ClubId { get; set; }
    public byte Rank { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public Club Club { get; set; } = null!;
}
