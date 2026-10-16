using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Domain.Entities.Ops;

/// <summary>A company goal (OKR). Table <c>ops.Goals</c>.</summary>
public class Goal : BaseEntity
{
    public int? DepartmentId { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Quarter { get; set; } = string.Empty;

    // Navigation
    public Department? Department { get; set; }
    public User? OwnerUser { get; set; }
    public ICollection<KeyResult> KeyResults { get; set; } = [];
}
