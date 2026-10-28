using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Pitches;
using SuperApp.Application.Features.Shop.Catalog;
using SuperApp.Application.Features.Shop.Orders;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Shipping;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Shop;

/// <summary>"My orders" and the order page: items grouped into parcels (one per store, one for printed items) with tracking.</summary>
public sealed partial class OrderRepository
{
    internal sealed record ItemRow(long Id, OrderItemKind Kind, long StoreId, string StoreHandle, string StoreName, StoreType StoreType, string? Slug, long? DesignId,
        string Title, string Size, string Color, int Qty, decimal UnitPrice, OrderItemStatus Status, DateTime? DeliveredAt,
        string? PhotoContainer, string? PhotoKey, long? PhotoId, string? ArtContainer, string? ArtKey, long? ArtId);

    private static readonly OrderItemStatus[] Progress = [OrderItemStatus.Received, OrderItemStatus.Printing, OrderItemStatus.Packed, OrderItemStatus.Shipped, OrderItemStatus.Delivered];

    public async Task<PagedResult<OrderSummaryDto>> MineAsync(Guid customerId, MyOrdersQuery query, CancellationToken ct)
    {
        var q = db.Set<Order>().AsNoTracking().Where(o => o.CustomerId == customerId);
        q = query.Status switch
        {
            "open" => q.Where(o => o.Status == OrderStatus.Placed || o.Status == OrderStatus.Paid || o.Status == OrderStatus.InProgress),
            "completed" => q.Where(o => o.Status == OrderStatus.Completed),
            "cancelled" => q.Where(o => o.Status == OrderStatus.Cancelled || o.Status == OrderStatus.Refunded),
            _ => q,
        };
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(o => o.CreatedAt).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(o => new { o.Id, o.Code, o.CreatedAt, o.Status, o.Total, o.PaymentMethod, Items = o.Items.Sum(i => i.Qty) }).ToListAsync(ct);
        var ids = rows.Select(r => r.Id).ToList();
        var first = await db.Set<OrderItem>().Where(i => ids.Contains(i.OrderId)).GroupBy(i => i.OrderId).Select(g => new { g.Key, Id = g.Min(i => i.Id) }).ToDictionaryAsync(x => x.Key, x => x.Id, ct);
        var firstIds = first.Values.ToList();
        var items = (await ItemRows(db.Set<OrderItem>().Where(i => firstIds.Contains(i.Id))).ToListAsync(ct)).ToDictionary(i => i.Id);
        var list = rows.Select(r =>
        {
            var item = first.TryGetValue(r.Id, out var itemId) ? items.GetValueOrDefault(itemId) : null;
            return new OrderSummaryDto(r.Code, r.CreatedAt, r.Status.ToString(), r.Items, r.Total, r.PaymentMethod.ToString(), item is null ? null : Image(item), item?.Title ?? "");
        }).ToList();
        return new PagedResult<OrderSummaryDto>(list, query.Page, query.PageSize, total);
    }

    public async Task<OrderDetailsDto?> DetailsAsync(Guid customerId, string code, CancellationToken ct)
    {
        var id = await db.Set<Order>().Where(o => o.CustomerId == customerId && o.Code == code).Select(o => (long?)o.Id).FirstOrDefaultAsync(ct);
        return id is null ? null : await DetailsByIdAsync(id.Value, ct);
    }

