using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Shop.Pod;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Shop;

/// <summary>The seller profile of a person, with the store and the level's rules.</summary>
public sealed class SellerScopes(AppDbContext db) : ISellerScopes
{
    public async Task<SellerScope?> ScopeAsync(Guid userId, CancellationToken ct)
    {
        var s = await db.Set<Seller>().AsNoTracking().Include(x => x.Store).Include(x => x.Level).FirstOrDefaultAsync(x => x.UserId == userId, ct);
        return s is null ? null : new SellerScope(s.Id, s.UserId, s.StoreId, s.Store.Handle, s.Store.Name, s.Mode, s.Status, s.FrozenUntil, s.Level, s.LifetimeSales, s.Rating);
    }
}
