using FluentValidation;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Communities;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Communities;

/// <summary>"Super App Admin" posts and replies, post flags, banned words and mutes.</summary>
public sealed partial class CommunityAdminService
{
    public async Task<PostDto> PostAsync(string slug, AdminPostRequest request, CancellationToken ct)
    {
        await postRules.ValidateAndThrowAsync(new CreatePostRequest(request.Text, request.PollOptions), ct);
        var community = await repository.CommunityAsync(slug.ToLowerInvariant(), ct) ?? throw new NotFoundException("Community not found.");
        var options = (request.PollOptions ?? []).Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).ToList();
        var post = new Post
        {
            CommunityId = community.Id, AuthorId = UserId, AsSuperAppAdmin = true, Text = request.Text.Trim(), HasPoll = options.Count > 0,
            IsPinned = request.IsPinned, IsAnnouncement = request.IsAnnouncement,
            PollOptions = options.Select((o, i) => new PollOption { Text = o, SortOrder = (byte)i }).ToList(),
        };
        repository.Add(post);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, "community.admin_post", PostsService.Excerpt(post.Text), "Post", post.Id.ToString(), ct: ct);
        var dto = (await repository.PostDtoAsync(post.Id, UserId, true, ct))!;
        await live.PublishAsync(community.Slug, "post", dto, ct);
        return dto;
    }

    public async Task<CommentDto> ReplyAsync(long postId, CommentRequest request, CancellationToken ct)
    {
        await commentRules.ValidateAndThrowAsync(request, ct);
        var post = await repository.PostAsync(postId, ct) ?? throw new NotFoundException("Post not found.");
        var comment = new Comment { PostId = postId, AuthorId = UserId, AsSuperAppAdmin = true, Text = request.Text.Trim() };
        repository.Add(comment);
        post.CommentsCount++;
        await unitOfWork.SaveChangesAsync(ct);
        if (!post.AsSuperAppAdmin)
        {
            await notifier.NotifyUserAsync(post.AuthorId, new Abstractions.Notifications.NotificationMessage(NotificationCategory.Community,
                "Super App replied to your post", PostsService.Excerpt(comment.Text), $"#/communities/post/{postId}"), ct);
        }
        return (await repository.CommentsAsync(postId, UserId, true, ct)).Single(c => c.Id == comment.Id);
    }

    public async Task<PostDto> FlagsAsync(long postId, PostFlagsRequest request, CancellationToken ct)
    {
        var post = await repository.PostAsync(postId, ct) ?? throw new NotFoundException("Post not found.");
        (post.IsPinned, post.IsAnnouncement, post.CommentsClosed, post.IsHidden) = (request.IsPinned, request.IsAnnouncement, request.CommentsClosed, request.IsHidden);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, "community.post_flags", $"Post {postId}: pinned {request.IsPinned}, hidden {request.IsHidden}", "Post", postId.ToString(), ct: ct);
        return (await repository.PostDtoAsync(postId, UserId, true, ct))!;
    }

    public Task<IReadOnlyList<BannedWordDto>> BannedWordsAsync(CancellationToken ct) => repository.BannedWordListAsync(ct);

    public async Task<BannedWordDto> AddBannedWordAsync(BannedWordRequest request, CancellationToken ct)
    {
        await wordRules.ValidateAndThrowAsync(request, ct);
        var word = request.Word.Trim().ToLowerInvariant();
        if (await repository.BannedWordExistsAsync(word, ct))
        {
            throw new ConflictException("This word is already on the list.", "word_exists");
        }
        var row = new BannedWord { Word = word, Language = request.Language };
        repository.Add(row);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, "community.banned_word.added", word, "BannedWord", row.Id.ToString(), ct: ct);
        return new BannedWordDto(row.Id, row.Word, row.Language);
    }

    public async Task RemoveBannedWordAsync(long id, CancellationToken ct)
    {
        var row = await repository.BannedWordAsync(id, ct) ?? throw new NotFoundException("Word not found.");
        repository.Remove(row);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, "community.banned_word.removed", row.Word, "BannedWord", id.ToString(), ct: ct);
    }

    public Task<IReadOnlyList<MuteDto>> MutesAsync(CancellationToken ct) => repository.MutesAsync(clock.UtcNow, ct);

    public async Task UnmuteAsync(long communityId, Guid userId, CancellationToken ct)
    {
        var member = await repository.MemberAsync(communityId, userId, ct);
        if (member?.MutedUntil is null)
        {
            throw new NotFoundException("This member is not muted.");
        }
        (member.MutedUntil, member.MuteReason) = (null, null);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, "community.unmuted", $"Member unmuted in community {communityId}", "CommunityMember", userId.ToString(), ct: ct);
    }
}

public sealed class MuteRequestValidator : AbstractValidator<MuteRequest>
{
    public MuteRequestValidator()
    {
        RuleFor(x => x.Days).InclusiveBetween(1, 365);
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Tell the member why.").MaximumLength(300);
    }
}

public sealed class CommunityRequestValidator : AbstractValidator<CommunityRequest>
{
    public CommunityRequestValidator()
    {
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(40).Matches("^[a-z0-9-]+$").WithMessage("Lowercase letters, digits and dashes.");
        RuleFor(x => x.Name).NotEmpty().Length(3, 80);
        RuleFor(x => x.About).MaximumLength(500);
        RuleFor(x => x.Audience).Must(a => Enum.TryParse<CommunityAudience>(a, true, out _)).WithMessage("Audience: Everyone, Sellers or ProAndEliteSellers.");
        RuleFor(x => x.Rules).Must(r => r is null || string.Join('\n', r).Length <= 1000).WithMessage("Rules up to 1,000 characters.");
        RuleFor(x => x.SlowModeSeconds).InclusiveBetween(0, 3600);
        RuleFor(x => x.Color).MaximumLength(30);
        RuleFor(x => x.Icon).MaximumLength(30);
    }
}

public sealed class BannedWordRequestValidator : AbstractValidator<BannedWordRequest>
{
    public BannedWordRequestValidator()
    {
        RuleFor(x => x.Word).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Language).Must(l => l is "ar" or "en").WithMessage("Language: ar or en.");
    }
}