    /// <summary>The order page for anyone allowed to see the order (the customer here; staff and stores reuse it).</summary>
    internal async Task<OrderDetailsDto> DetailsByIdAsync(long orderId, CancellationToken ct)
    {
        var o = await db.Set<Order>().AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == orderId).Select(x => new
        {
            x.Code, x.CreatedAt, x.Status, x.PaymentMethod, x.DeliverySpeed, x.AddressJson, x.Phone, x.Subtotal, x.DiscountAmount, x.DeliveryFee, x.Total, x.VatAmount,
            Promo = x.PromoCode != null ? x.PromoCode.Code : null,
            Paid = x.Payment != null && (x.Payment.Status == PaymentStatus.Paid || x.Payment.Status == PaymentStatus.Refunded),
        }).SingleAsync(ct);
        var items = await ItemRows(db.Set<OrderItem>().Where(i => i.OrderId == orderId)).ToListAsync(ct);
        var fulfilments = await db.Set<Fulfilment>().AsNoTracking().Where(f => f.OrderId == orderId).Select(f => new { f.Id, f.StoreId, f.Status }).ToListAsync(ct);
        var shipments = await db.Set<Shipment>().AsNoTracking().Where(s => s.OrderId == orderId && !s.IsReturn && s.Status != ShipmentStatus.Cancelled)
            .OrderByDescending(s => s.Id).Select(s => new { s.FulfilmentId, s.FactoryId, s.Provider, s.TrackingNumber }).ToListAsync(ct);
        var stages = await db.Set<Job>().AsNoTracking().Where(j => j.OrderItem.OrderId == orderId).Select(j => j.Stage).ToListAsync(ct);
        var groups = fulfilments.Select(f =>
        {
            var mine = items.Where(i => i.Kind == OrderItemKind.StoreProduct && i.StoreId == f.StoreId).ToList();
            var s = shipments.FirstOrDefault(x => x.FulfilmentId == f.Id);
            return new OrderGroupDto($"store-{f.StoreId}", Store(mine[0]), false, f.Status.ToString(), s?.Provider, s?.TrackingNumber, mine.Select(ToItem).ToList());
        }).ToList();
        var printed = items.Where(i => i.Kind == OrderItemKind.PrintedDesign).ToList();
        if (printed.Count > 0)
        {
            var s = shipments.FirstOrDefault(x => x.FulfilmentId == null);
            groups.Add(new OrderGroupDto("printed", null, true, PrintedStatus(printed), s?.Provider, s?.TrackingNumber, printed.Select(ToItem).ToList()));
        }
        var canCancel = MyOrdersService.CanCancel(o.Status, items.Select(i => i.Status), stages);
        return new OrderDetailsDto(o.Code, o.CreatedAt, o.Status.ToString(), o.PaymentMethod.ToString(), o.DeliverySpeed.ToString(), o.AddressJson, o.Phone,
            o.Subtotal, o.DiscountAmount, o.DeliveryFee, o.Total, o.VatAmount, o.Promo, canCancel, groups, o.Paid);
    }

    internal static IQueryable<ItemRow> ItemRows(IQueryable<OrderItem> q) => q.AsNoTracking().IgnoreQueryFilters().OrderBy(i => i.Id).Select(i => new ItemRow(
        i.Id, i.Kind, i.StoreId, i.Store.Handle, i.Store.Name, i.Store.Type, i.ProductVariant != null ? i.ProductVariant.Product.Slug : null, i.DesignId,
        i.Title, i.Size, i.Color, i.Qty, i.UnitPrice, i.Status, i.DeliveredAt,
        i.ProductVariant != null ? i.ProductVariant.Product.Images.OrderBy(x => x.SortOrder).Select(x => x.File.Container).FirstOrDefault() : null,
        i.ProductVariant != null ? i.ProductVariant.Product.Images.OrderBy(x => x.SortOrder).Select(x => x.File.StorageKey).FirstOrDefault() : null,
        i.ProductVariant != null ? i.ProductVariant.Product.Images.OrderBy(x => x.SortOrder).Select(x => (long?)x.FileId).FirstOrDefault() : null,
        i.Design != null && i.Design.FrontArtFile != null ? i.Design.FrontArtFile.Container : null,
        i.Design != null && i.Design.FrontArtFile != null ? i.Design.FrontArtFile.StorageKey : null,
        i.Design != null ? i.Design.FrontArtFileId : null));

    internal static OrderItemDto ToItem(ItemRow i) => new(
        i.Id, i.Kind == OrderItemKind.PrintedDesign ? "design" : "product",
        i.Kind == OrderItemKind.PrintedDesign ? i.DesignId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "" : i.Slug ?? "",
        i.Title, Image(i), i.Size, i.Color, i.Qty, i.UnitPrice, i.UnitPrice * i.Qty, i.Status.ToString(), i.DeliveredAt);

    internal static PhotoDto? Image(ItemRow i) => i.Kind == OrderItemKind.PrintedDesign
        ? ShopQueries.Art(i.DesignId ?? 0, "front", i.ArtContainer, i.ArtKey, i.ArtId)
        : ShopQueries.Photo(i.PhotoContainer, i.PhotoKey, i.PhotoId);

    internal static StoreRefDto Store(ItemRow i) => new(i.StoreHandle, i.StoreName, i.StoreType.ToString());

    /// <summary>Printed items move together: the parcel shows the least advanced open item (all cancelled → Cancelled).</summary>
    internal static string PrintedStatus(IReadOnlyList<ItemRow> items)
    {
        var open = items.Where(i => i.Status is not (OrderItemStatus.Cancelled or OrderItemStatus.Refused or OrderItemStatus.Returned)).ToList();
        return open.Count == 0 ? items[0].Status.ToString() : open.MinBy(i => Array.IndexOf(Progress, i.Status))!.Status.ToString();
    }
}
