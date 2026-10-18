using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Fantasy;

/// <summary>One player's numbers in one match, as the scoring rules need them.</summary>
public sealed record StatLine(
    int Minutes,
    int Goals,
    int Assists,
    int Saves,
    int OwnGoals,
    int PenaltiesSaved,
    int PenaltiesMissed,
    int YellowCards,
    bool RedCard,
    bool CleanSheet,
    int Bonus);

/// <summary>A pick going into the team score: slot 1–11 starts, 12–15 is the bench in order (12 = the goalkeeper).</summary>
public sealed record ScoringPick(long PlayerId, int Slot, PlayerPosition Position, bool IsCaptain, bool IsViceCaptain);

/// <summary>A player's total in the gameweek (two matches in a double gameweek are added together).</summary>
public sealed record PlayerGameweek(int Minutes, int Points);

/// <summary>The result for one pick: multiplier 0 = did not count, 1 = counted, 2 = captain, 3 = triple captain; <c>Points</c> = base × multiplier.</summary>
public sealed record ScoredPick(long PlayerId, int Slot, int Multiplier, int BasePoints, int Points);

public sealed record TeamScore(int Total, IReadOnlyList<ScoredPick> Picks, long? CaptainUsed);

/// <summary>
/// The fantasy scoring rules (PRD FR-FAN-04), pure so they are unit-tested on their own. They match the rules table the
/// site shows: minutes 1 / 2, goals 6 (GK, DEF) / 5 (MID) / 4 (FWD), assist 3, clean sheet 4 (GK, DEF) / 1 (MID),
/// every 3 saves 1, penalty save 5, penalty miss −2, yellow −1, red −3, own goal −2, bonus 1–3.
/// </summary>
public static class FantasyScoring
{
    public const string WildcardChip = "wildcard";
    public const string BenchBoostChip = "benchboost";
    public const string TripleCaptainChip = "triplecaptain";

    public static int PlayerPoints(PlayerPosition position, StatLine s)
    {
        if (s.Minutes <= 0)
        {
            return 0;
        }
        var points = s.Minutes >= 60 ? 2 : 1;
        points += s.Goals * GoalPoints(position) + s.Assists * 3;
        if (s.CleanSheet && s.Minutes >= 60)
        {
            points += position is PlayerPosition.Goalkeeper or PlayerPosition.Defender ? 4 : position == PlayerPosition.Midfielder ? 1 : 0;
        }
        if (position == PlayerPosition.Goalkeeper)
        {
            points += s.Saves / 3;
        }
        points += s.PenaltiesSaved * 5 - s.PenaltiesMissed * 2 - s.YellowCards - (s.RedCard ? 3 : 0) - s.OwnGoals * 2 + s.Bonus;
        return points;
    }

    private static int GoalPoints(PlayerPosition position) => position switch
    {
        PlayerPosition.Goalkeeper or PlayerPosition.Defender => 6,
        PlayerPosition.Midfielder => 5,
        _ => 4,
    };

    /// <summary>Bonus 3 / 2 / 1 for the three best match ratings (ties share the higher bonus; nobody below 60 minutes' worth of rating 0).</summary>
    public static IReadOnlyDictionary<int, int> Bonus(IReadOnlyList<(int PlayerKey, decimal Rating)> ratings)
    {
        var ranked = ratings.Where(r => r.Rating > 0).OrderByDescending(r => r.Rating).ToList();
        var result = new Dictionary<int, int>();
        var bonus = 3;
        for (var i = 0; i < ranked.Count && bonus > 0;)
        {
            var same = ranked.Skip(i).TakeWhile(r => r.Rating == ranked[i].Rating).ToList();
            same.ForEach(r => result[r.PlayerKey] = bonus);
            i += same.Count;
            bonus -= same.Count;
        }
        return result;
    }

    /// <summary>
    /// The team's points: automatic substitutions from the bench in order (keeping a valid formation), the captain doubled
    /// (or the vice-captain when the captain did not play), chips applied. Transfer hits are taken off by the caller.
    /// </summary>
    public static TeamScore TeamPoints(IReadOnlyList<ScoringPick> picks, IReadOnlyDictionary<long, PlayerGameweek> players, string? chip)
    {
        var played = (long id) => players.TryGetValue(id, out var p) && p.Minutes > 0;
        var counting = chip == BenchBoostChip ? picks.ToList() : AutoSubstitute(picks, played);
        var captain = picks.FirstOrDefault(p => p.IsCaptain && played(p.PlayerId)) ?? picks.FirstOrDefault(p => p.IsViceCaptain && played(p.PlayerId));
        var captainFactor = chip == TripleCaptainChip ? 3 : 2;
        var scored = picks.Select(p =>
        {
            var multiplier = !counting.Contains(p) ? 0 : captain?.PlayerId == p.PlayerId ? captainFactor : 1;
            var points = players.TryGetValue(p.PlayerId, out var g) ? g.Points : 0;
            return new ScoredPick(p.PlayerId, p.Slot, multiplier, points, points * multiplier);
        }).ToList();
        return new TeamScore(scored.Sum(s => s.Points), scored, captain?.PlayerId);
    }

    /// <summary>Each starter who did not play is replaced by the first bench player who played and keeps the formation valid.</summary>
    private static List<ScoringPick> AutoSubstitute(IReadOnlyList<ScoringPick> picks, Func<long, bool> played)
    {
        var xi = picks.Where(p => p.Slot <= 11).OrderBy(p => p.Slot).ToList();
        var bench = picks.Where(p => p.Slot > 11).OrderBy(p => p.Slot).ToList();
        foreach (var sub in bench.Where(b => played(b.PlayerId)))
        {
            var replaced = xi.FirstOrDefault(s => !played(s.PlayerId) && FormationAfter(xi, s, sub));
            if (replaced is not null)
            {
                xi[xi.IndexOf(replaced)] = sub;
            }
        }
        return xi;
    }

    private static bool FormationAfter(List<ScoringPick> xi, ScoringPick @out, ScoringPick @in)
    {
        if ((@out.Position == PlayerPosition.Goalkeeper) != (@in.Position == PlayerPosition.Goalkeeper))
        {
            return false;
        }
        var after = xi.Select(p => p == @out ? @in.Position : p.Position).ToList();
        return SquadRules.IsValidFormation(after);
    }
}
