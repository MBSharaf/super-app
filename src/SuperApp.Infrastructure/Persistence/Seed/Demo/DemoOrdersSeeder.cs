using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Seed.Demo;

/// <summary>
/// Demo 16: the 52 orders of printed designs the factory and sellers see today, with their items and statuses.
/// Payments, factory jobs and shipments for them are not created — those flows are built in their own phases.
/// </summary>
public sealed class DemoOrdersSeeder(AppDbContext db) : ISeeder
{
    private const decimal VatRate = 0.14m;

    private sealed record ItemRow(string Design, string Store, string Title, string Size, string Color, int Qty, decimal Price, decimal Base,
        decimal Commission, decimal Earning, string Status, DateTime? DeliveredAt);
    private sealed record OrderRow(string Code, string Customer, JsonElement Address, string Phone, decimal Subtotal, decimal DeliveryFee,
        string Payment, string Status, DateTime PlacedAt, List<ItemRow> Items);

    public int Order => 16;
    public bool IsDemo => true;

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        var existing = await db.Set<Order>().Select(o => o.Code).ToListAsync(ct);
        var rows = SeedFile.Read<List<OrderRow>>("Demo/orders.json").Where(o => !existing.Contains(o.Code)).ToList();
        var users = await DemoRefs.UsersAsync(db, ct);
        var stores = await db.Set<Store>().ToDictionaryAsync(s => s.Handle, s => s.Id, ct);
        var designs = await db.Set<Design>().ToDictionaryAsync(d => d.Title, d => d.Id, ct);
        db.Set<Order>().AddRange(rows.Select(r => ToOrder(r, users[r.Customer], stores, designs)));
        await db.SaveChangesAsync(ct);
        return rows.Count;
    }

    private static Order ToOrder(OrderRow r, Guid customerId, Dictionary<string, long> stores, Dictionary<string, long> designs)
    {
        var total = r.Subtotal + r.DeliveryFee;
        return new Order
        {
            Code = r.Code, CustomerId = customerId, AddressJson = r.Address.GetRawText(), Phone = r.Phone,
            Subtotal = r.Subtotal, DeliveryFee = r.DeliveryFee, Total = total, VatAmount = Math.Round(total * VatRate / (1 + VatRate), 2),
            PaymentMethod = DemoRefs.Parse<OrderPaymentMethod>(r.Payment), Status = DemoRefs.Parse<OrderStatus>(r.Status),
            DeliverySpeed = DeliverySpeed.Standard, CreatedAt = r.PlacedAt,
            Items = r.Items.Select(i => new OrderItem
            {
                Kind = OrderItemKind.PrintedDesign, StoreId = stores[i.Store], DesignId = designs[i.Design], Title = i.Title, Size = i.Size, Color = i.Color,
                Qty = i.Qty, UnitPrice = i.Price, BaseAmount = i.Base, CommissionAmount = i.Commission, SellerEarning = i.Earning,
                Status = DemoRefs.Parse<OrderItemStatus>(i.Status), DeliveredAt = i.DeliveredAt, CreatedAt = r.PlacedAt,
            }).ToList(),
        };
    }
}
