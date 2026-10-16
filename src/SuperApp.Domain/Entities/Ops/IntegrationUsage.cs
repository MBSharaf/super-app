using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Ops;

/// <summary>Calls, errors and quota of one integration on one day. Table <c>ops.IntegrationUsage</c>.</summary>
public class IntegrationUsage : Entity<long>
{
    public int IntegrationId { get; set; }
    public DateOnly Day { get; set; }
    public int Calls { get; set; }
    public int Errors { get; set; }
    public int? QuotaUsed { get; set; }
    public int? QuotaLimit { get; set; }

    // Navigation
    public Integration Integration { get; set; } = null!;
}
