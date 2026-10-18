using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Football;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Fantasy;

/// <summary>Admin fantasy tools (API §7.2, permission fantasy.manage): force a sync, re-score a gameweek, the quota bar.</summary>
public interface IFantasyAdminService
{
    Task<IReadOnlyList<SyncResult>> SyncAsync(string? competition, CancellationToken ct);

    Task<int> RescoreAsync(string competition, int gameweek, CancellationToken ct);

    Task<FootballUsage> UsageAsync(CancellationToken ct);
}

public sealed class FantasyAdminService(
    IFootballSyncService sync,
    IFantasyScoringRepository scoringRepository,
    IFantasyScoringService scoring,
    IFootballQuota quota,
    IAuditLogger audit) : IFantasyAdminService
{
    public async Task<IReadOnlyList<SyncResult>> SyncAsync(string? competition, CancellationToken ct)
    {
        var results = await sync.SyncAsync(string.IsNullOrWhiteSpace(competition) ? null : competition.ToUpperInvariant(), ct);
        if (results.Count == 0)
        {
            throw new NotFoundException("No league with that key.", "no_competition");
        }
        await audit.LogAsync(AuditSystem.Admin, "fantasy.synced", $"Football data synced: {string.Join(", ", results.Select(r => r.Competition))}",
            "Competition", competition, ct: ct);
        return results;
    }

    public async Task<int> RescoreAsync(string competition, int gameweek, CancellationToken ct)
    {
        var id = await scoringRepository.GameweekIdAsync(competition.ToUpperInvariant(), gameweek, ct)
            ?? throw new NotFoundException("Gameweek not found.");
        var info = await scoringRepository.GameweekAsync(id, ct);
        if (info is not { Over: true })
        {
            throw new BusinessRuleException("This gameweek still has matches to play.", "not_finished");
        }
        await audit.LogAsync(AuditSystem.Admin, "fantasy.rescore", $"Re-score of {competition.ToUpperInvariant()} gameweek {gameweek} asked", "Gameweek", id.ToString(), ct: ct);
        return await scoring.ScoreAsync(id, ct);
    }

    public Task<FootballUsage> UsageAsync(CancellationToken ct) => quota.TodayAsync(ct);
}
