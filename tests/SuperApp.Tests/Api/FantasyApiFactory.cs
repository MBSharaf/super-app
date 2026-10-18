using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using SuperApp.Application.Features.Football;

namespace SuperApp.Tests.Api;

/// <summary>
/// The throw-away API with the simulated football provider placed so that gameweeks 1–8 are played and gameweek 9
/// opens in two days. The first sync (both leagues) runs once per test class.
/// </summary>
public sealed class FantasyApiFactory : AccountsApiFactory
{
    private readonly SemaphoreSlim syncing = new(1, 1);
    private bool synced;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Football:Simulated:SeasonStart", DateTime.UtcNow.Date.AddDays(-56 + 2).ToString("yyyy-MM-dd"));
    }

    /// <summary>Runs the football sync as the CEO the first time it is asked.</summary>
    public async Task EnsureSyncedAsync()
    {
        await syncing.WaitAsync();
        try
        {
            if (!synced)
            {
                // Straight through the service: the first sync of two leagues can outlast an HTTP timeout while other classes run
                using var scope = Services.CreateScope();
                var results = await scope.ServiceProvider.GetRequiredService<IFootballSyncService>().SyncAsync(null, CancellationToken.None);
                results.ShouldAllBe(r => r.Skipped == null);
                synced = true;
            }
        }
        finally
        {
            syncing.Release();
        }
    }
}

/// <summary>Builds valid squads from the bootstrap data.</summary>
public static class FantasyKit
{
    private static readonly (string Position, int Count)[] Shape = [("GK", 2), ("DEF", 5), ("MID", 5), ("FWD", 3)];

    /// <summary>The cheapest valid squad (max 3 per club), skipping the given players, in GK, DEF, MID, FWD order.</summary>
    public static List<JsonElement> CheapSquad(JsonElement bootstrap, IEnumerable<long>? skip = null)
    {
        var skipped = skip?.ToHashSet() ?? [];
        var perClub = new Dictionary<long, int>();
        var squad = new List<JsonElement>();
        var players = bootstrap.GetProperty("players").EnumerateArray().OrderBy(p => p.GetProperty("price").GetDecimal()).ThenBy(p => p.GetProperty("id").GetInt64()).ToList();
        foreach (var (position, count) in Shape)
        {
            foreach (var p in players.Where(p => p.GetProperty("position").GetString() == position && !skipped.Contains(p.GetProperty("id").GetInt64())))
            {
                var club = p.GetProperty("clubId").GetInt64();
                if (squad.Count(s => s.GetProperty("position").GetString() == position) == count || perClub.GetValueOrDefault(club) == 3)
                {
                    continue;
                }
                perClub[club] = perClub.GetValueOrDefault(club) + 1;
                squad.Add(p);
            }
        }
        return squad;
    }

    public static long Id(this JsonElement player) => player.GetProperty("id").GetInt64();
}
