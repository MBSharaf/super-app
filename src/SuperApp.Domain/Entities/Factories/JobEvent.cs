using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Factories;

/// <summary>A stage change of a job. Table <c>factory.JobEvents</c>.</summary>
public class JobEvent : Entity<long>
{
    public long JobId { get; set; }
    public byte FromStage { get; set; }
    public byte ToStage { get; set; }
    public long? StaffId { get; set; }
    public string? Note { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;

    // Navigation
    public Job Job { get; set; } = null!;
    public FactoryStaffMember? Staff { get; set; }
}
