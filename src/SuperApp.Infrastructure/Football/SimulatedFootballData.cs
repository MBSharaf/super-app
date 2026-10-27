using Microsoft.Extensions.Configuration;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Features.Football;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Football;

/// <summary>
/// The offline football provider, used when no API-FOOTBALL key is set (development, tests, the demo). It plays a
/// whole season for the Egyptian and English leagues: one gameweek a week from the season start, matches on Friday,
/// Saturday and Sunday; matches that started are live, matches that ended have a score and player statistics.
/// Everything is generated from fixed seeds, so every call returns the same data for the same moment.
/// Season start: setting <c>Football:Simulated:SeasonStart</c> (yyyy-MM-dd), else the second Friday of August.
/// </summary>
public sealed class SimulatedFootballData(IClock clock, IConfiguration configuration) : IFootballData
{
    private static readonly string[] Positions = ["GK", "DEF", "MID", "FWD"];
    private static readonly int[] Shape = [2, 5, 5, 3];
    private static readonly decimal[] BasePrice = [4.0m, 4.0m, 4.5m, 4.5m];

    public string Provider => "simulated";

    public Task<int> CurrentSeasonAsync(int leagueId, CancellationToken ct) =>
        Task.FromResult(clock.UtcNow.Month >= 7 ? clock.UtcNow.Year : clock.UtcNow.Year - 1);

    public Task<IReadOnlyList<FdClub>> ClubsAsync(int leagueId, int season, CancellationToken ct)
    {
        var league = League(leagueId);
        IReadOnlyList<FdClub> clubs = league.Clubs.Select((c, i) => new FdClub(ClubId(league, i), c.Name, c.Code, c.Kit, c.Kit2)).ToList();
        return Task.FromResult(clubs);
    }

    public Task<IReadOnlyList<FdPlayer>> SquadAsync(int leagueId, int season, int clubExternalId, CancellationToken ct)
    {
        var league = League(leagueId);
        var index = clubExternalId - league.ExternalBase - 1;
        IReadOnlyList<FdPlayer> squad = index >= 0 && index < league.Clubs.Count ? Squad(league, index) : [];
        return Task.FromResult(squad);
    }

    public Task<IReadOnlyList<FdFixture>> FixturesAsync(int leagueId, int season, IReadOnlyCollection<int>? rounds, CancellationToken ct)
    {
        var league = League(leagueId);
        var start = SeasonStart(season);
        IReadOnlyList<FdFixture> fixtures = Rounds(league).SelectMany((pairs, r) => pairs.Select((pair, k) =>
            Fixture(league, r + 1, k, pair.Home, pair.Away, start))).Where(f => rounds is null || rounds.Contains(f.Round)).ToList();
        return Task.FromResult(fixtures);
    }

    public Task<IReadOnlyList<FdPlayerStat>> FixtureStatsAsync(int leagueId, int fixtureExternalId, CancellationToken ct)
    {
        var league = League(leagueId);
        var (round, slot) = FixtureKey(league, fixtureExternalId);
        var pair = Rounds(league)[round - 1][slot];
        var kickoff = Kickoff(SeasonStart(clock.UtcNow.Month >= 7 ? clock.UtcNow.Year : clock.UtcNow.Year - 1), round, slot);
        IReadOnlyList<FdPlayerStat> stats = clock.UtcNow < kickoff.AddMinutes(115) ? [] : MatchStats(league, fixtureExternalId, pair.Home, pair.Away);
        return Task.FromResult(stats);
    }

    /* ---------------- Squads ---------------- */

    private static int ClubId(SimLeague league, int index) => league.ExternalBase + index + 1;

    private static List<FdPlayer> Squad(SimLeague league, int clubIndex)
    {
        var club = league.Clubs[clubIndex];
        var clubId = ClubId(league, clubIndex);
        var random = new SeededRandom(club.Code + league.LeagueId);
        var stars = league.Stars.TryGetValue(club.Code, out var s) ? s : [];
        var squad = new List<FdPlayer>();
        for (var p = 0; p < Positions.Length; p++)
        {
            var own = stars.Where(x => x.Position == Positions[p]).ToList();
            for (var n = 0; n < Shape[p]; n++)
            {
                var (name, price) = n < own.Count ? (own[n].Name, own[n].Price) : Generated(league, club, p, random);
                var id = clubId * 100 + squad.Count + 1;
                squad.Add(new FdPlayer(id, clubId, name, (PlayerPosition)p, price, Status(id)));
            }
        }
        return squad;
    }

