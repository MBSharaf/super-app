using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Orders;

/// <summary>"My orders" (API §4.2): list, order page with a parcel per store and printed items, cancel before shipping, receipt PDF.</summary>
public interface IMyOrdersService
{
    Task<PagedResult<OrderSummaryDto>> MineAsync(MyOrdersQuery query, CancellationToken ct);

    Task<OrderDetailsDto> GetAsync(string code, CancellationToken ct);

    /// <summary>Only before anything is packed, shipped or printed (422 too_late); stock goes back and everyone is told.</summary>
    Task<OrderDetailsDto> CancelAsync(string code, CancellationToken ct);

    Task<(byte[] File, string Name)> ReceiptAsync(string code, CancellationToken ct);
}

public sealed class MyOrdersService(
    IOrderRepository orders,
    IUnitOfWork unitOfWork,
    IShopDocuments documents,
    OrderNotices notices,
    ICurrentUser currentUser,
    IValidator<MyOrdersQuery> rules) : IMyOrdersService
{
    public async Task<PagedResult<OrderSummaryDto>> MineAsync(MyOrdersQuery query, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(query, ct);
        return await orders.MineAsync(Me(), query, ct);
    }

    public async Task<OrderDetailsDto> GetAsync(string code, CancellationToken ct) =>
        await orders.DetailsAsync(Me(), code, ct) ?? throw new NotFoundException("Order not found.");

    public async Task<OrderDetailsDto> CancelAsync(string code, CancellationToken ct)
    {
        var order = await orders.FindAsync(Me(), code, ct) ?? throw new NotFoundException("Order not found.");
        var jobs = await orders.JobsAsync(order.Id, ct);
        EnsureCancellable(order, jobs);
        var people = await orders.PeopleToTellAsync(order.Id, ct);
        Cancel(order, jobs, "Cancelled by the customer");
        await unitOfWork.SaveChangesAsync(ct);
        await orders.RestockAsync(order.Items.Where(i => i.ProductVariantId is not null).Select(i => (i.ProductVariantId!.Value, i.Qty)).ToList(), ct);
        await orders.ReleasePromoAsync(order.Id, ct);
        await notices.CancelledAsync(order, people, "the customer", ct);
        return await GetAsync(code, ct);
    }

    public async Task<(byte[] File, string Name)> ReceiptAsync(string code, CancellationToken ct)
    {
        var order = await GetAsync(code, ct);
        return (documents.OrderReceipt(order, await orders.CustomerNameAsync(Me(), ct)), $"receipt-{order.Code}.pdf");
    }

    /// <summary>Whether the customer may still cancel: nothing packed, shipped or printing, and the order still open.</summary>
    public static bool CanCancel(OrderStatus status, IEnumerable<OrderItemStatus> items, IEnumerable<JobStage> jobs) =>
        status is OrderStatus.Placed or OrderStatus.Paid
        && items.All(s => s is OrderItemStatus.Received or OrderItemStatus.Cancelled)
        && jobs.All(s => s is JobStage.New or JobStage.Cancelled);

    private static void EnsureCancellable(Order order, IReadOnlyList<Job> jobs)
    {
        if (order.Status is OrderStatus.Cancelled or OrderStatus.Refunded or OrderStatus.Completed)
        {
            throw new BusinessRuleException("This order is already closed.", "order_closed");
        }
        if (!CanCancel(order.Status, order.Items.Select(i => i.Status), jobs.Select(j => j.Stage)))
        {
            throw new BusinessRuleException("Part of this order is already packed, printing or on its way, so it can’t be cancelled. You can return it after delivery.", "too_late");
        }
    }

    /// <summary>Cancels every open item, parcel and print job of the order (shared with store and admin cancels).</summary>
    public static void Cancel(Order order, IReadOnlyList<Job> jobs, string note)
    {
        foreach (var item in order.Items.Where(i => i.Status == OrderItemStatus.Received))
        {
            item.Status = OrderItemStatus.Cancelled;
        }
        foreach (var f in order.Fulfilments)
        {
            f.Status = FulfilmentStatus.Cancelled;
        }
        foreach (var job in jobs.Where(j => j.Stage != JobStage.Cancelled))
        {
            job.Events.Add(new JobEvent { FromStage = (byte)job.Stage, ToStage = (byte)JobStage.Cancelled, Note = note });
            job.Stage = JobStage.Cancelled;
        }
        order.Status = OrderStatus.Cancelled;
    }

    private Guid Me() => currentUser.UserId ?? throw new UnauthorizedException();
}

public sealed class MyOrdersQueryValidator : AbstractValidator<MyOrdersQuery>
{
    public MyOrdersQueryValidator()
    {
        RuleFor(x => x.Status).Must(s => s is null or "open" or "completed" or "cancelled" or "all").WithMessage("Status is open, completed, cancelled or all.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}
