using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Money;

/// <summary>A wallet; balances are cached from the ledger. Table <c>money.Wallets</c>.</summary>
public class Wallet : BaseEntity, IHasRowVersion
{
    public Guid? OwnerUserId { get; set; }
    public WalletType Type { get; set; }
    public string Currency { get; set; } = "EGP";
    public decimal PendingBalance { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal SettledTotal { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public User? OwnerUser { get; set; }
}
