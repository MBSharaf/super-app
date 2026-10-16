using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Geo;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Factories;

/// <summary>A print factory. Table <c>factory.Factories</c>.</summary>
public class Factory : BaseEntity, ISoftDelete
{
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int AreaId { get; set; }
    public int CapacityPerDay { get; set; }
    public short SlaHours { get; set; } = (short)48;
    public string Techniques { get; set; } = "DTF";
    public bool IntakePaused { get; set; }
    public FactoryStatus Status { get; set; }
    /// <summary>Encrypted payout account.</summary>
    public string PayoutDetail { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public User OwnerUser { get; set; } = null!;
    public Area Area { get; set; } = null!;
    public ICollection<FactoryDevice> Devices { get; set; } = [];
    public ICollection<FactoryPrice> Prices { get; set; } = [];
}
