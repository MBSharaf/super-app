using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Communities;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Communities;

/// <summary>Reactions, votes, reports, link cards and the shared checks of <see cref="PostsService"/>.</summary>
public sealed partial class PostsService
{
    /// <summary>Sets my reaction (null removes it). One reaction per person and post.</summary>
    public async Task<PostDto> ReactAsync(long postId, ReactionRequest? request, CancellationToken ct)
    {
        var post = await VisiblePostAsync(postId, ct);
        var existing = await repository.ReactionAsync(UserId, postId, null, ct);
        if (request is null)
        {
            if (existing is not null)
            {
                repository.Remove(existing);
                post.ReactionsCount = Math.Max(0, post.ReactionsCount - 1);
            }
        }
        else
        {
            var type = Enum.TryParse<ReactionType>(request.Type, true, out var t) ? t : throw new BadRequestException("Reaction: Like, Fire, Goal, Laugh, Clap or Sad.", "reaction");
            if (existing is null)
            {
                repository.Add(new Reaction { UserId = UserId, PostId = postId, Type = type });
                post.ReactionsCount++;
            }
            else
            {
                existing.Type = type;
            }
        }
        await unitOfWork.SaveChangesAsync(ct);
        return (await repository.PostDtoAsync(postId, UserId, false, ct))!;
    }

    /// <summary>One vote per poll (409 already_voted); members only.</summary>
    public async Task<PostDto> VoteAsync(long postId, VoteRequest request, CancellationToken ct)
    {
        var post = await VisiblePostAsync(postId, ct);
        await CanWriteAsync((await repository.CommunityAsync(post.CommunityId, ct))!, ct, allowMuted: true);
        var option = await repository.OptionAsync(request.OptionId, ct);
        if (option is null || option.PostId != postId)
        {
            throw new NotFoundException("Poll option not found.");
        }
        if (await repository.VotedAsync(postId, UserId, ct))
        {
            throw new ConflictException("You have already voted in this poll.", "already_voted");
        }
        repository.Add(new PollVote { OptionId = option.Id, PostId = postId, UserId = UserId });
        option.VotesCount++;
        await unitOfWork.SaveChangesAsync(ct);
        return (await repository.PostDtoAsync(postId, UserId, false, ct))!;
    }

    /// <summary>Reports a post or a comment once; the moderators are told.</summary>
    public async Task ReportAsync(long? postId, long? commentId, ReportRequest request, CancellationToken ct)
    {
        await reportRules.ValidateAndThrowAsync(request, ct);
        var comment = commentId is null ? null : await repository.CommentAsync(commentId.Value, ct) ?? throw new NotFoundException("Comment not found.");
        await VisiblePostAsync(postId ?? comment!.PostId, ct);
        if (await repository.ReportedAsync(UserId, postId, commentId, ct))
        {
            throw new ConflictException("You have already reported this.", "already_reported");
        }
        repository.Add(new ContentReport { ReporterId = UserId, PostId = postId, CommentId = commentId, Reason = Enum.Parse<ContentReportReason>(request.Reason, true), Detail = request.Detail?.Trim() });
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyStaffAsync("community.moderate", new NotificationMessage(NotificationCategory.Community, "A community post was reported",
            request.Reason, "#/admin/communities?tab=reports", NotificationTone.Warning), ct);
    }

    /// <summary>A rich card for a Super App link; outside links → 422 outside_link.</summary>
    public async Task<LinkCardDto> PreviewAsync(string url, CancellationToken ct)
    {
        var text = url?.Trim() ?? string.Empty;
        var host = Host();
        if (host is not null && text.Contains(host, StringComparison.OrdinalIgnoreCase) && text.Contains("#/", StringComparison.Ordinal))
        {
            text = text[text.IndexOf("#/", StringComparison.Ordinal)..];
        }
        var links = TextGuard.Links(text);
        if (links.Count == 0)
        {
            throw new BusinessRuleException("Only Super App links become cards.", "outside_link");
        }
        var cards = await repository.CardsAsync([links[0]], ct);
        return cards.Count > 0 ? cards[0] : throw new NotFoundException("Nothing found at this link.");
    }

    private async Task CanWriteAsync(Community community, CancellationToken ct, bool allowMuted = false)
    {
        await flags.EnsureOnAsync(KillSwitches.Community, ct);
        if (community.IsArchived)
        {
            throw new BusinessRuleException("This community is archived.", "archived");
        }
        var member = await repository.MemberAsync(community.Id, UserId, ct) ?? throw new ForbiddenException("Join the community to take part.", "not_member");
        if (!allowMuted && member.MutedUntil > clock.UtcNow)
        {
            throw new ForbiddenException($"You are muted here until {member.MutedUntil:d MMM HH:mm}. You can still read.", "muted");
        }
    }

    private async Task GuardAsync(string text, CancellationToken ct)
    {
        if (TextGuard.Check(text, await repository.BannedWordsAsync(ct), Host()) is { } broken)
        {
            throw new BusinessRuleException(broken.Message, broken.Code);
        }
    }

    private string? Host() => Uri.TryCreate(site.Value.PublicUrl, UriKind.Absolute, out var uri) ? uri.Authority : null;

    private async Task<Post> OwnPostAsync(long id, CancellationToken ct)
    {
        var post = await repository.PostAsync(id, ct);
        return post is not null && post.AuthorId == UserId && !post.AsSuperAppAdmin ? post : throw new NotFoundException("Post not found.");
    }

    private async Task<Post> VisiblePostAsync(long id, CancellationToken ct)
    {
        var post = await repository.PostAsync(id, ct);
        if (post is null || post.IsHidden)
        {
            throw new NotFoundException("Post not found.");
        }
        var community = (await repository.CommunityAsync(post.CommunityId, ct))!;
        var viewer = await repository.ViewerAsync(currentUser.UserId, currentUser.HasPermission("community.moderate"), ct);
        return CommunityRules.CanSee(community.Audience, viewer) ? post : throw new ForbiddenException("This community is for sellers.", "audience");
    }

    internal Post NewPost(long communityId, string text, IReadOnlyList<string>? poll, bool asAdmin)
    {
        var options = (poll ?? []).Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).ToList();
        var post = new Post { CommunityId = communityId, AuthorId = UserId, AsSuperAppAdmin = asAdmin, Text = text.Trim(), HasPoll = options.Count > 0 };
        post.PollOptions = options.Select((o, i) => new PollOption { Text = o, SortOrder = (byte)i }).ToList();
        return post;
    }
}

public sealed class CreatePostRequestValidator : AbstractValidator<CreatePostRequest>
{
    public CreatePostRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty().WithMessage("Write something.").MaximumLength(2000);
        RuleFor(x => x.PollOptions).Must(o => o is null || o.Count == 0 || (o.Count is >= 2 and <= 6 && o.All(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 80)))
            .WithMessage("A poll has 2 to 6 options of up to 80 characters.");
    }
}

public sealed class EditPostRequestValidator : AbstractValidator<EditPostRequest>
{
    public EditPostRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty().WithMessage("Write something.").MaximumLength(2000);
    }
}

public sealed class CommentRequestValidator : AbstractValidator<CommentRequest>
{
    public CommentRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty().WithMessage("Write a comment.").MaximumLength(1000);
    }
}

public sealed class ReportRequestValidator : AbstractValidator<ReportRequest>
{
    public ReportRequestValidator()
    {
        RuleFor(x => x.Reason).Must(r => Enum.TryParse<ContentReportReason>(r, true, out _)).WithMessage("Reason: Spam, Abuse, Scam, OffTopic or Other.");
        RuleFor(x => x.Detail).MaximumLength(400);
    }
}
