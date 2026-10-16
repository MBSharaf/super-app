using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Money;

/// <summary>A voucher (from points, a fantasy prize or an admin). Table <c>money.Vouchers</c>.</summary>
public class Voucher : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public VoucherSource Source { get; set; }
    public int PointsUsed { get; set; }
    public VoucherStatus Status { get; set; }
    public string? UsedOnType { get; set; }
    public long? UsedOnId { get; set; }
    public DateTime? ExpiresAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
