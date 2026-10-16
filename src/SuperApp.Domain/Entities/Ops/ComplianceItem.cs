using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Ops;

/// <summary>A legal or tax deadline in the compliance calendar. Table <c>ops.ComplianceItems</c>.</summary>
public class ComplianceItem : BaseEntity
{
    public int? DepartmentId { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateOnly DueOn { get; set; }
    public ComplianceRepeat Repeat { get; set; }
    public DateTime? DoneAt { get; set; }

    // Navigation
    public Department? Department { get; set; }
    public User? OwnerUser { get; set; }
}
