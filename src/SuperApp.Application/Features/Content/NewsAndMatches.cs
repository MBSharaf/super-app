using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Abstractions.Storage;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Entities.Content;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Content;

/* ---------- Matches (real football, from the fantasy cache) ---------- */

public sealed record MatchClubDto(long Id, string Name, string ShortName, string Kit, string Kit2);

/// <summary>
/// A real match of a followed competition. Status Scheduled / Live / Finished / Postponed / Cancelled. <c>Gameweek</c> is
/// the round; cups use 32 playoff, 16 last 16, 125 quarter-final, 150 semi-final, 200 final.
/// </summary>
public sealed record MatchDto(long Id, string Competition, int Gameweek, MatchClubDto Home, MatchClubDto Away, DateTime KickoffAt, string Status,
    int? HomeGoals, int? AwayGoals, int? Minute, string? Venue = null);

/// <summary>A competition shown on the matches page (with its colour), and whether it has fantasy.</summary>
public sealed record MatchCompetitionDto(string Key, string Name, string NameAr, string ShortName, string Region, string Color, bool Fantasy);

/// <summary>A player's line in a match (from the provider's statistics).</summary>
public sealed record MatchPlayerDto(long PlayerId, long ClubId, string Name, string Position, int Minutes, int Goals, int Assists, int YellowCards, bool RedCard, int Points);

public sealed record MatchDetailDto(MatchDto Match, IReadOnlyList<MatchPlayerDto> Players);

public interface IMatchesReader
{
    Task<IReadOnlyList<MatchDto>> BetweenAsync(DateTime fromUtc, DateTime toUtc, string? competition, CancellationToken ct);

    Task<MatchDetailDto?> DetailAsync(long id, CancellationToken ct);

    Task<IReadOnlyList<MatchCompetitionDto>> CompetitionsAsync(CancellationToken ct);
}

/* ---------- News (Super App posts) ---------- */

public sealed record NewsDto(long Id, string Category, string Title, string Excerpt, string? Body, string? ImageUrl, bool IsBreaking, bool IsHidden, DateTime PublishedAt);

public sealed record NewsQuery(string? Category = null, int Page = 1, int PageSize = 20);

/// <summary>Admin news post. <c>PublishedAt</c> in the future schedules it; empty = now.</summary>
public sealed record NewsRequest(string Category, string Title, string Excerpt, string Body, bool IsBreaking, bool IsHidden, DateTime? PublishedAt);

public interface INewsRepository
{
    Task<PagedResult<NewsDto>> ListAsync(NewsQuery query, bool publicOnly, DateTime now, CancellationToken ct);

    Task<NewsDto?> GetAsync(long id, bool publicOnly, DateTime now, CancellationToken ct);

    Task<NewsPost?> FindAsync(long id, CancellationToken ct);

    void Add(NewsPost post);
}

/// <summary>Matches (API §6.2) and Super App news (FR-NEWS-01): public reads, and the admin's news posts (content.news).</summary>
public interface INewsService
{
    /// <summary>Matches of <paramref name="days"/> Cairo days (1–14) from <paramref name="date"/> (default today).</summary>
    Task<IReadOnlyList<MatchDto>> MatchesAsync(DateOnly? date, int days, string? competition, CancellationToken ct);

    Task<IReadOnlyList<MatchCompetitionDto>> MatchCompetitionsAsync(CancellationToken ct);

    Task<MatchDetailDto> MatchAsync(long id, CancellationToken ct);

    Task<PagedResult<NewsDto>> NewsAsync(NewsQuery query, bool admin, CancellationToken ct);

    Task<NewsDto> PostAsync(long id, bool admin, CancellationToken ct);

    Task<NewsDto> SaveAsync(long? id, NewsRequest request, CancellationToken ct);

    Task<NewsDto> ImageAsync(long id, UploadedFile file, CancellationToken ct);

    Task DeleteAsync(long id, CancellationToken ct);
}

public sealed class NewsService(
    IMatchesReader matches,
    INewsRepository news,
    IFileStorage files,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<NewsRequest> rules) : INewsService
{
    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    /// <summary>The Cairo calendar days (default today, 1–14 days) as a UTC range.</summary>
    public Task<IReadOnlyList<MatchDto>> MatchesAsync(DateOnly? date, int days, string? competition, CancellationToken ct)
    {
        if (days is < 1 or > 14)
        {
            throw new BadRequestException("Ask for 1 to 14 days.", "days");
        }
        var day = date ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, Cairo));
        var from = TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), Cairo);
        var to = TimeZoneInfo.ConvertTimeToUtc(day.AddDays(days).ToDateTime(TimeOnly.MinValue), Cairo);
        return matches.BetweenAsync(from, to, string.IsNullOrWhiteSpace(competition) ? null : competition.ToUpperInvariant(), ct);
    }

    public Task<IReadOnlyList<MatchCompetitionDto>> MatchCompetitionsAsync(CancellationToken ct) => matches.CompetitionsAsync(ct);

    public async Task<MatchDetailDto> MatchAsync(long id, CancellationToken ct) =>
        await matches.DetailAsync(id, ct) ?? throw new NotFoundException("Match not found.");

    public Task<PagedResult<NewsDto>> NewsAsync(NewsQuery query, bool admin, CancellationToken ct) => news.ListAsync(query, !admin, clock.UtcNow, ct);

    public async Task<NewsDto> PostAsync(long id, bool admin, CancellationToken ct) =>
        await news.GetAsync(id, !admin, clock.UtcNow, ct) ?? throw new NotFoundException("This story is no longer available.");

    public async Task<NewsDto> SaveAsync(long? id, NewsRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var post = id is null ? new NewsPost { AuthorId = currentUser.UserId ?? throw new UnauthorizedException() }
            : await news.FindAsync(id.Value, ct) ?? throw new NotFoundException("News post not found.");
        if (id is null)
        {
            news.Add(post);
        }
        (post.Category, post.Title, post.Excerpt, post.Body) = (request.Category.Trim().ToLowerInvariant(), request.Title.Trim(), request.Excerpt.Trim(), request.Body.Trim());
        (post.IsBreaking, post.IsHidden, post.PublishedAt) = (request.IsBreaking, request.IsHidden, request.PublishedAt?.ToUniversalTime() ?? (id is null ? clock.UtcNow : post.PublishedAt));
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, id is null ? "news.created" : "news.updated", post.Title, "NewsPost", post.Id.ToString(), ct: ct);
        return (await news.GetAsync(post.Id, false, clock.UtcNow, ct))!;
    }

    public async Task<NewsDto> ImageAsync(long id, UploadedFile file, CancellationToken ct)
    {
        var post = await news.FindAsync(id, ct) ?? throw new NotFoundException("News post not found.");
        post.ImageFileId = await files.SaveAsync(file, UploadKind.Photo, currentUser.UserId ?? throw new UnauthorizedException(), "news", ct);
        await unitOfWork.SaveChangesAsync(ct);
        return (await news.GetAsync(id, false, clock.UtcNow, ct))!;
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        var post = await news.FindAsync(id, ct) ?? throw new NotFoundException("News post not found.");
        post.IsDeleted = true;
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, "news.deleted", post.Title, "NewsPost", id.ToString(), ct: ct);
    }
}

public sealed class NewsRequestValidator : AbstractValidator<NewsRequest>
{
    public NewsRequestValidator()
    {
        RuleFor(x => x.Category).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9-]+$").WithMessage("A category key such as league or transfers.");
        RuleFor(x => x.Title).NotEmpty().Length(10, 110);
        RuleFor(x => x.Excerpt).NotEmpty().Length(20, 260);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(20000);
    }
}
