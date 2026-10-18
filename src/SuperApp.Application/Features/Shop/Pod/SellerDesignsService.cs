using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Abstractions.Storage;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Shop.Catalog;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Pod;

/// <summary>
/// The design studio (API §4.4, FR-POD-02..08): drafts, listing and price, front / back art with a print-quality check,
/// submit for review (Elite: live at once with a spot check), pause, resume, delete and bulk upload.
/// </summary>
public interface ISellerDesignsService
{
    Task<IReadOnlyList<SellerCatalogDto>> CatalogueAsync(CancellationToken ct);

    Task<IReadOnlyList<SellerDesignDto>> ListAsync(string? status, CancellationToken ct);

    Task<SellerDesignDto> GetAsync(long id, CancellationToken ct);

    Task<SellerDesignDto> CreateAsync(NewDesignRequest request, CancellationToken ct);

    Task<SellerDesignDto> UpdateAsync(long id, DesignRequest request, CancellationToken ct);

    Task<ArtResultDto> UploadArtAsync(long id, string side, UploadedFile file, CancellationToken ct);

    Task<SellerDesignDto> RemoveArtAsync(long id, string side, CancellationToken ct);

    Task<SellerDesignDto> SubmitAsync(long id, CancellationToken ct);

    Task<SellerDesignDto> PauseAsync(long id, bool pause, CancellationToken ct);

    Task DeleteAsync(long id, CancellationToken ct);
}

