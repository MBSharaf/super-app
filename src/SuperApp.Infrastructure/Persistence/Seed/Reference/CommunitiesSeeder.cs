using SuperApp.Domain.Entities.Communities;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Seed.Reference;

/// <summary>Seed 9 (communities): the communities the admins created on the site, with their rules, and the banned words.</summary>
public sealed class CommunitiesSeeder(AppDbContext db) : ISeeder
{
    public int Order => 9;

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        var file = ContentSeeder.Load();
        var communities = file.Communities.Select(c => new Community
        {
            Slug = c.Slug,
            Name = c.Name,
            About = c.About,
            Audience = c.Audience switch
            {
                "sellers" => CommunityAudience.Sellers,
                "sellers-pro" => CommunityAudience.ProAndEliteSellers,
                _ => CommunityAudience.Everyone,
            },
            RulesText = string.Join('\n', c.Rules),
            SlowModeSeconds = c.SlowMode,
            Color = c.Color,
            Icon = c.Icon,
        });
        var added = await db.Set<Community>().AddMissingAsync(communities, c => c.Slug, ct);
        var words = file.BannedWords.Select(w => new BannedWord { Word = w.Word, Language = w.Language });
        added += await db.Set<BannedWord>().AddMissingAsync(words, w => w.Word, ct);
        await db.SaveChangesAsync(ct);
        return added;
    }
}
