using System.Text.Json;
using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Content;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Content;

/// <summary>Admin site content (API §7.2): banners and the announcement bar (content.banners), pop-ups and their timing (content.popups).</summary>
public interface IContentAdminService
{
    Task<IReadOnlyList<BannerDto>> BannersAsync(CancellationToken ct);

    Task<BannerDto> SaveBannerAsync(long? id, BannerRequest request, CancellationToken ct);

    Task DeleteBannerAsync(long id, CancellationToken ct);

    Task<AnnouncementDto> AnnouncementAsync(AnnouncementDto request, CancellationToken ct);

    Task<PopupsDto> PopupsAsync(CancellationToken ct);

    Task<PopupDto> SavePopupAsync(long? id, PopupRequest request, CancellationToken ct);

    Task DeletePopupAsync(long id, CancellationToken ct);

    Task<PopupTimingDto> TimingAsync(PopupTimingDto request, CancellationToken ct);
}

public sealed class ContentAdminService(
    IContentRepository content,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<BannerRequest> bannerRules,
    IValidator<AnnouncementDto> announcementRules,
    IValidator<PopupRequest> popupRules,
    IValidator<PopupTimingDto> timingRules) : IContentAdminService
{
    public Task<IReadOnlyList<BannerDto>> BannersAsync(CancellationToken ct) => content.BannersAsync(false, DateOnly.FromDateTime(clock.UtcNow), ct);

    public async Task<BannerDto> SaveBannerAsync(long? id, BannerRequest request, CancellationToken ct)
    {
        await bannerRules.ValidateAndThrowAsync(request, ct);
        var banner = id is null ? new Banner() : await content.BannerAsync(id.Value, ct) ?? throw new NotFoundException("Banner not found.");
        if (id is null)
        {
            content.Add(banner);
        }
        (banner.Title, banner.Badge, banner.CtaText, banner.Link, banner.ImageKey) = (request.Title.Trim(), request.Badge, request.CtaText, request.Link, request.ImageKey);
        (banner.IsActive, banner.SortOrder, banner.ShowFrom, banner.ShowUntil) = (request.IsActive, request.SortOrder, request.ShowFrom, request.ShowUntil);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, id is null ? "banner.created" : "banner.updated", banner.Title, "Banner", banner.Id.ToString(), ct: ct);
        return (await BannersAsync(ct)).Single(b => b.Id == banner.Id);
    }

    public async Task DeleteBannerAsync(long id, CancellationToken ct)
    {
        var banner = await content.BannerAsync(id, ct) ?? throw new NotFoundException("Banner not found.");
        content.Remove(banner);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, "banner.deleted", banner.Title, "Banner", id.ToString(), ct: ct);
    }

    public async Task<AnnouncementDto> AnnouncementAsync(AnnouncementDto request, CancellationToken ct)
    {
        await announcementRules.ValidateAndThrowAsync(request, ct);
        await content.SaveSettingAsync("content.announcement", "content", JsonSerializer.Serialize(request, ContentService.Json), currentUser.UserId, ct);
        await audit.LogAsync(AuditSystem.Admin, "announcement.updated", request.On ? request.Text : "Announcement switched off", "Setting", "content.announcement", ct: ct);
        return request;
    }

    public async Task<PopupsDto> PopupsAsync(CancellationToken ct)
    {
        var timing = ContentService.Read<PopupTimingDto>(await content.SettingAsync("popups.timing", ct)) ?? new PopupTimingDto(true, 6, 10, 7, 6);
        return new PopupsDto(timing, await content.PopupsAsync(false, false, ct));
    }

    public async Task<PopupDto> SavePopupAsync(long? id, PopupRequest request, CancellationToken ct)
    {
        await popupRules.ValidateAndThrowAsync(request, ct);
        var popup = id is null ? new PopupAd() : await content.PopupAsync(id.Value, ct) ?? throw new NotFoundException("Pop-up not found.");
        if (id is null)
        {
            content.Add(popup);
        }
        (popup.TitleEn, popup.TitleAr, popup.TextEn, popup.TextAr, popup.CtaEn, popup.CtaAr) = (request.TitleEn, request.TitleAr, request.TextEn, request.TextAr, request.CtaEn, request.CtaAr);
        (popup.Link, popup.Badge, popup.Color, popup.Shape) = (request.Link, request.Badge, request.Color.ToUpperInvariant(), ShapeOf(request.Shape)!.Value);
        (popup.Icons, popup.HideFromSellers, popup.IsActive, popup.SortOrder) = (request.Icons is { Count: > 0 } i ? string.Join(',', i) : null, request.HideFromSellers, request.IsActive, request.SortOrder);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, id is null ? "popup.created" : "popup.updated", popup.TitleEn, "PopupAd", popup.Id.ToString(), ct: ct);
        return (await content.PopupsAsync(false, false, ct)).Single(p => p.Id == popup.Id);
    }

    public async Task DeletePopupAsync(long id, CancellationToken ct)
    {
        var popup = await content.PopupAsync(id, ct) ?? throw new NotFoundException("Pop-up not found.");
        content.Remove(popup);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, "popup.deleted", popup.TitleEn, "PopupAd", id.ToString(), ct: ct);
    }

    /// <summary>cube, coin or tee (the 3D T-shirt).</summary>
    public static PopupShape? ShapeOf(string? shape) => shape?.ToLowerInvariant() switch
    {
        "cube" => PopupShape.Cube,
        "coin" => PopupShape.Coin,
        "tee" or "tee3d" => PopupShape.Tee3d,
        _ => null,
    };

    public async Task<PopupTimingDto> TimingAsync(PopupTimingDto request, CancellationToken ct)
    {
        await timingRules.ValidateAndThrowAsync(request, ct);
        await content.SaveSettingAsync("popups.timing", "popups", JsonSerializer.Serialize(request, ContentService.Json), currentUser.UserId, ct);
        await audit.LogAsync(AuditSystem.Admin, "popups.timing", $"Pop-ups {(request.On ? "on" : "off")}: first {request.First}s, every {request.Interval}s", "Setting", "popups.timing", ct: ct);
        return request;
    }
}

