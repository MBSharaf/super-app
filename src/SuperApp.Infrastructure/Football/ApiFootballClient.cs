using System.Globalization;
using System.Text.Json;
using SuperApp.Application.Features.Football;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Football;

/// <summary>
/// API-FOOTBALL v3 (api-sports.io). Used only when <c>FOOTBALL__APIFOOTBALL__KEY</c> is set in .env; the key never leaves
/// the server. Each call's quota headers (requests used / daily limit) go to ops.IntegrationUsage for the CEO usage bar.
/// Fantasy prices are not part of the provider's data: new players start at a price by position.
/// </summary>
public sealed class ApiFootballClient(HttpClient http, FootballUsageRecorder usage) : IFootballData
{
    public const string BaseAddress = "https://v3.football.api-sports.io/";

    public string Provider => "api-football";

    public async Task<int> CurrentSeasonAsync(int leagueId, CancellationToken ct)
    {
        var data = await GetAsync($"leagues?id={leagueId}&current=true", ct);
        foreach (var league in data.EnumerateArray())
        {
            foreach (var season in league.GetProperty("seasons").EnumerateArray().Where(s => s.GetProperty("current").GetBoolean()))
            {
                return season.GetProperty("year").GetInt32();
            }
        }
        throw new InvalidOperationException($"API-FOOTBALL has no current season for league {leagueId}.");
    }

    public async Task<IReadOnlyList<FdClub>> ClubsAsync(int leagueId, int season, CancellationToken ct)
    {
        var data = await GetAsync($"teams?league={leagueId}&season={season}", ct);
        return data.EnumerateArray().Select(t => t.GetProperty("team")).Select(t => new FdClub(
            t.GetProperty("id").GetInt32(), t.GetProperty("name").GetString() ?? "Club",
            ShortName(t), "#0D1411", "#FFFFFF")).ToList();
    }

    public async Task<IReadOnlyList<FdPlayer>> SquadAsync(int leagueId, int season, int clubExternalId, CancellationToken ct)
    {
        var data = await GetAsync($"players/squads?team={clubExternalId}", ct);
        return data.EnumerateArray().SelectMany(s => s.GetProperty("players").EnumerateArray()).Select(p =>
        {
            var position = Position(p.GetProperty("position").GetString());
            return new FdPlayer(p.GetProperty("id").GetInt32(), clubExternalId, p.GetProperty("name").GetString() ?? "Player",
                position, StartPrice(position), PlayerStatus.Available);
        }).ToList();
    }

    public async Task<IReadOnlyList<FdFixture>> FixturesAsync(int leagueId, int season, CancellationToken ct)
    {
        var data = await GetAsync($"fixtures?league={leagueId}&season={season}", ct);
        return data.EnumerateArray().Select(ToFixture).Where(f => f.Round > 0).ToList();
    }

    public async Task<IReadOnlyList<FdPlayerStat>> FixtureStatsAsync(int leagueId, int fixtureExternalId, CancellationToken ct)
    {
        var data = await GetAsync($"fixtures/players?fixture={fixtureExternalId}", ct);
        return data.EnumerateArray().SelectMany(team =>
        {
            var clubId = team.GetProperty("team").GetProperty("id").GetInt32();
            return team.GetProperty("players").EnumerateArray().Select(p => ToStat(p, clubId));
        }).Where(s => s.Minutes > 0).ToList();
    }

    private async Task<JsonElement> GetAsync(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(path, ct);
        await usage.RecordAsync(Header(response, "x-ratelimit-requests-remaining"), Header(response, "x-ratelimit-requests-limit"), !response.IsSuccessStatusCode, ct);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return json.RootElement.GetProperty("response").Clone();
    }

    private static int? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) && int.TryParse(values.FirstOrDefault(), out var n) ? n : null;

    private static FdFixture ToFixture(JsonElement f)
    {
        var fixture = f.GetProperty("fixture");
        var status = fixture.GetProperty("status");
        var round = f.GetProperty("league").GetProperty("round").GetString() ?? string.Empty;
        var number = int.TryParse(round[(round.LastIndexOf('-') + 1)..].Trim(), out var n) ? n : 0;
        var goals = f.GetProperty("goals");
        return new FdFixture(fixture.GetProperty("id").GetInt32(), number,
            f.GetProperty("teams").GetProperty("home").GetProperty("id").GetInt32(), f.GetProperty("teams").GetProperty("away").GetProperty("id").GetInt32(),
            DateTime.Parse(fixture.GetProperty("date").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal),
            Status(status.GetProperty("short").GetString()), Int(goals, "home"), Int(goals, "away"), Int(status, "elapsed"));
    }

    private static FdPlayerStat ToStat(JsonElement p, int clubId)
    {
        var s = p.GetProperty("statistics")[0];
        var rating = s.GetProperty("games").TryGetProperty("rating", out var r) && r.ValueKind == JsonValueKind.String
            && decimal.TryParse(r.GetString(), CultureInfo.InvariantCulture, out var value) ? value : 0;
        var goals = s.GetProperty("goals");
        return new FdPlayerStat(p.GetProperty("player").GetProperty("id").GetInt32(), clubId, Int(s.GetProperty("games"), "minutes") ?? 0,
            Int(goals, "total") ?? 0, Int(goals, "assists") ?? 0, Int(goals, "saves") ?? 0, 0,
            Int(s.GetProperty("penalty"), "saved") ?? 0, Int(s.GetProperty("penalty"), "missed") ?? 0,
            Int(s.GetProperty("cards"), "yellow") ?? 0, (Int(s.GetProperty("cards"), "red") ?? 0) > 0, rating);
    }

    private static int? Int(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

    private static string ShortName(JsonElement team)
    {
        var code = team.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
        var name = team.GetProperty("name").GetString() ?? "CLB";
        return (code ?? new string(name.Where(char.IsLetter).Take(3).ToArray())).ToUpperInvariant();
    }

    private static PlayerPosition Position(string? value) => value switch
    {
        "Goalkeeper" => PlayerPosition.Goalkeeper,
        "Defender" => PlayerPosition.Defender,
        "Midfielder" => PlayerPosition.Midfielder,
        _ => PlayerPosition.Forward,
    };

    private static decimal StartPrice(PlayerPosition position) => position switch
    {
        PlayerPosition.Goalkeeper or PlayerPosition.Defender => 4.5m,
        PlayerPosition.Midfielder => 5.5m,
        _ => 6.5m,
    };

    private static FixtureStatus Status(string? code) => code switch
    {
        "1H" or "HT" or "2H" or "ET" or "BT" or "P" or "LIVE" or "INT" or "SUSP" => FixtureStatus.Live,
        "FT" or "AET" or "PEN" => FixtureStatus.Finished,
        "PST" => FixtureStatus.Postponed,
        "CANC" or "ABD" or "AWD" or "WO" => FixtureStatus.Cancelled,
        _ => FixtureStatus.Scheduled,
    };
}
