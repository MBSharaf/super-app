using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Pod;

/// <summary>The seller the caller is, with the level's rules.</summary>
public sealed record SellerScope(long SellerId, Guid UserId, long StoreId, string Handle, string StoreName, SellerMode Mode, SellerStatus Status,
    DateTime? FrozenUntil, SellerLevel Level, int LifetimeSales, decimal Rating)
{
    public bool SellsOwnProducts => Mode is SellerMode.OwnProducts or SellerMode.Both;

    public bool SellsDesigns => Mode is SellerMode.PrintOnDemand or SellerMode.Both;
}

/// <summary>Who may use the seller dashboard.</summary>
public interface ISellerAccess
{
    /// <summary>The caller's seller profile. Not a seller → 403 not_seller; suspended → 423 seller_suspended.</summary>
    Task<SellerScope> CurrentAsync(CancellationToken ct);
}

/// <summary>Seller profiles in the database.</summary>
public interface ISellerScopes
{
    Task<SellerScope?> ScopeAsync(Guid userId, CancellationToken ct);
}

public sealed class SellerAccess(ISellerScopes sellers, ICurrentUser currentUser) : ISellerAccess
{
    private SellerScope? scope;

    public async Task<SellerScope> CurrentAsync(CancellationToken ct)
    {
        var me = currentUser.UserId ?? throw new UnauthorizedException();
        scope ??= await sellers.ScopeAsync(me, ct) ?? throw new ForbiddenException("This page is for sellers. Apply to open your store first.", "not_seller");
        return scope.Status == SellerStatus.Suspended
            ? throw new FeatureDisabledException("Your seller account is suspended. Contact Super App support.", "seller_suspended")
            : scope;
    }
}
