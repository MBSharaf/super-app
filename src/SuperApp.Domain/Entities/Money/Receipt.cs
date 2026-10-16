using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Money;

/// <summary>A PDF receipt, payout advice, statement or tax invoice. Table <c>money.Receipts</c>.</summary>
public class Receipt : BaseEntity
{
    public string Number { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public ReceiptKind Kind { get; set; }
    public decimal Amount { get; set; }
    public decimal VatAmount { get; set; }
    public string RefType { get; set; } = string.Empty;
    public long RefId { get; set; }
    public long? FileId { get; set; }
    public string? EtaUuid { get; set; }

    // Navigation
    public User? User { get; set; }
    public StoredFile? File { get; set; }
}
