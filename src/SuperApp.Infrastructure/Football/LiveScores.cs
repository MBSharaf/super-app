using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Football;

/// <summary>A match as the live feed shows it: the clock is the text to show ("57'", "45+2'", "HT", "FT").</summary>
public sealed record LiveMatch(long Id, string Home, string Away, DateTime KickoffUtc, FixtureStatus? Status, int? HomeGoals, int? AwayGoals, int? Minute, string? Clock);

/// <summary>The live clock text of each match being played, kept in memory (a restart clears it; the scores stay in the database).</summary>
public sealed class LiveClock
{
    private static readonly TimeSpan Fresh = TimeSpan.FromMinutes(4);
    private readonly ConcurrentDictionary<long, (string Text, DateTime At)> clocks = new();

    public void Set(long fixtureId, string? text, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            clocks.TryRemove(fixtureId, out _);
            return;
        }
        clocks[fixtureId] = (text, now);
    }

    public string? Get(long fixtureId, DateTime now) =>
        clocks.TryGetValue(fixtureId, out var c) && now - c.At <= Fresh ? c.Text : null;
}

/// <summary>
/// Live scores and minutes of every league for a day, from FotMob's public match list (one call covers all the
/// competitions, no key). The free fixture sources (TheSportsDB, FPL) update scores late or not at all while a match is on.
/// </summary>
public sealed class FotMobLiveFeed(HttpClient http)
{
    public const string BaseAddress = "https://www.fotmob.com/api/data/";

    public async Task<IReadOnlyList<LiveMatch>> DayAsync(DateOnly day, CancellationToken ct)
    {
        using var response = await http.GetAsync($"matches?date={day.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}", ct);
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(body, cancellationToken: ct);
        return Parse(doc.RootElement);
    }

    public static IReadOnlyList<LiveMatch> Parse(JsonElement root)
    {
        var list = new List<LiveMatch>();
        if (!root.TryGetProperty("leagues", out var leagues) || leagues.ValueKind != JsonValueKind.Array)
        {
            return list;
        }
        foreach (var match in leagues.EnumerateArray().Where(l => l.TryGetProperty("matches", out _)).SelectMany(l => l.GetProperty("matches").EnumerateArray()))
        {
            if (Read(match) is { } m)
            {
                list.Add(m);
            }
        }
        return list;
    }

    private static LiveMatch? Read(JsonElement m)
    {
        if (!m.TryGetProperty("status", out var s) || !m.TryGetProperty("home", out var home) || !m.TryGetProperty("away", out var away)
            || !DateTime.TryParse(Str(s, "utcTime"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var kickoff))
        {
            return null;
        }
        var (started, finished, cancelled) = (Bool(s, "started"), Bool(s, "finished"), Bool(s, "cancelled"));
        FixtureStatus? status = cancelled ? FixtureStatus.Cancelled : finished ? FixtureStatus.Finished : started ? FixtureStatus.Live : null;
        var clock = status switch
        {
            FixtureStatus.Live => s.TryGetProperty("liveTime", out var lt) ? Str(lt, "short") : null,
            FixtureStatus.Finished => s.TryGetProperty("reason", out var r) ? Str(r, "short") ?? "FT" : "FT",
            _ => null,
        };
        int? hs = started ? Int(home, "score") : null, aws = started ? Int(away, "score") : null;
        return new LiveMatch(m.TryGetProperty("id", out var id) && id.TryGetInt64(out var v) ? v : 0, Str(home, "name") ?? "", Str(away, "name") ?? "",
            kickoff, status, hs, aws, MinuteOf(clock), clock?.Replace("’", "'", StringComparison.Ordinal));
    }

    /// <summary>"57'" → 57, "45+2'" → 45, "HT" → 45.</summary>
    public static int? MinuteOf(string? clock)
    {
        if (string.IsNullOrWhiteSpace(clock))
        {
            return null;
        }
        if (clock.Trim().Equals("HT", StringComparison.OrdinalIgnoreCase))
        {
            return 45;
        }
        var digits = new string(clock.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, CultureInfo.InvariantCulture, out var n) ? Math.Min(n, 130) : null;
    }

    private static string? Str(JsonElement e, string name) => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static bool Bool(JsonElement e, string name) => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;

    private static int? Int(JsonElement e, string name) => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n) ? n : null;
}

/// <summary>Pairs our fixtures with the live feed's matches: kick-off close and both club names alike.</summary>
public static partial class LiveMatcher
{
    private static readonly HashSet<string> Fillers = new(StringComparer.Ordinal)
        { "fc", "sc", "ac", "afc", "cf", "club", "al", "el", "the", "de", "sporting", "sc.", "f.c.", "cd", "ss", "sv", "fk" };

