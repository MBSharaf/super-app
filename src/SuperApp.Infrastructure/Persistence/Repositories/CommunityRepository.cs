using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Communities;
using SuperApp.Domain.Entities.Communities;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Communities, members, posts, comments, reactions, polls, reports, banned words.</summary>
public sealed partial class CommunityRepository(AppDbContext db) : ICommunityRepository
{
    public async Task<Viewer> ViewerAsync(Guid? userId, bool moderator, CancellationToken ct)
    {
        if (userId is null)
        {
            return new Viewer(null, false, 0, moderator);
        }
        var rank = await db.Set<Seller>().Where(s => s.UserId == userId && s.Status == SellerStatus.Active).Select(s => (int?)s.Level.Rank).FirstOrDefaultAsync(ct);
        return new Viewer(userId, rank is not null, rank ?? 0, moderator);
    }

    public async Task<IReadOnlyList<CommunityDto>> CommunitiesAsync(Viewer viewer, bool includeArchived, CancellationToken ct)
    {
        var rows = await db.Set<Community>().AsNoTracking().Where(c => includeArchived || !c.IsArchived).OrderBy(c => c.Id)
            .Select(c => new
            {
                c, Members = c.Members.Count, Posts = db.Set<Post>().Count(p => p.CommunityId == c.Id && !p.IsHidden),
                Me = c.Members.Where(m => m.UserId == viewer.UserId).Select(m => new { m.Role, m.MutedUntil }).FirstOrDefault(),
            }).ToListAsync(ct);
        return rows.Select(r => new CommunityDto(r.c.Id, r.c.Slug, r.c.Name, r.c.About, r.c.Audience.ToString(),
            (r.c.RulesText ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries), r.c.SlowModeSeconds, r.c.Color, r.c.Icon, r.c.IsArchived,
            r.Members, r.Posts, r.Me is not null, CommunityRules.CanSee(r.c.Audience, viewer), r.Me?.Role.ToString(), r.Me?.MutedUntil)).ToList();
    }

    public Task<Community?> CommunityAsync(string slug, CancellationToken ct) => db.Set<Community>().FirstOrDefaultAsync(c => c.Slug == slug, ct);

    public Task<Community?> CommunityAsync(long id, CancellationToken ct) => db.Set<Community>().FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> SlugTakenAsync(string slug, long? exceptId, CancellationToken ct) => db.Set<Community>().AnyAsync(c => c.Slug == slug && c.Id != exceptId, ct);

    public void Add(Community community) => db.Set<Community>().Add(community);

    public Task<CommunityMember?> MemberAsync(long communityId, Guid userId, CancellationToken ct) =>
        db.Set<CommunityMember>().FirstOrDefaultAsync(m => m.CommunityId == communityId && m.UserId == userId, ct);

    public void AddMember(CommunityMember member) => db.Set<CommunityMember>().Add(member);

    public void RemoveMember(CommunityMember member) => db.Set<CommunityMember>().Remove(member);

    public async Task<IReadOnlyList<long>> MyCommunityIdsAsync(Guid userId, CancellationToken ct) =>
        await db.Set<CommunityMember>().Where(m => m.UserId == userId && !m.Community.IsArchived).Select(m => m.CommunityId).ToListAsync(ct);

    public Task<Post?> PostAsync(long id, CancellationToken ct) => db.Set<Post>().FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<DateTime?> LastPostAtAsync(long communityId, Guid userId, CancellationToken ct) =>
        db.Set<Post>().IgnoreQueryFilters().Where(p => p.CommunityId == communityId && p.AuthorId == userId).MaxAsync(p => (DateTime?)p.CreatedAt, ct);

    public void Add(Post post) => db.Set<Post>().Add(post);

    public Task<Comment?> CommentAsync(long id, CancellationToken ct) => db.Set<Comment>().FirstOrDefaultAsync(c => c.Id == id, ct);

    public void Add(Comment comment) => db.Set<Comment>().Add(comment);

    public Task<Reaction?> ReactionAsync(Guid userId, long? postId, long? commentId, CancellationToken ct) =>
        db.Set<Reaction>().FirstOrDefaultAsync(r => r.UserId == userId && r.PostId == postId && r.CommentId == commentId, ct);

