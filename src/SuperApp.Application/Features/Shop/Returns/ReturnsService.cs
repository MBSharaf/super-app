using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Shipping;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Returns;

/// <summary>
/// Returns for customers (API §4.3, FR-RET-01..03): within 14 days of delivery; store products can be refunded or exchanged
/// ("changed my mind" pays the return delivery); printed items only for a defect or a wrong print (reprint or refund).
/// One return is decided by one side: a store for its products, Super App for the official store and printed items.
/// </summary>
public interface IReturnsService
{
    Task<IReadOnlyList<ReturnableItemDto>> ReturnableAsync(string orderCode, CancellationToken ct);

    Task<ReturnDto> RequestAsync(ReturnRequestDto request, CancellationToken ct);

    Task<IReadOnlyList<ReturnDto>> MineAsync(CancellationToken ct);

    Task<ReturnDto> GetAsync(string code, CancellationToken ct);
}

public sealed class ReturnsService(
    IReturnRepository returns,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IValidator<ReturnRequestDto> rules) : IReturnsService
{
    public const int Days = 14;

    public async Task<IReadOnlyList<ReturnableItemDto>> ReturnableAsync(string orderCode, CancellationToken ct)
    {
        var order = await returns.OrderAsync(Me(), orderCode, ct) ?? throw new NotFoundException("Order not found.");
        return await returns.ItemsAsync(order.Id, ct);
    }

    public async Task<ReturnDto> RequestAsync(ReturnRequestDto request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var me = Me();
        var order = await returns.OrderAsync(me, request.OrderCode, ct) ?? throw new NotFoundException("Order not found.");
        var allowed = (await returns.ItemsAsync(order.Id, ct)).ToDictionary(i => i.OrderItemId);
        var reason = Enum.Parse<ReturnReason>(request.Reason, true);
        var items = request.Items.Select(r => Check(r, order, allowed, reason)).ToList();
        if (items.Select(i => DeciderOf(i.Item)).Distinct().Count() > 1)
        {
            throw new BusinessRuleException("Return items from one store at a time (printed items and Super App Store items go together).", "one_store_per_return");
        }
        var share = order.Subtotal <= 0 ? 0 : (order.Subtotal - order.DiscountAmount) / order.Subtotal;
        var ret = new ReturnRequest
        {
            Code = await returns.NewCodeAsync(ct), OrderId = order.Id, CustomerId = me, Reason = reason, Detail = request.Detail?.Trim(), Status = ReturnStatus.Requested,
            CustomerPaysDelivery = reason == ReturnReason.ChangedMyMind,
            RefundAmount = Math.Round(items.Where(i => i.Line.Action == ReturnAction.Refund).Sum(i => i.Item.UnitPrice * i.Line.Qty) * share, 2),
            Items = items.Select(i => i.Line).ToList(),
        };
        returns.Add(ret);
        await unitOfWork.SaveChangesAsync(ct);
        await TellDeciderAsync(ret, items[0].Item, ct);
        await audit.LogAsync(AuditSystem.Site, "return.requested", $"Return {ret.Code} for {order.Code}: {reason}", "Return", ret.Code, ct: ct);
        return await GetAsync(ret.Code, ct);
    }

    public async Task<IReadOnlyList<ReturnDto>> MineAsync(CancellationToken ct) => await returns.MineAsync(Me(), ct);

    public async Task<ReturnDto> GetAsync(string code, CancellationToken ct)
    {
        var dto = await returns.DtoAsync(code, ct);
        var mine = (await returns.MineAsync(Me(), ct)).Any(r => r.Code == code);
        return dto is not null && mine ? dto : throw new NotFoundException("Return not found.");
    }

    private static (OrderItem Item, ReturnItem Line) Check(ReturnItemRequest r, Order order, Dictionary<long, ReturnableItemDto> allowed, ReturnReason reason)
    {
        var item = order.Items.FirstOrDefault(i => i.Id == r.OrderItemId) ?? throw new NotFoundException("This item is not part of the order.");
        if (!allowed.TryGetValue(item.Id, out var ok))
        {
            throw item.Status == OrderItemStatus.Delivered && item.DeliveredAt >= DateTime.UtcNow.AddDays(-Days)
                ? new BusinessRuleException("This item is already being returned.", "qty_left")
                : new BusinessRuleException("Returns are possible within 14 days of delivery.", "outside_window");
        }
        var action = Enum.Parse<ReturnAction>(r.Action, true);
        if (r.Qty > ok.QtyLeft)
        {
            throw new BusinessRuleException($"You can return up to {ok.QtyLeft} of this item.", "qty_left");
        }
        if (item.Kind == OrderItemKind.PrintedDesign && reason is not (ReturnReason.FaultyOrDamaged or ReturnReason.WrongItemOrPrint))
        {
            throw new BusinessRuleException("Printed items are made for you, so they come back only for a print defect or a wrong print.", "printed_defect_only");
        }
        if (!ok.Actions.Contains(action.ToString()))
        {
            throw new BusinessRuleException($"{action} isn’t possible for this item.", "action_not_allowed");
        }
        if (action == ReturnAction.Exchange && string.IsNullOrWhiteSpace(r.NewSize) && string.IsNullOrWhiteSpace(r.NewColor))
        {
            throw new FluentValidation.ValidationException([new FluentValidation.Results.ValidationFailure("items", "Choose the new size or colour.")]);
        }
        return (item, new ReturnItem { OrderItemId = item.Id, Qty = r.Qty, Action = action, NewSize = r.NewSize?.Trim(), NewColor = r.NewColor?.Trim() });
    }

    /// <summary>null = Super App decides (printed items and the official store), else the store id.</summary>
    public static long? DeciderOf(OrderItem item) => item.Kind == OrderItemKind.PrintedDesign || item.Store?.Type == StoreType.Official ? null : item.StoreId;

    private async Task TellDeciderAsync(ReturnRequest ret, OrderItem first, CancellationToken ct)
    {
        var message = new NotificationMessage(NotificationCategory.Orders, $"Return request {ret.Code}", $"{ret.Reason} · decide within 2 working days", null, NotificationTone.Warning);
        if (DeciderOf(first) is { } storeId && await returns.StoreOwnerAsync(storeId, ct) is { } owner)
        {
            await notifier.NotifyUserAsync(owner, message with { Link = "#/dash/orders" }, ct);
        }
        else
        {
            await notifier.NotifyStaffAsync("returns.manage", message with { Link = "#/admin/operations" }, ct);
        }
        await notifier.NotifyUserAsync(ret.CustomerId, new NotificationMessage(NotificationCategory.Orders, "Return requested", $"{ret.Code} · we’ll tell you when it’s decided.", $"#/returns/{ret.Code}"), ct);
    }

    private Guid Me() => currentUser.UserId ?? throw new UnauthorizedException();
}

public sealed class ReturnRequestDtoValidator : AbstractValidator<ReturnRequestDto>
{
    public ReturnRequestDtoValidator()
    {
        RuleFor(x => x.OrderCode).NotEmpty().MaximumLength(12);
        RuleFor(x => x.Reason).IsEnumName(typeof(ReturnReason), caseSensitive: false).WithMessage("Choose a reason.");
        RuleFor(x => x.Detail).MaximumLength(600);
        RuleFor(x => x.Items).NotEmpty().Must(i => i.Count <= 20 && i.Select(x => x.OrderItemId).Distinct().Count() == i.Count).WithMessage("Each item once.");
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.Qty).InclusiveBetween(1, 20);
            i.RuleFor(x => x.Action).IsEnumName(typeof(ReturnAction), caseSensitive: false).WithMessage("Refund, Exchange or Reprint.");
            i.RuleFor(x => x.NewSize).MaximumLength(40);
            i.RuleFor(x => x.NewColor).MaximumLength(40);
        });
    }
}