public sealed class BannerRequestValidator : AbstractValidator<BannerRequest>
{
    public BannerRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Length(5, 70);
        RuleFor(x => x.Badge).MaximumLength(22);
        RuleFor(x => x.CtaText).MaximumLength(22);
        RuleFor(x => x.Link).NotEmpty().MaximumLength(300).Must(l => l.StartsWith("#/", StringComparison.Ordinal)).WithMessage("Links stay inside Super App (start with #/).");
        RuleFor(x => x.ImageKey).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ShowUntil).GreaterThanOrEqualTo(x => x.ShowFrom).When(x => x.ShowFrom is not null && x.ShowUntil is not null);
    }
}

public sealed class AnnouncementValidator : AbstractValidator<AnnouncementDto>
{
    public AnnouncementValidator()
    {
        RuleFor(x => x.Text).NotNull().MaximumLength(160);
        RuleFor(x => x.Text).NotEmpty().When(x => x.On).WithMessage("Write the announcement.");
        RuleFor(x => x.Link).MaximumLength(300).Must(l => string.IsNullOrEmpty(l) || l.StartsWith("#/", StringComparison.Ordinal)).WithMessage("Links stay inside Super App (start with #/).");
        RuleFor(x => x.Cta).MaximumLength(24);
        RuleFor(x => x.Tone).Must(t => t is "info" or "success" or "warning" or "danger").WithMessage("Tone: info, success, warning or danger.");
    }
}

public sealed class PopupRequestValidator : AbstractValidator<PopupRequest>
{
    public PopupRequestValidator()
    {
        RuleFor(x => x.TitleEn).NotEmpty().MaximumLength(60);
        RuleFor(x => x.TitleAr).NotEmpty().MaximumLength(60);
        RuleFor(x => x.TextEn).NotEmpty().MaximumLength(140);
        RuleFor(x => x.TextAr).NotEmpty().MaximumLength(140);
        RuleFor(x => x.CtaEn).NotEmpty().MaximumLength(24);
        RuleFor(x => x.CtaAr).NotEmpty().MaximumLength(24);
        RuleFor(x => x.Link).NotEmpty().MaximumLength(300).Must(l => l.StartsWith("#/", StringComparison.Ordinal)).WithMessage("Links stay inside Super App (start with #/).");
        RuleFor(x => x.Badge).MaximumLength(24);
        RuleFor(x => x.Color).Matches("^#[0-9A-Fa-f]{6}$").WithMessage("A colour like #0B6E4F.");
        RuleFor(x => x.Shape).Must(s => ContentAdminService.ShapeOf(s) is not null).WithMessage("Shape: cube, coin or tee.");
        RuleFor(x => x.Icons).Must(i => i is null || (i.Count <= 6 && string.Join(',', i).Length <= 200)).WithMessage("Up to 6 icons.");
    }
}

public sealed class PopupTimingValidator : AbstractValidator<PopupTimingDto>
{
    public PopupTimingValidator()
    {
        RuleFor(x => x.First).InclusiveBetween(0, 600);
        RuleFor(x => x.Interval).InclusiveBetween(5, 3600);
        RuleFor(x => x.Stay).InclusiveBetween(4, 120);
        RuleFor(x => x.Max).InclusiveBetween(0, 50);
    }
}
