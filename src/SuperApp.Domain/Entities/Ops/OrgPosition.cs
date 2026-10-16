using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Domain.Entities.Ops;

/// <summary>A box of the company pyramid (human holder or AI agent). Table <c>ops.OrgPositions</c>.</summary>
public class OrgPosition : BaseEntity
{
    public int? DepartmentId { get; set; }
    public long? ReportsToId { get; set; }
    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public Guid? HolderUserId { get; set; }
    public string? DutiesJson { get; set; }
    public int SortOrder { get; set; }

    // Navigation
    public Department? Department { get; set; }
    public OrgPosition? ReportsTo { get; set; }
    public User? HolderUser { get; set; }
}
