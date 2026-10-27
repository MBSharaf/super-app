using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SuperApp.Application.Features.Football;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Football;

/// <summary>
/// Fixtures and results of every other competition from TheSportsDB (free key <c>Football:TheSportsDb:Key</c>, default
/// "123"). Read round by round (<c>eventsround</c>; cups also use 32 playoff, 16 last 16, 125 quarter-final, 150 semi-final,
/// 200 final), only around the next match: the free key allows about ten calls a minute, so a whole season of thirteen
/// competitions would take hours. The free key gives no squads or player numbers, and its team lists are wrong, so
/// clubs come from the matches; their short names and colours are made from the names. Calls are spaced and a refused
/// call is tried again. Times are UTC.
/// </summary>
public sealed class TheSportsDbFootballData(HttpClient http, ILogger<TheSportsDbFootballData> logger) : IFootballData
{
    public const string BaseAddress = "https://www.thesportsdb.com/api/v1/json/";

    private static readonly int[] CupRounds = [32, 16, 125, 150, 200];
    private static readonly string[] Palette = ["#0B6E4F", "#C8102E", "#1F4E9C", "#F2C230", "#6C1D45", "#0098DB", "#111111", "#E85D04", "#2D5DA8", "#7A1F2B"];
    private static readonly HashSet<string> Fillers = new(StringComparer.OrdinalIgnoreCase) { "FC", "AC", "AS", "SC", "CF", "CD", "SV", "SS", "SSC", "US", "Al", "El", "The", "de", "of", "club" };
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly TimeSpan Spacing = TimeSpan.FromMilliseconds(6500);
    private const int MaxLeagueRounds = 46;
    private static DateTime lastCall = DateTime.MinValue;

    private readonly Dictionary<int, string> seasons = [];
    private readonly Dictionary<int, Dictionary<int, FdClub>> clubs = [];

    public string Provider => "thesportsdb";

    /// <summary>The league's current season, "2026-2027" (→ 2026) or "2026".</summary>
    public async Task<int> CurrentSeasonAsync(int leagueId, CancellationToken ct)
    {
        var leagues = await GetAsync($"lookupleague.php?id={leagueId}", "leagues", ct);
        var league = leagues.Count > 0 ? leagues[0] : default;
        var label = league.ValueKind == JsonValueKind.Object ? league.GetProperty("strCurrentSeason").GetString() : null;
        if (string.IsNullOrWhiteSpace(label) || !int.TryParse(label[..4], CultureInfo.InvariantCulture, out var year))
        {
            throw new HttpRequestException($"TheSportsDB has no current season for league {leagueId}.");
        }
        seasons[leagueId] = label;
        return year;
    }