    public sealed record Ours(long Id, string Home, string Away, DateTime KickoffAt);

    public static IReadOnlyDictionary<long, LiveMatch> Pair(IReadOnlyList<Ours> fixtures, IReadOnlyList<LiveMatch> feed)
    {
        var scored = from f in fixtures
                     from m in feed
                     let gap = Math.Abs((f.KickoffAt - m.KickoffUtc).TotalMinutes)
                     where gap <= 180
                     let h = Similar(f.Home, m.Home)
                     let a = Similar(f.Away, m.Away)
                     where (h > 0 && a > 0) || (gap <= 30 && h + a >= 1)
                     orderby h + a descending, gap
                     select (f.Id, m);
        var result = new Dictionary<long, LiveMatch>();
        var used = new HashSet<long>();
        foreach (var (id, m) in scored)
        {
            if (!result.ContainsKey(id) && used.Add(m.Id))
            {
                result[id] = m;
            }
        }
        return result;
    }

    /// <summary>Share of name words in common, ignoring "FC", "Al", "SC"… (1 = same words).</summary>
    public static double Similar(string a, string b)
    {
        var (x, y) = (Words(a), Words(b));
        if (x.Count == 0 || y.Count == 0)
        {
            return 0;
        }
        var common = x.Count(w => y.Any(v => v == w || (w.Length >= 5 && v.Length >= 5 && (v.StartsWith(w, StringComparison.Ordinal) || w.StartsWith(v, StringComparison.Ordinal)))));
        return (double)common / Math.Min(x.Count, y.Count);
    }

    private static List<string> Words(string name)
    {
        var plain = new string(name.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return NotLetters().Split(plain.ToLowerInvariant()).Where(w => w.Length > 1 && !Fillers.Contains(w)).Distinct().ToList();
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NotLetters();
}

/// <summary>
/// Every minute while a followed match is on (kick-off up to three hours ago, or in the next ten minutes): reads the live
/// feed, saves the score, status and minute of our matching fixtures and keeps the clock text ("HT", "45+2'") for the site.
/// Off in "Testing", with <c>Football:Offline</c>, or with <c>Football:Live:Enabled</c> false.
/// </summary>
public sealed class LiveScoresJob(IServiceScopeFactory scopes, IHostEnvironment environment, IConfiguration configuration, LiveClock clocks,
    ILogger<LiveScoresJob> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing") || configuration.GetValue("Football:Offline", false) || !configuration.GetValue("Football:Live:Enabled", true))
        {
            return;
        }
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        using var timer = new PeriodicTimer(Tick);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Live scores update failed; trying again in a minute.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow;
        var rows = await db.Set<Fixture>().Include(f => f.HomeClub).Include(f => f.AwayClub)
            .Where(f => f.Gameweek.Season.IsCurrent && f.KickoffAt >= now.AddHours(-3) && f.KickoffAt <= now.AddMinutes(10)
                && (f.Status == FixtureStatus.Scheduled || f.Status == FixtureStatus.Live))
            .ToListAsync(ct);
        if (rows.Count == 0)
        {
            return 0;
        }
        var feed = scope.ServiceProvider.GetRequiredService<FotMobLiveFeed>();
        var days = rows.SelectMany(f => new[] { DateOnly.FromDateTime(f.KickoffAt), DateOnly.FromDateTime(f.KickoffAt.AddHours(2)) }).Distinct();
        var live = new List<LiveMatch>();
        foreach (var day in days)
        {
            live.AddRange(await feed.DayAsync(day, ct));
        }
        var pairs = LiveMatcher.Pair(rows.Select(f => new LiveMatcher.Ours(f.Id, f.HomeClub.NameEn, f.AwayClub.NameEn, f.KickoffAt)).ToList(), live);
        foreach (var row in rows)
        {
            if (pairs.TryGetValue(row.Id, out var m) && m.Status is { } status)
            {
                Apply(row, m, status);
                clocks.Set(row.Id, status == FixtureStatus.Live ? m.Clock : null, now);
            }
        }
        await db.SaveChangesAsync(ct);
        return pairs.Count;
    }

    private static void Apply(Fixture row, LiveMatch m, FixtureStatus status)
    {
        row.Status = status;
        row.HomeGoals = (byte?)m.HomeGoals ?? row.HomeGoals;
        row.AwayGoals = (byte?)m.AwayGoals ?? row.AwayGoals;
        row.Minute = status == FixtureStatus.Finished ? null : (byte?)m.Minute;
    }
}
