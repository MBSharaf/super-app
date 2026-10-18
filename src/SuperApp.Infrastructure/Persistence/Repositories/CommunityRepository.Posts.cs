using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Communities;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Communities;
using SuperApp.Domain.Entities.Content;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Post and comment lists with authors, badges, polls, my reaction and link cards.</summary>
public sealed partial class CommunityRepository
{
    private const string AdminName = "Super App Admin";

    private sealed record PostRow(long Id, string Community, Guid AuthorId, string AuthorName, string? Level, bool AsAdmin, string Text, long? ImageFileId, bool HasPoll,
        bool IsPinned, bool IsAnnouncement, bool CommentsClosed, bool IsHidden, int CommentsCount, int ReactionsCount, ReactionType? MyReaction,
        long? MyVote, DateTime CreatedAt, DateTime? UpdatedAt);

    public async Task<PagedResult<PostDto>> PostsAsync(IReadOnlyCollection<long> communityIds, PostsQuery query, Guid? viewer, bool withHidden, CancellationToken ct)
    {
        var page = new PageRequest(query.Page, query.PageSize);
        var posts = db.Set<Post>().AsNoTracking().Where(p => communityIds.Contains(p.CommunityId) && (withHidden || !p.IsHidden));
        posts = query.Filter?.ToLowerInvariant() switch
        {
            "pinned" => posts.Where(p => p.IsPinned),
            "polls" => posts.Where(p => p.HasPoll),
            _ => posts,
        };
        var ordered = query.Filter?.ToLowerInvariant() == "top"
            ? posts.OrderByDescending(p => p.ReactionsCount + p.CommentsCount).ThenByDescending(p => p.CreatedAt)
            : posts.OrderByDescending(p => p.IsPinned).ThenByDescending(p => p.CreatedAt);
        var total = await posts.CountAsync(ct);
        var rows = await Rows(ordered.Skip(page.Skip).Take(page.SafePageSize), viewer).ToListAsync(ct);
        return new PagedResult<PostDto>(await ToDtosAsync(rows, viewer, ct), page.SafePage, page.SafePageSize, total);
    }

    public async Task<PostDto?> PostDtoAsync(long id, Guid? viewer, bool withHidden, CancellationToken ct)
    {
        var rows = await Rows(db.Set<Post>().AsNoTracking().Where(p => p.Id == id && (withHidden || !p.IsHidden)), viewer).ToListAsync(ct);
        return (await ToDtosAsync(rows, viewer, ct)).FirstOrDefault();
    }

    private IQueryable<PostRow> Rows(IQueryable<Post> posts, Guid? viewer) => posts.Select(p => new PostRow(p.Id, p.Community.Slug, p.AuthorId, p.Author.FullName,
        db.Set<Seller>().Where(s => s.UserId == p.AuthorId).Select(s => s.Level.Key).FirstOrDefault(), p.AsSuperAppAdmin, p.Text, p.ImageFileId, p.HasPoll,
        p.IsPinned, p.IsAnnouncement, p.CommentsClosed, p.IsHidden, p.CommentsCount, p.ReactionsCount,
        db.Set<Reaction>().Where(r => r.PostId == p.Id && r.UserId == viewer).Select(r => (ReactionType?)r.Type).FirstOrDefault(),
        db.Set<PollVote>().Where(v => v.PostId == p.Id && v.UserId == viewer).Select(v => (long?)v.OptionId).FirstOrDefault(),
        p.CreatedAt, p.UpdatedAt));

    private async Task<List<PostDto>> ToDtosAsync(List<PostRow> rows, Guid? viewer, CancellationToken ct)
    {
        var ids = rows.Where(r => r.HasPoll).Select(r => r.Id).ToList();
        var options = (await db.Set<PollOption>().AsNoTracking().Where(o => ids.Contains(o.PostId)).OrderBy(o => o.SortOrder).ToListAsync(ct)).ToLookup(o => o.PostId);
        var links = rows.ToDictionary(r => r.Id, r => TextGuard.Links(r.Text));
        var cards = (await CardsAsync(links.Values.SelectMany(l => l).DistinctBy(l => l.Link).ToList(), ct)).ToDictionary(c => c.Link);
        return rows.Select(r => new PostDto(r.Id, r.Community, Author(r.AsAdmin, r.AuthorName, r.Level), r.AsAdmin, r.Text,
            r.ImageFileId is null ? null : $"/api/v1/files/{r.ImageFileId}",
            r.HasPoll ? new PollDto(options[r.Id].Select(o => new PollOptionDto(o.Id, o.Text, o.VotesCount)).ToList(), r.MyVote, options[r.Id].Sum(o => o.VotesCount)) : null,
            r.IsPinned, r.IsAnnouncement, r.CommentsClosed, r.IsHidden, r.CommentsCount, r.ReactionsCount, r.MyReaction?.ToString(),
            links[r.Id].Where(l => cards.ContainsKey(l.Link)).Select(l => cards[l.Link]).ToList(), r.CreatedAt, r.UpdatedAt,
            viewer is not null && r.AuthorId == viewer && !r.AsAdmin)).ToList();
    }

