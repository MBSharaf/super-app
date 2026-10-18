using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Catalog;

/// <summary>Store pages <c>/s/{handle}</c> (API §4.1) and following a seller's store.</summary>
public interface IStoresService
{
    Task<StorePageDto> GetAsync(string handle, CancellationToken ct);

    /// <summary>Follow (true) or unfollow (false); returns the new follower count.</summary>
    Task<int> FollowAsync(string handle, bool follow, CancellationToken ct);
}

public sealed class StoresService(IShopReader reader, INotifier notifier, ICurrentUser currentUser) : IStoresService
{
    public async Task<StorePageDto> GetAsync(string handle, CancellationToken ct) =>
        await reader.StoreAsync(handle.Trim().ToLowerInvariant(), currentUser.UserId, ct) ?? throw new NotFoundException("This store doesn’t exist.");

    public async Task<int> FollowAsync(string handle, bool follow, CancellationToken ct)
    {
        var me = currentUser.UserId ?? throw new UnauthorizedException();
        handle = handle.Trim().ToLowerInvariant();
        var seller = await reader.SellerOfStoreAsync(handle, ct);
        if (seller is null)
        {
            _ = await reader.StoreAsync(handle, me, ct) ?? throw new NotFoundException("This store doesn’t exist.");
            throw new BusinessRuleException("Only creators’ stores can be followed.", "not_followable");
        }
        if (seller.Value.OwnerUserId == me)
        {
            throw new BusinessRuleException("This is your own store.", "own_store");
        }
        var changed = await reader.SetFollowAsync(seller.Value.SellerId, me, follow, ct);
        if (changed && follow)
        {
            await notifier.NotifyUserAsync(seller.Value.OwnerUserId, new NotificationMessage(NotificationCategory.Store, "New follower",
                "Someone started following your store.", "#/seller"), ct);
        }
        return (await reader.StoreAsync(handle, me, ct))!.Followers;
    }
}
