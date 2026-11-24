using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using SuperApp.Application.Features.Football;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Football;

/// <summary>
/// Fixtures and results of the leagues and cups without fantasy, from FotMob's league page (<c>leagues?id=</c>): the
/// whole season in one call, with every rescheduled match, the group stages of the African cups and the knockout rounds.
/// TheSportsDB's free key returns at most a few dozen matches per call, so cups and moved matches went missing.
/// FotMob's ids are stored negative (club −8456, match −4813325) so they can never meet the ids of the other sources.
/// Rounds use the site's codes: league rounds and group matchdays as numbers, 101/102 preliminary rounds, 32 play-off,
/// 33 round of 32, 16 round of 16, 125 quarter-final, 150 semi-final, 190 third place, 200 final.
/// </summary>
public sealed partial class FotMobFootballData(FotMobLiveFeed feed) : IFootballData
{
    public const string Name = "fotmob";
    private const string LogoBase = "https://images.fotmob.com/image_resources/logo/teamlogo/";
    private static readonly string[] Palette = ["#0B6E4F", "#C8102E", "#1F4E9C", "#F2C230", "#6C1D45", "#0098DB", "#111111", "#E85D04", "#2D5DA8", "#7A1F2B"];

    private readonly Dictionary<int, (int Season, List<FdFixture> Fixtures, Dictionary<int, FdClub> Clubs)> read = [];

    public string Provider => Name;

    public async Task<int> CurrentSeasonAsync(int leagueId, CancellationToken ct) => (await ReadAsync(leagueId, ct)).Season;

    public async Task<IReadOnlyList<FdClub>> ClubsAsync(int leagueId, int season, CancellationToken ct) =>
        (await ReadAsync(leagueId, ct)).Clubs.Values.ToList();

