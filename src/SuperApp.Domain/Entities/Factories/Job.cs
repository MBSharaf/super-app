using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Factories;

/// <summary>A printed item being made at a factory. Table <c>factory.Jobs</c>.</summary>
public class Job : BaseEntity, IHasRowVersion
{
    public string Code { get; set; } = string.Empty;
    public long OrderItemId { get; set; }
    public long FactoryId { get; set; }
    public JobStage Stage { get; set; }
    public DateTime DueAt { get; set; }
    public long? AssignedStaffId { get; set; }
    public byte ReprintCount { get; set; }
    public bool IsSample { get; set; }
    public decimal DeductionAmount { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public OrderItem OrderItem { get; set; } = null!;
    public Factory Factory { get; set; } = null!;
    public FactoryStaffMember? AssignedStaff { get; set; }
    public ICollection<JobEvent> Events { get; set; } = [];
    public ICollection<JobProblem> Problems { get; set; } = [];
}