    /// <summary>Admins appear as "Super App Admin" with the team badge; sellers carry their level; others first name and initial.</summary>
    private static AuthorDto Author(bool asAdmin, string fullName, string? level)
    {
        if (asAdmin)
        {
            return new AuthorDto(AdminName, ["team"]);
        }
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var name = parts.Length > 1 ? $"{parts[0]} {parts[^1][0]}." : parts.FirstOrDefault() ?? "Member";
        return new AuthorDto(name, level is null ? [] : ["seller", level]);
    }

    public async Task<IReadOnlyList<CommentDto>> CommentsAsync(long postId, Guid? viewer, bool withHidden, CancellationToken ct)
    {
        var rows = await db.Set<Comment>().AsNoTracking().Where(c => c.PostId == postId && (withHidden || !c.IsHidden)).OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
            .Select(c => new
            {
                c.Id, c.ParentCommentId, c.AuthorId, c.Author.FullName, c.AsSuperAppAdmin, c.Text, c.IsHidden, c.CreatedAt,
                Level = db.Set<Seller>().Where(s => s.UserId == c.AuthorId).Select(s => s.Level.Key).FirstOrDefault(),
                Reactions = db.Set<Reaction>().Count(r => r.CommentId == c.Id),
            }).ToListAsync(ct);
        return rows.Select(c => new CommentDto(c.Id, postId, c.ParentCommentId, Author(c.AsSuperAppAdmin, c.FullName, c.Level), c.AsSuperAppAdmin, c.Text, c.IsHidden,
            c.Reactions, c.CreatedAt, viewer is not null && c.AuthorId == viewer && !c.AsSuperAppAdmin)).ToList();
    }

    public async Task<IReadOnlyList<LinkCardDto>> CardsAsync(IReadOnlyList<SuperAppLink> links, CancellationToken ct)
    {
        var cards = new List<LinkCardDto>();
        foreach (var link in links)
        {
            if (await CardAsync(link, ct) is { } card)
            {
                cards.Add(card);
            }
        }
        return cards;
    }

    private async Task<LinkCardDto?> CardAsync(SuperAppLink l, CancellationToken ct)
    {
        var id = long.TryParse(l.Id, out var n) ? n : 0;
        var slug = l.Id.StartsWith("p-", StringComparison.Ordinal) ? l.Id[2..] : l.Id;
        return l.Kind switch
        {
            "pitch" => await db.Set<Pitch>().Where(p => p.Slug == slug && p.Status == PitchStatus.Live)
                .Select(p => new LinkCardDto("pitch", l.Id, p.Name, p.Venue.Name, null, l.Link)).FirstOrDefaultAsync(ct),
            "product" => await db.Set<Product>().Where(p => p.Id == id || p.Slug == l.Id)
                .Select(p => new LinkCardDto("product", l.Id, p.Name, p.Price + " EGP", null, l.Link)).FirstOrDefaultAsync(ct),
            "design" => await db.Set<Design>().Where(d => d.Id == id).Select(d => new LinkCardDto("design", l.Id, d.Title, d.Price + " EGP", null, l.Link)).FirstOrDefaultAsync(ct),
            "store" => await db.Set<Store>().Where(s => s.Handle == l.Id || s.Id == id).Select(s => new LinkCardDto("store", l.Id, s.Name, null, null, l.Link)).FirstOrDefaultAsync(ct),
            "tournament" => await db.Set<Tournament>().Where(t => t.Id == id).Select(t => new LinkCardDto("tournament", l.Id, t.Name, t.PrizeText, null, l.Link)).FirstOrDefaultAsync(ct),
            "post" => await db.Set<Post>().Where(p => p.Id == id && !p.IsHidden)
                .Select(p => new LinkCardDto("post", l.Id, p.Community.Name, p.Text.Length > 80 ? p.Text.Substring(0, 80) : p.Text, null, l.Link)).FirstOrDefaultAsync(ct),
            "community" => await db.Set<Community>().Where(c => c.Slug == l.Id).Select(c => new LinkCardDto("community", l.Id, c.Name, c.About, null, l.Link)).FirstOrDefaultAsync(ct),
            "news" => await db.Set<NewsPost>().Where(p => p.Id == id && !p.IsHidden).Select(p => new LinkCardDto("news", l.Id, p.Title, p.Excerpt, null, l.Link)).FirstOrDefaultAsync(ct),
            _ => new LinkCardDto(l.Kind, l.Id, l.Kind == "game" ? "Pickup game" : "Free slot", null, null, l.Link),
        };
    }
}
