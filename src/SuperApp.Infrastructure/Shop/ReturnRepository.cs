using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Shop.Returns;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Shipping;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Shop;

/// <summary>Returns and what can still be returned.</summary>
public sealed class ReturnRepository(AppDbContext db) : IReturnRepository
{
    private const string CodeLetters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public Task<Order?> OrderAsync(Guid customerId, string code, CancellationToken ct) =>
        db.Set<Order>().Include(o => o.Items).ThenInclude(i => i.Store).AsSplitQuery().FirstOrDefaultAsync(o => o.CustomerId == customerId && o.Code == code, ct);

    public async Task<IReadOnlyDictionary<long, int>> ReturnedQtyAsync(long orderId, CancellationToken ct) =>
        await db.Set<ReturnItem>().Where(r => r.Return.OrderId == orderId && r.Return.Status != ReturnStatus.Rejected)
            .GroupBy(r => r.OrderItemId).Select(g => new { g.Key, Qty = g.Sum(r => r.Qty) }).ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

    public async Task<IReadOnlyList<ReturnableItemDto>> ItemsAsync(long orderId, CancellationToken ct)
    {
        var since = DateTime.UtcNow - ReturnsService.Window;
        var returned = await ReturnedQtyAsync(orderId, ct);
        var rows = await OrderRepository.ItemRows(db.Set<OrderItem>().Where(i => i.OrderId == orderId && i.Status == OrderItemStatus.Delivered && i.DeliveredAt >= since)).ToListAsync(ct);
        var delivered = await db.Set<OrderItem>().Where(i => i.OrderId == orderId).ToDictionaryAsync(i => i.Id, i => i.DeliveredAt, ct);
        return rows.Select(r => new ReturnableItemDto(r.Id, r.Kind == OrderItemKind.PrintedDesign ? "design" : "product", r.Title, r.Size, r.Color, r.Qty - returned.GetValueOrDefault(r.Id),
                r.UnitPrice, delivered[r.Id]!.Value + ReturnsService.Window,
                r.Kind == OrderItemKind.PrintedDesign ? ["Reprint", "Refund"] : ["Refund", "Exchange"], OrderRepository.Image(r)))
            .Where(i => i.QtyLeft > 0).ToList();
    }

    public async Task<string> NewCodeAsync(CancellationToken ct)
    {
        while (true)
        {
            var code = "RT-" + new string(Enumerable.Range(0, 7).Select(_ => CodeLetters[RandomNumberGenerator.GetInt32(CodeLetters.Length)]).ToArray());
            if (!await db.Set<ReturnRequest>().AnyAsync(r => r.Code == code, ct))
            {
                return code;
            }
        }
    }

    public void Add(ReturnRequest request) => db.Set<ReturnRequest>().Add(request);

    public async Task<IReadOnlyList<ReturnDto>> MineAsync(Guid customerId, CancellationToken ct) =>
        await DtosAsync(db.Set<ReturnRequest>().Where(r => r.CustomerId == customerId), ct);

    public async Task<ReturnDto?> DtoAsync(string code, CancellationToken ct)
    {
        var list = await DtosAsync(db.Set<ReturnRequest>().Where(r => r.Code == code), ct);
        return list.Count == 0 ? null : list[0];
    }

    public async Task<IReadOnlyList<ReturnDto>> ForDeciderAsync(long? storeId, string? status, CancellationToken ct)
    {
        var q = Decided(db.Set<ReturnRequest>(), storeId);
        if (Enum.TryParse<ReturnStatus>(status, true, out var s)) { q = q.Where(r => r.Status == s); }
        return await DtosAsync(q, ct);
    }

    public Task<ReturnRequest?> ForDecisionAsync(long? storeId, string code, CancellationToken ct) =>
        Decided(db.Set<ReturnRequest>(), storeId).Include(r => r.Items).Include(r => r.Order).ThenInclude(o => o.Items).Include(r => r.Order).ThenInclude(o => o.Fulfilments)
            .AsSplitQuery().FirstOrDefaultAsync(r => r.Code == code, ct);

