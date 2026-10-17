using SuperApp.Domain.Entities.Fantasy;

namespace SuperApp.Infrastructure.Persistence.Seed.Reference;

/// <summary>
/// Seed 10: the two fantasy competitions (Egyptian and English Premier League) with their API-FOOTBALL league ids.
/// Clubs, players and fixtures come from the first API-FOOTBALL sync; public holidays from the first holidays sync.
/// </summary>
public sealed class FantasySeeder(AppDbContext db) : ISeeder
{
    private sealed record CompetitionRow(string Key, string En, string Ar, int ExternalLeagueId, string Country, decimal Budget, byte MaxPerClub);

    public int Order => 10;

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        var rows = SeedFile.Read<List<CompetitionRow>>("Reference/fantasy.json").Select((c, i) => new Competition
        {
            Key = c.Key, NameEn = c.En, NameAr = c.Ar, ExternalLeagueId = c.ExternalLeagueId, Country = c.Country,
            SquadBudget = c.Budget, MaxPerClub = c.MaxPerClub, SortOrder = i + 1,
        });
        var added = await db.Set<Competition>().AddMissingAsync(rows, c => c.Key, ct);
        await db.SaveChangesAsync(ct);
        return added;
    }
}
