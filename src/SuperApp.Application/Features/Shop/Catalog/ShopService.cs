using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Catalog;

/// <summary>The public shop (API §4.1): one list of store products and live printed designs, their pages and design reports.</summary>
public interface IShopService
{
    Task<PagedResult<ShopCardDto>> ListAsync(ShopQuery query, CancellationToken ct);

    Task<ProductDetailsDto> ProductAsync(string slug, CancellationToken ct);

    Task<DesignDetailsDto> DesignAsync(long id, CancellationToken ct);

    /// <summary>The design's art for one side ("front" / "back"): public when live, else only its seller and reviewers.</summary>
    Task<ArtImage> ArtAsync(long id, string side, CancellationToken ct);

    Task ReportAsync(long id, ReportDesignRequest request, CancellationToken ct);
}

public sealed class ShopService(
    IShopReader reader,
    IArtworkStore artwork,
    IFeatureFlags flags,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    ICurrentUser currentUser,
    IValidator<ShopQuery> queryRules,
    IValidator<ReportDesignRequest> reportRules) : IShopService
{
    public async Task<PagedResult<ShopCardDto>> ListAsync(ShopQuery query, CancellationToken ct)
    {
        await queryRules.ValidateAndThrowAsync(query, ct);
        query = query with { Q = query.Q?.Trim() };
        var designsOnly = query.Kind == "design" || query.Category == ShopRules.DesignsCategory;
        var cards = new List<ShopCardDto>();
        if (!designsOnly && query.Kind != "design" && string.IsNullOrEmpty(query.DesignCategory))
        {
            cards.AddRange(await reader.ProductCardsAsync(query, ct));
        }
        if (query.Kind != "product" && (designsOnly || string.IsNullOrEmpty(query.Category)) && await flags.IsOnAsync(KillSwitches.Pod, ct))
        {
            cards.AddRange(await reader.DesignCardsAsync(query, ct));
        }
        var sorted = Sort(cards, query.Sort).ToList();
        var page = sorted.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return new PagedResult<ShopCardDto>(page, query.Page, query.PageSize, sorted.Count);
    }

    public async Task<ProductDetailsDto> ProductAsync(string slug, CancellationToken ct) =>
        await reader.ProductAsync(slug, ct) ?? throw new NotFoundException("This product isn’t available any more.");

    public async Task<DesignDetailsDto> DesignAsync(long id, CancellationToken ct)
    {
        var design = await flags.IsOnAsync(KillSwitches.Pod, ct) ? await reader.DesignAsync(id, ct) : null;
        if (design is null)
        {
            throw new NotFoundException("This design isn’t available any more.");
        }
        await reader.CountViewAsync(id, ct);
        return design;
    }

    public async Task<ArtImage> ArtAsync(long id, string side, CancellationToken ct)
    {
        var access = await reader.DesignAccessAsync(id, ct) ?? throw new NotFoundException();
        if (!access.IsLive && currentUser.UserId != access.SellerUserId && !currentUser.HasPermission("designs.review"))
        {
            throw new NotFoundException();
        }
        var file = await reader.ArtFileAsync(id, side == "back" ? 1 : 0, ct);
        var image = file is { } f && !f.Container.StartsWith("demo", StringComparison.Ordinal) ? await artwork.PreviewAsync(f.FileId, ct) : null;
        return image ?? throw new NotFoundException("This side has no art.");
    }

    public async Task ReportAsync(long id, ReportDesignRequest request, CancellationToken ct)
    {
        await reportRules.ValidateAndThrowAsync(request, ct);
        var access = await reader.DesignAccessAsync(id, ct);
        if (access is not { IsLive: true })
        {
            throw new NotFoundException("This design isn’t available any more.");
        }
        if (currentUser.UserId is { } me && await reader.HasOpenReportAsync(id, me, ct))
        {
            throw new ConflictException("You already reported this design. Our team is looking at it.", "already_reported");
        }
        reader.AddReport(new DesignReport
        {
            DesignId = id, ReporterUserId = currentUser.UserId, Reason = Enum.Parse<DesignReportReason>(request.Reason, true),
            Detail = request.Detail?.Trim(), Contact = request.Contact?.Trim(), Status = DesignReportStatus.Open,
        });
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyStaffAsync("designs.reports", new NotificationMessage(NotificationCategory.Designs, "A design was reported",
            $"{request.Reason} · design #{id}", "#/admin/pod", NotificationTone.Warning), ct);
    }

    private static IEnumerable<ShopCardDto> Sort(List<ShopCardDto> cards, string? sort) => sort switch
    {
        "price" => cards.OrderBy(c => c.Price).ThenBy(c => c.Name, StringComparer.Ordinal),
        "-price" => cards.OrderByDescending(c => c.Price).ThenBy(c => c.Name, StringComparer.Ordinal),
        "rating" => cards.OrderByDescending(c => c.Rating).ThenByDescending(c => c.Rank),
        "newest" => cards.OrderByDescending(c => c.CreatedAt),
        _ => cards.OrderByDescending(c => c.Rank).ThenBy(c => c.Name, StringComparer.Ordinal),
    };
}

/// <summary>Shared shop constants.</summary>
public static class ShopRules
{
    /// <summary>The site's category id for printed designs.</summary>
    public const string DesignsCategory = "designs";

    public static readonly string[] Sorts = ["featured", "price", "-price", "rating", "newest"];
}

public sealed class ShopQueryValidator : AbstractValidator<ShopQuery>
{
    public ShopQueryValidator()
    {
        RuleFor(x => x.Q).MaximumLength(100);
        RuleFor(x => x.Category).MaximumLength(40);
        RuleFor(x => x.DesignCategory).MaximumLength(40);
        RuleFor(x => x.Store).MaximumLength(40);
        RuleFor(x => x.Tag).MaximumLength(40);
        RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0).When(x => x.MinPrice is not null);
        RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(x => x.MinPrice ?? 0).When(x => x.MaxPrice is not null).WithMessage("The highest price must be above the lowest.");
        RuleFor(x => x.Kind).Must(k => k is null or "product" or "design").WithMessage("Kind is product or design.");
        RuleFor(x => x.Sort).Must(s => s is null || ShopRules.Sorts.Contains(s)).WithMessage("Sort by featured, price, -price, rating or newest.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 60);
    }
}

public sealed class ReportDesignRequestValidator : AbstractValidator<ReportDesignRequest>
{
    public ReportDesignRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().IsEnumName(typeof(DesignReportReason), caseSensitive: false).WithMessage("Choose copyright, trademark, offensive or other.");
        RuleFor(x => x.Detail).MaximumLength(600);
        RuleFor(x => x.Detail).NotEmpty().When(x => string.Equals(x.Reason, "Other", StringComparison.OrdinalIgnoreCase)).WithMessage("Tell us what is wrong.");
        RuleFor(x => x.Contact).MaximumLength(120);
    }
}
