using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Shop;

namespace SuperApp.Domain.Entities.Money;

/// <summary>One use of a discount code. Table <c>money.PromoCodeUses</c>.</summary>
public class PromoCodeUse : BaseEntity
{
    public long PromoCodeId { get; set; }
    public Guid UserId { get; set; }
    public long? OrderId { get; set; }
    public long? BookingId { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal RevenueAmount { get; set; }

    // Navigation
    public PromoCode PromoCode { get; set; } = null!;
    public User User { get; set; } = null!;
    public Order? Order { get; set; }
    public Booking? Booking { get; set; }
}
