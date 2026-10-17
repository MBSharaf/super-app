using Microsoft.EntityFrameworkCore;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Seed.Demo;

/// <summary>Demo 17: the 3 tournaments (finished cup, running league, cup open for registration) with their registered teams.</summary>
public sealed class DemoTournamentsSeeder(AppDbContext db) : ISeeder
{
    private sealed record TeamRow(string Name, string Captain, string Phone);
    private sealed record TournamentRow(string Name, string Partner, string Pitch, string Type, byte TeamCount, decimal Fee, string? Prize,
        DateOnly StartsOn, DateOnly EndsOn, string Hours, string? Rules, string Status, List<TeamRow> Teams);

    public int Order => 17;
    public bool IsDemo => true;

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        var existing = await db.Set<Tournament>().Select(t => t.Name).ToListAsync(ct);
        var rows = SeedFile.Read<List<TournamentRow>>("Demo/tournaments.json").Where(t => !existing.Contains(t.Name)).ToList();
        var users = await DemoRefs.UsersAsync(db, ct);
        foreach (var row in rows)
        {
            db.Set<Tournament>().Add(new Tournament
            {
                PartnerId = await db.Set<Partner>().Where(p => p.UserId == users[row.Partner]).Select(p => p.Id).SingleAsync(ct),
                PitchId = await db.Set<Pitch>().Where(p => p.Slug == row.Pitch).Select(p => p.Id).SingleAsync(ct),
                Name = row.Name, Type = DemoRefs.Parse<TournamentType>(row.Type), TeamCount = row.TeamCount, EntryFee = row.Fee, PrizeText = row.Prize,
                StartsOn = row.StartsOn, EndsOn = row.EndsOn, MatchHours = row.Hours, RulesText = row.Rules, Status = DemoRefs.Parse<TournamentStatus>(row.Status),
                Teams = row.Teams.Select(t => new TournamentTeam { Name = t.Name, CaptainId = users[t.Captain], Phone = t.Phone, Status = TournamentTeamStatus.Registered }).ToList(),
            });
        }
        await db.SaveChangesAsync(ct);
        return rows.Count;
    }
}
