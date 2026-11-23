using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Fantasy;
using SuperApp.Domain.Entities.Fantasy;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>
/// Copies teams' picks into a gameweek that has none yet (their latest earlier squad, with what they paid). A free hit
/// lasts one gameweek: the squad from before it comes back, and the bank its transfers changed is put back.
/// </summary>
internal static class PickCarry
{
    public static async Task<List<Pick>> CarryAsync(AppDbContext db, IReadOnlyCollection<long> teamIds, long gameweekId, int number, CancellationToken ct)
    {
        var earlier = await db.Set<Pick>().AsNoTracking().Where(p => teamIds.Contains(p.TeamId) && p.Gameweek.Number < number)
            .Select(p => new { p.TeamId, p.GameweekId, p.Gameweek.Number, p.PlayerId, p.Slot, p.IsCaptain, p.IsViceCaptain, p.PurchasePrice }).ToListAsync(ct);
        var freeHits = (await db.Set<TeamGameweekPoint>().AsNoTracking()
            .Where(r => teamIds.Contains(r.TeamId) && r.ChipPlayed == FantasyScoring.FreeHitChip && r.Gameweek.Number < number)
            .Select(r => new { r.TeamId, r.GameweekId }).ToListAsync(ct)).Select(r => (r.TeamId, r.GameweekId)).ToHashSet();
        var fresh = new List<Pick>();
        var undo = new List<(long TeamId, long GameweekId)>();
        foreach (var team in earlier.GroupBy(p => p.TeamId))
        {
            var latest = team.Max(p => p.Number);
            var latestId = team.First(p => p.Number == latest).GameweekId;
            var source = team.Where(p => !freeHits.Contains((p.TeamId, p.GameweekId))).ToList();
            if (freeHits.Contains((team.Key, latestId)) && source.Count > 0)
            {
                latest = source.Max(p => p.Number);
                undo.Add((team.Key, latestId));
            }
            else
            {
                source = [.. team];
            }
            fresh.AddRange(source.Where(p => p.Number == latest).Select(p => new Pick
            {
                TeamId = p.TeamId, GameweekId = gameweekId, PlayerId = p.PlayerId, Slot = p.Slot, IsCaptain = p.IsCaptain,
                IsViceCaptain = p.IsViceCaptain, PurchasePrice = p.PurchasePrice,
            }));
        }
        await UndoFreeHitBankAsync(db, undo, ct);
        db.Set<Pick>().AddRange(fresh);
        return fresh;
    }

    /// <summary>Takes back what the free hit's transfers did to each bank (sold for − bought for).</summary>
    private static async Task UndoFreeHitBankAsync(AppDbContext db, List<(long TeamId, long GameweekId)> undo, CancellationToken ct)
    {
        if (undo.Count == 0)
        {
            return;
        }
        var teamIds = undo.Select(u => u.TeamId).ToList();
        var gameweekIds = undo.Select(u => u.GameweekId).ToList();
        var moves = await db.Set<Transfer>().AsNoTracking().Where(t => teamIds.Contains(t.TeamId) && gameweekIds.Contains(t.GameweekId))
            .Select(t => new { t.TeamId, t.GameweekId, t.PriceOut, t.PriceIn }).ToListAsync(ct);
        var teams = await db.Set<FantasyTeam>().Where(t => teamIds.Contains(t.Id)).ToListAsync(ct);
        foreach (var team in teams)
        {
            var week = undo.First(u => u.TeamId == team.Id).GameweekId;
            team.Bank -= moves.Where(m => m.TeamId == team.Id && m.GameweekId == week).Sum(m => m.PriceOut - m.PriceIn);
        }
    }
}
