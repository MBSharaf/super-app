using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Shop.Pod;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Shop;

/// <summary>What a seller sold and earned, samples, analytics, calendar, codes and the ID check.</summary>
public sealed class SellerInsightsRepository(AppDbContext db) : ISellerInsightsRepository
{
    private static readonly OrderItemStatus[] Lost = [OrderItemStatus.Cancelled, OrderItemStatus.Refused, OrderItemStatus.Returned];

    public async Task<PagedResult<SellerOrderDto>> OrdersAsync(long storeId, SellerOrdersQuery query, CancellationToken ct)
    {
        var q = db.Set<OrderItem>().AsNoTracking().Where(i => i.StoreId == storeId && i.Order.CustomerId != i.Store.OwnerUserId);
        q = query.Status switch
        {
            "open" => q.Where(i => i.Status == OrderItemStatus.Received || i.Status == OrderItemStatus.Printing || i.Status == OrderItemStatus.Packed || i.Status == OrderItemStatus.Shipped),
            "delivered" => q.Where(i => i.Status == OrderItemStatus.Delivered),
            "cancelled" => q.Where(i => Lost.Contains(i.Status)),
            _ => q,
        };
        var total = await q.CountAsync(ct);
        var ids = await q.OrderByDescending(i => i.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(i => i.Id).ToListAsync(ct);
        var rows = await OrderRepository.ItemRows(db.Set<OrderItem>().Where(i => ids.Contains(i.Id))).ToListAsync(ct);
        var extra = await db.Set<OrderItem>().AsNoTracking().Where(i => ids.Contains(i.Id)).Select(i => new
        {
            i.Id, i.Order.Code, i.Order.CreatedAt, i.Order.AddressJson, i.SellerEarning,
            Stage = db.Set<Job>().Where(j => j.OrderItemId == i.Id).Select(j => (JobStage?)j.Stage).FirstOrDefault(),
        }).ToDictionaryAsync(x => x.Id, ct);
        var list = rows.OrderByDescending(r => r.Id).Select(r =>
        {
            var x = extra[r.Id];
            return new SellerOrderDto(x.Code, x.CreatedAt, r.Id, r.Kind == OrderItemKind.PrintedDesign ? "design" : "product", r.Title, r.Size, r.Color, r.Qty, r.UnitPrice,
                x.SellerEarning, r.Status.ToString(), x.Stage?.ToString(), City(x.AddressJson), OrderRepository.Image(r));
        }).ToList();
        return new PagedResult<SellerOrderDto>(list, query.Page, query.PageSize, total);
    }

    public async Task<SellerKpisDto> KpisAsync(long sellerId, long storeId, int holdDays, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var released = now.AddDays(-holdDays);
        var since = now.AddDays(-30);
        var items = db.Set<OrderItem>().AsNoTracking().Where(i => i.StoreId == storeId && !Lost.Contains(i.Status) && i.Order.CustomerId != i.Store.OwnerUserId);
        var available = await items.Where(i => i.Status == OrderItemStatus.Delivered && i.DeliveredAt <= released).SumAsync(i => i.SellerEarning, ct);
        var pending = await items.Where(i => i.Status != OrderItemStatus.Delivered || i.DeliveredAt > released).SumAsync(i => i.SellerEarning, ct);
        var recent = await items.Where(i => i.CreatedAt >= since).GroupBy(_ => 1).Select(g => new { Qty = g.Sum(i => i.Qty), Earn = g.Sum(i => i.SellerEarning) }).FirstOrDefaultAsync(ct);
        var samples = await db.Set<Sample>().Where(s => s.SellerId == sellerId && !s.IsFree && s.Status != SampleStatus.Rejected).SumAsync(s => s.Price, ct);
        var designs = await db.Set<Design>().Where(d => d.SellerId == sellerId).GroupBy(d => d.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var followers = await db.Set<SellerFollower>().CountAsync(f => f.SellerId == sellerId, ct);
        return new SellerKpisDto(designs.Where(d => d.Key == DesignStatus.Live).Sum(d => d.Count), designs.Where(d => d.Key == DesignStatus.InReview).Sum(d => d.Count),
            recent?.Qty ?? 0, recent?.Earn ?? 0, pending, Math.Max(0, available - samples), followers);
    }

    public async Task<IReadOnlyList<SampleDto>> SamplesAsync(long sellerId, CancellationToken ct) =>
        await db.Set<Sample>().AsNoTracking().Where(s => s.SellerId == sellerId).OrderByDescending(s => s.Id)
            .Select(s => new SampleDto(s.Id, s.DesignId, s.Design.Title, s.Color, s.Size, s.Address, s.Price, s.IsFree, s.Status.ToString(), s.Order != null ? s.Order.Code : null, s.CreatedAt))
            .ToListAsync(ct);

    public Task<int> FreeSamplesSinceAsync(long sellerId, DateTime since, CancellationToken ct) =>
        db.Set<Sample>().CountAsync(s => s.SellerId == sellerId && s.IsFree && s.CreatedAt >= since, ct);

    public void AddSample(Sample sample) => db.Set<Sample>().Add(sample);

    public async Task<(IReadOnlyList<AnalyticsDayDto> Days, IReadOnlyList<AnalyticsDesignDto> Designs)> AnalyticsAsync(long sellerId, long storeId, DateTime from, DateTime to, CancellationToken ct)
    {
        var items = await db.Set<OrderItem>().AsNoTracking()
            .Where(i => i.StoreId == storeId && i.CreatedAt >= from && i.CreatedAt <= to && !Lost.Contains(i.Status) && i.Order.CustomerId != i.Store.OwnerUserId)
            .Select(i => new { i.CreatedAt, i.Qty, i.SellerEarning, i.DesignId }).ToListAsync(ct);
        var days = items.GroupBy(i => DateOnly.FromDateTime(i.CreatedAt)).OrderBy(g => g.Key).Select(g => new AnalyticsDayDto(g.Key, g.Sum(i => i.Qty), g.Sum(i => i.SellerEarning))).ToList();
        var designs = await db.Set<Design>().AsNoTracking().Where(d => d.SellerId == sellerId).Select(d => new { d.Id, d.Title, d.Views }).ToListAsync(ct);
        var perDesign = designs.Select(d => new AnalyticsDesignDto(d.Id, d.Title, d.Views, items.Where(i => i.DesignId == d.Id).Sum(i => i.Qty), items.Where(i => i.DesignId == d.Id).Sum(i => i.SellerEarning)))
            .OrderByDescending(d => d.Items).ThenByDescending(d => d.Views).ToList();
        return (days, perDesign);
    }

    public async Task<IReadOnlyList<CalendarEventDto>> HolidaysAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
        await db.Set<PublicHoliday>().AsNoTracking().Where(h => h.Date >= from && h.Date <= to).OrderBy(h => h.Date)
            .Select(h => new CalendarEventDto(h.Date, h.NameEn, h.NameAr, "Holiday")).ToListAsync(ct);

    public async Task<IReadOnlyList<SellerCodeDto>> CodesAsync(long storeId, CancellationToken ct) =>
        await db.Set<PromoCode>().AsNoTracking().Where(c => c.StoreId == storeId).OrderByDescending(c => c.Id)
            .Select(c => new SellerCodeDto(c.Id, c.Code, c.DiscountType.ToString(), c.Value, c.MinSpend, c.MaxUses, c.PerCustomer, c.UsesCount, c.StartsOn, c.EndsOn, c.Status.ToString(), c.Label))
            .ToListAsync(ct);

    public Task<PromoCode?> CodeAsync(long storeId, long id, CancellationToken ct) => db.Set<PromoCode>().FirstOrDefaultAsync(c => c.StoreId == storeId && c.Id == id, ct);

    public Task<int> ActiveCodesAsync(long storeId, long? exceptId, CancellationToken ct) =>
        db.Set<PromoCode>().CountAsync(c => c.StoreId == storeId && c.Status == CodeStatus.Active && c.Id != exceptId, ct);

    public Task<bool> CodeTakenAsync(string code, long? exceptId, CancellationToken ct) =>
        db.Set<PromoCode>().IgnoreQueryFilters().AnyAsync(c => c.Code == code && c.Id != exceptId, ct);

    public void AddCode(PromoCode code) => db.Set<PromoCode>().Add(code);

    public async Task<KycStatusDto> KycAsync(Guid userId, CancellationToken ct) =>
        await db.Set<KycDocument>().AsNoTracking().Where(k => k.UserId == userId).OrderByDescending(k => k.Id)
            .Select(k => new KycStatusDto(k.Status.ToString(), k.RejectReason, k.CreatedAt)).FirstOrDefaultAsync(ct) ?? new KycStatusDto("None", null, null);

    private static string City(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("city", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? "" : "";
        }
        catch (JsonException)
        {
            return "";
        }
    }
}