    public void Add(Reaction reaction) => db.Set<Reaction>().Add(reaction);

    public void Remove(Reaction reaction) => db.Set<Reaction>().Remove(reaction);

    public Task<bool> VotedAsync(long postId, Guid userId, CancellationToken ct) => db.Set<PollVote>().AnyAsync(v => v.PostId == postId && v.UserId == userId, ct);

    public Task<PollOption?> OptionAsync(long optionId, CancellationToken ct) => db.Set<PollOption>().FirstOrDefaultAsync(o => o.Id == optionId, ct);

    public void Add(PollVote vote) => db.Set<PollVote>().Add(vote);

    public void Add(ContentReport report) => db.Set<ContentReport>().Add(report);

    public Task<bool> ReportedAsync(Guid reporterId, long? postId, long? commentId, CancellationToken ct) =>
        db.Set<ContentReport>().AnyAsync(r => r.ReporterId == reporterId && r.PostId == postId && r.CommentId == commentId, ct);

    public Task<ContentReport?> ReportAsync(long id, CancellationToken ct) => db.Set<ContentReport>().FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<string>> BannedWordsAsync(CancellationToken ct) => await db.Set<BannedWord>().Select(w => w.Word).ToListAsync(ct);

    public async Task<IReadOnlyList<BannedWordDto>> BannedWordListAsync(CancellationToken ct) =>
        await db.Set<BannedWord>().OrderBy(w => w.Word).Select(w => new BannedWordDto(w.Id, w.Word, w.Language)).ToListAsync(ct);

    public Task<BannedWord?> BannedWordAsync(long id, CancellationToken ct) => db.Set<BannedWord>().FirstOrDefaultAsync(w => w.Id == id, ct);

    public Task<bool> BannedWordExistsAsync(string word, CancellationToken ct) => db.Set<BannedWord>().AnyAsync(w => w.Word == word, ct);

    public void Add(BannedWord word) => db.Set<BannedWord>().Add(word);

    public void Remove(BannedWord word) => db.Set<BannedWord>().Remove(word);

    public async Task<IReadOnlyList<MuteDto>> MutesAsync(DateTime now, CancellationToken ct) =>
        await db.Set<CommunityMember>().AsNoTracking().Where(m => m.MutedUntil > now).OrderBy(m => m.MutedUntil)
            .Select(m => new MuteDto(m.CommunityId, m.Community.Name, m.UserId, m.User.FullName, m.MutedUntil!.Value, m.MuteReason)).ToListAsync(ct);

    public async Task<PagedResult<ReportDto>> ReportsAsync(string? status, int page, int pageSize, CancellationToken ct)
    {
        var request = new PageRequest(page, pageSize);
        var wanted = Enum.TryParse<ContentReportStatus>(status, true, out var s) ? s : ContentReportStatus.Open;
        var query = db.Set<ContentReport>().AsNoTracking().Where(r => r.Status == wanted);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(r => r.CreatedAt).Skip(request.Skip).Take(request.SafePageSize).Select(r => new
        {
            r.Id, r.CommentId, PostId = r.PostId ?? r.Comment!.PostId, Community = r.PostId != null ? r.Post!.Community.Name : r.Comment!.Post.Community.Name,
            Text = r.PostId != null ? r.Post!.Text : r.Comment!.Text, Author = r.PostId != null ? r.Post!.Author.FullName : r.Comment!.Author.FullName,
            r.Reason, r.Detail, Reporter = r.Reporter.FullName, r.Status, r.CreatedAt,
        }).ToListAsync(ct);
        var items = rows.Select(r => new ReportDto(r.Id, r.CommentId is null ? "post" : "comment", r.CommentId ?? r.PostId, r.PostId, r.Community,
            r.Text.Length > 140 ? r.Text[..137] + "…" : r.Text, r.Author, r.Reason.ToString(), r.Detail, r.Reporter, r.Status.ToString(), r.CreatedAt)).ToList();
        return new PagedResult<ReportDto>(items, request.SafePage, request.SafePageSize, total);
    }
}
