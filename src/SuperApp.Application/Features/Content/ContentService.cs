using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Options;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Options;
using SuperApp.Domain.Entities.Content;

namespace SuperApp.Application.Features.Content;

/// <summary>
/// Public site content (API §6.6, FR-CNT-01/02/04/05): home banners and the announcement bar, pop-ups and their view /
/// click counters, cookie consent, and the sitemap and robots file built from live data.
/// </summary>
public interface IContentService
{
    Task<HomeDto> HomeAsync(CancellationToken ct);

    Task<PopupsDto> PopupsAsync(CancellationToken ct);

    Task CountPopupAsync(long id, PopupEventRequest request, CancellationToken ct);

    Task CookieConsentAsync(CookieConsentRequest request, CancellationToken ct);

    Task<string> SitemapAsync(CancellationToken ct);

    string Robots();
}

public sealed class ContentService(
    IContentRepository content,
    ILegalHelpRepository legal,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock,
    IOptions<SiteOptions> site,
    IValidator<CookieConsentRequest> consentRules) : IContentService
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<HomeDto> HomeAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var banners = (await content.BannersAsync(true, today, ct)).Take(3).ToList();
        var announcement = Read<AnnouncementDto>(await content.SettingAsync("content.announcement", ct));
        var live = announcement is { On: true } && !string.IsNullOrWhiteSpace(announcement.Text) && (announcement.Until is null || announcement.Until >= today);
        return new HomeDto(banners, live ? announcement : null);
    }

    public async Task<PopupsDto> PopupsAsync(CancellationToken ct)
    {
        var seller = currentUser.UserId is { } id && await content.IsSellerAsync(id, ct);
        var timing = Read<PopupTimingDto>(await content.SettingAsync("popups.timing", ct)) ?? new PopupTimingDto(true, 6, 10, 7, 6);
        return new PopupsDto(timing, timing.On ? await content.PopupsAsync(true, seller, ct) : []);
    }

    public async Task CountPopupAsync(long id, PopupEventRequest request, CancellationToken ct)
    {
        var kind = request.Kind?.ToLowerInvariant();
        if (kind is not ("shown" or "clicked"))
        {
            throw new BadRequestException("Kind: shown or clicked.", "kind");
        }
        if (!await content.CountAsync(id, kind == "clicked", ct))
        {
            throw new NotFoundException("Pop-up not found.");
        }
    }

    /// <summary>Stored per visitor with the version of the cookies policy (FR-CNT-04).</summary>
    public async Task CookieConsentAsync(CookieConsentRequest request, CancellationToken ct)
    {
        await consentRules.ValidateAndThrowAsync(request, ct);
        var cookies = (await legal.DocumentsAsync(false, ct)).FirstOrDefault(d => d.Key == "cookies");
        content.AddConsent(new CookieConsent
        {
            VisitorId = request.VisitorId.Trim(), UserId = currentUser.UserId, Analytics = request.Analytics, PolicyVersion = (short)(cookies?.Version ?? 1), At = clock.UtcNow,
        });
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Public pages, live pitches, news posts and communities; private systems are left out.</summary>
    public async Task<string> SitemapAsync(CancellationToken ct)
    {
        var (pitches, news, communities) = await content.SitemapAsync(clock.UtcNow, ct);
        var root = BaseUrl();
        string[] pages = ["", "pitches", "players", "fantasy", "matches", "news", "shop", "communities", "tournaments", "help", "legal"];
        var urls = pages.Select(p => $"{root}#/{p}").Concat(pitches.Select(s => $"{root}#/pitches/p-{s}")).Concat(news.Select(n => $"{root}#/news/{n}"))
            .Concat(communities.Select(c => $"{root}#/communities/{c}"));
        var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
        foreach (var url in urls)
        {
            xml.Append(CultureInfo.InvariantCulture, $"  <url><loc>{WebUtility.HtmlEncode(url)}</loc></url>\n");
        }
        return xml.Append("</urlset>\n").ToString();
    }

    public string Robots() =>
        $"User-agent: *\nAllow: /\nDisallow: /api/\nDisallow: /*#/admin\nDisallow: /*#/ceo\nDisallow: /*#/dash\nDisallow: /*#/factory\nDisallow: /*#/seller\n" +
        $"Sitemap: {BaseUrl()}api/v1/seo/sitemap.xml\n";

    private string BaseUrl()
    {
        var url = site.Value.PublicUrl;
        return string.IsNullOrWhiteSpace(url) ? "https://localhost/super-app/" : url.TrimEnd('/') + "/";
    }

    internal static T? Read<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<T>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed class CookieConsentRequestValidator : AbstractValidator<CookieConsentRequest>
{
    public CookieConsentRequestValidator()
    {
        RuleFor(x => x.VisitorId).NotEmpty().MaximumLength(40).Matches("^[A-Za-z0-9_-]+$");
    }
}
