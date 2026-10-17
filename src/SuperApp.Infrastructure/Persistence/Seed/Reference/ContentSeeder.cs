using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Domain.Entities.Content;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Seed.Reference;

/// <summary>
/// Seed 9 (content): legal documents as version 1 (Arabic and English drafts, published so the site works, marked
/// as awaiting the lawyer's review), help topics and articles, home banners and the pop-up ads.
/// </summary>
public sealed class ContentSeeder(AppDbContext db, IClock clock) : ISeeder
{
    internal sealed record LegalRow(string Key, string TitleEn, string TitleAr, bool Acceptance, string SummaryEn, string SummaryAr, string BodyEn, string BodyAr);
    internal sealed record TopicRow(string Key, string Icon, string En, string Ar);
    internal sealed record ArticleRow(string Topic, string QEn, string QAr, string AEn, string AAr);
    internal sealed record BannerRow(string Title, string? Badge, string? Cta, string Link, string Image, bool Active, int Order);
    internal sealed record PopupRow(string TitleEn, string TitleAr, string TextEn, string TextAr, string CtaEn, string CtaAr, string Link,
        string? Badge, string Color, PopupShape Shape, string? Icons, bool HideFromSellers, bool Active, int Order);
    internal sealed record CommunityRow(string Slug, string Name, string About, string Audience, List<string> Rules, int SlowMode, string? Color, string? Icon, int Order);
    internal sealed record WordRow(string Word, string Language);
    internal sealed record ContentFile(List<LegalRow> Legal, List<TopicRow> HelpTopics, List<ArticleRow> HelpArticles, List<BannerRow> Banners,
        List<PopupRow> Popups, List<CommunityRow> Communities, List<WordRow> BannedWords);

    internal static ContentFile Load() => SeedFile.Read<ContentFile>("Reference/content.json");

    public int Order => 9;

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        var file = Load();
        var added = await SeedLegalAsync(file.Legal, ct);
        added += await SeedHelpAsync(file, ct);
        added += await SeedBannersAndPopupsAsync(file, ct);
        return added;
    }

    private async Task<int> SeedLegalAsync(List<LegalRow> rows, CancellationToken ct)
    {
        var documents = rows.Select((r, i) => new LegalDocument { Key = r.Key, TitleEn = r.TitleEn, TitleAr = r.TitleAr, RequiresAcceptance = r.Acceptance, SortOrder = i + 1 });
        var added = await db.Set<LegalDocument>().AddMissingAsync(documents, d => d.Key, ct);
        await db.SaveChangesAsync(ct);

        var ids = await db.Set<LegalDocument>().ToDictionaryAsync(d => d.Key, d => d.Id, ct);
        var versions = rows.Select(r => new LegalVersion
        {
            DocumentId = ids[r.Key], Version = 1, BodyEn = r.BodyEn, BodyAr = r.BodyAr, SummaryEn = r.SummaryEn, SummaryAr = r.SummaryAr,
            PublishedAt = clock.UtcNow, LawyerNote = "Draft written by the team — awaiting the lawyer's review.",
        });
        added += await db.Set<LegalVersion>().AddMissingAsync(versions, v => (v.DocumentId, v.Version), ct);
        await db.SaveChangesAsync(ct);
        return added;
    }

    private async Task<int> SeedHelpAsync(ContentFile file, CancellationToken ct)
    {
        var topics = file.HelpTopics.Select((t, i) => new HelpTopic { Key = t.Key, Icon = t.Icon, NameEn = t.En, NameAr = t.Ar, SortOrder = i + 1 });
        var added = await db.Set<HelpTopic>().AddMissingAsync(topics, t => t.Key, ct);
        await db.SaveChangesAsync(ct);

        var ids = await db.Set<HelpTopic>().ToDictionaryAsync(t => t.Key, t => t.Id, ct);
        var articles = file.HelpArticles.Select((a, i) => new HelpArticle
        {
            TopicId = ids[a.Topic], QuestionEn = a.QEn, QuestionAr = a.QAr, AnswerEn = a.AEn, AnswerAr = a.AAr, SortOrder = i + 1,
        });
        added += await db.Set<HelpArticle>().AddMissingAsync(articles, a => a.QuestionEn, ct);
        await db.SaveChangesAsync(ct);
        return added;
    }

    private async Task<int> SeedBannersAndPopupsAsync(ContentFile file, CancellationToken ct)
    {
        var banners = file.Banners.Select(b => new Banner
        {
            Title = b.Title, Badge = b.Badge, CtaText = b.Cta, Link = b.Link, ImageKey = b.Image, IsActive = b.Active, SortOrder = b.Order,
        });
        var added = await db.Set<Banner>().AddMissingAsync(banners, b => b.Title, ct);
        var popups = file.Popups.Select(p => new PopupAd
        {
            TitleEn = p.TitleEn, TitleAr = p.TitleAr, TextEn = p.TextEn, TextAr = p.TextAr, CtaEn = p.CtaEn, CtaAr = p.CtaAr, Link = p.Link,
            Badge = p.Badge, Color = p.Color, Shape = p.Shape, Icons = p.Icons, HideFromSellers = p.HideFromSellers, IsActive = p.Active, SortOrder = p.Order,
        });
        added += await db.Set<PopupAd>().AddMissingAsync(popups, p => p.TitleEn, ct);
        await db.SaveChangesAsync(ct);
        return added;
    }
}
