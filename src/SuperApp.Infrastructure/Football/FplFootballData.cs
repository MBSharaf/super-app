using System.Globalization;
using System.Text.Json;
using SuperApp.Application.Features.Football;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Football;

/// <summary>
/// The English Premier League from the public fantasy.premierleague.com API (no key): clubs, players with their prices,
/// positions and availability, every fixture with its score, and each player's numbers per match. Its BPS is used as
/// the match rating, so the bonus follows the official game. A run reads three files at most (bootstrap, fixtures,
/// one "live" file per gameweek) and keeps them for the run. Not an official developer API — it can change without
/// notice; the lawyer checks its use before launch (decisions 2026-10-27).
/// </summary>
public sealed class FplFootballData(HttpClient http) : IFootballData
{
    public const string BaseAddress = "https://fantasy.premierleague.com/api/";

    private static readonly Dictionary<string, (string Main, string Second)> Kits = new()
    {
        ["ARS"] = ("#EF0107", "#FFFFFF"), ["AVL"] = ("#670E36", "#95BFE5"), ["BOU"] = ("#DA291C", "#111111"), ["BRE"] = ("#E30613", "#FFFFFF"),
        ["BHA"] = ("#0057B8", "#FFFFFF"), ["CHE"] = ("#034694", "#FFFFFF"), ["CRY"] = ("#1B458F", "#C4122E"), ["EVE"] = ("#003399", "#FFFFFF"),
        ["FUL"] = ("#FFFFFF", "#111111"), ["LEE"] = ("#FFFFFF", "#1D428A"), ["LIV"] = ("#C8102E", "#00B2A9"), ["MCI"] = ("#6CABDD", "#1C2C5B"),
        ["MUN"] = ("#DA291C", "#FBE122"), ["NEW"] = ("#241F20", "#FFFFFF"), ["NFO"] = ("#DD0000", "#FFFFFF"), ["TOT"] = ("#FFFFFF", "#132257"),
        ["SUN"] = ("#EB172B", "#FFFFFF"), ["WHU"] = ("#7A263A", "#1BB1E7"), ["WOL"] = ("#FDB913", "#231F20"), ["IPS"] = ("#3A64A3", "#FFFFFF"),
        ["COV"] = ("#87CEEB", "#FFFFFF"), ["HUL"] = ("#F5A12D", "#111111"), ["BUR"] = ("#6C1D45", "#99D6EA"), ["LEI"] = ("#003090", "#FDBE11"),
    };

    private JsonDocument? bootstrap;
    private JsonDocument? fixtures;
    private readonly Dictionary<int, JsonDocument> live = [];

    public string Provider => "fpl";

    /// <summary>The year the season starts (its first deadline).</summary>
    public async Task<int> CurrentSeasonAsync(int leagueId, CancellationToken ct)
    {
        var events = (await BootstrapAsync(ct)).GetProperty("events");
        return DateTime.Parse(events[0].GetProperty("deadline_time").GetString()!, CultureInfo.InvariantCulture).Year;
    }

    public async Task<IReadOnlyList<FdClub>> ClubsAsync(int leagueId, int season, CancellationToken ct) =>
        (await BootstrapAsync(ct)).GetProperty("teams").EnumerateArray().Select(t =>
        {
            var code = t.GetProperty("short_name").GetString() ?? "CLB";
            var kit = Kits.GetValueOrDefault(code, ("#0D1411", "#FFFFFF"));
            return new FdClub(t.GetProperty("id").GetInt32(), t.GetProperty("name").GetString() ?? code, code, kit.Item1, kit.Item2);
        }).ToList();

    public async Task<IReadOnlyList<FdPlayer>> SquadAsync(int leagueId, int season, int clubExternalId, CancellationToken ct) =>
        (await BootstrapAsync(ct)).GetProperty("elements").EnumerateArray().Where(p => p.GetProperty("team").GetInt32() == clubExternalId)
            .Select(p => new FdPlayer(p.GetProperty("id").GetInt32(), clubExternalId, p.GetProperty("web_name").GetString() ?? "Player",
                Position(p.GetProperty("element_type").GetInt32()), p.GetProperty("now_cost").GetInt32() / 10m, Status(p.GetProperty("status").GetString())))
            .ToList();

    /// <summary>Matches without a gameweek or a kick-off time (postponed, not yet rescheduled) are left out until they get one.</summary>
    public async Task<IReadOnlyList<FdFixture>> FixturesAsync(int leagueId, int season, IReadOnlyCollection<int>? rounds, CancellationToken ct) =>
        (await FixturesDocAsync(ct)).EnumerateArray()
            .Where(f => f.GetProperty("event").ValueKind == JsonValueKind.Number && f.GetProperty("kickoff_time").ValueKind == JsonValueKind.String)
            .Select(ToFixture).Where(f => rounds is null || rounds.Contains(f.Round)).ToList();

