using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Features.Content;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Football;

/// <summary>
/// League tables and match facts (statistics, events, line-ups) from the live feed, cached in memory: a table for five
/// minutes, a match's facts for 45 seconds while it is on and longer once it is over. Our competition and match are
/// found in the feed by pairing (see <see cref="LiveMatcher"/>); a known league id is the fallback.
/// </summary>
public sealed class MatchInsights(AppDbContext db, FotMobLiveFeed feed, FotMobIds ids, IMemoryCache cache, IClock clock, ILogger<MatchInsights> logger)
    : IMatchInsights
{
    /// <summary>The feed's league ids of our competitions, used when no match of theirs has been paired yet.</summary>
    private static readonly Dictionary<string, int> KnownLeagues = new(StringComparer.Ordinal)
    {
        ["epl"] = 47, ["egy"] = 519, ["ucl"] = 42, ["laliga"] = 87, ["seriea"] = 55, ["bundesliga"] = 54, ["ligue1"] = 53,
        ["superlig"] = 71, ["uel"] = 73, ["uecl"] = 10216, ["afcon"] = 289, ["euro"] = 50, ["wc"] = 77,
        ["cafcl"] = 526, ["cafcc"] = 9468,
    };

    public async Task<StandingsDto?> StandingsAsync(string competition, CancellationToken ct)
    {
        if (!await db.Set<Competition>().AnyAsync(c => c.Key == competition && c.IsActive, ct))
        {
            return null;
        }
        return await cache.GetOrCreateAsync($"fm:table:{competition}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            try
            {
                var league = await LeagueIdAsync(competition, ct);
                if (league is null)
                {
                    return new StandingsDto(competition, null, [], []);
                }
                using var doc = await feed.LeagueAsync(league.Value, ct);
                return FotMobParse.Standings(competition, doc.RootElement);
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "League table of {Competition} could not be read", competition);
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return new StandingsDto(competition, null, [], []);
            }
        });
    }

    public async Task<MatchFactsDto?> FactsAsync(long matchId, CancellationToken ct)
    {
        var f = await db.Set<Fixture>().AsNoTracking().Where(x => x.Id == matchId)
            .Select(x => new { x.Id, x.Status, x.KickoffAt, Home = x.HomeClub.NameEn, Away = x.AwayClub.NameEn, Competition = x.Gameweek.Season.Competition.Key })
            .FirstOrDefaultAsync(ct);
        if (f is null)
        {
            return null;
        }
        var empty = new MatchFactsDto([], [], null, null);
        if (f.Status == FixtureStatus.Scheduled && f.KickoffAt > clock.UtcNow.AddMinutes(90))
        {
            return empty;   // line-ups come about an hour before kick-off
        }
        try
        {
            var fm = ids.Match(f.Id) ?? await FindMatchAsync(new LiveMatcher.Ours(f.Id, f.Home, f.Away, f.KickoffAt), f.Competition, ct);
            if (fm is null)
            {
                return empty;
            }
            return await cache.GetOrCreateAsync($"fm:facts:{fm}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = f.Status switch
                {
                    FixtureStatus.Finished when f.KickoffAt < clock.UtcNow.AddHours(-3) => TimeSpan.FromHours(6),
                    FixtureStatus.Finished => TimeSpan.FromMinutes(10),
                    FixtureStatus.Live => TimeSpan.FromSeconds(45),
                    _ => TimeSpan.FromMinutes(3),
                };
                using var doc = await feed.MatchAsync(fm.Value, ct);
                return FotMobParse.Facts(doc.RootElement);
            }) ?? empty;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Facts of match {Match} could not be read", matchId);
            return empty;
        }
    }

    /// <summary>The known id of the main competition, else the league learnt from a paired match, else found by pairing one of its matches near today.</summary>
    private async Task<int?> LeagueIdAsync(string competition, CancellationToken ct)
    {
        // The known main competition wins: a learned id can be a qualifying stage (CAF has its own "Qualification" league)
        if (KnownLeagues.TryGetValue(competition, out var main))
        {
            return main;
        }
        if (ids.League(competition) is { } known)
        {
            return known;
        }
        var now = clock.UtcNow;
        var near = await db.Set<Fixture>().AsNoTracking()
            .Where(x => x.Gameweek.Season.IsCurrent && x.Gameweek.Season.Competition.Key == competition && x.KickoffAt > now.AddDays(-10) && x.KickoffAt < now.AddDays(10))
            .OrderBy(x => x.KickoffAt < now ? EF.Functions.DateDiffMinute(x.KickoffAt, now) : EF.Functions.DateDiffMinute(now, x.KickoffAt))
            .Select(x => new LiveMatcher.Ours(x.Id, x.HomeClub.NameEn, x.AwayClub.NameEn, x.KickoffAt)).FirstOrDefaultAsync(ct);
        if (near is not null)
        {
            await FindMatchAsync(near, competition, ct);
        }
        return ids.League(competition);
    }

    /// <summary>Pairs a match of ours with the feed's list of its day (cached two minutes) and remembers the ids.</summary>
    private async Task<long?> FindMatchAsync(LiveMatcher.Ours ours, string competition, CancellationToken ct)
    {
        var list = new List<LiveMatch>();
        foreach (var day in new[] { DateOnly.FromDateTime(ours.KickoffAt), DateOnly.FromDateTime(ours.KickoffAt.AddHours(2)) }.Distinct())
        {
            list.AddRange(await cache.GetOrCreateAsync($"fm:day:{day:yyyyMMdd}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
                return await feed.DayAsync(day, ct);
            }) ?? []);
        }
        if (!LiveMatcher.Pair([ours], list).TryGetValue(ours.Id, out var m))
        {
            return null;
        }
        ids.Remember(ours.Id, competition, m);
        return m.Id;
    }
}

