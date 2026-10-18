using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Shop.Stores;
using SuperApp.Domain.Entities.Shipping;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Shop;

/// <summary>A store's parts of orders (its parcels) with the customer, items, earnings and courier tracking.</summary>
public sealed partial class StoreRepository
{
    public async Task<PagedResult<StoreOrderDto>> OrdersAsync(long storeId, StoreOrdersQuery query, CancellationToken ct)
    {
        var q = db.Set<Fulfilment>().AsNoTracking().Where(f => f.StoreId == storeId);
        q = query.Status switch
        {
            "open" => q.Where(f => f.Status == FulfilmentStatus.New || f.Status == FulfilmentStatus.Packed),
            "new" => q.Where(f => f.Status == FulfilmentStatus.New),
            "packed" => q.Where(f => f.Status == FulfilmentStatus.Packed),
            "shipped" => q.Where(f => f.Status == FulfilmentStatus.Shipped),
            "delivered" => q.Where(f => f.Status == FulfilmentStatus.Delivered),
            "cancelled" => q.Where(f => f.Status == FulfilmentStatus.Cancelled),
            _ => q,
        };
        if (!string.IsNullOrEmpty(query.Q))
        {
            q = q.Where(f => f.Order.Code.Contains(query.Q) || f.Order.Phone.Contains(query.Q) || f.Order.Customer.FullName.Contains(query.Q));
        }
        var total = await q.CountAsync(ct);
        var ids = await q.OrderByDescending(f => f.Order.CreatedAt).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(f => f.Id).ToListAsync(ct);
        var list = await BuildAsync(storeId, ids, ct);
        return new PagedResult<StoreOrderDto>(list, query.Page, query.PageSize, total);
    }

    public async Task<StoreOrderDto?> OrderAsync(long storeId, string orderCode, CancellationToken ct)
    {
        var id = await db.Set<Fulfilment>().Where(f => f.StoreId == storeId && f.Order.Code == orderCode).Select(f => (long?)f.Id).FirstOrDefaultAsync(ct);
        return id is null ? null : (await BuildAsync(storeId, [id.Value], ct)).Single();
    }

    public async Task<(Order Order, Fulfilment Fulfilment)?> FulfilmentAsync(long storeId, string orderCode, CancellationToken ct)
    {
        var order = await db.Set<Order>().Include(o => o.Items).Include(o => o.Fulfilments).AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Code == orderCode && o.Fulfilments.Any(f => f.StoreId == storeId), ct);
        return order is null ? null : (order, order.Fulfilments.Single(f => f.StoreId == storeId));
    }

    private async Task<List<StoreOrderDto>> BuildAsync(long storeId, List<long> fulfilmentIds, CancellationToken ct)
    {
        var parcels = await db.Set<Fulfilment>().AsNoTracking().IgnoreQueryFilters().Where(f => fulfilmentIds.Contains(f.Id)).Select(f => new
        {
            f.Id, f.OrderId, f.Status, f.Order.Code, f.Order.CreatedAt, Customer = f.Order.Customer.FullName, f.Order.Phone, f.Order.AddressJson,
            f.Order.PaymentMethod, f.Order.DeliverySpeed,
        }).ToListAsync(ct);
        var orderIds = parcels.Select(p => p.OrderId).ToList();
        var items = await OrderRepository.ItemRows(db.Set<OrderItem>().Where(i => orderIds.Contains(i.OrderId) && i.StoreId == storeId && i.Kind == OrderItemKind.StoreProduct))
            .ToListAsync(ct);
        var money = await db.Set<OrderItem>().AsNoTracking().Where(i => orderIds.Contains(i.OrderId) && i.StoreId == storeId)
            .Select(i => new { i.Id, i.OrderId, i.CommissionAmount, i.SellerEarning }).ToDictionaryAsync(i => i.Id, ct);
        var shipments = await db.Set<Shipment>().AsNoTracking().Where(s => s.FulfilmentId != null && fulfilmentIds.Contains(s.FulfilmentId.Value) && !s.IsReturn && s.Status != ShipmentStatus.Cancelled)
            .OrderByDescending(s => s.Id).Select(s => new { s.FulfilmentId, s.Provider, s.TrackingNumber, s.Status }).ToListAsync(ct);
        return fulfilmentIds.Select(id => parcels.Single(p => p.Id == id)).Select(p =>
        {
            var mine = items.Where(i => money[i.Id].OrderId == p.OrderId).ToList();
            var ship = shipments.FirstOrDefault(s => s.FulfilmentId == p.Id);
            return new StoreOrderDto(p.Code, p.CreatedAt, p.Status.ToString(), p.Customer, p.Phone, p.AddressJson, p.PaymentMethod.ToString(), p.DeliverySpeed.ToString(),
                mine.Sum(i => i.UnitPrice * i.Qty), mine.Where(i => i.Status != OrderItemStatus.Cancelled).Sum(i => money[i.Id].SellerEarning), ship?.Provider, ship?.TrackingNumber, ship?.Status.ToString(),
                mine.Select(i => new StoreOrderItemDto(i.Id, i.Title, i.Size, i.Color, i.Qty, i.UnitPrice, money[i.Id].CommissionAmount, money[i.Id].SellerEarning,
                    i.Status.ToString(), OrderRepository.Image(i))).ToList());
        }).ToList();
    }
}
