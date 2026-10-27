using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Entities.Shop;

namespace SuperApp.Application.Features.Shop.Stores;

/// <summary>Stores, their products and their parts of orders. Ids of other stores are never returned.</summary>
public interface IStoreRepository
{
    Task<Store?> StoreAsync(long storeId, CancellationToken ct);

    Task<Store?> OwnedStoreAsync(Guid ownerUserId, Domain.Enums.StoreType type, CancellationToken ct);

    Task<Store?> OfficialStoreAsync(CancellationToken ct);

    void AddStore(Store store);

    Task<string> FreeHandleAsync(string name, CancellationToken ct);

    Task<bool> HandleTakenAsync(string handle, long exceptStoreId, CancellationToken ct);

    Task<StoreProfileDto> ProfileAsync(long storeId, CancellationToken ct);

    Task<IReadOnlyList<OwnProductDto>> ProductsAsync(long storeId, CancellationToken ct);

    Task<OwnProductDto?> ProductDtoAsync(long storeId, long productId, CancellationToken ct);

    /// <summary>The store's product with variants, specs and images, tracked for changes.</summary>
    Task<Product?> ProductAsync(long storeId, long productId, CancellationToken ct);

    void AddProduct(Product product);

    Task<int?> CategoryIdAsync(string key, CancellationToken ct);

    Task<string> FreeSlugAsync(string name, CancellationToken ct);

    Task<string> NewSkuAsync(string slug, CancellationToken ct);

    Task<bool> SkuTakenAsync(string sku, long? exceptVariantId, CancellationToken ct);

    /// <summary>True when an order or cart refers to the variant (it is kept with no stock instead of being removed).</summary>
    Task<bool> VariantInUseAsync(long variantId, CancellationToken ct);

    void RemoveVariant(ProductVariant variant);

    Task<PagedResult<StoreOrderDto>> OrdersAsync(long storeId, StoreOrdersQuery query, CancellationToken ct);

    Task<StoreOrderDto?> OrderAsync(long storeId, string orderCode, CancellationToken ct);

    /// <summary>The order (with all items and parcels) and this store's parcel, tracked for changes.</summary>
    Task<(Order Order, Fulfilment Fulfilment)?> FulfilmentAsync(long storeId, string orderCode, CancellationToken ct);

    Task<string> CustomerNameAsync(Guid customerId, CancellationToken ct);

    /// <summary>The seller who owns the store, tracked for changes.</summary>
    Task<Domain.Entities.Pod.Seller?> SellerOfStoreAsync(long storeId, CancellationToken ct);

    Task<bool> IsLiveDesignOfAsync(long sellerId, long designId, CancellationToken ct);

    /// <summary>Admin: any product by id (tracked), with its store's owner.</summary>
    Task<(Product Product, Guid? OwnerUserId)?> AnyProductAsync(long productId, CancellationToken ct);

    /// <summary>Products waiting for review, oldest first.</summary>
    Task<IReadOnlyList<ReviewProductDto>> ReviewQueueAsync(CancellationToken ct);
}
