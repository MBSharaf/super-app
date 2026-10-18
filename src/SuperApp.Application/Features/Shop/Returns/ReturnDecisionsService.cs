using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Shop.Orders;
using SuperApp.Application.Features.Shop.Shipping;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Shipping;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Returns;

/// <summary>
/// Deciding returns (API §4.3, FR-RET-04/05): the store (or Super App for the official store and printed items) approves —
/// the courier pickup is booked — or rejects with a reason; when the items are back: refund (amount recorded; the money moves
/// in Phase 5, with the seller charged its share and the commission returned), exchange (stock back, the new size sent),
/// or reprint (the factory prints it again).
/// </summary>
public interface IReturnDecisionsService
{
    Task<IReadOnlyList<ReturnDto>> ListAsync(ReturnScope scope, string? status, CancellationToken ct);

    Task<ReturnDto> ApproveAsync(ReturnScope scope, string code, CancellationToken ct);

    Task<ReturnDto> RejectAsync(ReturnScope scope, string code, RejectReturnRequest request, CancellationToken ct);

    Task<ReturnDto> ReceivedAsync(ReturnScope scope, string code, CancellationToken ct);
}

public sealed class ReturnDecisionsService(
    IReturnRepository returns,
    IOrderRepository orders,
    ShipmentBooker courier,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IClock clock) : IReturnDecisionsService
{
    public Task<IReadOnlyList<ReturnDto>> ListAsync(ReturnScope scope, string? status, CancellationToken ct) => returns.ForDeciderAsync(scope.StoreId, status, ct);

    public async Task<ReturnDto> ApproveAsync(ReturnScope scope, string code, CancellationToken ct)
    {
        var ret = await InStatusAsync(scope, code, ReturnStatus.Requested, ct);
        (ret.Status, ret.DecidedById) = (ReturnStatus.Approved, currentUser.UserId);
        var name = await orders.CustomerNameAsync(ret.CustomerId, ct);
        var pickup = await courier.BookReturnAsync(ret.Order, name, ret.Items.Sum(i => i.Qty), (scope.Name, "Returns desk"), ct);
        await unitOfWork.SaveChangesAsync(ct);
        await TellAsync(ret, "Return approved", $"A courier will collect the items · {pickup.TrackingNumber}{(ret.CustomerPaysDelivery ? " · the return delivery is on you" : "")}", ct);
        await audit.LogAsync(scope.System, "return.approved", $"{scope.Name} approved return {ret.Code}", "Return", ret.Code, ct: ct);
        return (await returns.DtoAsync(code, ct))!;
    }

    public async Task<ReturnDto> RejectAsync(ReturnScope scope, string code, RejectReturnRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length is < 5 or > 400)
        {
            throw new FluentValidation.ValidationException([new FluentValidation.Results.ValidationFailure("reason", "Tell the customer why (5–400 characters).")]);
        }
        var ret = await InStatusAsync(scope, code, ReturnStatus.Requested, ct);
        (ret.Status, ret.DecidedById, ret.RejectReason) = (ReturnStatus.Rejected, currentUser.UserId, request.Reason.Trim());
        await unitOfWork.SaveChangesAsync(ct);
        await TellAsync(ret, "Return not accepted", ret.RejectReason, ct);
        await audit.LogAsync(scope.System, "return.rejected", $"{scope.Name} rejected return {ret.Code}: {ret.RejectReason}", "Return", ret.Code, ct: ct);
        return (await returns.DtoAsync(code, ct))!;
    }

    public async Task<ReturnDto> ReceivedAsync(ReturnScope scope, string code, CancellationToken ct)
    {
        var ret = await InStatusAsync(scope, code, ReturnStatus.Approved, ct);
        var restock = new List<(long, int)>();
        foreach (var line in ret.Items)
        {
            var item = ret.Order.Items.Single(i => i.Id == line.OrderItemId);
            if (line.Action == ReturnAction.Reprint) { continue; }
            item.Status = line.Qty >= item.Qty ? OrderItemStatus.Returned : item.Status;
            if (item.ProductVariantId is { } v) { restock.Add((v, line.Qty)); }
        }
        await ReprintAsync(ret, ct);
        var exchanged = await ExchangeAsync(ret, ct);
        ret.Status = ret.Items.Any(i => i.Action == ReturnAction.Reprint) ? ReturnStatus.Reprinting : exchanged ? ReturnStatus.Exchanged : ReturnStatus.Refunded;
        OrderProgress.Update(ret.Order);
        await unitOfWork.SaveChangesAsync(ct);
        await orders.RestockAsync(restock, ct);
        await TellAsync(ret, ret.Status switch { ReturnStatus.Refunded => "Refund on its way", ReturnStatus.Exchanged => "Your exchange is on its way", _ => "We’re printing it again" },
            ret.Status == ReturnStatus.Refunded ? $"{ret.RefundAmount:0} EGP goes back to you." : ret.Code, ct);
        await audit.LogAsync(scope.System, "return.received", $"{scope.Name} received return {ret.Code} → {ret.Status}", "Return", ret.Code, ct: ct);
        return (await returns.DtoAsync(code, ct))!;
    }

    /// <summary>Printed items come back to the factory queue (made again at the factory's cost).</summary>
    private async Task ReprintAsync(ReturnRequest ret, CancellationToken ct)
    {
        var ids = ret.Items.Where(i => i.Action == ReturnAction.Reprint).Select(i => i.OrderItemId).ToList();
        foreach (var job in await returns.JobsAsync(ids, ct))
        {
            job.Events.Add(new JobEvent { FromStage = (byte)job.Stage, ToStage = (byte)JobStage.New, Note = $"Reprint for return {ret.Code}", At = clock.UtcNow });
            (job.Stage, job.ReprintCount, job.DueAt) = (JobStage.New, (byte)(job.ReprintCount + 1), clock.UtcNow.AddHours(48));
            ret.Order.Items.Single(i => i.Id == job.OrderItemId).Status = OrderItemStatus.Printing;
        }
    }

    /// <summary>Takes the new size / colour from stock and books the courier to send it.</summary>
    private async Task<bool> ExchangeAsync(ReturnRequest ret, CancellationToken ct)
    {
        var lines = ret.Items.Where(i => i.Action == ReturnAction.Exchange).ToList();
        if (lines.Count == 0)
        {
            return false;
        }
        foreach (var line in lines)
        {
            var item = ret.Order.Items.Single(i => i.Id == line.OrderItemId);
            var sibling = item.ProductVariantId is { } v ? await returns.SiblingVariantAsync(v, line.NewSize, line.NewColor, ct) : null;
            if (sibling is { } s) { await returns.TakeStockAsync(s, line.Qty, ct); }
        }
        var parcel = ret.Order.Fulfilments.FirstOrDefault(f => f.StoreId == ret.Order.Items.Single(i => i.Id == lines[0].OrderItemId).StoreId);
        await courier.BookAsync(ret.Order, parcel?.Id, null, 0, lines.Sum(l => l.Qty), await orders.CustomerNameAsync(ret.CustomerId, ct), ("Super App returns", "Returns desk"), ct);
        return true;
    }

    private async Task<ReturnRequest> InStatusAsync(ReturnScope scope, string code, ReturnStatus status, CancellationToken ct)
    {
        var ret = await returns.ForDecisionAsync(scope.StoreId, code, ct) ?? throw new NotFoundException("Return not found.");
        return ret.Status == status ? ret : throw new ConflictException($"This return is {ret.Status.ToString().ToLowerInvariant()}, so this step isn’t possible now.", "wrong_state");
    }

    private Task TellAsync(ReturnRequest ret, string title, string? body, CancellationToken ct) =>
        notifier.NotifyUserAsync(ret.CustomerId, new NotificationMessage(NotificationCategory.Orders, $"{title} ({ret.Code})", body, $"#/returns/{ret.Code}"), ct);
}
