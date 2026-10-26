using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Tournaments;

/// <summary>
/// Match days, the draw, results and the table. Match days are weekly from the first one; on each day the matches run one
/// after the other from the start hour. Knockout: round r (1-based) has teams / 2^r matches. League: round robin (circle
/// method), floor(teams / 2) matches per round.
/// </summary>
public static class TournamentRules
{
    public const int RegistrationClosesDaysBefore = 2;

    /// <summary>Matches per round, round 1 first.</summary>
    public static IReadOnlyList<int> Rounds(TournamentType type, int teams)
    {
        if (type == TournamentType.KnockoutCup)
        {
            var list = new List<int>();
            for (var m = teams / 2; m >= 1; m /= 2)
            {
                list.Add(m);
            }
            return list;
        }
        var rounds = teams % 2 == 0 ? teams - 1 : teams;
        return Enumerable.Repeat(teams / 2, rounds).ToList();
    }

    public static DateOnly DayOf(DateOnly startsOn, int round) => startsOn.AddDays(7 * (round - 1));

    public static int Hour(Tournament t) => int.Parse(t.MatchHours, System.Globalization.CultureInfo.InvariantCulture);

    public static DateOnly RegistrationClosesOn(Tournament t) => t.StartsOn.AddDays(-RegistrationClosesDaysBefore);

    public static IEnumerable<TournamentTeam> Active(Tournament t) => t.Teams.Where(x => x.Status == TournamentTeamStatus.Registered);

    /// <summary>League pairs per round for these teams (circle method; home and away swap every other round).</summary>
    public static IReadOnlyList<IReadOnlyList<(long Home, long Away)>> RoundRobin(IReadOnlyList<long> ids)
    {
        var list = ids.Select(i => (long?)i).ToList();
        if (list.Count % 2 == 1)
        {
            list.Add(null);
        }
        var n = list.Count;
        var rounds = new List<IReadOnlyList<(long, long)>>();
        for (var r = 0; r < n - 1; r++)
        {
            var pairs = new List<(long, long)>();
            for (var i = 0; i < n / 2; i++)
            {
                if (list[i] is { } a && list[n - 1 - i] is { } b)
                {
                    pairs.Add(r % 2 == 1 ? (b, a) : (a, b));
                }
            }
            rounds.Add(pairs);
            var last = list[^1];
            list.RemoveAt(n - 1);
            list.Insert(1, last);
        }
        return rounds;
    }

    /// <summary>Sets a result; a knockout winner moves into the next round (slot / 2, home when the slot is even).</summary>
    public static void SetResult(Tournament t, TournamentMatch m, int slot, MatchResultRequest r, Func<TournamentMatch, int> slotOf)
    {
        if (m.HomeTeamId is null || m.AwayTeamId is null)
        {
            throw new BusinessRuleException("The teams for this match are not known yet.", "teams_unknown");
        }
        var knockout = t.Type == TournamentType.KnockoutCup;
        var pens = knockout && r.HomeGoals == r.AwayGoals ? r.PensWinnerId : null;
        if (knockout && r.HomeGoals == r.AwayGoals && pens != m.HomeTeamId && pens != m.AwayTeamId)
        {
            throw new BusinessRuleException("It's a draw — choose who won on penalties.", "pens_winner");
        }
        var next = knockout ? t.Matches.FirstOrDefault(x => x.Round == m.Round + 1 && slotOf(x) == slot / 2) : null;
        if (next?.HomeGoals is not null)
        {
            throw new BusinessRuleException("The next round is already played — this result can't change.", "later_round_played");
        }
        (m.HomeGoals, m.AwayGoals) = ((byte)r.HomeGoals, (byte)r.AwayGoals);
        (m.HomePens, m.AwayPens) = pens is null ? ((byte?)null, (byte?)null) : pens == m.HomeTeamId ? ((byte?)1, (byte?)0) : (0, 1);
        m.WinnerTeamId = r.HomeGoals > r.AwayGoals ? m.HomeTeamId : r.HomeGoals < r.AwayGoals ? m.AwayTeamId : pens;
        if (next is not null)
        {
            if (slot % 2 == 0) { next.HomeTeamId = m.WinnerTeamId; } else { next.AwayTeamId = m.WinnerTeamId; }
        }
        Finish(t);
    }

    /// <summary>Finished when the final (knockout) or every match (league) is played; the champion is set.</summary>
    private static void Finish(Tournament t)
    {
        if (t.Type == TournamentType.KnockoutCup)
        {
            var final = t.Matches.OrderByDescending(x => x.Round).First();
            if (final.WinnerTeamId is { } champion)
            {
                (t.Status, t.ChampionTeamId) = (TournamentStatus.Finished, champion);
            }
            return;
        }
        if (t.Matches.All(x => x.HomeGoals is not null))
        {
            var table = Table(t);
            (t.Status, t.ChampionTeamId) = (TournamentStatus.Finished, table.Count > 0 ? table[0].TeamId : null);
        }
    }

    /// <summary>League table: 3 points a win, then goal difference, goals scored, name.</summary>
    public static IReadOnlyList<StandingDto> Table(Tournament t)
    {
        var rows = Active(t).ToDictionary(x => x.Id, x => new int[7]);
        foreach (var m in t.Matches.Where(x => x.HomeGoals is not null && x.HomeTeamId is not null && x.AwayTeamId is not null))
        {
            if (!rows.TryGetValue(m.HomeTeamId!.Value, out var h) || !rows.TryGetValue(m.AwayTeamId!.Value, out var a))
            {
                continue;
            }
            int hg = m.HomeGoals!.Value, ag = m.AwayGoals!.Value;
            h[0]++; a[0]++; h[4] += hg; h[5] += ag; a[4] += ag; a[5] += hg;
            if (hg > ag) { h[1]++; a[3]++; h[6] += 3; } else if (hg < ag) { a[1]++; h[3]++; a[6] += 3; } else { h[2]++; a[2]++; h[6]++; a[6]++; }
        }
        var names = t.Teams.ToDictionary(x => x.Id, x => x.Name);
        return rows.Select(kv => new StandingDto(kv.Key, names[kv.Key], kv.Value[0], kv.Value[1], kv.Value[2], kv.Value[3], kv.Value[4], kv.Value[5], kv.Value[6]))
            .OrderByDescending(s => s.Points).ThenByDescending(s => s.GoalsFor - s.GoalsAgainst).ThenByDescending(s => s.GoalsFor).ThenBy(s => s.Name).ToList();
    }
}