    public Task<IReadOnlyList<FdPlayer>> SquadAsync(int leagueId, int season, int clubExternalId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<FdPlayer>>([]);

    public Task<IReadOnlyList<FdPlayerStat>> FixtureStatsAsync(int leagueId, int fixtureExternalId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<FdPlayerStat>>([]);

    /// <summary>The whole season (one call), whatever rounds are asked: a moved match keeps its round but changes day.</summary>
    public async Task<IReadOnlyList<FdFixture>> FixturesAsync(int leagueId, int season, IReadOnlyCollection<int>? rounds, CancellationToken ct) =>
        (await ReadAsync(leagueId, ct)).Fixtures;

    private async Task<(int Season, List<FdFixture> Fixtures, Dictionary<int, FdClub> Clubs)> ReadAsync(int leagueId, CancellationToken ct)
    {
        if (read.TryGetValue(leagueId, out var done))
        {
            return done;
        }
        using var doc = await feed.LeagueAsync(leagueId, ct);
        var result = Parse(doc.RootElement);
        read[leagueId] = result;
        return result;
    }

    /// <summary>The league page → its season ("2026/2027" → 2026), matches and clubs.</summary>
    public static (int Season, List<FdFixture> Fixtures, Dictionary<int, FdClub> Clubs) Parse(JsonElement root)
    {
        var label = root.TryGetProperty("details", out var d) ? Str(d, "selectedSeason") : null;
        if (label is null || label.Length < 4 || !int.TryParse(label[..4], CultureInfo.InvariantCulture, out var season))
        {
            throw new HttpRequestException("FotMob has no current season for this league.");
        }
        var (fixtures, clubs) = (new List<FdFixture>(), new Dictionary<int, FdClub>());
        if (root.TryGetProperty("fixtures", out var f) && f.TryGetProperty("allMatches", out var all) && all.ValueKind == JsonValueKind.Array)
        {
            foreach (var m in all.EnumerateArray())
            {
                if (ToFixture(m, clubs) is { } fixture)
                {
                    fixtures.Add(fixture);
                }
            }
        }
        return (season, fixtures, clubs);
    }

    private static FdFixture? ToFixture(JsonElement m, Dictionary<int, FdClub> clubs)
    {
        if (!m.TryGetProperty("home", out var home) || !m.TryGetProperty("away", out var away) || !m.TryGetProperty("status", out var s)
            || Id(m) is not { } id || Id(home) is not { } h || Id(away) is not { } a || h == a || Placeholder(home) || Placeholder(away)
            || RoundCode(Str(m, "round"), Str(m, "roundName")) is not { } round
            || !DateTime.TryParse(Str(s, "utcTime"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var kickoff))
        {
            return null;
        }
        Remember(clubs, h, Str(home, "name"));
        Remember(clubs, a, Str(away, "name"));
        var (status, hs, aws) = StateOf(s);
        return new FdFixture(-id, round, -h, -a, kickoff, status, hs, aws, null);
    }

    private static (FixtureStatus, int?, int?) StateOf(JsonElement s)
    {
        var reason = s.TryGetProperty("reason", out var r) ? Str(r, "short") ?? Str(r, "long") : null;
        if (Bool(s, "cancelled"))
        {
            return (reason is not null && reason.StartsWith("P", StringComparison.OrdinalIgnoreCase) ? FixtureStatus.Postponed : FixtureStatus.Cancelled, null, null);
        }
        if (!Bool(s, "started"))
        {
            return (reason is "PP" or "Postp." ? FixtureStatus.Postponed : FixtureStatus.Scheduled, null, null);
        }
        var score = ScoreRegex().Match(Str(s, "scoreStr") ?? string.Empty);
        int? hs = score.Success ? int.Parse(score.Groups[1].Value, CultureInfo.InvariantCulture) : null;
        int? aws = score.Success ? int.Parse(score.Groups[2].Value, CultureInfo.InvariantCulture) : null;
        return (Bool(s, "finished") ? FixtureStatus.Finished : FixtureStatus.Live, hs, aws);
    }

    /// <summary>FotMob's round ("14", "Group Stage - Round 3", "1/8", "final", "1st Preliminary Round") → the site's code.</summary>
    public static int? RoundCode(string? round, string? roundName)
    {
        var text = (round ?? roundName ?? string.Empty).Trim();
        if (int.TryParse(text, CultureInfo.InvariantCulture, out var n))
        {
            return n is >= 1 and <= 99 ? n : null;
        }
        var lower = text.ToLowerInvariant();
        var code = lower switch
        {
            "playoff" or "play-off" or "knockout round play-offs" => 32,
            "1/16" => 33,
            "1/8" => 16,
            "1/4" => 125,
            "1/2" => 150,
            "bronze" => 190,
            "final" => 200,
            _ => (int?)null,
        };
        if (code is not null)
        {
            return code;
        }
        var number = DigitsRegex().Match(lower);
        if (!number.Success || !int.TryParse(number.Value, CultureInfo.InvariantCulture, out var k))
        {
            return null;
        }
        if (lower.Contains("preliminary", StringComparison.Ordinal) || lower.Contains("qualif", StringComparison.Ordinal))
        {
            return k is >= 1 and <= 9 ? 100 + k : null;
        }
        return k is >= 1 and <= 99 ? k : null;   // "Group Stage - Round 3", "Matchday 3"
    }

    /// <summary>A team not known yet ("TBC", "1A", "3ADEF", "Winner QF 1"): FotMob gives it a tiny id; such matches wait until the draw.</summary>
    private static bool Placeholder(JsonElement team) =>
        Id(team) is < 100 || PlaceholderRegex().IsMatch(Str(team, "name") ?? "TBC");

    private static void Remember(Dictionary<int, FdClub> clubs, int id, string? name)
    {
        if (clubs.ContainsKey(-id) || string.IsNullOrWhiteSpace(name))
        {
            return;
        }
        var main = Palette[(int)((uint)id % (uint)Palette.Length)];
        clubs[-id] = new FdClub(-id, name.Trim(), TheSportsDbFootballData.ShortName(name), main, id % 2 == 0 ? "#FFFFFF" : "#111111",
            $"{LogoBase}{id.ToString(CultureInfo.InvariantCulture)}_small.png");
    }

    private static int? Id(JsonElement e) =>
        e.TryGetProperty("id", out var v) && (v.ValueKind == JsonValueKind.Number ? v.TryGetInt32(out var n)
            : int.TryParse(v.GetString(), CultureInfo.InvariantCulture, out n)) && n > 0 ? n : null;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString()
        : e.TryGetProperty(name, out v) && v.ValueKind == JsonValueKind.Number ? v.GetRawText() : null;

    private static bool Bool(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    [GeneratedRegex(@"(\d+)\s*-\s*(\d+)")]
    private static partial Regex ScoreRegex();

    [GeneratedRegex(@"^(TBC|TBD|\d[A-Z]+|[A-Z]\d|[WL]\d+|(Winner|Loser) .+)$", RegexOptions.IgnoreCase)]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitsRegex();
}
