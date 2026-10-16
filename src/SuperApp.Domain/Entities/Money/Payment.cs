using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Money;

/// <summary>A payment through the gateway (or cash / wallet). Table <c>money.Payments</c>.</summary>
public class Payment : BaseEntity, IHasRowVersion
{
    public string Code { get; set; } = string.Empty;
    public Guid? PayerUserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? ProviderRef { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public PaymentPurpose Purpose { get; set; }
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public decimal VatAmount { get; set; }
    public string Currency { get; set; } = "EGP";
    public PaymentStatus Status { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? RawWebhookJson { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public User? PayerUser { get; set; }
}
