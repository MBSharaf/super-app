using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Shop.Orders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Shop;

/// <summary>Orders: placing one (stock, jobs, code use and cart in one transaction), reading and cancelling.</summary>
public sealed partial class OrderRepository(AppDbContext db) : IOrderRepository
{
    private const string CodeLetters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public Task<string?> CodeByKeyAsync(Guid customerId, string idempotencyKey, CancellationToken ct) =>
        db.Set<Order>().Where(o => o.CustomerId == customerId && o.IdempotencyKey == idempotencyKey).Select(o => o.Code).FirstOrDefaultAsync(ct);

    public async Task<string> NewCodeAsync(CancellationToken ct)
    {
        while (true)
        {
            var code = "ORD-" + RandomNumberGenerator.GetInt32(100_000, 1_000_000);
            if (!await db.Set<Order>().AnyAsync(o => o.Code == code, ct))
            {
                return code;
            }
        }
    }

    public async Task<string> NewJobCodeAsync(CancellationToken ct)
    {
        while (true)
        {
            var code = "JB-" + new string(Enumerable.Range(0, 8).Select(_ => CodeLetters[RandomNumberGenerator.GetInt32(CodeLetters.Length)]).ToArray());
            if (!await db.Set<Job>().AnyAsync(j => j.Code == code, ct) && !db.Set<Job>().Local.Any(j => j.Code == code))
            {
                return code;
            }
        }
    }

    public async Task<(long Id, Guid OwnerUserId, short SlaHours)?> AcceptingFactoryAsync(CancellationToken ct) =>
        await ShopQueries.AcceptingFactoryAsync(db, ct) is { } f ? (f.Id, f.OwnerUserId, f.SlaHours) : null;

    public Task<decimal?> FactoryPriceAsync(long factoryId, int catalogProductId, bool backPrinted, CancellationToken ct) =>
        db.Set<FactoryPrice>().Where(p => p.FactoryId == factoryId && p.CatalogProductId == catalogProductId)
            .Select(p => (decimal?)(p.PricePerPiece + (backPrinted ? p.BackPrintPrice : 0))).FirstOrDefaultAsync(ct);

    public async Task<(PrepayRule Rule, int Refusals, UserStatus Status)> CustomerRulesAsync(Guid customerId, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().Where(x => x.Id == customerId).Select(x => new { x.MustPrepay, x.CodRefusals, x.Status }).SingleAsync(ct);
        return (u.MustPrepay, u.CodRefusals, u.Status);
    }

    public async Task<(string Json, string Phone)?> AddressAsync(Guid userId, long addressId, CancellationToken ct)
    {
        var a = await db.Set<UserAddress>().AsNoTracking().Where(x => x.Id == addressId && x.UserId == userId)
            .Select(x => new { x.Label, x.Street, x.Building, x.Floor, x.Apartment, x.Landmark, x.Phone, x.AreaId, Area = x.Area.NameEn }).FirstOrDefaultAsync(ct);
        if (a is null)
        {
            return null;
        }
        var line2 = string.Join(", ", new[] { a.Building, a.Floor is null ? null : $"floor {a.Floor}", a.Apartment is null ? null : $"apt {a.Apartment}", a.Landmark }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return (JsonSerializer.Serialize(new { label = a.Label, line1 = a.Street, line2, city = a.Area, areaId = a.AreaId }), a.Phone);
    }

    public async Task PlaceAsync(Order order, IReadOnlyList<Job> jobs, IReadOnlyList<(long VariantId, int Qty)> stock, PromoCodeUse? promoUse,
        IReadOnlyList<long> cartLineIds, CancellationToken ct)
    {
        try
        {
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                await TakeStockAsync(stock, ct);
                await UsePromoAsync(promoUse, ct);
                db.Set<Order>().Add(order);
                db.Set<Job>().AddRange(jobs);
                await db.SaveChangesAsync(ct);
                await db.Set<CartItem>().Where(c => cartLineIds.Contains(c.Id)).ExecuteDeleteAsync(ct);
                await tx.CommitAsync(ct);
            });
        }
        catch (AppException)
        {
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task TakeStockAsync(IReadOnlyList<(long VariantId, int Qty)> stock, CancellationToken ct)
    {
        foreach (var (variantId, qty) in stock)
        {
            var taken = await db.Set<ProductVariant>().Where(v => v.Id == variantId && v.Stock - v.Reserved >= qty)
                .ExecuteUpdateAsync(u => u.SetProperty(v => v.Stock, v => v.Stock - qty), ct);
            if (taken == 0)
            {
                throw new ConflictException("Someone just bought the last of an item in your cart. Check your cart and try again.", "stock_changed");
            }
        }
    }

    private async Task UsePromoAsync(PromoCodeUse? use, CancellationToken ct)
    {
        if (use is null)
        {
            return;
        }
        var counted = await db.Set<PromoCode>().Where(p => p.Id == use.PromoCodeId && (p.MaxUses == null || p.UsesCount < p.MaxUses))
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.UsesCount, p => p.UsesCount + 1), ct);
        if (counted == 0)
        {
            throw new BusinessRuleException("This code has just been used up.", "code_used_up");
        }
        db.Set<PromoCodeUse>().Add(use);
    }

    public Task<Order?> FindAsync(Guid customerId, string code, CancellationToken ct) =>
        db.Set<Order>().Include(o => o.Items).Include(o => o.Fulfilments).AsSplitQuery().FirstOrDefaultAsync(o => o.CustomerId == customerId && o.Code == code, ct);

    public async Task<IReadOnlyList<Job>> JobsAsync(long orderId, CancellationToken ct) =>
        await db.Set<Job>().Where(j => j.OrderItem.OrderId == orderId).ToListAsync(ct);

    public async Task RestockAsync(IReadOnlyList<(long VariantId, int Qty)> stock, CancellationToken ct)
    {
        foreach (var (variantId, qty) in stock)
        {
            await db.Set<ProductVariant>().IgnoreQueryFilters().Where(v => v.Id == variantId).ExecuteUpdateAsync(u => u.SetProperty(v => v.Stock, v => v.Stock + qty), ct);
        }
    }

    public async Task ReleasePromoAsync(long orderId, CancellationToken ct)
    {
        foreach (var use in await db.Set<PromoCodeUse>().Where(u => u.OrderId == orderId).ToListAsync(ct))
        {
            await db.Set<PromoCode>().IgnoreQueryFilters().Where(p => p.Id == use.PromoCodeId && p.UsesCount > 0)
                .ExecuteUpdateAsync(u => u.SetProperty(p => p.UsesCount, p => p.UsesCount - 1), ct);
            db.Set<PromoCodeUse>().Remove(use);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> PeopleToTellAsync(long orderId, CancellationToken ct)
    {
        var stores = await db.Set<OrderItem>().IgnoreQueryFilters().Where(i => i.OrderId == orderId && i.Kind == OrderItemKind.StoreProduct && i.Store.OwnerUserId != null)
            .Select(i => i.Store.OwnerUserId!.Value).Distinct().ToListAsync(ct);
        var factories = await db.Set<Job>().IgnoreQueryFilters().Where(j => j.OrderItem.OrderId == orderId).Select(j => j.Factory.OwnerUserId).Distinct().ToListAsync(ct);
        return [.. stores, .. factories];
    }

    public Task<string> CustomerNameAsync(Guid userId, CancellationToken ct) =>
        db.Users.IgnoreQueryFilters().Where(u => u.Id == userId).Select(u => u.FullName).SingleAsync(ct);
}