public sealed partial class SellerDesignsService(
    ISellerAccess access,
    IDesignRepository designs,
    IArtworkStore artwork,
    IFeatureFlags flags,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IAuditLogger audit,
    IClock clock,
    IValidator<DesignRequest> rules) : ISellerDesignsService
{
    public async Task<IReadOnlyList<SellerCatalogDto>> CatalogueAsync(CancellationToken ct) =>
        await designs.CatalogueAsync((await access.CurrentAsync(ct)).Level.BaseDiscount, ct);

    public async Task<IReadOnlyList<SellerDesignDto>> ListAsync(string? status, CancellationToken ct)
    {
        if (status is not null && !Enum.TryParse<DesignStatus>(status, true, out _))
        {
            throw new BadRequestException("Status is Draft, InReview, Live, Rejected, Paused or Removed.", "bad_status");
        }
        var me = await access.CurrentAsync(ct);
        return await designs.DesignsAsync(me.SellerId, status, me.Level.BaseDiscount, ct);
    }

    public async Task<SellerDesignDto> GetAsync(long id, CancellationToken ct)
    {
        var me = await access.CurrentAsync(ct);
        return await designs.DesignDtoAsync(me.SellerId, id, me.Level.BaseDiscount, ct) ?? throw new NotFoundException("Design not found.");
    }

    public async Task<SellerDesignDto> CreateAsync(NewDesignRequest request, CancellationToken ct)
    {
        var me = await SellerAsync(ct);
        var product = await designs.CatalogProductAsync(request.CatalogProductId, ct);
        if (product is not { Status: CatalogProductStatus.Active })
        {
            throw new BadRequestException("Choose a product from the catalogue.", "unknown_product");
        }
        DesignRules.EnsureRoom(me.Level, await designs.ActiveDesignsAsync(me.SellerId, ct));
        var design = new Design { SellerId = me.SellerId, CatalogProductId = product.Id, Price = DesignRules.Base(product, false, me.Level.BaseDiscount), Status = DesignStatus.Draft };
        designs.AddDesign(design);
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(design.Id, ct);
    }

    public async Task<SellerDesignDto> UpdateAsync(long id, DesignRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var me = await SellerAsync(ct);
        var design = await EditableAsync(me, id, ct);
        if (DesignRules.Token(design) != request.RowVersion)
        {
            throw new ConflictException("This design was changed somewhere else. Reload it and try again.", "stale");
        }
        var product = await designs.CatalogProductAsync(request.CatalogProductId, ct) ?? throw new BadRequestException("Choose a product from the catalogue.", "unknown_product");
        if (request.ColorIds.Any(c => product.Colors.All(x => x.Id != c || !x.IsActive)))
        {
            throw new BusinessRuleException("Choose colours of this product.", "bad_color");
        }
        var back = design.BackArtFileId is not null && product.HasBackPrint;
        DesignRules.EnsurePrice(request.Price, DesignRules.Base(product, back, me.Level.BaseDiscount), DesignRules.Max(product, back));
        await ApplyAsync(design, request, product, ct);
        DesignRules.AfterEdit(design, me.Level);
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<SellerDesignDto> SubmitAsync(long id, CancellationToken ct)
    {
        await flags.EnsureOnAsync(KillSwitches.Pod, ct);
        var me = await SellerAsync(ct);
        var design = await designs.DesignAsync(me.SellerId, id, ct) ?? throw new NotFoundException("Design not found.");
        if (design.Status is not (DesignStatus.Draft or DesignStatus.Rejected))
        {
            throw new ConflictException("Only drafts can be sent for review.", "wrong_status");
        }
        var product = await designs.CatalogProductAsync(design.CatalogProductId, ct) ?? throw new BusinessRuleException("This product is no longer printed. Choose another.", "unknown_product");
        EnsureComplete(design);
        var back = design.BackArtFileId is not null && product.HasBackPrint;
        DesignRules.EnsurePrice(design.Price, DesignRules.Base(product, back, me.Level.BaseDiscount), DesignRules.Max(product, back));
        DesignRules.EnsureRoom(me.Level, await designs.ActiveDesignsAsync(me.SellerId, ct));
        var instant = me.Level.ReviewLane == ReviewLane.InstantWithSpotCheck;
        (design.Status, design.SpotCheck, design.SubmittedAt, design.RejectReason) = (instant ? DesignStatus.Live : DesignStatus.InReview, instant, clock.UtcNow, null);
        design.ReviewedAt = instant ? clock.UtcNow : null;
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyStaffAsync("designs.review", new NotificationMessage(NotificationCategory.Designs,
            instant ? "Spot check: an Elite design went live" : me.Level.ReviewLane == ReviewLane.Priority ? "Priority design to review" : "New design to review",
            $"“{design.Title}” · {me.StoreName}", "#/admin/pod"), ct);
        await audit.LogAsync(AuditSystem.Seller, "design.submitted", $"{me.StoreName} sent “{design.Title}”{(instant ? " (live, spot check)" : " for review")}", "Design", id.ToString(System.Globalization.CultureInfo.InvariantCulture), ct: ct);
        return await GetAsync(id, ct);
    }

    public async Task<SellerDesignDto> PauseAsync(long id, bool pause, CancellationToken ct)
    {
        var me = await SellerAsync(ct);
        var design = await designs.DesignAsync(me.SellerId, id, ct) ?? throw new NotFoundException("Design not found.");
        var (from, to) = pause ? (DesignStatus.Live, DesignStatus.Paused) : (DesignStatus.Paused, DesignStatus.Live);
        if (design.Status != from)
        {
            throw new ConflictException(pause ? "Only live designs can be paused." : "Only paused designs can be resumed.", "wrong_status");
        }
        design.Status = to;
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        var me = await SellerAsync(ct);
        var design = await designs.DesignAsync(me.SellerId, id, ct) ?? throw new NotFoundException("Design not found.");
        design.IsDeleted = true;
        design.DeletedAt = clock.UtcNow;
        await designs.ClearFeaturedAsync(me.SellerId, id, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Seller, "design.deleted", $"{me.StoreName} deleted “{design.Title}”", "Design", id.ToString(System.Globalization.CultureInfo.InvariantCulture), ct: ct);
    }

    private async Task<SellerScope> SellerAsync(CancellationToken ct)
    {
        var me = await access.CurrentAsync(ct);
        return me.SellsDesigns ? me : throw new BusinessRuleException("Your store sells your own products only. Switch the store mode to sell printed designs.", "designs_off");
    }

    private async Task<Design> EditableAsync(SellerScope me, long id, CancellationToken ct)
    {
        var design = await designs.DesignAsync(me.SellerId, id, ct) ?? throw new NotFoundException("Design not found.");
        return design.Status == DesignStatus.Removed ? throw new ConflictException("This design was taken down and can’t be edited.", "wrong_status") : design;
    }

    private static void EnsureComplete(Design d)
    {
        var missing = new List<string>();
        if (d.FrontArtFileId is null && d.BackArtFileId is null) { missing.Add("artwork"); }
        if (d.Title.Trim().Length < 3) { missing.Add("a title"); }
        if (d.CategoryId is null) { missing.Add("a category"); }
        if (d.Colors.Count == 0) { missing.Add("at least one colour"); }
        if (missing.Count > 0)
        {
            throw new BusinessRuleException($"Add {string.Join(", ", missing)} before sending it for review.", "incomplete");
        }
    }
}
