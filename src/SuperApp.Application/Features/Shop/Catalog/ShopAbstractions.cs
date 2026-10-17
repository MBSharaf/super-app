using SuperApp.Application.Abstractions.Storage;
using SuperApp.Domain.Entities.Pod;

namespace SuperApp.Application.Features.Shop.Catalog;

/// <summary>Read-only queries for the public shop (live items only), implemented in Infrastructure.</summary>
public interface IShopReader
{
    /// <summary>Published, visible products of open stores matching the filter (store = handle).</summary>
    Task<IReadOnlyList<ShopCardDto>> ProductCardsAsync(ShopQuery query, CancellationToken ct);

    /// <summary>Live designs of active sellers whose printable product is on sale.</summary>
    Task<IReadOnlyList<ShopCardDto>> DesignCardsAsync(ShopQuery query, CancellationToken ct);

    Task<ProductDetailsDto?> ProductAsync(string slug, CancellationToken ct);

    Task<DesignDetailsDto?> DesignAsync(long id, CancellationToken ct);

    /// <summary>Who owns the design and whether it is live; null when it does not exist.</summary>
    Task<DesignAccess?> DesignAccessAsync(long id, CancellationToken ct);

    /// <summary>The art file of one side (0 front, 1 back) with its container and key.</summary>
    Task<(long FileId, string Container, string Key)?> ArtFileAsync(long designId, int side, CancellationToken ct);

    Task CountViewAsync(long designId, CancellationToken ct);

    /// <summary>The store page; owner = the caller owns it (sees designs that are not public yet).</summary>
    Task<StorePageDto?> StoreAsync(string handle, Guid? caller, CancellationToken ct);

    /// <summary>The seller behind a store handle (only seller stores can be followed).</summary>
    Task<(long SellerId, Guid OwnerUserId)?> SellerOfStoreAsync(string handle, CancellationToken ct);

    Task<bool> SetFollowAsync(long sellerId, Guid userId, bool follow, CancellationToken ct);

    Task<bool> HasOpenReportAsync(long designId, Guid userId, CancellationToken ct);

    void AddReport(DesignReport report);
}

/// <summary>
/// Studio artwork (FR-POD-02/03): originals are private; the site gets resized PNG previews. PNG transparency is kept
/// (photos are re-encoded as JPEG, art is not), hidden data is removed, and SVGs with scripts or outside links are refused.
/// </summary>
public interface IArtworkStore
{
    /// <summary>Saves the art and returns the file id with its pixel size (0 × 0 for SVG, which is vector).</summary>
    Task<(long FileId, int Width, int Height, bool IsVector)> SaveAsync(UploadedFile file, Guid ownerUserId, string field, CancellationToken ct);

    /// <summary>A PNG preview (max 1200 px) of a stored art file, or the SVG itself; null when the file is missing.</summary>
    Task<ArtImage?> PreviewAsync(long fileId, CancellationToken ct);

    /// <summary>The original file (for the factory's print file).</summary>
    Task<ArtImage?> OriginalAsync(long fileId, CancellationToken ct);
}
