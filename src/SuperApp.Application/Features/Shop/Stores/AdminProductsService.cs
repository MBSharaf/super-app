using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Stores;

/// <summary>Admins hide one product with a reason, or show it again (FR-SHOP-10); the store's owner is told.</summary>
public interface IAdminProductsService
{
    Task HideAsync(long id, HideProductRequest request, CancellationToken ct);

    Task UnhideAsync(long id, CancellationToken ct);
}

public sealed class AdminProductsService(
    IStoreRepository stores,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IAuditLogger audit,
    IValidator<HideProductRequest> rules) : IAdminProductsService
{
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
