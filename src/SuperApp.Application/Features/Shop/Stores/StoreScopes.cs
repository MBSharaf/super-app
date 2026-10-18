using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Partners;
using SuperApp.Application.Features.Shop.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Stores;

/// <summary>Finds the store the caller runs: the partner's shop (made on first use), the seller's store, or the official store.</summary>
public interface IStoreScopes
{
    /// <summary>The pitch partner's shop: owner or manager (PartnerAccess rules). Created on first use.</summary>
    Task<StoreScope> PartnerAsync(CancellationToken ct);

    /// <summary>The seller's store; <paramref name="products"/> = own products, which need the "store" or "both" mode (422 own_products_off).</summary>
    Task<StoreScope> SellerAsync(bool products, CancellationToken ct);

    /// <summary>The Super App Store, for staff with store.official (checked on the endpoint).</summary>
    Task<StoreScope> OfficialAsync(CancellationToken ct);
}

public sealed class StoreScopes(IPartnerAccess partners, ISellerAccess sellers, IStoreRepository stores, IUnitOfWork unitOfWork) : IStoreScopes
{
    public async Task<StoreScope> PartnerAsync(CancellationToken ct)
    {
        var partner = await partners.ManageAsync(ct);
        var store = await stores.OwnedStoreAsync(partner.OwnerUserId, StoreType.PitchPartner, ct);
        if (store is null)
        {
            store = new Store
            {
                OwnerUserId = partner.OwnerUserId, Type = StoreType.PitchPartner, Handle = await stores.FreeHandleAsync(partner.BusinessName, ct),
                Name = partner.BusinessName.Length > 80 ? partner.BusinessName[..80] : partner.BusinessName, Status = StoreStatus.Active,
            };
            stores.AddStore(store);
            await unitOfWork.SaveChangesAsync(ct);
        }
        return new StoreScope(store.Id, store.Type, store.OwnerUserId, store.Handle, store.Name, AuditSystem.Partner, partner.BusinessName);
    }

    public async Task<StoreScope> SellerAsync(bool products, CancellationToken ct)
    {
        var seller = await sellers.CurrentAsync(ct);
        if (products && !seller.SellsOwnProducts)
        {
            throw new BusinessRuleException("Your store sells printed designs only. Switch the store mode to sell your own products too.", "own_products_off");
        }
        return new StoreScope(seller.StoreId, StoreType.Seller, seller.UserId, seller.Handle, seller.StoreName, AuditSystem.Seller, seller.StoreName);
    }

    public async Task<StoreScope> OfficialAsync(CancellationToken ct)
    {
        var store = await stores.OfficialStoreAsync(ct) ?? throw new NotFoundException("The official store is not set up yet.");
        return new StoreScope(store.Id, store.Type, null, store.Handle, store.Name, AuditSystem.Admin, "Super App");
    }
}