    /// <summary>A store decides returns of its own products; Super App (null) decides printed items and the official store's.</summary>
    private static IQueryable<ReturnRequest> Decided(IQueryable<ReturnRequest> q, long? storeId) => storeId is { } id
        ? q.Where(r => r.Items.All(i => i.OrderItem.StoreId == id && i.OrderItem.Kind == OrderItemKind.StoreProduct))
        : q.Where(r => r.Items.Any(i => i.OrderItem.Kind == OrderItemKind.PrintedDesign || i.OrderItem.Store.Type == StoreType.Official));

    private async Task<IReadOnlyList<ReturnDto>> DtosAsync(IQueryable<ReturnRequest> q, CancellationToken ct)
    {
        var rows = await q.AsNoTracking().IgnoreQueryFilters().OrderByDescending(r => r.Id).Select(r => new
        {
            r.Id, r.Code, Order = r.Order.Code, r.OrderId, r.Reason, r.Detail, r.Status, r.RejectReason, r.RefundAmount, r.CustomerPaysDelivery, r.CreatedAt,
            Customer = r.Customer.FullName, r.Order.AddressJson,
            Lines = r.Items.Select(i => new { i.OrderItemId, i.Qty, i.Action, i.NewSize, i.NewColor }).ToList(),
            Decider = r.Items.Any(i => i.OrderItem.Kind == OrderItemKind.PrintedDesign || i.OrderItem.Store.Type == StoreType.Official) ? "Super App" : r.Items.Select(i => i.OrderItem.Store.Name).FirstOrDefault(),
            Pickup = db.Set<Shipment>().Where(s => s.OrderId == r.OrderId && s.IsReturn).OrderByDescending(s => s.Id).Select(s => s.TrackingNumber).FirstOrDefault(),
        }).ToListAsync(ct);
        var itemIds = rows.SelectMany(r => r.Lines.Select(l => l.OrderItemId)).Distinct().ToList();
        var items = (await OrderRepository.ItemRows(db.Set<OrderItem>().Where(i => itemIds.Contains(i.Id))).ToListAsync(ct)).ToDictionary(i => i.Id);
        return rows.Select(r => new ReturnDto(r.Code, r.Order, r.Reason.ToString(), r.Detail, r.Status.ToString(), r.Decider ?? "Store", r.RejectReason, r.RefundAmount,
            r.CustomerPaysDelivery, r.Pickup, r.CreatedAt, r.Lines.Select(l =>
            {
                var i = items[l.OrderItemId];
                return new ReturnLineDto(l.OrderItemId, i.Title, i.Size, i.Color, l.Qty, l.Action.ToString(), l.NewSize, l.NewColor, OrderRepository.Image(i));
            }).ToList(), r.Customer, CityOf(r.AddressJson))).ToList();
    }

    /// <summary>The city of the order's delivery address (for the store's returns list).</summary>
    private static string? CityOf(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("city", out var city) ? city.GetString() : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<Job>> JobsAsync(IReadOnlyList<long> orderItemIds, CancellationToken ct) =>
        await db.Set<Job>().Where(j => orderItemIds.Contains(j.OrderItemId)).ToListAsync(ct);

    public async Task<long?> SiblingVariantAsync(long variantId, string? size, string? color, CancellationToken ct)
    {
        var v = await db.Set<ProductVariant>().AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == variantId, ct);
        if (v is null)
        {
            return null;
        }
        var s = string.IsNullOrWhiteSpace(size) ? v.Size : size.Trim();
        var c = string.IsNullOrWhiteSpace(color) ? v.ColorName : color.Trim();
        return await db.Set<ProductVariant>().Where(x => x.ProductId == v.ProductId && x.Size == s && x.ColorName == c).Select(x => (long?)x.Id).FirstOrDefaultAsync(ct);
    }

    public Task<Guid?> StoreOwnerAsync(long storeId, CancellationToken ct) => db.Set<Store>().Where(s => s.Id == storeId).Select(s => s.OwnerUserId).FirstOrDefaultAsync(ct);

    public async Task<bool> TakeStockAsync(long variantId, int qty, CancellationToken ct) =>
        await db.Set<ProductVariant>().Where(v => v.Id == variantId && v.Stock - v.Reserved >= qty).ExecuteUpdateAsync(u => u.SetProperty(v => v.Stock, v => v.Stock - qty), ct) > 0;
}
