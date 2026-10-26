using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Shop.Orders;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Shop;

/// <summary>Delivery fees from ops.Settings "delivery.fees" (standard 60, express 120, free over 1,500 EGP when missing).</summary>
public sealed class ShopSettings(AppDbContext db) : IShopSettings
{
    private static readonly DeliveryFees Fallback = new(60, 120, 1500);

    public async Task<DeliveryFees> DeliveryFeesAsync(CancellationToken ct)
    {
        var json = await db.Set<Setting>().Where(s => s.Key == "delivery.fees").Select(s => s.Value).FirstOrDefaultAsync(ct);
        if (json is null)
        {
            return Fallback;
        }
        var root = JsonDocument.Parse(json).RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("value", out var inner) && inner.ValueKind == JsonValueKind.Object)
        {
            root = inner;
        }
        return new DeliveryFees(Number(root, "standard", Fallback.Standard), Number(root, "express", Fallback.Express), Number(root, "freeOver", Fallback.FreeOver));
    }

    private static decimal Number(JsonElement root, string name, decimal fallback) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : fallback;
}
