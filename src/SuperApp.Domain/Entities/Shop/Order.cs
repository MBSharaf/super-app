using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Shop;

/// <summary>A shop order. Table <c>shop.Orders</c>.</summary>
public class Order : BaseEntity, IHasRowVersion
{
    public string Code { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string AddressJson { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal Total { get; set; }
    public decimal VatAmount { get; set; }
    public long? PromoCodeId { get; set; }
    public OrderPaymentMethod PaymentMethod { get; set; }
    public long? PaymentId { get; set; }
    public OrderStatus Status { get; set; }
    public DeliverySpeed DeliverySpeed { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public User Customer { get; set; } = null!;
    public PromoCode? PromoCode { get; set; }
    public Payment? Payment { get; set; }
    public ICollection<OrderItem> Items { get; set; } = [];
    public ICollection<Fulfilment> Fulfilments { get; set; } = [];
}
