using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Entities.Communities;

namespace SuperApp.Application.Features.Communities;

/// <summary>A community. Audience Everyone / Sellers / ProAndEliteSellers; <c>CanJoin</c> says whether the caller's role fits.</summary>
public sealed record CommunityDto(long Id, string Slug, string Name, string? About, string Audience, IReadOnlyList<string> Rules, int SlowModeSeconds,
    string? Color, string? Icon, bool IsArchived, int Members, int Posts, bool Joined, bool CanJoin, string? MyRole, DateTime? MutedUntil);

/// <summary>Who wrote a post or comment. Admin posts show as "Super App Admin" with the team badge; sellers show their level.</summary>
public sealed record AuthorDto(string Name, IReadOnlyList<string> Badges);

public sealed record PollOptionDto(long Id, string Text, int Votes);

public sealed record PollDto(IReadOnlyList<PollOptionDto> Options, long? MyVote, int TotalVotes);

/// <summary>A rich card for a Super App link (pitch, product, store, tournament, post, community, news…).</summary>
public sealed record LinkCardDto(string Kind, string Id, string Title, string? Subtitle, string? Image, string Link);

public sealed record PostDto(long Id, string Community, AuthorDto Author, bool AsSuperAppAdmin, string Text, string? ImageUrl, PollDto? Poll,
    bool IsPinned, bool IsAnnouncement, bool CommentsClosed, bool IsHidden, int CommentsCount, int ReactionsCount, string? MyReaction,
    IReadOnlyList<LinkCardDto> Cards, DateTime CreatedAt, DateTime? UpdatedAt, bool IsMine);

public sealed record CommentDto(long Id, long PostId, long? ParentId, AuthorDto Author, bool AsSuperAppAdmin, string Text, bool IsHidden, int Reactions,
    DateTime CreatedAt, bool IsMine);

/// <summary>GET /communities/{slug}/posts — filter latest (default), top, pinned, polls.</summary>
public sealed record PostsQuery(string? Filter = null, int Page = 1, int PageSize = 20);

/// <summary>A post: text, an optional poll (2–6 options); a photo is added after with POST /posts/{id}/image.</summary>
public sealed record CreatePostRequest(string Text, IReadOnlyList<string>? PollOptions);

public sealed record EditPostRequest(string Text);

public sealed record CommentRequest(string Text);

/// <summary>Like, Fire, Goal, Laugh, Clap or Sad.</summary>
public sealed record ReactionRequest(string Type);

public sealed record VoteRequest(long OptionId);

/// <summary>Spam, Abuse, Scam, OffTopic or Other.</summary>
public sealed record ReportRequest(string Reason, string? Detail);

/* ---------- Admin ---------- */

public sealed record ReportDto(long Id, string Kind, long TargetId, long PostId, string Community, string Excerpt, string Author, string Reason, string? Detail,
    string Reporter, string Status, DateTime CreatedAt);

/// <summary>Remove and mute the author: muted members can read but not post, for <c>Days</c> (1–365).</summary>
public sealed record MuteRequest(int Days, string Reason);

public sealed record CommunityRequest(string Slug, string Name, string? About, string Audience, IReadOnlyList<string>? Rules, int SlowModeSeconds, string? Color, string? Icon);

/// <summary>An admin post as "Super App Admin" — pinned and/or announcement.</summary>
public sealed record AdminPostRequest(string Text, IReadOnlyList<string>? PollOptions, bool IsPinned, bool IsAnnouncement);

public sealed record PostFlagsRequest(bool IsPinned, bool IsAnnouncement, bool CommentsClosed, bool IsHidden);

public sealed record BannedWordDto(long Id, string Word, string Language);

public sealed record BannedWordRequest(string Word, string Language);

public sealed record MuteDto(long CommunityId, string Community, Guid UserId, string Name, DateTime MutedUntil, string? Reason);

/* ---------- Persistence and live updates ---------- */

/// <summary>The caller as the community rules see them.</summary>
public sealed record Viewer(Guid? UserId, bool IsSeller, int SellerRank, bool IsModerator);

public interface ICommunityRepository
{
    Task<Viewer> ViewerAsync(Guid? userId, bool moderator, CancellationToken ct);

    Task<IReadOnlyList<CommunityDto>> CommunitiesAsync(Viewer viewer, bool includeArchived, CancellationToken ct);

    Task<Community?> CommunityAsync(string slug, CancellationToken ct);

    Task<Community?> CommunityAsync(long id, CancellationToken ct);

    Task<bool> SlugTakenAsync(string slug, long? exceptId, CancellationToken ct);

    void Add(Community community);

    Task<CommunityMember?> MemberAsync(long communityId, Guid userId, CancellationToken ct);

    void AddMember(CommunityMember member);

    void RemoveMember(CommunityMember member);

    Task<PagedResult<PostDto>> PostsAsync(IReadOnlyCollection<long> communityIds, PostsQuery query, Guid? viewer, bool withHidden, CancellationToken ct);

    Task<PostDto?> PostDtoAsync(long id, Guid? viewer, bool withHidden, CancellationToken ct);

    Task<IReadOnlyList<long>> MyCommunityIdsAsync(Guid userId, CancellationToken ct);

    Task<Post?> PostAsync(long id, CancellationToken ct);

    Task<DateTime?> LastPostAtAsync(long communityId, Guid userId, CancellationToken ct);

    void Add(Post post);

    Task<IReadOnlyList<CommentDto>> CommentsAsync(long postId, Guid? viewer, bool withHidden, CancellationToken ct);

    Task<Comment?> CommentAsync(long id, CancellationToken ct);

    void Add(Comment comment);

    Task<Reaction?> ReactionAsync(Guid userId, long? postId, long? commentId, CancellationToken ct);

    void Add(Reaction reaction);

    void Remove(Reaction reaction);

    Task<bool> VotedAsync(long postId, Guid userId, CancellationToken ct);

    Task<PollOption?> OptionAsync(long optionId, CancellationToken ct);

    void Add(PollVote vote);

    void Add(ContentReport report);

    Task<bool> ReportedAsync(Guid reporterId, long? postId, long? commentId, CancellationToken ct);

    Task<PagedResult<ReportDto>> ReportsAsync(string? status, int page, int pageSize, CancellationToken ct);

    Task<ContentReport?> ReportAsync(long id, CancellationToken ct);

    Task<IReadOnlyList<string>> BannedWordsAsync(CancellationToken ct);

    Task<IReadOnlyList<BannedWordDto>> BannedWordListAsync(CancellationToken ct);

    Task<BannedWord?> BannedWordAsync(long id, CancellationToken ct);

    Task<bool> BannedWordExistsAsync(string word, CancellationToken ct);

    void Add(BannedWord word);

    void Remove(BannedWord word);

    Task<IReadOnlyList<MuteDto>> MutesAsync(DateTime now, CancellationToken ct);

    /// <summary>Rich cards for Super App links (unknown targets are left out).</summary>
    Task<IReadOnlyList<LinkCardDto>> CardsAsync(IReadOnlyList<SuperAppLink> links, CancellationToken ct);
}

/// <summary>Live community events (SignalR group per community): <c>post</c>, <c>comment</c>, <c>reaction</c>, <c>hidden</c>.</summary>
public interface ICommunityLive
{
    Task PublishAsync(string communitySlug, string eventName, object payload, CancellationToken ct);
}

/// <summary>Slow mode (429): the member must wait before posting again.</summary>
public sealed class SlowModeException(int seconds)
    : TooManyRequestsException($"Slow mode is on — you can post again in {seconds} seconds.", seconds, "slow_mode");
