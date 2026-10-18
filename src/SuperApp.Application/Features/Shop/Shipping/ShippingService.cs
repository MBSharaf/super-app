using System.Text.Json;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Shipping;

public sealed record ShipmentEventDto(string Status, string? Description, DateTime At);

/// <summary>A parcel of the order with its history. From = the store or the factory that sends it.</summary>
public sealed record ShipmentTrackDto(long Id, string From, string Provider, string Mode, string TrackingNumber, string Status, bool IsReturn, decimal CodAmount,
    DateTime CreatedAt, DateTime? DeliveredAt, IReadOnlyList<ShipmentEventDto> Events);

/// <summary>Courier status update (simulated courier format): status = AwaitingPickup | PickedUp | OutForDelivery | Delivered | Refused | Cancelled.</summary>
public sealed record CourierUpdate(string TrackingNumber, string Status, string? EventId, string? Description);

public sealed record SimulateRequest(string Status);

/// <summary>Reads parcels for customers and admins.</summary>
public interface IShipmentReader
{
    Task<IReadOnlyList<ShipmentTrackDto>?> TrackingAsync(Guid customerId, string orderCode, CancellationToken ct);

    Task<PagedResult<ShipmentTrackDto>> ListAsync(string? status, string? provider, int page, int pageSize, CancellationToken ct);

    Task<ShipmentTrackDto?> OneAsync(long id, CancellationToken ct);

    /// <summary>Who sends and receives the parcel, for booking it again.</summary>
    Task<(string Receiver, string SenderName, string SenderAddress)> PartiesAsync(long shipmentId, CancellationToken ct);
}

/// <summary>Customer tracking, courier webhooks and admin parcel tools (API §4.7).</summary>
public interface IShippingService
{
    Task<IReadOnlyList<ShipmentTrackDto>> TrackingAsync(string orderCode, CancellationToken ct);

    /// <summary>Signature checked (401 bad_signature); an event seen before is ignored. Returns false for a duplicate.</summary>
    Task<bool> WebhookAsync(string provider, string body, string? signature, CancellationToken ct);

    Task<PagedResult<ShipmentTrackDto>> ListAsync(string? status, string? provider, int page, int pageSize, CancellationToken ct);

    Task<ShipmentTrackDto> CancelAsync(long id, CancellationToken ct);

    Task<ShipmentTrackDto> RebookAsync(long id, CancellationToken ct);

    /// <summary>Moves a simulated parcel (403 in test / live mode).</summary>
    Task<ShipmentTrackDto> SimulateAsync(long id, SimulateRequest request, CancellationToken ct);
}

