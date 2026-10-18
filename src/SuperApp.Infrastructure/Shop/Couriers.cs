using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SuperApp.Application.Features.Shop.Shipping;
using SuperApp.Domain.Entities.Shipping;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Shop;

/// <summary>
/// The simulated courier (FR-SHIP-04): books parcels with a "SIM…" tracking number and accepts status updates signed
/// with HMAC-SHA256 (<c>X-Courier-Signature</c>, hex). The key is <c>SHIPPING__SIMULATED_SECRET</c>, or a random one per run.
/// </summary>
public sealed class SimulatedCourier(IConfiguration configuration) : IShippingProvider
{
    public const string ProviderKey = "courier.simulated";
    private static readonly string RandomSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public string Key => ProviderKey;

    public IntegrationMode Mode => IntegrationMode.Simulated;

    public Task<ShipmentBooking> CreateAsync(ShipmentRequest request, CancellationToken ct) =>
        Task.FromResult(new ShipmentBooking("SIM" + RandomNumberGenerator.GetInt32(100_000_000, 1_000_000_000), ShipmentStatus.AwaitingPickup));

    public Task CancelAsync(string trackingNumber, CancellationToken ct) => Task.CompletedTask;

    public bool IsSignatureValid(string body, string? signature) =>
        signature is not null && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Sign(body)), Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant()));

    /// <summary>The signature the simulated courier puts on a webhook body.</summary>
    public string Sign(string body) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(configuration["Shipping:Simulated_Secret"] ?? RandomSecret), Encoding.UTF8.GetBytes(body)));
}

/// <summary>The courier adapters; the active one comes from settings "delivery.courier" ({"provider": key}).</summary>
public sealed class ShippingProviders(IEnumerable<IShippingProvider> providers, AppDbContext db) : IShippingProviders
{
    public async Task<IShippingProvider> ActiveAsync(CancellationToken ct)
    {
        var json = await db.Set<Domain.Entities.Ops.Setting>().Where(s => s.Key == "delivery.courier").Select(s => s.Value).FirstOrDefaultAsync(ct);
        var key = json is null ? null : System.Text.RegularExpressions.Regex.Match(json, "\"provider\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value;
        return ByKey(key ?? "") ?? ByKey(SimulatedCourier.ProviderKey)!;
    }

    public IShippingProvider? ByKey(string key) => providers.FirstOrDefault(p => p.Key == key);
}

/// <summary>Parcels in ship.Shipments.</summary>
public sealed class ShipmentRepository(AppDbContext db) : IShipmentRepository
{
    public void Add(Shipment shipment) => db.Set<Shipment>().Add(shipment);

    public Task<Shipment?> FindAsync(long id, CancellationToken ct) => db.Set<Shipment>().Include(s => s.Events).FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<Shipment?> ByTrackingAsync(string provider, string trackingNumber, CancellationToken ct) =>
        db.Set<Shipment>().Include(s => s.Events).FirstOrDefaultAsync(s => s.Provider == provider && s.TrackingNumber == trackingNumber, ct);

    public Task<bool> EventSeenAsync(string providerEventId, CancellationToken ct) => db.Set<ShipmentEvent>().AnyAsync(e => e.ProviderEventId == providerEventId, ct);

    public Task<int> OpenCountAsync(long orderId, CancellationToken ct) =>
        db.Set<Shipment>().CountAsync(s => s.OrderId == orderId && !s.IsReturn && s.Status != ShipmentStatus.Cancelled, ct);
}
