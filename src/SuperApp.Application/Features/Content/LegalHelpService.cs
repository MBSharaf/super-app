using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Content;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Content;

/// <summary>
/// Legal documents (FR-CNT-03: versioned, every acceptance stored with its version, "draft" until the CEO marks the
/// lawyer's review) and the help center (FR-SUP-06: Arabic and English, search, "was this helpful" votes).
/// </summary>
public interface ILegalHelpService
{
    Task<IReadOnlyList<LegalDocDto>> DocumentsAsync(CancellationToken ct);

    Task<LegalDocDto> DocumentAsync(string key, CancellationToken ct);

    Task AcceptAsync(string key, AcceptLegalRequest body, CancellationToken ct);

    Task<IReadOnlyList<LegalVersionDto>> VersionsAsync(string key, CancellationToken ct);

    Task<LegalVersionDto> NewVersionAsync(string key, LegalVersionRequest body, CancellationToken ct);

    Task<LegalVersionDto> PublishAsync(string key, int version, CancellationToken ct);

    Task<LegalVersionDto> LawyerReviewedAsync(string key, int version, LawyerReviewRequest body, CancellationToken ct);

    Task<IReadOnlyList<HelpTopicDto>> TopicsAsync(CancellationToken ct);

    Task<IReadOnlyList<HelpArticleDto>> ArticlesAsync(HelpQuery query, bool admin, CancellationToken ct);

    Task<HelpArticleDto> VoteAsync(long id, HelpVoteRequest body, CancellationToken ct);

    Task<HelpArticleDto> SaveArticleAsync(long? id, HelpArticleRequest body, CancellationToken ct);
}

public sealed class LegalHelpService(
    ILegalHelpRepository repository,
    ILegalRepository acceptances,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IRequestInfo request,
    IClock clock,
    IValidator<AcceptLegalRequest> acceptRules,
    IValidator<LegalVersionRequest> versionRules,
    IValidator<HelpVoteRequest> voteRules,
    IValidator<HelpArticleRequest> articleRules) : ILegalHelpService
{
    public Task<IReadOnlyList<LegalDocDto>> DocumentsAsync(CancellationToken ct) => repository.DocumentsAsync(false, ct);

    public async Task<LegalDocDto> DocumentAsync(string key, CancellationToken ct) =>
        (await repository.DocumentsAsync(true, ct)).FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase)) ?? throw new NotFoundException("Document not found.");

    public async Task AcceptAsync(string key, AcceptLegalRequest body, CancellationToken ct)
    {
        await acceptRules.ValidateAndThrowAsync(body, ct);
        var user = currentUser.UserId ?? throw new UnauthorizedException();
        var versionId = await acceptances.CurrentVersionIdAsync(key.ToLowerInvariant(), ct) ?? throw new NotFoundException("Document not found.");
        acceptances.AddAcceptance(versionId, user, body.Context.ToLowerInvariant(), request.IpAddress);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LegalVersionDto>> VersionsAsync(string key, CancellationToken ct) =>
        await repository.VersionsAsync((await DocAsync(key, ct)).Id, ct);

    public async Task<LegalVersionDto> NewVersionAsync(string key, LegalVersionRequest body, CancellationToken ct)
    {
        await versionRules.ValidateAndThrowAsync(body, ct);
        var doc = await DocAsync(key, ct);
        var version = new LegalVersion
        {
            DocumentId = doc.Id, Version = (short)await repository.NextVersionAsync(doc.Id, ct),
            SummaryEn = body.SummaryEn.Trim(), SummaryAr = body.SummaryAr.Trim(), BodyEn = body.BodyEn, BodyAr = body.BodyAr,
        };
        repository.Add(version);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, "legal.drafted", $"{doc.Key} version {version.Version} drafted", "LegalVersion", version.Id.ToString(), ct: ct);
        return await VersionDtoAsync(doc.Id, version.Version, ct);
    }

    /// <summary>The newest published version is the one people accept from now on.</summary>
    public async Task<LegalVersionDto> PublishAsync(string key, int version, CancellationToken ct)
    {
        var doc = await DocAsync(key, ct);
        var row = await repository.VersionAsync(doc.Id, version, ct) ?? throw new NotFoundException("Version not found.");
        if (row.PublishedAt is not null)
        {
            throw new ConflictException("This version is already published.", "already_published");
        }
        row.PublishedAt = clock.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, "legal.published", $"{doc.Key} version {version} published", "LegalVersion", row.Id.ToString(), ct: ct);
        return await VersionDtoAsync(doc.Id, version, ct);
    }

    /// <summary>The CEO marks a version as reviewed by the lawyer; the "Draft" note disappears.</summary>
    public async Task<LegalVersionDto> LawyerReviewedAsync(string key, int version, LawyerReviewRequest body, CancellationToken ct)
    {
        var doc = await DocAsync(key, ct);
        var row = await repository.VersionAsync(doc.Id, version, ct) ?? throw new NotFoundException("Version not found.");
        (row.LawyerReviewedAt, row.LawyerNote, row.ReviewedById) = (clock.UtcNow, body.Note?.Trim() is { Length: > 0 } n ? n[..Math.Min(400, n.Length)] : null, currentUser.UserId);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Ceo, "legal.reviewed", $"{doc.Key} version {version} reviewed by the lawyer", "LegalVersion", row.Id.ToString(), ct: ct);
        return await VersionDtoAsync(doc.Id, version, ct);
    }

    public Task<IReadOnlyList<HelpTopicDto>> TopicsAsync(CancellationToken ct) => repository.TopicsAsync(ct);

    public Task<IReadOnlyList<HelpArticleDto>> ArticlesAsync(HelpQuery query, bool admin, CancellationToken ct) => repository.ArticlesAsync(query, !admin, ct);

    /// <summary>One vote per person (or visitor) and article; voting again changes the vote.</summary>
    public async Task<HelpArticleDto> VoteAsync(long id, HelpVoteRequest body, CancellationToken ct)
    {
        await voteRules.ValidateAndThrowAsync(body, ct);
        var user = currentUser.UserId;
        if (user is null && string.IsNullOrWhiteSpace(body.VisitorId))
        {
            throw new BadRequestException("Send your visitor id or sign in.", "visitor");
        }
        var article = await repository.ArticleAsync(id, ct);
        if (article is not { IsPublished: true })
        {
            throw new NotFoundException("Article not found.");
        }
        var vote = await repository.VoteAsync(id, user, user is null ? body.VisitorId : null, ct);
        if (vote is null)
        {
            repository.Add(new HelpVote { ArticleId = id, UserId = user, VisitorId = user is null ? body.VisitorId : null, Helpful = body.Helpful });
            Count(article, body.Helpful, +1);
        }
        else if (vote.Helpful != body.Helpful)
        {
            Count(article, vote.Helpful, -1);
            Count(article, body.Helpful, +1);
            vote.Helpful = body.Helpful;
        }
        await unitOfWork.SaveChangesAsync(ct);
        return (await repository.ArticlesAsync(new HelpQuery(), false, ct)).Single(a => a.Id == id);
    }

    private static void Count(HelpArticle article, bool helpful, int by)
    {
        if (helpful)
        {
            article.HelpfulCount = Math.Max(0, article.HelpfulCount + by);
        }
        else
        {
            article.NotHelpfulCount = Math.Max(0, article.NotHelpfulCount + by);
        }
    }

    public async Task<HelpArticleDto> SaveArticleAsync(long? id, HelpArticleRequest body, CancellationToken ct)
    {
        await articleRules.ValidateAndThrowAsync(body, ct);
        if (!await repository.TopicExistsAsync(body.TopicId, ct))
        {
            throw new BadRequestException("Topic not found.", "topic");
        }
        var article = id is null ? new HelpArticle() : await repository.ArticleAsync(id.Value, ct) ?? throw new NotFoundException("Article not found.");
        if (id is null)
        {
            repository.Add(article);
        }
        (article.TopicId, article.QuestionEn, article.QuestionAr, article.AnswerEn, article.AnswerAr) = (body.TopicId, body.QuestionEn.Trim(), body.QuestionAr.Trim(), body.AnswerEn, body.AnswerAr);
        (article.SortOrder, article.IsPublished) = (body.SortOrder, body.IsPublished);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, id is null ? "help.created" : "help.updated", article.QuestionEn, "HelpArticle", article.Id.ToString(), ct: ct);
        return (await repository.ArticlesAsync(new HelpQuery(), false, ct)).Single(a => a.Id == article.Id);
    }

    private async Task<LegalDocument> DocAsync(string key, CancellationToken ct) =>
        await repository.DocumentAsync(key.ToLowerInvariant(), ct) ?? throw new NotFoundException("Document not found.");

    private async Task<LegalVersionDto> VersionDtoAsync(int documentId, int version, CancellationToken ct) =>
        (await repository.VersionsAsync(documentId, ct)).Single(v => v.Version == version);
}

