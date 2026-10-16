using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Pod;

/// <summary>A report of a design (copyright, trademark, offensive). Table <c>pod.DesignReports</c>.</summary>
public class DesignReport : BaseEntity
{
    public long DesignId { get; set; }
    public Guid? ReporterUserId { get; set; }
    public DesignReportReason Reason { get; set; }
    public string? Detail { get; set; }
    public string? Contact { get; set; }
    public DesignReportStatus Status { get; set; }
    public Guid? HandledById { get; set; }
    public DateTime? HandledAt { get; set; }

    // Navigation
    public Design Design { get; set; } = null!;
    public User? ReporterUser { get; set; }
    public User? HandledBy { get; set; }
}
