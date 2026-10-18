using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Abstractions.Storage;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Stores;

/// <summary>The store profile (API §4.4 /seller/store, §4.5 /partner/store): name, link, look, a break, and for sellers the mode and featured design.</summary>
public interface IStoreProfileService
{
    Task<StoreProfileDto> GetAsync(StoreScope scope, CancellationToken ct);

    Task<StoreProfileDto> UpdateAsync(StoreScope scope, StoreProfileRequest request, CancellationToken ct);

    /// <summary>kind = "logo" or "cover".</summary>
    Task<StoreProfileDto> ImageAsync(StoreScope scope, string kind, UploadedFile file, CancellationToken ct);
}

public sealed class StoreProfileService(
    IStoreRepository stores,
    IFileStorage files,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock,
    IValidator<StoreProfileRequest> rules) : IStoreProfileService
{
    public Task<StoreProfileDto> GetAsync(StoreScope scope, CancellationToken ct) => stores.ProfileAsync(scope.StoreId, ct);

    public async Task<StoreProfileDto> UpdateAsync(StoreScope scope, StoreProfileRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var store = await stores.StoreAsync(scope.StoreId, ct) ?? throw new NotFoundException();
        var handle = request.Handle.Trim().ToLowerInvariant();
        if (handle != store.Handle && (scope.Type == StoreType.Official || await stores.HandleTakenAsync(handle, store.Id, ct)))
        {
            throw new ConflictException("This store link is taken. Try another.", "handle_taken");
        }
        (store.Name, store.Handle, store.Tagline, store.About) = (request.Name.Trim(), handle, Clean(request.Tagline), Clean(request.About));
        store.AccentColor = request.AccentColor ?? store.AccentColor;
        store.CoverPositionY = (byte)request.CoverPositionY;
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var onBreak = request.BreakUntil is { } until && until >= today;
        (store.BreakUntil, store.BreakNote) = onBreak ? (request.BreakUntil, Clean(request.BreakNote)) : (null, null);
        store.Status = store.Status == StoreStatus.Suspended ? StoreStatus.Suspended : onBreak ? StoreStatus.OnBreak : StoreStatus.Active;
        if (scope.Type == StoreType.Seller)
        {
            await ApplySellerAsync(scope, request, ct);
        }
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(scope.System, "store.updated", $"{scope.Who} updated the store profile", "Store", store.Handle, ct: ct);
        return await stores.ProfileAsync(store.Id, ct);
    }

    public async Task<StoreProfileDto> ImageAsync(StoreScope scope, string kind, UploadedFile file, CancellationToken ct)
    {
        var store = await stores.StoreAsync(scope.StoreId, ct) ?? throw new NotFoundException();
        var id = await files.SaveAsync(file, UploadKind.Photo, scope.OwnerUserId ?? Guid.Empty, kind, ct);
        if (kind == "logo") { store.LogoFileId = id; } else { store.CoverFileId = id; }
        await unitOfWork.SaveChangesAsync(ct);
        return await stores.ProfileAsync(store.Id, ct);
    }

    private async Task ApplySellerAsync(StoreScope scope, StoreProfileRequest request, CancellationToken ct)
    {
        var seller = await stores.SellerOfStoreAsync(scope.StoreId, ct) ?? throw new NotFoundException();
        seller.Mode = request.Mode switch { "store" => SellerMode.OwnProducts, "both" => SellerMode.Both, "pod" => SellerMode.PrintOnDemand, _ => seller.Mode };
        if (request.FeaturedDesignId is { } featured && !await stores.IsLiveDesignOfAsync(seller.Id, featured, ct))
        {
            throw new BusinessRuleException("Feature one of your live designs.", "bad_featured");
        }
        seller.FeaturedDesignId = request.FeaturedDesignId;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class StoreProfileRequestValidator : AbstractValidator<StoreProfileRequest>
{
    public StoreProfileRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 80);
        RuleFor(x => x.Handle).NotEmpty().Length(3, 40).Matches("^[a-zA-Z0-9-]+$").WithMessage("Use letters, numbers and dashes.");
        RuleFor(x => x.Tagline).MaximumLength(120);
        RuleFor(x => x.About).MaximumLength(1000);
        RuleFor(x => x.AccentColor).Matches("^#[0-9A-Fa-f]{6}$").When(x => x.AccentColor is not null).WithMessage("Colour like #0B6E4F.");
        RuleFor(x => x.CoverPositionY).InclusiveBetween(0, 100);
        RuleFor(x => x.BreakNote).MaximumLength(200);
        RuleFor(x => x.Mode).Must(m => m is null or "pod" or "store" or "both").WithMessage("Mode is pod, store or both.");
    }
}
