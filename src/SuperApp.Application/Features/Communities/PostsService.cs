using FluentValidation;
using Microsoft.Extensions.Options;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Abstractions.Storage;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Options;
using SuperApp.Domain.Entities.Communities;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Communities;

/// <summary>
/// Posts, comments, reactions, poll votes and reports (API §6.3, FR-COM-02/03/04/07). Members only; muted members
/// read but cannot post (403 muted); slow mode answers 429; outside links, phone numbers and banned words → 422.
/// New posts and comments go live to the community (SignalR).
/// </summary>
public interface IPostsService
{
    Task<PostDto> CreateAsync(string slug, CreatePostRequest request, CancellationToken ct);

    Task<PostDto> EditAsync(long id, EditPostRequest request, CancellationToken ct);

    Task DeleteAsync(long id, CancellationToken ct);

    Task<PostDto> ImageAsync(long id, UploadedFile file, CancellationToken ct);

    Task<CommentDto> CommentAsync(long postId, long? parentId, CommentRequest request, CancellationToken ct);

    Task<PostDto> ReactAsync(long postId, ReactionRequest? request, CancellationToken ct);

    Task<PostDto> VoteAsync(long postId, VoteRequest request, CancellationToken ct);

    Task ReportAsync(long? postId, long? commentId, ReportRequest request, CancellationToken ct);

    Task<LinkCardDto> PreviewAsync(string url, CancellationToken ct);
}

public sealed partial class PostsService(
    ICommunityRepository repository,
    IUnitOfWork unitOfWork,
    IFeatureFlags flags,
    IFileStorage files,
    INotifier notifier,
    ICommunityLive live,
    ICurrentUser currentUser,
    IClock clock,
    IOptions<SiteOptions> site,
    IValidator<CreatePostRequest> postRules,
    IValidator<EditPostRequest> editRules,
    IValidator<CommentRequest> commentRules,
    IValidator<ReportRequest> reportRules) : IPostsService
{
    private Guid UserId => currentUser.UserId ?? throw new UnauthorizedException();

    public async Task<PostDto> CreateAsync(string slug, CreatePostRequest request, CancellationToken ct)
    {
        await postRules.ValidateAndThrowAsync(request, ct);
        var community = await repository.CommunityAsync(slug.ToLowerInvariant(), ct) ?? throw new NotFoundException("Community not found.");
        await CanWriteAsync(community, ct);
        await GuardAsync(request.Text, ct);
        if (community.SlowModeSeconds > 0 && await repository.LastPostAtAsync(community.Id, UserId, ct) is { } last
            && (clock.UtcNow - last).TotalSeconds < community.SlowModeSeconds)
        {
            throw new SlowModeException(community.SlowModeSeconds - (int)(clock.UtcNow - last).TotalSeconds);
        }
        var post = NewPost(community.Id, request.Text, request.PollOptions, asAdmin: false);
        repository.Add(post);
        await unitOfWork.SaveChangesAsync(ct);
        var dto = (await repository.PostDtoAsync(post.Id, UserId, false, ct))!;
        await live.PublishAsync(community.Slug, "post", dto, ct);
        return dto;
    }

    public async Task<PostDto> EditAsync(long id, EditPostRequest request, CancellationToken ct)
    {
        await editRules.ValidateAndThrowAsync(request, ct);
        var post = await OwnPostAsync(id, ct);
        await GuardAsync(request.Text, ct);
        post.Text = request.Text.Trim();
        await unitOfWork.SaveChangesAsync(ct);
        return (await repository.PostDtoAsync(id, UserId, false, ct))!;
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        var post = await OwnPostAsync(id, ct);
        post.IsDeleted = true;
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<PostDto> ImageAsync(long id, UploadedFile file, CancellationToken ct)
    {
        var post = await OwnPostAsync(id, ct);
        post.ImageFileId = await files.SaveAsync(file, UploadKind.Photo, UserId, "community", ct);
        await unitOfWork.SaveChangesAsync(ct);
        return (await repository.PostDtoAsync(id, UserId, false, ct))!;
    }

    public async Task<CommentDto> CommentAsync(long postId, long? parentId, CommentRequest request, CancellationToken ct)
    {
        await commentRules.ValidateAndThrowAsync(request, ct);
        var post = await VisiblePostAsync(postId, ct);
        var community = (await repository.CommunityAsync(post.CommunityId, ct))!;
        await CanWriteAsync(community, ct);
        if (post.CommentsClosed)
        {
            throw new BusinessRuleException("Comments are closed on this post.", "comments_closed");
        }
        var parent = parentId is null ? null : await repository.CommentAsync(parentId.Value, ct);
        if (parentId is not null && (parent is null || parent.PostId != postId || parent.IsHidden))
        {
            throw new NotFoundException("Comment not found.");
        }
        await GuardAsync(request.Text, ct);
        var comment = new Comment { PostId = postId, ParentCommentId = parent?.ParentCommentId ?? parent?.Id, AuthorId = UserId, Text = request.Text.Trim() };
        repository.Add(comment);
        post.CommentsCount++;
        await unitOfWork.SaveChangesAsync(ct);
        await TellAboutCommentAsync(post, parent, ct);
        var dto = (await repository.CommentsAsync(postId, UserId, false, ct)).Single(c => c.Id == comment.Id);
        await live.PublishAsync(community.Slug, "comment", dto, ct);
        return dto;
    }

    private async Task TellAboutCommentAsync(Post post, Comment? parent, CancellationToken ct)
    {
        var target = parent?.AuthorId ?? post.AuthorId;
        if (target != UserId)
        {
            await notifier.NotifyUserAsync(target, new NotificationMessage(NotificationCategory.Community,
                parent is null ? "New comment on your post" : "New reply to your comment", Excerpt(post.Text), $"#/communities/post/{post.Id}"), ct);
        }
    }

    internal static string Excerpt(string text) => text.Length <= 120 ? text : text[..117] + "…";
}
