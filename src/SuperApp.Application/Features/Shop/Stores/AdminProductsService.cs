using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Stores;

/// <summary>
/// Admins hide one product with a reason, or show it again (FR-SHOP-10), and review the products sellers and partners
/// ask to sell (owner's decision, 2026-10-27). The store's owner is always told.
/// </summary>
public interface IAdminProductsService
{
    Task HideAsync(long id, HideProductRequest request, CancellationToken ct);

    Task UnhideAsync(long id, CancellationToken ct);

    Task<IReadOnlyList<ReviewProductDto>> QueueAsync(CancellationToken ct);

    /// <summary>On sale at once; 409 <c>not_waiting</c> when it isn't in the queue.</summary>
    Task ApproveAsync(long id, CancellationToken ct);

    /// <summary>Back to the store with the reason; they fix it and ask again.</summary>
    Task RejectAsync(long id, HideProductRequest request, CancellationToken ct);
}

public sealed class AdminProductsService(
    IStoreRepository stores,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IAuditLogger audit,
    IClock clock,
    IValidator<HideProductRequest> rules) : IAdminProductsService
{
    public Task<IReadOnlyList<ReviewProductDto>> QueueAsync(CancellationToken ct) => stores.ReviewQueueAsync(ct);

    public async Task ApproveAsync(long id, CancellationToken ct)
    {
        var (product, owner) = await WaitingAsync(id, ct);
        (product.Status, product.ApprovedAt, product.ReviewNote) = (ProductStatus.Published, clock.UtcNow, null);
        await unitOfWork.SaveChangesAsync(ct);
        await TellAsync(owner, $"“{product.Name}” is on sale", "Super App approved it — customers can buy it now.", NotificationTone.Success, ct);
        await audit.LogAsync(AuditSystem.Admin, "product.approved", $"Approved “{product.Name}”", "Product", product.Slug, ct: ct);
    }

    public async Task RejectAsync(long id, HideProductRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var (product, owner) = await WaitingAsync(id, ct);
        (product.Status, product.ReviewNote) = (ProductStatus.Rejected, request.Reason.Trim());
        await unitOfWork.SaveChangesAsync(ct);
        await TellAsync(owner, $"“{product.Name}” needs changes", product.ReviewNote, NotificationTone.Warning, ct);
        await audit.LogAsync(AuditSystem.Admin, "product.rejected", $"Sent back “{product.Name}”: {product.ReviewNote}", "Product", product.Slug, ct: ct);
    }

    private async Task<(Domain.Entities.Shop.Product Product, Guid? Owner)> WaitingAsync(long id, CancellationToken ct)
    {
        var found = await stores.AnyProductAsync(id, ct) ?? throw new NotFoundException("Product not found.");
        return found.Product.Status == ProductStatus.WaitingForReview ? found
            : throw new ConflictException("This product is not waiting for review.", "not_waiting");
    }

    private Task TellAsync(Guid? owner, string title, string? body, NotificationTone tone, CancellationToken ct) =>
        owner is { } o ? notifier.NotifyUserAsync(o, new NotificationMessage(NotificationCategory.Store, title, body, "#/dash/store", tone), ct) : Task.CompletedTask;

    public async Task HideAsync(long id, HideProductRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var (product, owner) = await stores.AnyProductAsync(id, ct) ?? throw new NotFoundException("Product not found.");
        product.HiddenByAdmin = true;
        product.HiddenReason = request.Reason.Trim();
        await unitOfWork.SaveChangesAsync(ct);
        if (owner is { } o)
        {
            await notifier.NotifyUserAsync(o, new NotificationMessage(NotificationCategory.Store, $"“{product.Name}” was hidden from the shop",
                product.HiddenReason, "#/dash/store", NotificationTone.Warning), ct);
        }
        await audit.LogAsync(AuditSystem.Admin, "product.hidden", $"Hid “{product.Name}”: {product.HiddenReason}", "Product", id.ToString(System.Globalization.CultureInfo.InvariantCulture), ct: ct);
    }

    public async Task UnhideAsync(long id, CancellationToken ct)
    {
        var (product, owner) = await stores.AnyProductAsync(id, ct) ?? throw new NotFoundException("Product not found.");
        if (!product.HiddenByAdmin)
        {
            throw new ConflictException("This product is not hidden.", "wrong_state");
        }
        product.HiddenByAdmin = false;
        product.HiddenReason = null;
        await unitOfWork.SaveChangesAsync(ct);
        if (owner is { } o)
        {
            await notifier.NotifyUserAsync(o, new NotificationMessage(NotificationCategory.Store, $"“{product.Name}” is back in the shop", null, "#/dash/store", NotificationTone.Success), ct);
        }
        await audit.LogAsync(AuditSystem.Admin, "product.unhidden", $"Showed “{product.Name}” again", "Product", id.ToString(System.Globalization.CultureInfo.InvariantCulture), ct: ct);
    }
}

public sealed class HideProductRequestValidator : AbstractValidator<HideProductRequest>
{
    public HideProductRequestValidator() => RuleFor(x => x.Reason).NotEmpty().Length(5, 400);
}