public sealed class AcceptLegalRequestValidator : AbstractValidator<AcceptLegalRequest>
{
    private static readonly string[] Contexts = ["signup", "booking", "game", "tournament", "seller", "partner", "cookies", "site"];

    public AcceptLegalRequestValidator()
    {
        RuleFor(x => x.Context).NotEmpty().Must(c => Contexts.Contains(c.ToLowerInvariant())).WithMessage("Unknown context.");
    }
}

public sealed class LegalVersionRequestValidator : AbstractValidator<LegalVersionRequest>
{
    public LegalVersionRequestValidator()
    {
        RuleFor(x => x.SummaryEn).NotEmpty().MaximumLength(400);
        RuleFor(x => x.SummaryAr).NotEmpty().MaximumLength(400);
        RuleFor(x => x.BodyEn).NotEmpty().MaximumLength(200_000);
        RuleFor(x => x.BodyAr).NotEmpty().MaximumLength(200_000);
    }
}

public sealed class HelpVoteRequestValidator : AbstractValidator<HelpVoteRequest>
{
    public HelpVoteRequestValidator()
    {
        RuleFor(x => x.VisitorId).MaximumLength(40).Matches("^[A-Za-z0-9_-]+$").When(x => !string.IsNullOrEmpty(x.VisitorId));
    }
}

public sealed class HelpArticleRequestValidator : AbstractValidator<HelpArticleRequest>
{
    public HelpArticleRequestValidator()
    {
        RuleFor(x => x.TopicId).GreaterThan(0);
        RuleFor(x => x.QuestionEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.QuestionAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AnswerEn).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.AnswerAr).NotEmpty().MaximumLength(4000);
    }
}
