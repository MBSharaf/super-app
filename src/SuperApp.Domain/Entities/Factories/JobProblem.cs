using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Factories;

/// <summary>A problem reported on a job. Table <c>factory.JobProblems</c>.</summary>
public class JobProblem : BaseEntity
{
    public long JobId { get; set; }
    public long? ReportedByStaffId { get; set; }
    public JobProblemKind Kind { get; set; }
    public string Detail { get; set; } = string.Empty;
    public JobProblemStatus Status { get; set; }
    public Guid? ResolvedById { get; set; }
    public string? Resolution { get; set; }

    // Navigation
    public Job Job { get; set; } = null!;
    public FactoryStaffMember? ReportedByStaff { get; set; }
    public User? ResolvedBy { get; set; }
}
