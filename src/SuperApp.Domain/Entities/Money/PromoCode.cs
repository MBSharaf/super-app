using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Money;

/// <summary>A discount code funded by an owner or by Super App. Table <c>money.PromoCodes</c>.</summary>
public class PromoCode : BaseEntity, ISoftDelete, IHasAuthor
{
    public string Code { get; set; } = string.Empty;
    public CodeFunding Funding { get; set; }
    public Guid? OwnerUserId { get; set; }
    public long? StoreId { get; set; }
    public CodeScope Scope { get; set; }
    public DiscountType DiscountType { get; set; }
    public decimal Value { get; set; }
    public decimal MinSpend { get; set; }
    public int? MaxUses { get; set; }
    public int? PerCustomer { get; set; }
    public int UsesCount { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public CodeStatus Status { get; set; }
    public string? Label { get; set; }
    public string? Note { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? CreatedById { get; set; }

    // Navigation
    public User? OwnerUser { get; set; }
    public Store? Store { get; set; }
    public ICollection<PromoCodePitch> Pitches { get; set; } = [];
}