/// <summary>Reads the feed's league and match pages into our shapes.</summary>
public static class FotMobParse
{
    private const string LogoBase = "https://images.fotmob.com/image_resources/logo/teamlogo/";

    public static StandingsDto Standings(string competition, JsonElement root)
    {
        var groups = new List<StandingsGroupDto>();
        var legend = new List<StandingsLegendDto>();
        string? season = null;
        if (root.TryGetProperty("table", out var tables) && tables.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in tables.EnumerateArray().Where(t => t.TryGetProperty("data", out _)))
            {
                var data = t.GetProperty("data");
                season ??= Str(data, "selectedSeason");
                if (data.TryGetProperty("table", out var single))
                {
                    groups.Add(new StandingsGroupDto(null, Rows(single)));
                }
                if (data.TryGetProperty("tables", out var many) && many.ValueKind == JsonValueKind.Array)
                {
                    groups.AddRange(many.EnumerateArray().Where(g => g.TryGetProperty("table", out _))
                        .Select(g => new StandingsGroupDto(Str(g, "leagueName"), Rows(g.GetProperty("table")))));
                }
                if (data.TryGetProperty("legend", out var lg) && lg.ValueKind == JsonValueKind.Array)
                {
                    legend.AddRange(lg.EnumerateArray().Select(l => new StandingsLegendDto(Str(l, "title") ?? "", Str(l, "color") ?? "")).Where(l => l.Title.Length > 0));
                }
            }
        }
        return new StandingsDto(competition, season, groups.Where(g => g.Rows.Count > 0).ToList(), legend.DistinctBy(l => l.Title).ToList());
    }

    private static List<StandingRowDto> Rows(JsonElement table)
    {
        if (!table.TryGetProperty("all", out var all) || all.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        return all.EnumerateArray().Select(r =>
        {
            var score = (Str(r, "scoresStr") ?? "0-0").Split('-');
            int Part(int i) => score.Length > i && int.TryParse(score[i], CultureInfo.InvariantCulture, out var n) ? n : 0;
            var id = Int(r, "id");
            return new StandingRowDto(Int(r, "idx") ?? 0, Str(r, "name") ?? "", id is null ? null : $"{LogoBase}{id}_small.png",
                Int(r, "played") ?? 0, Int(r, "wins") ?? 0, Int(r, "draws") ?? 0, Int(r, "losses") ?? 0, Part(0), Part(1), Int(r, "goalConDiff") ?? Part(0) - Part(1),
                Int(r, "pts") ?? 0, Str(r, "qualColor"));
        }).ToList();
    }

    public static MatchFactsDto Facts(JsonElement root)
    {
        if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object)
        {
            return new MatchFactsDto([], [], null, null);
        }
        MatchLineupDto? home = null, away = null;
        if (content.TryGetProperty("lineup", out var lineup) && lineup.ValueKind == JsonValueKind.Object)
        {
            home = lineup.TryGetProperty("homeTeam", out var h) ? Lineup(h) : null;
            away = lineup.TryGetProperty("awayTeam", out var a) ? Lineup(a) : null;
        }
        return new MatchFactsDto(Stats(content), Events(content), home, away);
    }

    private static List<MatchStatGroupDto> Stats(JsonElement content)
    {
        if (!content.TryGetProperty("stats", out var stats) || stats.ValueKind != JsonValueKind.Object
            || !stats.TryGetProperty("Periods", out var periods) || !periods.TryGetProperty("All", out var all) || !all.TryGetProperty("stats", out var groups))
        {
            return [];
        }
        return groups.EnumerateArray().Select(g => new MatchStatGroupDto(Str(g, "title") ?? "",
                g.TryGetProperty("stats", out var items) && items.ValueKind == JsonValueKind.Array
                    ? items.EnumerateArray().Where(i => Str(i, "type") != "title" && i.TryGetProperty("stats", out var v) && v.ValueKind == JsonValueKind.Array && v.GetArrayLength() == 2)
                        .Select(i =>
                        {
                            var v = i.GetProperty("stats");
                            var (h, a) = (Text(v[0]), Text(v[1]));
                            return new MatchStatDto(Str(i, "key") ?? "", Str(i, "title") ?? "", h, a, Lead(h) is not null && Lead(a) is not null);
                        }).ToList()
                    : []))
            .Where(g => g.Items.Count > 0).ToList();
    }

    private static List<MatchEventDto> Events(JsonElement content)
    {
        if (!content.TryGetProperty("matchFacts", out var facts) || facts.ValueKind != JsonValueKind.Object
            || !facts.TryGetProperty("events", out var holder) || !holder.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        var list = new List<MatchEventDto>();
        var (homeGoals, awayGoals) = (0, 0);   // the score after each goal, counted here (the feed's is the score before it)
        foreach (var e in events.EnumerateArray())
        {
            var type = Str(e, "type");
            var side = e.TryGetProperty("isHome", out var ih) && ih.ValueKind == JsonValueKind.True ? "home" : "away";
            var minute = Int(e, "time") ?? 0;
            int? added = Int(e, "overloadTime") is > 0 and var o ? o : null;
            var player = e.TryGetProperty("player", out var p) && p.ValueKind == JsonValueKind.Object ? Str(p, "name") : null;
            switch (type)
            {
                case "Goal":
                    var detail = e.TryGetProperty("ownGoal", out var og) && og.ValueKind == JsonValueKind.True ? "Own goal"
                        : Str(e, "goalDescriptionKey") == "penalty" || Str(e, "suffix") == "Pen" ? "Penalty" : null;
                    (homeGoals, awayGoals) = side == "home" ? (homeGoals + 1, awayGoals) : (homeGoals, awayGoals + 1);
                    list.Add(new MatchEventDto(minute, added, "goal", side, player ?? Str(e, "nameStr"), detail, null, null, homeGoals, awayGoals));
                    break;
                case "Card":
                    list.Add(new MatchEventDto(minute, added, "card", side, player ?? Str(e, "nameStr"), Str(e, "card") is "Red" or "YellowRed" ? "Red" : "Yellow", null, null, null, null));
                    break;
                case "Substitution" when e.TryGetProperty("swap", out var swap) && swap.ValueKind == JsonValueKind.Array && swap.GetArrayLength() == 2:
                    list.Add(new MatchEventDto(minute, added, "sub", side, null, null, Str(swap[0], "name"), Str(swap[1], "name"), null, null));
                    break;
            }
        }
        return list;
    }

    private static MatchLineupDto? Lineup(JsonElement team)
    {
        if (team.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        List<LineupPlayerDto> Players(string name) => team.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array
            ? arr.EnumerateArray().Select(p => new LineupPlayerDto(Str(p, "name") ?? "", Str(p, "shirtNumber"),
                p.TryGetProperty("performance", out var perf) && perf.ValueKind == JsonValueKind.Object && perf.TryGetProperty("rating", out var r) ? Text(r) : null)).ToList()
            : [];
        var coach = team.TryGetProperty("coach", out var c)
            ? c.ValueKind == JsonValueKind.Object ? Str(c, "name") : c.ValueKind == JsonValueKind.Array && c.GetArrayLength() > 0 ? Str(c[0], "name") : null
            : null;
        var starters = Players("starters");
        return starters.Count == 0 ? null : new MatchLineupDto(Str(team, "formation"), coach, starters, Players("subs"));
    }

    /// <summary>The leading number of a shown value ("245 (83%)" → 245).</summary>
    public static double? Lead(string text)
    {
        var digits = new string(text.TakeWhile(ch => char.IsDigit(ch) || ch == '.').ToArray());
        return double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static string Text(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => v.GetString() ?? "",
        JsonValueKind.Number => v.GetRawText(),
        _ => "",
    };

    private static string? Str(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static int? Int(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p)
        ? p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n) ? n : p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), CultureInfo.InvariantCulture, out var s) ? s : null
        : null;
}
