using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Content;
using SuperApp.Domain.Entities.Communities;
using SuperApp.Domain.Entities.Content;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Banners, pop-ups, JSON settings, cookie consents and the sitemap's live addresses.</summary>
public sealed class ContentRepository(AppDbContext db) : IContentRepository
{
    public async Task<IReadOnlyList<BannerDto>> BannersAsync(bool liveOnly, DateOnly today, CancellationToken ct) =>
        await db.Set<Banner>().AsNoTracking()
            .Where(b => !liveOnly || (b.IsActive && (b.ShowFrom == null || b.ShowFrom <= today) && (b.ShowUntil == null || b.ShowUntil >= today)))
            .OrderBy(b => b.SortOrder).ThenBy(b => b.Id)
            .Select(b => new BannerDto(b.Id, b.Title, b.Badge, b.CtaText, b.Link, b.ImageKey, b.IsActive, b.SortOrder, b.ShowFrom, b.ShowUntil)).ToListAsync(ct);

    public Task<Banner?> BannerAsync(long id, CancellationToken ct) => db.Set<Banner>().FirstOrDefaultAsync(b => b.Id == id, ct);

    public void Add(Banner banner) => db.Set<Banner>().Add(banner);

    public void Remove(Banner banner) => db.Set<Banner>().Remove(banner);

    public async Task<IReadOnlyList<PopupDto>> PopupsAsync(bool activeOnly, bool forSeller, CancellationToken ct)
    {
        var rows = await db.Set<PopupAd>().AsNoTracking().Where(p => (!activeOnly || p.IsActive) && (!forSeller || !p.HideFromSellers))
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToListAsync(ct);
        return rows.Select(p => new PopupDto(p.Id, p.TitleEn, p.TitleAr, p.TextEn, p.TextAr, p.CtaEn, p.CtaAr, p.Link, p.Badge, p.Color,
            p.Shape == PopupShape.Tee3d ? "tee" : p.Shape.ToString().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(p.Icons) ? [] : p.Icons.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            p.HideFromSellers, p.IsActive, p.SortOrder, p.ShownCount, p.ClickedCount)).ToList();
    }

    public Task<PopupAd?> PopupAsync(long id, CancellationToken ct) => db.Set<PopupAd>().FirstOrDefaultAsync(p => p.Id == id, ct);

    public void Add(PopupAd popup) => db.Set<PopupAd>().Add(popup);

    public void Remove(PopupAd popup) => db.Set<PopupAd>().Remove(popup);

    public async Task<bool> CountAsync(long popupId, bool clicked, CancellationToken ct) =>
        clicked
            ? await db.Set<PopupAd>().Where(p => p.Id == popupId).ExecuteUpdateAsync(u => u.SetProperty(p => p.ClickedCount, p => p.ClickedCount + 1), ct) > 0
            : await db.Set<PopupAd>().Where(p => p.Id == popupId).ExecuteUpdateAsync(u => u.SetProperty(p => p.ShownCount, p => p.ShownCount + 1), ct) > 0;

    public Task<string?> SettingAsync(string key, CancellationToken ct) =>
        db.Set<Setting>().AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync(ct);

    public async Task SaveSettingAsync(string key, string group, string json, Guid? by, CancellationToken ct)
    {
        var row = await db.Set<Setting>().FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is null)
        {
            row = new Setting { Key = key, Group = group };
            db.Set<Setting>().Add(row);
        }
        (row.Value, row.UpdatedAt, row.UpdatedById) = (json, DateTime.UtcNow, by);
        await db.SaveChangesAsync(ct);
    }

    public Task<bool> IsSellerAsync(Guid userId, CancellationToken ct) => db.Set<Seller>().AnyAsync(s => s.UserId == userId, ct);

    public void AddConsent(CookieConsent consent) => db.Set<CookieConsent>().Add(consent);

    public async Task<(IReadOnlyList<string> Pitches, IReadOnlyList<long> News, IReadOnlyList<string> Communities)> SitemapAsync(DateTime now, CancellationToken ct)
    {
        var pitches = await db.Set<Pitch>().Where(p => p.Status == PitchStatus.Live).OrderBy(p => p.Slug).Select(p => p.Slug).ToListAsync(ct);
        var news = await db.Set<NewsPost>().Where(n => !n.IsHidden && n.PublishedAt <= now).OrderByDescending(n => n.PublishedAt).Select(n => n.Id).Take(500).ToListAsync(ct);
        var communities = await db.Set<Community>().Where(c => !c.IsArchived && c.Audience == CommunityAudience.Everyone).Select(c => c.Slug).ToListAsync(ct);
        return (pitches, news, communities);
    }
}

