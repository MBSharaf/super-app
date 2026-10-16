using SuperApp.Domain.Common;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Factories;

/// <summary>A factory staff member who signs in with a PIN. Table <c>factory.FactoryStaff</c>.</summary>
public class FactoryStaffMember : BaseEntity, ISoftDelete
{
    public long FactoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public FactoryRole Role { get; set; }
    public byte[] PinHash { get; set; } = [];
    public byte FailedPins { get; set; }
    public DateTime? LockedUntil { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public Factory Factory { get; set; } = null!;
}
