using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Entities.Communities;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Communities;

/// <summary>
/// Community admin (API §7.2): reports (remove, keep, remove and mute the author — community.moderate), communities
/// (community.manage), posts and replies as "Super App Admin" (community.postAsAdmin), banned words and mutes.
/// </summary>
public interface ICommunityAdminService
{
    Task<PagedResult<ReportDto>> ReportsAsync(string? status, int page, int pageSize, CancellationToken ct);

    Task DecideAsync(long reportId, ContentReportStatus decision, MuteRequest? mute, CancellationToken ct);

    Task<IReadOnlyList<CommunityDto>> CommunitiesAsync(CancellationToken ct);

    Task<CommunityDto> SaveCommunityAsync(long? id, CommunityRequest request, CancellationToken ct);

    Task ArchiveAsync(long id, bool archived, CancellationToken ct);

    Task<PostDto> PostAsync(string slug, AdminPostRequest request, CancellationToken ct);

    Task<CommentDto> ReplyAsync(long postId, CommentRequest request, CancellationToken ct);

    Task<PostDto> FlagsAsync(long postId, PostFlagsRequest request, CancellationToken ct);

    Task<IReadOnlyList<BannedWordDto>> BannedWordsAsync(CancellationToken ct);

    Task<BannedWordDto> AddBannedWordAsync(BannedWordRequest request, CancellationToken ct);

    Task RemoveBannedWordAsync(long id, CancellationToken ct);

    Task<IReadOnlyList<MuteDto>> MutesAsync(CancellationToken ct);

    Task UnmuteAsync(long communityId, Guid userId, CancellationToken ct);
}

public sealed partial class CommunityAdminService(
    ICommunityRepository repository,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    ICommunityLive live,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<MuteRequest> muteRules,
    IValidator<CommunityRequest> communityRules,
    IValidator<CreatePostRequest> postRules,
    IValidator<CommentRequest> commentRules,
    IValidator<BannedWordRequest> wordRules) : ICommunityAdminService
{
    private Guid UserId => currentUser.UserId ?? throw new UnauthorizedException();

    public Task<PagedResult<ReportDto>> ReportsAsync(string? status, int page, int pageSize, CancellationToken ct) => repository.ReportsAsync(status, page, pageSize, ct);

    public async Task DecideAsync(long reportId, ContentReportStatus decision, MuteRequest? mute, CancellationToken ct)
    {
        if (mute is not null)
        {
            await muteRules.ValidateAndThrowAsync(mute, ct);
        }
        var report = await repository.ReportAsync(reportId, ct) ?? throw new NotFoundException("Report not found.");
        if (report.Status != ContentReportStatus.Open)
        {
            throw new ConflictException("This report was already handled.", "already_handled");
        }
        var (post, comment) = (report.PostId is { } p ? await repository.PostAsync(p, ct) : null, report.CommentId is { } c ? await repository.CommentAsync(c, ct) : null);
        var authorId = comment?.AuthorId ?? post!.AuthorId;
        var postId = comment?.PostId ?? post!.Id;
        if (decision != ContentReportStatus.Kept)
        {
            await HideAsync(post, comment, ct);
        }
        if (decision == ContentReportStatus.RemovedAndMuted)
        {
            await MuteAsync(postId, authorId, mute!, ct);
        }
        (report.Status, report.HandledById) = (decision, UserId);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, "community.report." + decision.ToString().ToLowerInvariant(), $"Report {reportId}: {decision}", "ContentReport", reportId.ToString(), ct: ct);
    }

    private async Task HideAsync(Post? post, Comment? comment, CancellationToken ct)
    {
        if (comment is not null)
        {
            comment.IsHidden = true;
        }
        else if (post is not null)
        {
            post.IsHidden = true;
            var community = await repository.CommunityAsync(post.CommunityId, ct);
            await live.PublishAsync(community!.Slug, "hidden", new { postId = post.Id }, ct);
        }
        var author = comment?.AuthorId ?? post!.AuthorId;
        await notifier.NotifyUserAsync(author, new NotificationMessage(NotificationCategory.Community, "Your post was removed",
            "It broke the community rules.", "#/communities", NotificationTone.Warning), ct);
    }

    private async Task MuteAsync(long postId, Guid authorId, MuteRequest mute, CancellationToken ct)
    {
        var post = await repository.PostAsync(postId, ct);
        var member = await repository.MemberAsync(post!.CommunityId, authorId, ct);
        if (member is null)
        {
            member = new CommunityMember { CommunityId = post.CommunityId, UserId = authorId };
            repository.AddMember(member);
        }
        (member.MutedUntil, member.MuteReason) = (clock.UtcNow.AddDays(mute.Days), mute.Reason.Trim());
        await notifier.NotifyUserAsync(authorId, new NotificationMessage(NotificationCategory.Community, $"You are muted for {mute.Days} day(s)",
            $"{mute.Reason.Trim()} You can still read the community.", "#/communities", NotificationTone.Danger), ct);
    }

    public async Task<IReadOnlyList<CommunityDto>> CommunitiesAsync(CancellationToken ct) =>
        await repository.CommunitiesAsync(await repository.ViewerAsync(currentUser.UserId, true, ct), true, ct);

    public async Task<CommunityDto> SaveCommunityAsync(long? id, CommunityRequest request, CancellationToken ct)
    {
        await communityRules.ValidateAndThrowAsync(request, ct);
        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await repository.SlugTakenAsync(slug, id, ct))
        {
            throw new ConflictException("Another community uses this address.", "slug_taken");
        }
        var community = id is null ? new Community() : await repository.CommunityAsync(id.Value, ct) ?? throw new NotFoundException("Community not found.");
        if (id is null)
        {
            repository.Add(community);
        }
        (community.Slug, community.Name, community.About, community.Audience) = (slug, request.Name.Trim(), request.About?.Trim(), Enum.Parse<CommunityAudience>(request.Audience, true));
        (community.RulesText, community.SlowModeSeconds, community.Color, community.Icon) = (string.Join('\n', request.Rules ?? []), request.SlowModeSeconds, request.Color, request.Icon);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, id is null ? "community.created" : "community.updated", community.Name, "Community", community.Id.ToString(), ct: ct);
        return (await CommunitiesAsync(ct)).Single(c => c.Id == community.Id);
    }

    public async Task ArchiveAsync(long id, bool archived, CancellationToken ct)
    {
        var community = await repository.CommunityAsync(id, ct) ?? throw new NotFoundException("Community not found.");
        community.IsArchived = archived;
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, archived ? "community.archived" : "community.restored", community.Name, "Community", id.ToString(), ct: ct);
    }
}