public sealed class ShippingService(
    IShippingProviders providers,
    IShipmentRepository shipments,
    IShipmentReader reader,
    ShipmentTracker tracker,
    IParcelRepository parcels,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    ICurrentUser currentUser) : IShippingService
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ShipmentTrackDto>> TrackingAsync(string orderCode, CancellationToken ct) =>
        await reader.TrackingAsync(currentUser.UserId ?? throw new UnauthorizedException(), orderCode, ct) ?? throw new NotFoundException("Order not found.");

    public async Task<bool> WebhookAsync(string provider, string body, string? signature, CancellationToken ct)
    {
        var courier = providers.ByKey(provider) ?? throw new NotFoundException("Unknown courier.");
        if (!courier.IsSignatureValid(body, signature))
        {
            throw new UnauthorizedException("The signature is not valid.", "bad_signature");
        }
        CourierUpdate? update;
        try { update = JsonSerializer.Deserialize<CourierUpdate>(body, Web); }
        catch (JsonException) { throw new BadRequestException("The update is not valid JSON.", "bad_body"); }
        if (update is null || !Enum.TryParse<ShipmentStatus>(update.Status, true, out var status) || !Enum.IsDefined(status))
        {
            throw new BadRequestException("Unknown status.", "bad_status");
        }
        var shipment = await shipments.ByTrackingAsync(provider, update.TrackingNumber ?? "", ct) ?? throw new NotFoundException("Unknown tracking number.");
        return await tracker.ApplyAsync(shipment, status, update.Description, update.EventId, body.Length > 4000 ? null : body, ct);
    }

    public Task<PagedResult<ShipmentTrackDto>> ListAsync(string? status, string? provider, int page, int pageSize, CancellationToken ct) =>
        reader.ListAsync(status, provider, Math.Max(1, page), pageSize is < 1 or > 200 ? 50 : pageSize, ct);

    public async Task<ShipmentTrackDto> CancelAsync(long id, CancellationToken ct)
    {
        var shipment = await shipments.FindAsync(id, ct) ?? throw new NotFoundException("Shipment not found.");
        if (shipment.Status is not (ShipmentStatus.Created or ShipmentStatus.AwaitingPickup))
        {
            throw new ConflictException("The courier already has this parcel.", "wrong_state");
        }
        await (providers.ByKey(shipment.Provider) ?? await providers.ActiveAsync(ct)).CancelAsync(shipment.TrackingNumber, ct);
        await tracker.ApplyAsync(shipment, ShipmentStatus.Cancelled, "Cancelled by Super App", null, null, ct);
        await audit.LogAsync(AuditSystem.Admin, "shipment.cancelled", $"Cancelled parcel {shipment.TrackingNumber}", "Shipment", Id(id), ct: ct);
        return (await reader.OneAsync(id, ct))!;
    }

    public async Task<ShipmentTrackDto> RebookAsync(long id, CancellationToken ct)
    {
        var old = await shipments.FindAsync(id, ct) ?? throw new NotFoundException("Shipment not found.");
        if (old.Status != ShipmentStatus.Cancelled)
        {
            await CancelAsync(id, ct);
        }
        var order = await parcels.OrderAsync(old.OrderId, ct);
        var (receiver, senderName, senderAddress) = await reader.PartiesAsync(id, ct);
        var provider = await providers.ActiveAsync(ct);
        var booking = await provider.CreateAsync(new ShipmentRequest(order.Code, receiver, order.Phone, order.AddressJson, old.CodAmount, 1, senderName, senderAddress, old.IsReturn), ct);
        var fresh = new Domain.Entities.Shipping.Shipment
        {
            OrderId = old.OrderId, FulfilmentId = old.FulfilmentId, FactoryId = old.FactoryId, Provider = provider.Key, Mode = provider.Mode, TrackingNumber = booking.TrackingNumber,
            CodAmount = old.CodAmount, Status = booking.Status, IsReturn = old.IsReturn,
        };
        fresh.Events.Add(new Domain.Entities.Shipping.ShipmentEvent { Status = (byte)booking.Status, Description = $"Booked again (was {old.TrackingNumber})", At = DateTime.UtcNow });
        shipments.Add(fresh);
        if (order.Fulfilments.FirstOrDefault(f => f.Id == old.FulfilmentId) is { } parcel)
        {
            parcel.Status = FulfilmentStatus.Shipped;
            foreach (var item in order.Items.Where(i => i.StoreId == parcel.StoreId && i.Kind == OrderItemKind.StoreProduct && i.Status == OrderItemStatus.Packed)) { item.Status = OrderItemStatus.Shipped; }
        }
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, "shipment.rebooked", $"Parcel {old.TrackingNumber} booked again as {fresh.TrackingNumber}", "Shipment", Id(fresh.Id), ct: ct);
        return (await reader.OneAsync(fresh.Id, ct))!;
    }

    public async Task<ShipmentTrackDto> SimulateAsync(long id, SimulateRequest request, CancellationToken ct)
    {
        var shipment = await shipments.FindAsync(id, ct) ?? throw new NotFoundException("Shipment not found.");
        if (shipment.Mode != IntegrationMode.Simulated)
        {
            throw new ForbiddenException("Only simulated parcels can be moved by hand.", "not_simulated");
        }
        if (!Enum.TryParse<ShipmentStatus>(request.Status, true, out var status) || !Enum.IsDefined(status))
        {
            throw new BadRequestException("Status is AwaitingPickup, PickedUp, OutForDelivery, Delivered, Refused or Cancelled.", "bad_status");
        }
        await tracker.ApplyAsync(shipment, status, $"Simulated: {status}", null, null, ct);
        return (await reader.OneAsync(id, ct))!;
    }

    private static string Id(long id) => id.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
