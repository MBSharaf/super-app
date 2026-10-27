using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Features.Football;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Football;

/// <summary>
/// Writes each API-FOOTBALL call to ops.IntegrationUsage (calls, errors, quota used / limit from the response headers).
/// Uses its own database scope so a call never saves someone else's half-done changes.
/// </summary>
public sealed class FootballUsageRecorder(IServiceScopeFactory scopes, IClock clock)
{
    public const string IntegrationKey = "football.apifootball";

    public async Task RecordAsync(int? remaining, int? limit, bool failed, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var integration = await db.Set<Integration>().FirstOrDefaultAsync(i => i.Key == IntegrationKey, ct);
        if (integration is null)
        {
            return;
        }
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var row = await db.Set<IntegrationUsage>().FirstOrDefaultAsync(u => u.IntegrationId == integration.Id && u.Day == today, ct);
        if (row is null)
        {
            row = new IntegrationUsage { IntegrationId = integration.Id, Day = today };
            db.Set<IntegrationUsage>().Add(row);
        }
        row.Calls++;
        row.Errors += failed ? 1 : 0;
        row.QuotaLimit = limit ?? row.QuotaLimit ?? integration.DailyQuota;
        row.QuotaUsed = limit is not null && remaining is not null ? limit - remaining : row.Calls;
        integration.LastCallAt = clock.UtcNow;
        if (failed)
        {
            integration.LastErrorAt = clock.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Today's API-FOOTBALL usage against the daily limit: green below 70%, yellow from 70%, red from 90% (INTEGRATIONS §7.2).</summary>
public sealed class FootballQuota(AppDbContext db, IClock clock, Microsoft.Extensions.Configuration.IConfiguration configuration) : IFootballQuota
{
    public async Task<FootballUsage> TodayAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var integration = await db.Set<Integration>().AsNoTracking().FirstOrDefaultAsync(i => i.Key == FootballUsageRecorder.IntegrationKey, ct);
        var row = integration is null ? null : await db.Set<IntegrationUsage>().AsNoTracking()
            .FirstOrDefaultAsync(u => u.IntegrationId == integration.Id && u.Day == today, ct);
        var limit = row?.QuotaLimit ?? integration?.DailyQuota ?? 100;
        var used = row?.QuotaUsed ?? row?.Calls ?? 0;
        var percent = limit <= 0 ? 0 : (int)Math.Min(100, Math.Round(used * 100m / limit));
        var level = percent >= 90 ? "red" : percent >= 70 ? "yellow" : "green";
        var message = !HasKey ? "Free sources (FPL, TheSportsDB) — no API-FOOTBALL key, no daily quota."
            : percent >= 90 ? "Upgrade the API-FOOTBALL plan." : null;
        return new FootballUsage(HasKey ? "api-football" : "fpl + thesportsdb", used, limit, percent, level, message);
    }

    private bool HasKey => !string.IsNullOrWhiteSpace(configuration["Football:ApiFootball:Key"]);

    public async Task<bool> CanCallAsync(int calls, CancellationToken ct)
    {
        if (!HasKey)
        {
            return true;
        }
        var usage = await TodayAsync(ct);
        return usage.Used + calls <= usage.Limit;
    }
}