/// <summary>Legal documents and versions, help topics, articles and votes.</summary>
public sealed class LegalHelpRepository(AppDbContext db) : ILegalHelpRepository
{
    public async Task<IReadOnlyList<LegalDocDto>> DocumentsAsync(bool withBody, CancellationToken ct) =>
        await db.Set<LegalDocument>().AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.SortOrder)
            .Select(d => new { d, v = d.Versions.Where(v => v.PublishedAt != null).OrderByDescending(v => v.Version).FirstOrDefault() })
            .Select(x => new LegalDocDto(x.d.Key, x.d.TitleEn, x.d.TitleAr, x.d.RequiresAcceptance, x.v == null ? null : (int?)x.v.Version, x.v == null ? null : x.v.PublishedAt,
                x.v == null || x.v.LawyerReviewedAt == null, x.v == null ? null : x.v.SummaryEn, x.v == null ? null : x.v.SummaryAr,
                withBody && x.v != null ? x.v.BodyEn : null, withBody && x.v != null ? x.v.BodyAr : null))
            .ToListAsync(ct);

    public Task<LegalDocument?> DocumentAsync(string key, CancellationToken ct) => db.Set<LegalDocument>().FirstOrDefaultAsync(d => d.Key == key, ct);

    public async Task<IReadOnlyList<LegalVersionDto>> VersionsAsync(int documentId, CancellationToken ct) =>
        await db.Set<LegalVersion>().AsNoTracking().Where(v => v.DocumentId == documentId).OrderByDescending(v => v.Version)
            .Select(v => new LegalVersionDto(v.Id, v.Version, v.PublishedAt, v.LawyerReviewedAt, v.LawyerNote, v.SummaryEn, v.SummaryAr, v.BodyEn, v.BodyAr,
                db.Set<LegalAcceptance>().Count(a => a.VersionId == v.Id))).ToListAsync(ct);

    public Task<LegalVersion?> VersionAsync(int documentId, int version, CancellationToken ct) =>
        db.Set<LegalVersion>().FirstOrDefaultAsync(v => v.DocumentId == documentId && v.Version == version, ct);

    public async Task<int> NextVersionAsync(int documentId, CancellationToken ct) =>
        (await db.Set<LegalVersion>().Where(v => v.DocumentId == documentId).MaxAsync(v => (int?)v.Version, ct) ?? 0) + 1;

    public void Add(LegalVersion version) => db.Set<LegalVersion>().Add(version);

    public async Task<IReadOnlyList<HelpTopicDto>> TopicsAsync(CancellationToken ct) =>
        await db.Set<HelpTopic>().AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.SortOrder)
            .Select(t => new HelpTopicDto(t.Id, t.Key, t.NameEn, t.NameAr, t.Icon, db.Set<HelpArticle>().Count(a => a.TopicId == t.Id && a.IsPublished))).ToListAsync(ct);

    public Task<bool> TopicExistsAsync(int id, CancellationToken ct) => db.Set<HelpTopic>().AnyAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<HelpArticleDto>> ArticlesAsync(HelpQuery query, bool publishedOnly, CancellationToken ct)
    {
        var q = query.Q?.Trim();
        return await db.Set<HelpArticle>().AsNoTracking()
            .Where(a => (!publishedOnly || a.IsPublished) && (query.Topic == null || a.Topic.Key == query.Topic))
            .Where(a => q == null || a.QuestionEn.Contains(q) || a.QuestionAr.Contains(q) || a.AnswerEn.Contains(q) || a.AnswerAr.Contains(q))
            .OrderBy(a => a.Topic.SortOrder).ThenBy(a => a.SortOrder).ThenBy(a => a.Id)
            .Select(a => new HelpArticleDto(a.Id, a.TopicId, a.Topic.Key, a.QuestionEn, a.QuestionAr, a.AnswerEn, a.AnswerAr, a.SortOrder, a.IsPublished,
                a.HelpfulCount, a.NotHelpfulCount)).ToListAsync(ct);
    }

    public Task<HelpArticle?> ArticleAsync(long id, CancellationToken ct) => db.Set<HelpArticle>().FirstOrDefaultAsync(a => a.Id == id, ct);

    public void Add(HelpArticle article) => db.Set<HelpArticle>().Add(article);

    public Task<HelpVote?> VoteAsync(long articleId, Guid? userId, string? visitorId, CancellationToken ct) =>
        db.Set<HelpVote>().FirstOrDefaultAsync(v => v.ArticleId == articleId && (userId != null ? v.UserId == userId : v.VisitorId == visitorId), ct);

    public void Add(HelpVote vote) => db.Set<HelpVote>().Add(vote);
}