    private static (string Name, decimal Price) Generated(SimLeague league, SimClub club, int position, SeededRandom random)
    {
        var lift = (club.Strength - 60) / 32m;
        var price = Math.Round((BasePrice[position] + (decimal)random.Next() * 1.6m + lift * 1.2m) * 2, MidpointRounding.AwayFromZero) / 2;
        var name = $"{league.FirstNames[random.Below(league.FirstNames.Length)]} {league.LastNames[random.Below(league.LastNames.Length)]}";
        return (name, Math.Min(price, position == 0 ? 5.0m : 6.5m));
    }

    private static PlayerStatus Status(int playerId)
    {
        var roll = new SeededRandom("status" + playerId).Next();
        return roll < 0.04 ? PlayerStatus.Injured : roll < 0.09 ? PlayerStatus.Doubtful : PlayerStatus.Available;
    }

    /* ---------------- Calendar ---------------- */

    private DateTime SeasonStart(int season)
    {
        if (DateTime.TryParse(configuration["Football:Simulated:SeasonStart"], out var configured))
        {
            return DateTime.SpecifyKind(configured.Date, DateTimeKind.Utc);
        }
        var august = new DateTime(season, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var firstFriday = august.AddDays(((int)DayOfWeek.Friday - (int)august.DayOfWeek + 7) % 7);
        return firstFriday.AddDays(7);
    }

    /// <summary>Gameweek r plays from the season start + (r − 1) weeks: three matches a day from Friday, at 15:00, 17:00 and 19:00 UTC.</summary>
    private static DateTime Kickoff(DateTime start, int round, int slot) =>
        start.AddDays((round - 1) * 7 + slot / 3).AddHours(15 + slot % 3 * 2);

    /// <summary>Double round robin (circle method), as the site's demo data builds it.</summary>
    private static List<List<(int Home, int Away)>> Rounds(SimLeague league)
    {
        var ids = Enumerable.Range(0, league.Clubs.Count).ToList();
        var rounds = new List<List<(int, int)>>();
        for (var r = 0; r < ids.Count - 1; r++)
        {
            var pairs = new List<(int, int)>();
            for (var i = 0; i < ids.Count / 2; i++)
            {
                var (a, b) = (ids[i], ids[ids.Count - 1 - i]);
                pairs.Add(r % 2 == 0 ? (a, b) : (b, a));
            }
            rounds.Add(pairs);
            ids.Insert(1, ids[^1]);
            ids.RemoveAt(ids.Count - 1);
        }
        var second = rounds.Select(rd => rd.Select(p => (p.Item2, p.Item1)).ToList()).ToList();
        return rounds.Concat(second).Take(league.Gameweeks).ToList();
    }

    private static int FixtureId(SimLeague league, int round, int slot) => league.ExternalBase + 50_000 + round * 100 + slot;

    private static (int Round, int Slot) FixtureKey(SimLeague league, int fixtureId)
    {
        var rest = fixtureId - league.ExternalBase - 50_000;
        return (rest / 100, rest % 100);
    }

    private FdFixture Fixture(SimLeague league, int round, int slot, int home, int away, DateTime start)
    {
        var id = FixtureId(league, round, slot);
        var kickoff = Kickoff(start, round, slot);
        var now = clock.UtcNow;
        var homeId = ClubId(league, home);
        var awayId = ClubId(league, away);
        if (now < kickoff)
        {
            return new FdFixture(id, round, homeId, awayId, kickoff, FixtureStatus.Scheduled, null, null, null);
        }
        var (homeGoals, awayGoals) = Score(MatchStats(league, id, home, away), homeId);
        var minute = (int)Math.Min(90, (now - kickoff).TotalMinutes);
        if (now < kickoff.AddMinutes(115))
        {
            var share = minute / 90m;
            return new FdFixture(id, round, homeId, awayId, kickoff, FixtureStatus.Live, (int)(homeGoals * share), (int)(awayGoals * share), Math.Max(1, minute));
        }
        return new FdFixture(id, round, homeId, awayId, kickoff, FixtureStatus.Finished, homeGoals, awayGoals, null);
    }

    private static (int Home, int Away) Score(IReadOnlyList<FdPlayerStat> stats, int homeId) =>
        (stats.Where(s => s.ClubExternalId == homeId).Sum(s => s.Goals) + stats.Where(s => s.ClubExternalId != homeId).Sum(s => s.OwnGoals),
         stats.Where(s => s.ClubExternalId != homeId).Sum(s => s.Goals) + stats.Where(s => s.ClubExternalId == homeId).Sum(s => s.OwnGoals));

    /* ---------------- Match statistics ---------------- */

    private static List<FdPlayerStat> MatchStats(SimLeague league, int fixtureId, int home, int away) =>
        [.. SideStats(league, fixtureId, home, away), .. SideStats(league, fixtureId, away, home)];

    /// <summary>One club's players in a match: who played, goals, assists, saves and cards from fixed seeds.</summary>
    private static IEnumerable<FdPlayerStat> SideStats(SimLeague league, int fixtureId, int club, int opponent)
    {
        var edge = (league.Clubs[club].Strength - league.Clubs[opponent].Strength) / 100m;
        foreach (var player in Squad(league, club))
        {
            var r = new SeededRandom($"{player.ExternalId}|{fixtureId}");
            var quality = (player.Price - 4) / 6;
            if ((decimal)r.Next() >= 0.55m + quality * 0.4m)
            {
                continue;
            }
            yield return Performance(player, r, quality, edge);
        }
    }

    private static FdPlayerStat Performance(FdPlayer player, SeededRandom r, decimal quality, decimal edge)
    {
        var position = (int)player.Position;
        var minutes = r.Next() < 0.8 ? 90 : 30 + r.Below(50);
        var goalRate = new[] { 0m, 0.06m, 0.16m, 0.3m }[position] * (0.6m + quality * 1.4m) * (1 + edge);
        var assistRate = new[] { 0.01m, 0.07m, 0.17m, 0.12m }[position] * (0.6m + quality * 1.2m);
        var goals = (decimal)r.Next() < goalRate ? (r.Next() < 0.18 ? 2 : 1) : 0;
        var assists = (decimal)r.Next() < assistRate ? 1 : 0;
        var saves = position == 0 ? r.Below(6) : 0;
        var yellow = r.Next() < 0.12 ? 1 : 0;
        var red = r.Next() < 0.01;
        var ownGoal = r.Next() < 0.01 ? 1 : 0;
        var rating = Math.Round(6.0m + (decimal)r.Next() * 1.5m + goals * 0.8m + assists * 0.5m + saves * 0.1m, 1);
        return new FdPlayerStat(player.ExternalId, player.ClubExternalId, minutes, goals, assists, saves, ownGoal, 0, 0, yellow, red, rating);
    }

    private static SimLeague League(int leagueId) =>
        SimulatedLeagues.Find(leagueId) ?? throw new InvalidOperationException($"The simulated provider has no league {leagueId}.");
}

/// <summary>A small repeatable random generator (mulberry32) seeded from a text.</summary>
internal sealed class SeededRandom(string seed)
{
    private uint state = Hash(seed);

    public double Next()
    {
        unchecked
        {
            state += 0x6D2B79F5;
            var t = state;
            t = (t ^ (t >> 15)) * (t | 1);
            t ^= t + (t ^ (t >> 7)) * (t | 61);
            return ((t ^ (t >> 14)) & 0xFFFFFFFF) / 4294967296.0;
        }
    }

    public int Below(int max) => (int)(Next() * max);

    private static uint Hash(string text)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var ch in text)
            {
                hash = (hash ^ ch) * 16777619u;
            }
            return hash;
        }
    }
}
