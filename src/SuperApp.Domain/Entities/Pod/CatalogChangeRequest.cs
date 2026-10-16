using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Pod;

/// <summary>A factory's change to the catalogue, waiting for an admin. Table <c>pod.CatalogChangeRequests</c>.</summary>
public class CatalogChangeRequest : BaseEntity
{
    public int CatalogProductId { get; set; }
    public long FactoryId { get; set; }
    public long? RequestedByStaffId { get; set; }
    public CatalogChangeKind Kind { get; set; }
    public string ChangeJson { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public ChangeRequestStatus Status { get; set; }
    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }

    // Navigation
    public CatalogProduct CatalogProduct { get; set; } = null!;
    public Factory Factory { get; set; } = null!;
    public FactoryStaffMember? RequestedByStaff { get; set; }
    public User? ReviewedBy { get; set; }
}
