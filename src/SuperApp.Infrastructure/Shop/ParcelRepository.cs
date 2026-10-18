using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Shop.Shipping;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shipping;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Shop;

/// <summary>Orders, jobs, sales counts and level-ups touched by a parcel status, and parcel reading for customers and admins.</summary>
public sealed class ParcelRepository(AppDbContext db) : IParcelRepository, IShipmentReader
{
    public Task<Order> OrderAsync(long orderId, CancellationToken ct) =>
        db.Set<Order>().Include(o => o.Items).Include(o => o.Fulfilments).AsSplitQuery().SingleAsync(o => o.Id == orderId, ct);

    public async Task<IReadOnlyList<Job>> JobsAsync(long orderId, CancellationToken ct) => await db.Set<Job>().Where(j => j.OrderItem.OrderId == orderId).ToListAsync(ct);

    public async Task<IReadOnlyList<(Guid UserId, string LevelName)>> CountSalesAsync(IReadOnlyList<OrderItem> delivered, CancellationToken ct)
    {
        var raised = new List<(Guid, string)>();
        foreach (var group in delivered.Where(i => i.DesignId is not null).GroupBy(i => i.DesignId!.Value))
        {
            var qty = group.Sum(i => i.Qty);
            await db.Set<Design>().IgnoreQueryFilters().Where(d => d.Id == group.Key).ExecuteUpdateAsync(u => u.SetProperty(d => d.Sales, d => d.Sales + qty), ct);
        }
        var stores = delivered.GroupBy(i => i.StoreId).Select(g => (StoreId: g.Key, Qty: g.Sum(i => i.Qty))).ToList();
        foreach (var (storeId, qty) in stores)
        {
            var seller = await db.Set<Seller>().Include(s => s.Level).FirstOrDefaultAsync(s => s.StoreId == storeId, ct);
            if (seller is null)
            {
                continue;
            }
            seller.LifetimeSales += qty;
            var next = await db.Set<SellerLevel>().Where(l => l.Rank > seller.Level.Rank && l.SalesNeeded <= seller.LifetimeSales).OrderByDescending(l => l.Rank).FirstOrDefaultAsync(ct);
            // A seller with no rating yet is not held back by the rating rule; a frozen level (copyright strike) does not move
            if (next is not null && (seller.Rating == 0 || seller.Rating >= next.MinRating) && (seller.FrozenUntil is null || seller.FrozenUntil < DateTime.UtcNow))
            {
                seller.LevelId = next.Id;
                raised.Add((seller.UserId, next.NameEn));
            }
        }
        return raised;
    }

    public Task CountRefusalAsync(Guid customerId, CancellationToken ct) =>
        db.Users.Where(u => u.Id == customerId).ExecuteUpdateAsync(u => u.SetProperty(x => x.CodRefusals, x => x.CodRefusals + 1), ct);

    public async Task<IReadOnlyList<Guid>> StoreOwnersAsync(IReadOnlyList<long> storeIds, CancellationToken ct) =>
        await db.Set<Store>().IgnoreQueryFilters().Where(s => storeIds.Contains(s.Id) && s.OwnerUserId != null).Select(s => s.OwnerUserId!.Value).ToListAsync(ct);

    public async Task<IReadOnlyList<ShipmentTrackDto>?> TrackingAsync(Guid customerId, string orderCode, CancellationToken ct)
    {
        var orderId = await db.Set<Order>().Where(o => o.CustomerId == customerId && o.Code == orderCode).Select(o => (long?)o.Id).FirstOrDefaultAsync(ct);
        return orderId is null ? null : await Tracks(db.Set<Shipment>().Where(s => s.OrderId == orderId).OrderBy(s => s.Id)).ToListAsync(ct);
    }

    public async Task<PagedResult<ShipmentTrackDto>> ListAsync(string? status, string? provider, int page, int pageSize, CancellationToken ct)
    {
        var q = db.Set<Shipment>().AsQueryable();
        if (Enum.TryParse<ShipmentStatus>(status, true, out var s)) { q = q.Where(x => x.Status == s); }
        if (!string.IsNullOrEmpty(provider)) { q = q.Where(x => x.Provider == provider); }
        var total = await q.CountAsync(ct);
        var items = await Tracks(q.OrderByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct);
        return new PagedResult<ShipmentTrackDto>(items, page, pageSize, total);
    }

    public Task<ShipmentTrackDto?> OneAsync(long id, CancellationToken ct) => Tracks(db.Set<Shipment>().Where(s => s.Id == id)).FirstOrDefaultAsync(ct);

    public async Task<(string Receiver, string SenderName, string SenderAddress)> PartiesAsync(long shipmentId, CancellationToken ct)
    {
        var p = await db.Set<Shipment>().AsNoTracking().IgnoreQueryFilters().Where(s => s.Id == shipmentId).Select(s => new
        {
            Customer = s.Order.Customer.FullName, Store = s.Fulfilment != null ? s.Fulfilment.Store.Name : null,
            Factory = s.Factory != null ? s.Factory.Name : null, FactoryAddress = s.Factory != null ? s.Factory.Address : null,
        }).SingleAsync(ct);
        return (p.Customer, p.Factory ?? p.Store ?? "Super App", p.FactoryAddress ?? "Store pickup");
    }

    private static IQueryable<ShipmentTrackDto> Tracks(IQueryable<Shipment> q) => q.AsNoTracking().IgnoreQueryFilters().Select(s => new ShipmentTrackDto(
        s.Id, s.Factory != null ? "Printed by Super App" : s.Fulfilment != null ? s.Fulfilment.Store.Name : "Super App", s.Provider, s.Mode.ToString(), s.TrackingNumber,
        s.Status.ToString(), s.IsReturn, s.CodAmount, s.CreatedAt, s.DeliveredAt,
        s.Events.OrderBy(e => e.At).ThenBy(e => e.Id).Select(e => new ShipmentEventDto(((ShipmentStatus)e.Status).ToString(), e.Description, e.At)).ToList()));
}
