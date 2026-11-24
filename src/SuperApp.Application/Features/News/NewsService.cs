using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Favorites;
using SuperApp.Domain.Entities.Content;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.News;

/// <summary>A story in the list (the first lines of its text as the excerpt).</summary>
public sealed record NewsItemDto(long Id, string Title, string Excerpt, string Language, string? SourceName, string? SourceUrl, FollowClubDto? Club,
    bool Important, DateTime PublishedAt);

/// <summary>A whole story; <c>Published</c> is shown to the admin.</summary>
public sealed record NewsPostDto(NewsItemDto Item, string Body, bool Published);

/// <summary>Write or edit a story. Important tells everyone; a club tells that club's followers. Notify only on publishing a new story.</summary>
public sealed record NewsPostRequest(string Title, string Body, string Language, string? SourceName, string? SourceUrl, long? ClubId, bool Important,
    bool Published = true);

public interface INewsRepository
{
    Task<IReadOnlyList<NewsItemDto>> ListAsync(long? clubId, bool publishedOnly, int skip, int take, CancellationToken ct);

    Task<NewsPostDto?> GetAsync(long id, bool publishedOnly, CancellationToken ct);

    Task<NewsPost?> FindAsync(long id, CancellationToken ct);

    void Add(NewsPost post);

    Task<bool> ClubExistsAsync(long clubId, CancellationToken ct);

    /// <summary>Who hears about a story: the club's followers, or every active person when it is important.</summary>
    Task<IReadOnlyList<Guid>> AudienceAsync(long? clubId, bool everyone, CancellationToken ct);
}

public interface INewsService
{
    Task<IReadOnlyList<NewsItemDto>> ListAsync(long? clubId, int page, CancellationToken ct);

    Task<NewsPostDto> GetAsync(long id, CancellationToken ct);

    Task<IReadOnlyList<NewsItemDto>> AdminListAsync(int page, CancellationToken ct);

    Task<NewsPostDto> AdminGetAsync(long id, CancellationToken ct);

    Task<NewsPostDto> SaveAsync(long? id, NewsPostRequest request, CancellationToken ct);

    Task DeleteAsync(long id, CancellationToken ct);
}

/// <summary>News written by the team: the public list and story pages, and the admin's editor (permission content.news).</summary>
public sealed class NewsService(
    INewsRepository news,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock,
    INotifier notifier,
    IAuditLogger audit,
    IValidator<NewsPostRequest> rules) : INewsService
{
    private const int PageSize = 20;

    public Task<IReadOnlyList<NewsItemDto>> ListAsync(long? clubId, int page, CancellationToken ct) =>
        news.ListAsync(clubId, true, Math.Max(0, page - 1) * PageSize, PageSize, ct);

    public async Task<NewsPostDto> GetAsync(long id, CancellationToken ct) =>
        await news.GetAsync(id, true, ct) ?? throw new NotFoundException("This story isn’t available.");

    public Task<IReadOnlyList<NewsItemDto>> AdminListAsync(int page, CancellationToken ct) =>
        news.ListAsync(null, false, Math.Max(0, page - 1) * PageSize, PageSize, ct);

    public async Task<NewsPostDto> AdminGetAsync(long id, CancellationToken ct) =>
        await news.GetAsync(id, false, ct) ?? throw new NotFoundException("Story not found.");

    public async Task<NewsPostDto> SaveAsync(long? id, NewsPostRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        if (request.ClubId is { } club && !await news.ClubExistsAsync(club, ct))
        {
            throw new BusinessRuleException("Pick the club from the list.", "unknown_club");
        }
        var post = id is { } existing ? await news.FindAsync(existing, ct) ?? throw new NotFoundException("Story not found.") : NewPost();
        var announce = request.Published && (id is null || !post.IsPublished);
        Fill(post, request);
        if (id is null)
        {
            news.Add(post);
        }
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, id is null ? "news.create" : "news.edit", post.Title, "NewsPost", post.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), ct: ct);
        if (announce)
        {
            await AnnounceAsync(post, ct);
        }
        return await AdminGetAsync(post.Id, ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        var post = await news.FindAsync(id, ct) ?? throw new NotFoundException("Story not found.");
        post.IsPublished = false;
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, "news.hide", post.Title, "NewsPost", id.ToString(System.Globalization.CultureInfo.InvariantCulture), ct: ct);
    }

    private NewsPost NewPost() => new() { AuthorId = currentUser.UserId ?? throw new UnauthorizedException(), PublishedAt = clock.UtcNow };

    private void Fill(NewsPost post, NewsPostRequest r)
    {
        (post.Title, post.Body, post.Language) = (r.Title.Trim(), r.Body.Trim(), r.Language);
        (post.SourceName, post.SourceUrl, post.ClubId, post.Important) = (Blank(r.SourceName), Blank(r.SourceUrl), r.ClubId, r.Important);
        if (r.Published && !post.IsPublished)
        {
            post.PublishedAt = clock.UtcNow;   // brought back: it goes to the top again
        }
        post.IsPublished = r.Published;
    }

    /// <summary>Tells the club's followers (or everyone when important) with the title and the first lines.</summary>
    private async Task AnnounceAsync(NewsPost post, CancellationToken ct)
    {
        if (!post.Important && post.ClubId is null)
        {
            return;
        }
        var people = await news.AudienceAsync(post.ClubId, post.Important, ct);
        var message = new NotificationMessage(NotificationCategory.News, post.Title, Excerpt(post.Body, 140), $"#/news/{post.Id}");
        await notifier.NotifyManyAsync(people.Select(p => (p, message)).ToList(), ct);
    }

    public static string Excerpt(string body, int length)
    {
        var flat = string.Join(' ', body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return flat.Length <= length ? flat : $"{flat[..length].TrimEnd()}…";
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

public sealed class NewsPostRequestValidator : AbstractValidator<NewsPostRequest>
{
    public NewsPostRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(6000);
        RuleFor(x => x.Language).Must(l => l is "ar" or "en").WithMessage("Arabic or English.");
        RuleFor(x => x.SourceName).MaximumLength(80);
        RuleFor(x => x.SourceUrl).MaximumLength(500)
            .Must(u => string.IsNullOrWhiteSpace(u) || (Uri.TryCreate(u.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("The source link must start with https://");
        RuleFor(x => x.ClubId).GreaterThan(0).When(x => x.ClubId is not null);
    }
}
