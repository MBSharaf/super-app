using SuperApp.Domain.Entities.Content;

namespace SuperApp.Application.Features.Content;

/* ---------- Home: banners and the announcement bar ---------- */

public sealed record BannerDto(long Id, string Title, string? Badge, string? CtaText, string Link, string ImageKey, bool IsActive, int SortOrder,
    DateOnly? ShowFrom, DateOnly? ShowUntil);

/// <summary>The announcement bar (setting <c>content.announcement</c>). Tone info / success / warning / danger.</summary>
public sealed record AnnouncementDto(bool On, string Text, string? Link, string? Cta, string Tone, DateOnly? Until);

/// <summary>GET /content/home — up to 3 live banners in order, and the announcement when it is on and not expired.</summary>
public sealed record HomeDto(IReadOnlyList<BannerDto> Banners, AnnouncementDto? Announcement);

public sealed record BannerRequest(string Title, string? Badge, string? CtaText, string Link, string ImageKey, bool IsActive, int SortOrder, DateOnly? ShowFrom, DateOnly? ShowUntil);

/* ---------- Pop-ups ---------- */

/// <summary>Shape Cube / Coin / Tee (the 3D T-shirt); <c>Icons</c> for the cube faces.</summary>
public sealed record PopupDto(long Id, string TitleEn, string TitleAr, string TextEn, string TextAr, string CtaEn, string CtaAr, string Link, string? Badge,
    string Color, string Shape, IReadOnlyList<string> Icons, bool HideFromSellers, bool IsActive, int SortOrder, int ShownCount, int ClickedCount);

/// <summary>Timing in seconds (setting <c>popups.timing</c>): first after, every, stays, at most per visit.</summary>
public sealed record PopupTimingDto(bool On, int First, int Interval, int Stay, int Max);

public sealed record PopupsDto(PopupTimingDto Timing, IReadOnlyList<PopupDto> Items);

/// <summary>POST /content/popups/{id}/event — kind <c>shown</c> or <c>clicked</c>.</summary>
public sealed record PopupEventRequest(string Kind);

public sealed record PopupRequest(string TitleEn, string TitleAr, string TextEn, string TextAr, string CtaEn, string CtaAr, string Link, string? Badge,
    string Color, string Shape, IReadOnlyList<string>? Icons, bool HideFromSellers, bool IsActive, int SortOrder);

/* ---------- Legal, cookies ---------- */

/// <summary>The current version of a legal document. <c>IsDraft</c> until the CEO marks it reviewed by the lawyer.</summary>
public sealed record LegalDocDto(string Key, string TitleEn, string TitleAr, bool RequiresAcceptance, int? Version, DateTime? PublishedAt, bool IsDraft,
    string? SummaryEn, string? SummaryAr, string? BodyEn, string? BodyAr);

/// <summary>POST /legal/{key}/accept — context signup / booking / game / tournament / seller / partner.</summary>
public sealed record AcceptLegalRequest(string Context, long? ContextId);

public sealed record CookieConsentRequest(string VisitorId, bool Analytics);

public sealed record LegalVersionDto(long Id, int Version, DateTime? PublishedAt, DateTime? LawyerReviewedAt, string? LawyerNote, string SummaryEn, string SummaryAr,
    string BodyEn, string BodyAr, int Acceptances);

public sealed record LegalVersionRequest(string SummaryEn, string SummaryAr, string BodyEn, string BodyAr);

public sealed record LawyerReviewRequest(string? Note);

/* ---------- Help center ---------- */

public sealed record HelpTopicDto(int Id, string Key, string NameEn, string NameAr, string Icon, int Articles);

public sealed record HelpArticleDto(long Id, int TopicId, string Topic, string QuestionEn, string QuestionAr, string AnswerEn, string AnswerAr, int SortOrder,
    bool IsPublished, int HelpfulCount, int NotHelpfulCount);

public sealed record HelpQuery(string? Q = null, string? Topic = null);

/// <summary>"Was this helpful?" — signed-in people vote once per article; visitors by their visitor id.</summary>
public sealed record HelpVoteRequest(bool Helpful, string? VisitorId);

public sealed record HelpArticleRequest(int TopicId, string QuestionEn, string QuestionAr, string AnswerEn, string AnswerAr, int SortOrder, bool IsPublished);

/* ---------- Persistence ---------- */

/// <summary>Banners, pop-ups, JSON settings, cookie consents and the live addresses for the sitemap.</summary>
public interface IContentRepository
{
    Task<IReadOnlyList<BannerDto>> BannersAsync(bool liveOnly, DateOnly today, CancellationToken ct);

    Task<Banner?> BannerAsync(long id, CancellationToken ct);

    void Add(Banner banner);

    void Remove(Banner banner);

    Task<IReadOnlyList<PopupDto>> PopupsAsync(bool activeOnly, bool forSeller, CancellationToken ct);

    Task<PopupAd?> PopupAsync(long id, CancellationToken ct);

    void Add(PopupAd popup);

    void Remove(PopupAd popup);

    /// <summary>Adds one view or click straight in the database (no lost counts under load). False when the pop-up does not exist.</summary>
    Task<bool> CountAsync(long popupId, bool clicked, CancellationToken ct);

    Task<string?> SettingAsync(string key, CancellationToken ct);

    Task SaveSettingAsync(string key, string group, string json, Guid? by, CancellationToken ct);

    Task<bool> IsSellerAsync(Guid userId, CancellationToken ct);

    void AddConsent(CookieConsent consent);

    /// <summary>Live pitch slugs, news ids and community slugs (sitemap).</summary>
    Task<(IReadOnlyList<string> Pitches, IReadOnlyList<long> News, IReadOnlyList<string> Communities)> SitemapAsync(DateTime now, CancellationToken ct);
}

/// <summary>Legal documents and versions, and the help center.</summary>
public interface ILegalHelpRepository
{
    Task<IReadOnlyList<LegalDocDto>> DocumentsAsync(bool withBody, CancellationToken ct);

    Task<LegalDocument?> DocumentAsync(string key, CancellationToken ct);

    Task<IReadOnlyList<LegalVersionDto>> VersionsAsync(int documentId, CancellationToken ct);

    Task<LegalVersion?> VersionAsync(int documentId, int version, CancellationToken ct);

    Task<int> NextVersionAsync(int documentId, CancellationToken ct);

    void Add(LegalVersion version);

    Task<IReadOnlyList<HelpTopicDto>> TopicsAsync(CancellationToken ct);

    Task<bool> TopicExistsAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<HelpArticleDto>> ArticlesAsync(HelpQuery query, bool publishedOnly, CancellationToken ct);

    Task<HelpArticle?> ArticleAsync(long id, CancellationToken ct);

    void Add(HelpArticle article);

    Task<HelpVote?> VoteAsync(long articleId, Guid? userId, string? visitorId, CancellationToken ct);

    void Add(HelpVote vote);
}