    /// <summary>The clubs seen in the matches read by <see cref="FixturesAsync"/> in this run.</summary>
    public Task<IReadOnlyList<FdClub>> ClubsAsync(int leagueId, int season, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<FdClub>>(clubs.TryGetValue(leagueId, out var seen) ? seen.Values.ToList() : []);

    public Task<IReadOnlyList<FdPlayer>> SquadAsync(int leagueId, int season, int clubExternalId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<FdPlayer>>([]);

    public Task<IReadOnlyList<FdPlayerStat>> FixtureStatsAsync(int leagueId, int fixtureExternalId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<FdPlayerStat>>([]);

    /// <summary>
    /// The given rounds, or the rounds around the next match (two before, three after); a cup's knockout rounds are added
    /// once its next match is one of them (or round 16 / 32, which in a league are plain matchdays).
    /// </summary>
    public async Task<IReadOnlyList<FdFixture>> FixturesAsync(int leagueId, int season, IReadOnlyCollection<int>? rounds, CancellationToken ct)
    {
        var label = seasons.GetValueOrDefault(leagueId) ?? season.ToString(CultureInfo.InvariantCulture);
        var wanted = rounds ?? AroundRounds(await CurrentRoundAsync(leagueId, ct));
        var found = new Dictionary<int, FdFixture>();
        foreach (var r in wanted)
        {
            await ReadRoundAsync(leagueId, label, r, found, ct);
        }
        return found.Values.ToList();
    }

    private static List<int> AroundRounds(int current)
    {
        var plain = Enumerable.Range(current - 2, 6).Where(r => r is >= 1 and <= MaxLeagueRounds);
        return current > MaxLeagueRounds || current is 16 or 32 ? plain.Concat(CupRounds).Distinct().ToList() : plain.ToList();
    }

    /// <summary>The round of the league's next match, else of its last one (between seasons), else 1.</summary>
    private async Task<int> CurrentRoundAsync(int leagueId, CancellationToken ct)
    {
        foreach (var endpoint in new[] { "eventsnextleague", "eventspastleague" })
        {
            var events = await GetAsync($"{endpoint}.php?id={leagueId}", "events", ct);
            if (events.Count > 0 && Int(events[0], "intRound") is > 0 and var round)
            {
                return round;
            }
        }
        return 1;
    }

    private async Task<int> ReadRoundAsync(int leagueId, string season, int round, Dictionary<int, FdFixture> found, CancellationToken ct)
    {
        var events = await GetAsync($"eventsround.php?id={leagueId}&r={round}&s={Uri.EscapeDataString(season)}", "events", ct);
        var count = 0;
        foreach (var e in events.Where(e => e.ValueKind == JsonValueKind.Object))
        {
            if (ToFixture(e, round, leagueId) is { } fixture)
            {
                found[fixture.ExternalId] = fixture;
                count++;
            }
        }
        return count;
    }

    private FdFixture? ToFixture(JsonElement e, int round, int leagueId)
    {
        var (id, home, away) = (Int(e, "idEvent"), Int(e, "idHomeTeam"), Int(e, "idAwayTeam"));
        if (id is null || home is null || away is null || home == away || Kickoff(e) is not { } kickoff)
        {
            return null;
        }
        Remember(leagueId, home.Value, Text(e, "strHomeTeam"));
        Remember(leagueId, away.Value, Text(e, "strAwayTeam"));
        var (hs, aws) = (Int(e, "intHomeScore"), Int(e, "intAwayScore"));
        return new FdFixture(id.Value, round, home.Value, away.Value, kickoff, Status(Text(e, "strStatus"), hs, kickoff), hs, aws, null, Text(e, "strVenue"));
    }

    private void Remember(int leagueId, int id, string? name)
    {
        var seen = clubs.TryGetValue(leagueId, out var list) ? list : clubs[leagueId] = [];
        if (!seen.ContainsKey(id) && !string.IsNullOrWhiteSpace(name))
        {
            var (main, second) = Colours(name);
            seen[id] = new FdClub(id, name.Trim(), ShortName(name), main, second);
        }
    }

    /// <summary>"Manchester United" → MUN, "Al Ahly" → AHL, "Real Madrid" → RMA.</summary>
    public static string ShortName(string name)
    {
        var words = name.Split([' ', '-', '.', '\''], StringSplitOptions.RemoveEmptyEntries).Where(w => !Fillers.Contains(w) && w.Any(char.IsLetter)).ToList();
        var letters = words.Count >= 2 ? $"{words[0][0]}{new string(words[1].Where(char.IsLetter).Take(2).ToArray())}"
            : new string((words.FirstOrDefault() ?? name).Where(char.IsLetter).Take(3).ToArray());
        return letters.ToUpperInvariant();
    }

    private static (string, string) Colours(string name)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        return (Palette[hash[0] % Palette.Length], hash[1] % 2 == 0 ? "#FFFFFF" : "#111111");
    }

    private static DateTime? Kickoff(JsonElement e)
    {
        var stamp = Text(e, "strTimestamp") ?? (Text(e, "dateEvent") is { } day ? $"{day}T{Text(e, "strTime") ?? "00:00:00"}" : null);
        return DateTime.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at) ? at : null;
    }

    private static FixtureStatus Status(string? status, int? homeGoals, DateTime kickoff) => status?.Trim().ToUpperInvariant() switch
    {
        "MATCH FINISHED" or "FT" or "AET" or "PEN" or "AP" => FixtureStatus.Finished,
        "1H" or "2H" or "HT" or "ET" or "LIVE" or "IN PROGRESS" or "BT" or "P" => FixtureStatus.Live,
        "POSTPONED" or "PST" or "MATCH POSTPONED" => FixtureStatus.Postponed,
        "CANCELLED" or "CANC" or "ABANDONED" or "ABD" or "MATCH CANCELLED" => FixtureStatus.Cancelled,
        _ => homeGoals is not null && kickoff < DateTime.UtcNow.AddHours(-3) ? FixtureStatus.Finished : FixtureStatus.Scheduled,
    };

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString() : null;

    private static int? Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.Number ? v.TryGetInt32(out var n)
            : int.TryParse(v.GetString(), CultureInfo.InvariantCulture, out n)) ? n : null;

    /// <summary>One call at a time, at least 2.2 s apart (shared by every run); a 429 waits and tries again (3 times).</summary>
    private async Task<IReadOnlyList<JsonElement>> GetAsync(string path, string list, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            await Gate.WaitAsync(ct);
            HttpResponseMessage response;
            try
            {
                var wait = lastCall + Spacing - DateTime.UtcNow;
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, ct);
                }
                response = await http.GetAsync(path, ct);
                lastCall = DateTime.UtcNow;
            }
            finally
            {
                Gate.Release();
            }
            using (response)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt <= 3)
                {
                    var seconds = attempt * 10;
                    if (logger.IsEnabled(LogLevel.Information))
                    {
                        logger.LogInformation("TheSportsDB asked to slow down; waiting {Seconds} s", seconds);
                    }
                    await Task.Delay(TimeSpan.FromSeconds(seconds), ct);
                    continue;
                }
                response.EnsureSuccessStatusCode();
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                return doc.RootElement.TryGetProperty(list, out var items) && items.ValueKind == JsonValueKind.Array
                    ? items.EnumerateArray().Select(i => i.Clone()).ToList() : [];
            }
        }
    }
}
