using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Shop;

/// <summary>A store: the official Super App Store, a partner's store or a seller's store. Table <c>shop.Stores</c>.</summary>
public class Store : BaseEntity, ISoftDelete
{
    public Guid? OwnerUserId { get; set; }
    public StoreType Type { get; set; }
    public string Handle { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Tagline { get; set; }
    public string? About { get; set; }
    public long? LogoFileId { get; set; }
    public long? CoverFileId { get; set; }
    public byte CoverPositionY { get; set; } = (byte)50;
    public string AccentColor { get; set; } = "#17795A";
    public StoreStatus Status { get; set; }
    public DateOnly? BreakUntil { get; set; }
    public string? BreakNote { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public User? OwnerUser { get; set; }
    public StoredFile? LogoFile { get; set; }
    public StoredFile? CoverFile { get; set; }
}
