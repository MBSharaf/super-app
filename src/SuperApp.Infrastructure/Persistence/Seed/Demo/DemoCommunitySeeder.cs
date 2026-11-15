using Microsoft.EntityFrameworkCore;
using SuperApp.Domain.Entities.Communities;

namespace SuperApp.Infrastructure.Persistence.Seed.Demo;

/// <summary>
/// Demo 18: the 18 community posts (members, partners and Super App announcements), including the spam post the
/// moderators see reported. Game and slot cards are linked when the games phase exists.
/// </summary>
public sealed class DemoCommunitySeeder(AppDbContext db) : ISeeder
{
    private sealed record PostRow(string Community, string Author, bool AsAdmin, string Text, bool Pinned, bool Announcement, int Reactions, int Comments, DateTime At);

    public int Order => 18;
    public bool IsDemo => true;

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        if (await db.Set<Post>().IgnoreQueryFilters().AnyAsync(ct))
        {
            return 0;   // posts have no natural key: add them once, into an empty table
        }
        var users = await DemoRefs.UsersAsync(db, ct);
        var communities = await db.Set<Community>().ToDictionaryAsync(c => c.Slug, c => c.Id, ct);
        var rows = SeedFile.Read<List<PostRow>>("Demo/community.json");
        db.Set<Post>().AddRange(rows.Select(p => new Post
        {
            CommunityId = communities[p.Community], AuthorId = users[p.Author], AsSuperAppAdmin = p.AsAdmin, Text = p.Text,
            IsPinned = p.Pinned, IsAnnouncement = p.Announcement, ReactionsCount = p.Reactions, CommentsCount = p.Comments, CreatedAt = p.At,
        }));
        await db.SaveChangesAsync(ct);
        return rows.Count;
    }
}
