using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Entities.Communities;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Communities;

/// <summary>
/// Communities (API §6.3, FR-COM-01/06): only Super App creates them; audience everyone / sellers / Pro and Elite sellers.
/// Reading public communities needs no account; sellers-only communities need the role; there are no private messages.
/// </summary>
public interface ICommunitiesService
{
    Task<IReadOnlyList<CommunityDto>> ListAsync(CancellationToken ct);

    Task<CommunityDto> GetAsync(string slug, CancellationToken ct);

    Task<CommunityDto> JoinAsync(string slug, CancellationToken ct);

    Task LeaveAsync(string slug, CancellationToken ct);

    Task<PagedResult<PostDto>> PostsAsync(string slug, PostsQuery query, CancellationToken ct);

    Task<PagedResult<PostDto>> FeedAsync(PostsQuery query, CancellationToken ct);

    Task<PostDto> PostAsync(long id, CancellationToken ct);

    Task<IReadOnlyList<CommentDto>> CommentsAsync(long postId, CancellationToken ct);
}

public sealed class CommunitiesService(ICommunityRepository repository, IUnitOfWork unitOfWork, ICurrentUser currentUser) : ICommunitiesService
{
    public async Task<IReadOnlyList<CommunityDto>> ListAsync(CancellationToken ct) =>
        await repository.CommunitiesAsync(await ViewerAsync(ct), false, ct);

    public async Task<CommunityDto> GetAsync(string slug, CancellationToken ct)
    {
        var viewer = await ViewerAsync(ct);
        var dto = (await repository.CommunitiesAsync(viewer, true, ct)).FirstOrDefault(c => string.Equals(c.Slug, slug, StringComparison.OrdinalIgnoreCase)) ?? throw new NotFoundException("Community not found.");
        return dto.CanJoin || dto.Joined ? dto : throw new ForbiddenException(AudienceMessage(dto.Audience), "audience");
    }

    public async Task<CommunityDto> JoinAsync(string slug, CancellationToken ct)
    {
        var user = currentUser.UserId ?? throw new UnauthorizedException();
        var (community, viewer) = await VisibleAsync(slug, ct);
        if (community.IsArchived)
        {
            throw new BusinessRuleException("This community is archived.", "archived");
        }
        if (await repository.MemberAsync(community.Id, user, ct) is null)
        {
            repository.AddMember(new CommunityMember { CommunityId = community.Id, UserId = user, Role = CommunityRole.Member });
            await unitOfWork.SaveChangesAsync(ct);
        }
        return (await repository.CommunitiesAsync(viewer, true, ct)).Single(c => c.Id == community.Id);
    }

    public async Task LeaveAsync(string slug, CancellationToken ct)
    {
        var user = currentUser.UserId ?? throw new UnauthorizedException();
        var community = await repository.CommunityAsync(slug.ToLowerInvariant(), ct) ?? throw new NotFoundException("Community not found.");
        var member = await repository.MemberAsync(community.Id, user, ct) ?? throw new NotFoundException("You're not a member.", "not_member");
        if (member.MutedUntil is not null)
        {
            throw new BusinessRuleException("Muted members stay in the community until the mute ends.", "muted");
        }
        repository.RemoveMember(member);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<PostDto>> PostsAsync(string slug, PostsQuery query, CancellationToken ct)
    {
        var (community, viewer) = await VisibleAsync(slug, ct);
        return await repository.PostsAsync([community.Id], query, viewer.UserId, viewer.IsModerator, ct);
    }

    /// <summary>Latest posts of the communities I joined.</summary>
    public async Task<PagedResult<PostDto>> FeedAsync(PostsQuery query, CancellationToken ct)
    {
        var user = currentUser.UserId ?? throw new UnauthorizedException();
        return await repository.PostsAsync(await repository.MyCommunityIdsAsync(user, ct), query with { Filter = null }, user, false, ct);
    }

    public async Task<PostDto> PostAsync(long id, CancellationToken ct)
    {
        var viewer = await ViewerAsync(ct);
        var post = await repository.PostAsync(id, ct) ?? throw new NotFoundException("Post not found.");
        await EnsureVisibleAsync(post.CommunityId, viewer, ct);
        return await repository.PostDtoAsync(id, viewer.UserId, viewer.IsModerator, ct) ?? throw new NotFoundException("Post not found.");
    }

    public async Task<IReadOnlyList<CommentDto>> CommentsAsync(long postId, CancellationToken ct)
    {
        var viewer = await ViewerAsync(ct);
        var post = await repository.PostAsync(postId, ct);
        if (post is null || (post.IsHidden && !viewer.IsModerator))
        {
            throw new NotFoundException("Post not found.");
        }
        await EnsureVisibleAsync(post.CommunityId, viewer, ct);
        return await repository.CommentsAsync(postId, viewer.UserId, viewer.IsModerator, ct);
    }

    private Task<Viewer> ViewerAsync(CancellationToken ct) => repository.ViewerAsync(currentUser.UserId, currentUser.HasPermission("community.moderate"), ct);

    private async Task<(Community Community, Viewer Viewer)> VisibleAsync(string slug, CancellationToken ct)
    {
        var community = await repository.CommunityAsync(slug.ToLowerInvariant(), ct) ?? throw new NotFoundException("Community not found.");
        var viewer = await ViewerAsync(ct);
        return CommunityRules.CanSee(community.Audience, viewer) ? (community, viewer)
            : throw new ForbiddenException(AudienceMessage(community.Audience.ToString()), "audience");
    }

    private async Task EnsureVisibleAsync(long communityId, Viewer viewer, CancellationToken ct)
    {
        var community = await repository.CommunityAsync(communityId, ct) ?? throw new NotFoundException("Community not found.");
        if (!CommunityRules.CanSee(community.Audience, viewer))
        {
            throw new ForbiddenException(AudienceMessage(community.Audience.ToString()), "audience");
        }
    }

    private static string AudienceMessage(string audience) =>
        audience == nameof(CommunityAudience.Sellers) ? "This community is for Super App sellers." : "This community is for Pro and Elite sellers.";
}

/// <summary>Who may see and join a community.</summary>
public static class CommunityRules
{
    public const int ProRank = 3;

    public static bool CanSee(CommunityAudience audience, Viewer viewer) => viewer.IsModerator || audience switch
    {
        CommunityAudience.Everyone => true,
        CommunityAudience.Sellers => viewer.IsSeller,
        _ => viewer.SellerRank >= ProRank,
    };
}
