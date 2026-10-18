using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Shop.Orders;
using SuperApp.Application.Features.Shop.Shipping;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Stores;

/// <summary>
/// A store's part of each order (FR-SHOP-07): New → Packed → Shipped (courier booked, tracking) → Delivered, or Cancelled
/// (stock back, the customer told why). A step from the wrong state → 409 wrong_state.
/// </summary>
public interface IStoreOrdersService
{
    Task<PagedResult<StoreOrderDto>> ListAsync(StoreScope scope, StoreOrdersQuery query, CancellationToken ct);

    Task<StoreOrderDto> GetAsync(StoreScope scope, string code, CancellationToken ct);

    Task<StoreOrderDto> PackAsync(StoreScope scope, string code, CancellationToken ct);

    Task<StoreOrderDto> ShipAsync(StoreScope scope, string code, CancellationToken ct);

    Task<StoreOrderDto> DeliverAsync(StoreScope scope, string code, CancellationToken ct);

    Task<StoreOrderDto> CancelAsync(StoreScope scope, string code, CancelReasonRequest request, CancellationToken ct);
}

public sealed class StoreOrdersService(
    IStoreRepository stores,
    IOrderRepository orders,
    ShipmentBooker courier,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IAuditLogger audit,
    IClock clock,
    IValidator<StoreOrdersQuery> queryRules) : IStoreOrdersService
{
    public async Task<PagedResult<StoreOrderDto>> ListAsync(StoreScope scope, StoreOrdersQuery query, CancellationToken ct)
    {
        await queryRules.ValidateAndThrowAsync(query, ct);
        return await stores.OrdersAsync(scope.StoreId, query with { Q = query.Q?.Trim() }, ct);
    }

    public async Task<StoreOrderDto> GetAsync(StoreScope scope, string code, CancellationToken ct) =>
        await stores.OrderAsync(scope.StoreId, code, ct) ?? throw new NotFoundException("Order not found.");

    public Task<StoreOrderDto> PackAsync(StoreScope scope, string code, CancellationToken ct) =>
        StepAsync(scope, code, FulfilmentStatus.New, FulfilmentStatus.Packed, OrderItemStatus.Packed, "packed", ct);

    public async Task<StoreOrderDto> ShipAsync(StoreScope scope, string code, CancellationToken ct)
    {
        var (order, parcel) = await LoadAsync(scope, code, ct);
        Expect(parcel, FulfilmentStatus.Packed);
        var items = Mine(order, scope).Where(i => i.Status == OrderItemStatus.Packed).ToList();
        var shipment = await courier.BookAsync(order, parcel.Id, null, items.Sum(i => i.UnitPrice * i.Qty), items.Sum(i => i.Qty),
            await stores.CustomerNameAsync(order.CustomerId, ct), (scope.Name, "Store pickup"), ct);
        Move(order, parcel, items, FulfilmentStatus.Shipped, OrderItemStatus.Shipped);
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyUserAsync(order.CustomerId, new NotificationMessage(NotificationCategory.Orders, "Your order is on its way",
            $"{scope.Name} shipped part of {order.Code} · tracking {shipment.TrackingNumber}", $"#/order/{order.Code}", NotificationTone.Success), ct);
        await audit.LogAsync(scope.System, "order.shipped", $"{scope.Who} shipped {order.Code} ({shipment.TrackingNumber})", "Order", order.Code, ct: ct);
        return await GetAsync(scope, code, ct);
    }

    public Task<StoreOrderDto> DeliverAsync(StoreScope scope, string code, CancellationToken ct) =>
        StepAsync(scope, code, FulfilmentStatus.Shipped, FulfilmentStatus.Delivered, OrderItemStatus.Delivered, "delivered", ct);

    public async Task<StoreOrderDto> CancelAsync(StoreScope scope, string code, CancelReasonRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length is < 3 or > 300)
        {
            throw new FluentValidation.ValidationException([new FluentValidation.Results.ValidationFailure("reason", "Tell the customer why (3–300 characters).")]);
        }
        var (order, parcel) = await LoadAsync(scope, code, ct);
        if (parcel.Status is not (FulfilmentStatus.New or FulfilmentStatus.Packed))
        {
            throw new ConflictException("This part of the order is already with the courier and can’t be cancelled.", "wrong_state");
        }
        var items = Mine(order, scope).Where(i => i.Status is OrderItemStatus.Received or OrderItemStatus.Packed).ToList();
        Move(order, parcel, items, FulfilmentStatus.Cancelled, OrderItemStatus.Cancelled);
        await unitOfWork.SaveChangesAsync(ct);
        await orders.RestockAsync(items.Where(i => i.ProductVariantId is not null).Select(i => (i.ProductVariantId!.Value, i.Qty)).ToList(), ct);
        await notifier.NotifyUserAsync(order.CustomerId, new NotificationMessage(NotificationCategory.Orders, $"{scope.Name} cancelled part of {order.Code}",
            $"{request.Reason.Trim()} · you won’t pay for these items.", $"#/order/{order.Code}", NotificationTone.Warning), ct);
        await audit.LogAsync(scope.System, "order.cancelled", $"{scope.Who} cancelled their part of {order.Code}: {request.Reason.Trim()}", "Order", order.Code, ct: ct);
        return await GetAsync(scope, code, ct);
    }

    private async Task<StoreOrderDto> StepAsync(StoreScope scope, string code, FulfilmentStatus from, FulfilmentStatus to, OrderItemStatus itemStatus, string verb, CancellationToken ct)
    {
        var (order, parcel) = await LoadAsync(scope, code, ct);
        Expect(parcel, from);
        Move(order, parcel, Mine(order, scope).Where(i => i.Status != OrderItemStatus.Cancelled).ToList(), to, itemStatus);
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyUserAsync(order.CustomerId, new NotificationMessage(NotificationCategory.Orders, $"Order {order.Code} {verb}",
            $"{scope.Name} marked your items as {verb}.", $"#/order/{order.Code}"), ct);
        await audit.LogAsync(scope.System, $"order.{verb}", $"{scope.Who} marked {order.Code} {verb}", "Order", order.Code, ct: ct);
        return await GetAsync(scope, code, ct);
    }

    private async Task<(Order Order, Fulfilment Parcel)> LoadAsync(StoreScope scope, string code, CancellationToken ct) =>
        await stores.FulfilmentAsync(scope.StoreId, code, ct) ?? throw new NotFoundException("Order not found.");

    private static IEnumerable<OrderItem> Mine(Order order, StoreScope scope) =>
        order.Items.Where(i => i.StoreId == scope.StoreId && i.Kind == OrderItemKind.StoreProduct);

    private static void Expect(Fulfilment parcel, FulfilmentStatus status)
    {
        if (parcel.Status != status)
        {
            throw new ConflictException($"This order is {parcel.Status.ToString().ToLowerInvariant()}, so this step isn’t possible now.", "wrong_state");
        }
    }

    private void Move(Order order, Fulfilment parcel, IReadOnlyList<OrderItem> items, FulfilmentStatus to, OrderItemStatus itemStatus)
    {
        parcel.Status = to;
        foreach (var item in items)
        {
            item.Status = itemStatus;
            item.DeliveredAt = itemStatus == OrderItemStatus.Delivered ? clock.UtcNow : item.DeliveredAt;
        }
        OrderProgress.Update(order);
    }
}

public sealed class StoreOrdersQueryValidator : AbstractValidator<StoreOrdersQuery>
{
    private static readonly string[] Statuses = ["open", "new", "packed", "shipped", "delivered", "cancelled", "all"];

    public StoreOrdersQueryValidator()
    {
        RuleFor(x => x.Status).Must(s => s is null || Statuses.Contains(s)).WithMessage("Status is open, new, packed, shipped, delivered, cancelled or all.");
        RuleFor(x => x.Q).MaximumLength(60);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
