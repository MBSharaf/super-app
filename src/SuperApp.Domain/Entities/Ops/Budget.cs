using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Ops;

/// <summary>A department budget for one month. Table <c>ops.Budgets</c>.</summary>
public class Budget : BaseEntity
{
    public int DepartmentId { get; set; }
    public DateOnly Month { get; set; }
    public decimal Amount { get; set; }

    // Navigation
    public Department Department { get; set; } = null!;
}
