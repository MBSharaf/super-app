using SuperApp.Domain.Entities.Fantasy;

namespace SuperApp.Infrastructure.Persistence.Seed.Reference;

/// <summary>
/// Seed 10: the competitions the site follows, with their data provider and league id (FPL for the English Premier
/// League with fantasy; TheSportsDB for the others, fixtures only). Clubs, players and fixtures come from the first sync;
/// public holidays from the first holidays sync.
/// </summary>
public sealed class FantasySeeder(AppDbContext db) : ISeeder
{
    private sealed record CompetitionRow(string Key, string En, string Ar, int ExternalLeagueId, string Country, decimal Budget, byte MaxPerClub,
        string Provider, bool Fantasy, string Short, string Region, string Color);

    public int Order => 10;

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        var rows = SeedFile.Read<List<CompetitionRow>>("Reference/fantasy.json").Select((c, i) => new Competition
        {
            Key = c.Key, NameEn = c.En, NameAr = c.Ar, ExternalLeagueId = c.ExternalLeagueId, Country = c.Country,
            SquadBudget = c.Budget, MaxPerClub = c.MaxPerClub, SortOrder = i + 1,
            Provider = c.Provider, FantasyEnabled = c.Fantasy, ShortName = c.Short, Region = c.Region, Color = c.Color,
        });
        var added = await db.Set<Competition>().AddMissingAsync(rows, c => c.Key, ct);
        await db.SaveChangesAsync(ct);
        return added;
    }
}