    /// <summary>Minutes from the gameweek's live file; goals, assists, cards, saves and BPS from the fixture itself.</summary>
    public async Task<IReadOnlyList<FdPlayerStat>> FixtureStatsAsync(int leagueId, int fixtureExternalId, CancellationToken ct)
    {
        var fixture = (await FixturesDocAsync(ct)).EnumerateArray().FirstOrDefault(f => f.GetProperty("id").GetInt32() == fixtureExternalId);
        if (fixture.ValueKind != JsonValueKind.Object || fixture.GetProperty("event").ValueKind != JsonValueKind.Number)
        {
            return [];
        }
        var minutes = Minutes(await LiveAsync(fixture.GetProperty("event").GetInt32(), ct), fixtureExternalId);
        var numbers = Numbers(fixture);
        var clubs = (await BootstrapAsync(ct)).GetProperty("elements").EnumerateArray()
            .ToDictionary(p => p.GetProperty("id").GetInt32(), p => p.GetProperty("team").GetInt32());
        int N(string stat, int player) => numbers.TryGetValue((stat, player), out var v) ? v : 0;
        return minutes.Where(m => m.Value > 0 && clubs.ContainsKey(m.Key)).Select(m => new FdPlayerStat(m.Key, clubs[m.Key], m.Value,
            N("goals_scored", m.Key), N("assists", m.Key), N("saves", m.Key), N("own_goals", m.Key), N("penalties_saved", m.Key),
            N("penalties_missed", m.Key), N("yellow_cards", m.Key), N("red_cards", m.Key) > 0, N("bps", m.Key))).ToList();
    }

    private static FdFixture ToFixture(JsonElement f)
    {
        var done = f.GetProperty("finished_provisional").GetBoolean();
        var started = f.GetProperty("started").ValueKind == JsonValueKind.True;
        var status = done ? FixtureStatus.Finished : started ? FixtureStatus.Live : FixtureStatus.Scheduled;
        return new FdFixture(f.GetProperty("id").GetInt32(), f.GetProperty("event").GetInt32(), f.GetProperty("team_h").GetInt32(), f.GetProperty("team_a").GetInt32(),
            DateTime.Parse(f.GetProperty("kickoff_time").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal),
            status, Int(f, "team_h_score"), Int(f, "team_a_score"), status == FixtureStatus.Live ? Int(f, "minutes") : null);
    }

    /// <summary>Minutes each player played in this fixture (a double gameweek lists each match separately).</summary>
    private static Dictionary<int, int> Minutes(JsonElement live, int fixtureId) =>
        live.GetProperty("elements").EnumerateArray().ToDictionary(e => e.GetProperty("id").GetInt32(), e => e.GetProperty("explain").EnumerateArray()
            .Where(x => x.GetProperty("fixture").GetInt32() == fixtureId)
            .SelectMany(x => x.GetProperty("stats").EnumerateArray())
            .Where(s => s.GetProperty("identifier").GetString() == "minutes").Sum(s => s.GetProperty("value").GetInt32()));

    /// <summary>The fixture's per-player numbers: (stat, player) → value, both sides together.</summary>
    private static Dictionary<(string, int), int> Numbers(JsonElement fixture) =>
        fixture.GetProperty("stats").EnumerateArray()
            .SelectMany(s => s.GetProperty("h").EnumerateArray().Concat(s.GetProperty("a").EnumerateArray())
                .Select(v => (Key: (s.GetProperty("identifier").GetString()!, v.GetProperty("element").GetInt32()), Value: v.GetProperty("value").GetInt32())))
            .GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.Sum(x => x.Value));

    private static int? Int(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

    private static PlayerPosition Position(int elementType) => elementType switch
    {
        1 => PlayerPosition.Goalkeeper,
        2 => PlayerPosition.Defender,
        3 => PlayerPosition.Midfielder,
        _ => PlayerPosition.Forward,
    };

    /// <summary>a available, d doubtful, i injured, s suspended, u / n gone (left, on loan).</summary>
    private static PlayerStatus Status(string? code) => code switch
    {
        "a" => PlayerStatus.Available,
        "d" => PlayerStatus.Doubtful,
        "i" or "s" => PlayerStatus.Injured,
        _ => PlayerStatus.LeftTheClub,
    };

    private async Task<JsonElement> BootstrapAsync(CancellationToken ct) => (bootstrap ??= await ReadAsync("bootstrap-static/", ct)).RootElement;

    private async Task<JsonElement> FixturesDocAsync(CancellationToken ct) => (fixtures ??= await ReadAsync("fixtures/", ct)).RootElement;

    private async Task<JsonElement> LiveAsync(int gameweek, CancellationToken ct)
    {
        if (!live.TryGetValue(gameweek, out var doc))
        {
            live[gameweek] = doc = await ReadAsync($"event/{gameweek}/live/", ct);
        }
        return doc.RootElement;
    }

    private async Task<JsonDocument> ReadAsync(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(path, ct);
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(body, cancellationToken: ct);
    }
}
