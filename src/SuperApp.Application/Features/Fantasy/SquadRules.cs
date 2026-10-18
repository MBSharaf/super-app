using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Fantasy;

/// <summary>A player as the squad rules see him.</summary>
public sealed record SquadPlayer(long Id, long ClubId, PlayerPosition Position, decimal Price);

/// <summary>The starting XI, the bench in order (first = the substitute goalkeeper), captain and vice-captain.</summary>
public sealed record Lineup(IReadOnlyList<long> Starters, IReadOnlyList<long> Bench, long CaptainId, long ViceCaptainId);

/// <summary>
/// Squad, lineup and transfer rules (PRD FR-FAN-03), pure so they are unit-tested on their own: 15 players (2 GK, 5 DEF,
/// 5 MID, 3 FWD) within the budget, at most N per club; a starting XI of 1 GK, at least 3 DEF, 2 MID and 1 FWD; one free
/// transfer a gameweek (two can be saved), every extra one costs 4 points unless a wildcard is played.
/// </summary>
public static class SquadRules
{
    public const int SquadSize = 15;
    public const int MaxSavedFreeTransfers = 2;
    public const int TransferHit = 4;

    private static readonly IReadOnlyDictionary<PlayerPosition, int> Shape = new Dictionary<PlayerPosition, int>
    {
        [PlayerPosition.Goalkeeper] = 2,
        [PlayerPosition.Defender] = 5,
        [PlayerPosition.Midfielder] = 5,
        [PlayerPosition.Forward] = 3,
    };

    /// <summary>The first broken rule as (message, code), or null when the squad is valid.</summary>
    public static (string Message, string Code)? CheckSquad(IReadOnlyList<SquadPlayer> squad, decimal budget, int maxPerClub)
    {
        if (squad.Count != SquadSize || squad.Select(p => p.Id).Distinct().Count() != SquadSize)
        {
            return ("Pick 15 different players.", "squad_size");
        }
        foreach (var (position, needed) in Shape)
        {
            var have = squad.Count(p => p.Position == position);
            if (have != needed)
            {
                return ($"Pick {needed} {Name(position, needed)} (you have {have}).", "formation");
            }
        }
        if (squad.GroupBy(p => p.ClubId).FirstOrDefault(g => g.Count() > maxPerClub) is { } club)
        {
            return ($"At most {maxPerClub} players from one club.", "max_per_club");
        }
        var cost = squad.Sum(p => p.Price);
        return cost > budget ? ($"Over budget by {cost - budget:0.0}M.", "over_budget") : null;
    }

    public static bool IsValidFormation(IReadOnlyList<PlayerPosition> starters) =>
        starters.Count == 11
        && starters.Count(p => p == PlayerPosition.Goalkeeper) == 1
        && starters.Count(p => p == PlayerPosition.Defender) >= 3
        && starters.Count(p => p == PlayerPosition.Midfielder) >= 2
        && starters.Any(p => p == PlayerPosition.Forward);

    /// <summary>The first broken lineup rule as (message, code), or null when the lineup is valid for this squad.</summary>
    public static (string Message, string Code)? CheckLineup(Lineup lineup, IReadOnlyDictionary<long, SquadPlayer> squad)
    {
        var all = lineup.Starters.Concat(lineup.Bench).ToList();
        if (all.Count != SquadSize || all.Distinct().Count() != SquadSize || all.Any(id => !squad.ContainsKey(id)))
        {
            return ("The lineup must use each of your 15 players once.", "lineup_players");
        }
        if (!IsValidFormation(lineup.Starters.Select(id => squad[id].Position).ToList()))
        {
            return ("Start 1 goalkeeper, at least 3 defenders, 2 midfielders and 1 forward.", "formation");
        }
        if (squad[lineup.Bench[0]].Position != PlayerPosition.Goalkeeper)
        {
            return ("The substitute goalkeeper sits first on the bench.", "bench_goalkeeper");
        }
        if (lineup.CaptainId == lineup.ViceCaptainId || !lineup.Starters.Contains(lineup.CaptainId) || !lineup.Starters.Contains(lineup.ViceCaptainId))
        {
            return ("Pick a captain and a different vice-captain from your starting XI.", "captain");
        }
        return null;
    }

    /// <summary>
    /// A starting lineup for a new squad: 1 GK, 4 DEF, 4 MID, 2 FWD in the order given (falls back to 3-5-2 shapes when
    /// needed), the rest on the bench with the second goalkeeper first; the two most expensive starters lead.
    /// </summary>
    public static Lineup DefaultLineup(IReadOnlyList<SquadPlayer> squad)
    {
        var take = new Dictionary<PlayerPosition, int>
        {
            [PlayerPosition.Goalkeeper] = 1, [PlayerPosition.Defender] = 4, [PlayerPosition.Midfielder] = 4, [PlayerPosition.Forward] = 2,
        };
        var starters = take.SelectMany(t => squad.Where(p => p.Position == t.Key).Take(t.Value)).ToList();
        var bench = squad.Except(starters).OrderBy(p => p.Position == PlayerPosition.Goalkeeper ? 0 : 1).ToList();
        var leaders = starters.OrderByDescending(p => p.Price).ThenBy(p => p.Id).Take(2).ToList();
        return new Lineup(starters.Select(p => p.Id).ToList(), bench.Select(p => p.Id).ToList(), leaders[0].Id, leaders[1].Id);
    }

    /// <summary>Points taken off for this many transfers in one gameweek.</summary>
    public static int Hits(int transfersMade, int freeTransfers, bool wildcard) =>
        wildcard ? 0 : Math.Max(0, transfersMade - freeTransfers) * TransferHit;

    /// <summary>
    /// Puts the incoming players in the outgoing players' places (same position) so the lineup stays valid; a captain
    /// or vice-captain who leaves hands the armband to another starter.
    /// </summary>
    public static Lineup ApplyTransfers(Lineup current, IReadOnlyList<(long Out, long In)> swaps)
    {
        var map = swaps.ToDictionary(s => s.Out, s => s.In);
        long Swap(long id) => map.TryGetValue(id, out var replacement) ? replacement : id;
        var starters = current.Starters.Select(Swap).ToList();
        var bench = current.Bench.Select(Swap).ToList();
        var captain = Swap(current.CaptainId);
        var vice = Swap(current.ViceCaptainId);
        return new Lineup(starters, bench, captain, vice);
    }

    private static string Name(PlayerPosition position, int count) => (position, count == 1) switch
    {
        (PlayerPosition.Goalkeeper, true) => "goalkeeper",
        (PlayerPosition.Goalkeeper, false) => "goalkeepers",
        (PlayerPosition.Defender, true) => "defender",
        (PlayerPosition.Defender, false) => "defenders",
        (PlayerPosition.Midfielder, true) => "midfielder",
        (PlayerPosition.Midfielder, false) => "midfielders",
        (_, true) => "forward",
        _ => "forwards",
    };
}
