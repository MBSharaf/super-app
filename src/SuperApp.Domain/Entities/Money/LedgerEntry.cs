using SuperApp.Domain.Common;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Money;

/// <summary>Append-only money movement; balances are sums of these rows. Table <c>money.LedgerEntries</c>.</summary>
public class LedgerEntry : Entity<long>
{
    public long WalletId { get; set; }
    public decimal Amount { get; set; }
    public LedgerBucket Bucket { get; set; }
    public LedgerKind Kind { get; set; }
    public string RefType { get; set; } = string.Empty;
    public long RefId { get; set; }
    public DateTime? AvailableAt { get; set; }
    public Guid GroupId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Wallet Wallet { get; set; } = null!;
}
