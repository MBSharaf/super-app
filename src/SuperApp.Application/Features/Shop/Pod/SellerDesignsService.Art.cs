using FluentValidation;
using SuperApp.Application.Abstractions.Storage;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Shop.Catalog;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Pod;

/// <summary>Listing fields and artwork of a design.</summary>
public sealed partial class SellerDesignsService
{
    public async Task<ArtResultDto> UploadArtAsync(long id, string side, UploadedFile file, CancellationToken ct)
    {
        var me = await SellerAsync(ct);
        var design = await EditableAsync(me, id, ct);
        var product = await designs.CatalogProductAsync(design.CatalogProductId, ct) ?? throw new BusinessRuleException("This product is no longer printed.", "unknown_product");
        var back = side == "back";
        if (back && !product.HasBackPrint)
        {
            throw new BusinessRuleException("This product has no back print.", "no_back_print");
        }
        var saved = await artwork.SaveAsync(file, me.UserId, "file", ct);
        var area = product.PrintAreas.FirstOrDefault(a => a.Side == (back ? PrintSide.Back : PrintSide.Front));
        var (x, y, w) = area is null ? (0.5m, 0.4m, 0.4m) : DesignRules.DefaultPlacement(area);
        if (back)
        {
            var wasPrinted = design.BackArtFileId is not null;
            design.BackArtFileId = saved.FileId;
            (design.BackX, design.BackY, design.BackW) = (design.BackX ?? x, design.BackY ?? y, design.BackW ?? w);
            // FR-POD-05: adding the back raises the price by the back-print extra, so the profit stays the same
            design.Price += wasPrinted ? 0 : product.BackPrintExtra;
        }
        else
        {
            design.FrontArtFileId = saved.FileId;
            (design.FrontX, design.FrontY, design.FrontW) = (design.FrontX ?? x, design.FrontY ?? y, design.FrontW ?? w);
        }
        DesignRules.AfterEdit(design, me.Level);
        await unitOfWork.SaveChangesAsync(ct);
        var (dpi, quality) = DesignRules.Quality(saved.Width, saved.IsVector, area?.PrintWidthCm ?? 30);
        return new ArtResultDto(await GetAsync(id, ct), saved.Width, saved.Height, dpi, quality);
    }

    public async Task<SellerDesignDto> RemoveArtAsync(long id, string side, CancellationToken ct)
    {
        var me = await SellerAsync(ct);
        var design = await EditableAsync(me, id, ct);
        if (side == "back")
        {
            var product = await designs.CatalogProductAsync(design.CatalogProductId, ct);
            design.Price -= design.BackArtFileId is not null && product is not null ? Math.Min(product.BackPrintExtra, design.Price) : 0;
            (design.BackArtFileId, design.BackX, design.BackY, design.BackW) = (null, null, null, null);
        }
        else
        {
            (design.FrontArtFileId, design.FrontX, design.FrontY, design.FrontW) = (null, null, null, null);
        }
        if (design.Status is DesignStatus.Live or DesignStatus.InReview && design.FrontArtFileId is null && design.BackArtFileId is null)
        {
            throw new BusinessRuleException("A design on sale needs art on at least one side.", "last_art");
        }
        DesignRules.AfterEdit(design, me.Level);
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task ApplyAsync(Design design, DesignRequest r, CatalogProduct product, CancellationToken ct)
    {
        design.CatalogProductId = product.Id;
        design.CategoryId = string.IsNullOrWhiteSpace(r.CategoryKey) ? null
            : await designs.DesignCategoryIdAsync(r.CategoryKey, ct) ?? throw new BadRequestException("Choose a category from the list.", "unknown_category");
        design.Title = r.Title.Trim();
        design.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        design.Price = r.Price;
        (design.FrontX, design.FrontY, design.FrontW) = design.FrontArtFileId is null || r.Front is null ? (design.FrontX, design.FrontY, design.FrontW) : (r.Front.X, r.Front.Y, r.Front.W);
        (design.BackX, design.BackY, design.BackW) = design.BackArtFileId is null || r.Back is null ? (design.BackX, design.BackY, design.BackW) : (r.Back.X, r.Back.Y, r.Back.W);
        var colors = r.ColorIds.Distinct().ToList();
        foreach (var gone in design.Colors.Where(c => !colors.Contains(c.CatalogColorId)).ToList()) { design.Colors.Remove(gone); }
        foreach (var color in colors.Where(c => design.Colors.All(x => x.CatalogColorId != c))) { design.Colors.Add(new DesignColor { CatalogColorId = color }); }
        var tags = r.Tags.Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).Distinct().ToList();
        foreach (var gone in design.Tags.Where(t => !tags.Contains(t.Tag)).ToList()) { design.Tags.Remove(gone); }
        foreach (var tag in tags.Where(t => design.Tags.All(x => x.Tag != t))) { design.Tags.Add(new DesignTag { Tag = tag }); }
    }
}

public sealed class DesignRequestValidator : AbstractValidator<DesignRequest>
{
    public DesignRequestValidator()
    {
        RuleFor(x => x.CatalogProductId).GreaterThan(0);
        RuleFor(x => x.CategoryKey).MaximumLength(40);
        RuleFor(x => x.Title).NotNull().MaximumLength(60);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.ColorIds).NotNull().Must(c => c.Count <= 12).WithMessage("Up to 12 colours.");
        RuleFor(x => x.Tags).NotNull().Must(t => t.Count <= 10).WithMessage("Up to 10 tags.");
        RuleForEach(x => x.Tags).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Front!).Must(Inside).When(x => x.Front is not null).WithMessage("Placement values are between 0 and 1.");
        RuleFor(x => x.Back!).Must(Inside).When(x => x.Back is not null).WithMessage("Placement values are between 0 and 1.");
        RuleFor(x => x.RowVersion).NotEmpty();
    }

    private static bool Inside(PlacementDto p) => p.X is >= 0 and <= 1 && p.Y is >= 0 and <= 1 && p.W is > 0 and <= 1;
}
