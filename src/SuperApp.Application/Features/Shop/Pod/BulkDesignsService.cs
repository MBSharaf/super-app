using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Abstractions.Storage;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Shop.Catalog;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Pod;

/// <summary>
/// Bulk upload (FR-POD-08, Pro and Elite): many front artworks with shared product, category, price and colours. Each file
/// becomes a design named after the file and is sent for review (Elite: live with a spot check).
/// </summary>
public interface IBulkDesignsService
{
    Task<IReadOnlyList<SellerDesignDto>> UploadAsync(BulkDesignRequest request, IReadOnlyList<UploadedFile> files, CancellationToken ct);
}

public sealed class BulkDesignsService(
    ISellerAccess access,
    IDesignRepository designs,
    IArtworkStore artwork,
    ISellerDesignsService studio,
    IFeatureFlags flags,
    IUnitOfWork unitOfWork,
    IValidator<BulkDesignRequest> rules) : IBulkDesignsService
{
    public const int MaxFiles = 20;

    public async Task<IReadOnlyList<SellerDesignDto>> UploadAsync(BulkDesignRequest request, IReadOnlyList<UploadedFile> files, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        await flags.EnsureOnAsync(KillSwitches.Pod, ct);
        var me = await access.CurrentAsync(ct);
        if (!me.Level.BulkUpload)
        {
            throw new ForbiddenException("Bulk upload opens at the Pro level.", "bulk_level");
        }
        if (files.Count is 0 or > MaxFiles)
        {
            throw new BusinessRuleException($"Upload 1 to {MaxFiles} files at a time.", "bulk_files");
        }
        DesignRules.EnsureRoom(me.Level, await designs.ActiveDesignsAsync(me.SellerId, ct), files.Count);
        var product = await designs.CatalogProductAsync(request.CatalogProductId, ct) ?? throw new BadRequestException("Choose a product from the catalogue.", "unknown_product");
        var category = await designs.DesignCategoryIdAsync(request.CategoryKey, ct) ?? throw new BadRequestException("Choose a category from the list.", "unknown_category");
        if (request.ColorIds.Count == 0 || request.ColorIds.Any(c => product.Colors.All(x => x.Id != c || !x.IsActive)))
        {
            throw new BusinessRuleException("Choose colours of this product.", "bad_color");
        }
        DesignRules.EnsurePrice(request.Price, DesignRules.Base(product, false, me.Level.BaseDiscount), DesignRules.Max(product, false));
        var created = await CreateDraftsAsync(me, product, category, request, files, ct);
        var result = new List<SellerDesignDto>();
        foreach (var id in created)
        {
            result.Add(await studio.SubmitAsync(id, ct));
        }
        return result;
    }

    private async Task<List<long>> CreateDraftsAsync(SellerScope me, CatalogProduct product, int category, BulkDesignRequest request, IReadOnlyList<UploadedFile> files, CancellationToken ct)
    {
        var area = product.PrintAreas.FirstOrDefault(a => a.Side == PrintSide.Front);
        var (x, y, w) = area is null ? (0.5m, 0.4m, 0.4m) : DesignRules.DefaultPlacement(area);
        var created = new List<long>();
        foreach (var file in files)
        {
            var art = await artwork.SaveAsync(file, me.UserId, "files", ct);
            var design = new Design
            {
                SellerId = me.SellerId, CatalogProductId = product.Id, CategoryId = category, Title = TitleOf(file.FileName), Price = request.Price,
                FrontArtFileId = art.FileId, FrontX = x, FrontY = y, FrontW = w, Status = DesignStatus.Draft,
                Colors = request.ColorIds.Distinct().Select(c => new DesignColor { CatalogColorId = c }).ToList(),
                Tags = (request.Tags ?? []).Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).Distinct().Select(t => new DesignTag { Tag = t }).ToList(),
            };
            designs.AddDesign(design);
            await unitOfWork.SaveChangesAsync(ct);
            created.Add(design.Id);
        }
        return created;
    }

    private static string TitleOf(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName).Replace('_', ' ').Replace('-', ' ').Trim();
        stem = stem.Length < 3 ? "New design" : stem;
        return stem.Length > 60 ? stem[..60] : stem;
    }
}

public sealed class BulkDesignRequestValidator : AbstractValidator<BulkDesignRequest>
{
    public BulkDesignRequestValidator()
    {
        RuleFor(x => x.CatalogProductId).GreaterThan(0);
        RuleFor(x => x.CategoryKey).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.ColorIds).NotNull();
        RuleFor(x => x.Tags).Must(t => t is null || t.Count <= 10).WithMessage("Up to 10 tags.");
    }
}
