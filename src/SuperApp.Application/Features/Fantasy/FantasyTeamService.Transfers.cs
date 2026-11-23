using FluentValidation;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Fantasy;

namespace SuperApp.Application.Features.Fantasy;

/// <summary>Transfers and the shared helpers of <see cref="FantasyTeamService"/>.</summary>
public sealed partial class FantasyTeamService
{
    public async Task<TransfersResultDto> TransfersAsync(string competition, TransfersRequest request, CancellationToken ct)
    {
        await transferRules.ValidateAndThrowAsync(request, ct);
        var e = await EditableAsync(competition, ct);
        var swaps = request.Transfers.Select(t => (t.Out, t.In)).ToList();
        var squadIds = e.Picks.Select(p => p.PlayerId).ToList();
        var players = await repository.PlayersAsync(e.Season.SeasonId, squadIds.Concat(swaps.Select(s => s.In)).ToList(), ct);
        var paid = Paid(e.Picks);
        var sell = swaps.ToDictionary(s => s.Out, s => players.TryGetValue(s.Out, out var p) ? SquadRules.SellingPrice(paid.GetValueOrDefault(s.Out), p.Price) : 0m);
        var bank = CheckTransfers(e, squadIds, swaps, players, sell);
        var row = await repository.GameweekRowAsync(e.Team.Id, e.Open.Id, ct);
        // Free: a wildcard or free hit, or a team before its first deadline (unlimited changes, as in the official game)
        var free = row.ChipPlayed is FantasyScoring.WildcardChip or FantasyScoring.FreeHitChip || !await repository.PlayedBeforeAsync(e.Team.Id, e.Open.Number, ct);
        if (!free && (await repository.TransfersAsync(e.Team.Id, e.Open.Id, ct)).Count + swaps.Count > SquadRules.MaxTransfersPerGameweek)
        {
            throw new BusinessRuleException($"At most {SquadRules.MaxTransfersPerGameweek} transfers a gameweek (a wildcard or free hit has no limit).", "too_many_transfers");
        }
        var hits = free ? 0 : SquadRules.Hits(swaps.Count, e.Team.FreeTransfers, false);
        repository.AddTransfers(swaps.Select((s, i) => new Transfer
        {
            TeamId = e.Team.Id, GameweekId = e.Open.Id, PlayerOutId = s.Out, PlayerInId = s.In, PriceOut = sell[s.Out], PriceIn = players[s.In].Price,
            PointsHit = (byte)(free || i < e.Team.FreeTransfers ? 0 : SquadRules.TransferHit),
        }));
        e.Team.Bank = bank;
        e.Team.FreeTransfers = (byte)(free ? e.Team.FreeTransfers : Math.Max(0, e.Team.FreeTransfers - swaps.Count));
        row.TransfersCost = (byte)Math.Min(255, row.TransfersCost + hits);
        foreach (var s in swaps)
        {
            paid[s.In] = players[s.In].Price;
        }
        await repository.ReplacePicksAsync(e.Team.Id, e.Open.Id, Picks(SquadRules.ApplyTransfers(CurrentLineup(e.Picks), swaps), e.Team.Id, e.Open.Id, paid), ct);
        return new TransfersResultDto(swaps.Count, hits, await ToDtoAsync(e.Season, e.Team, ct));
    }

    /// <summary>Checks the transfers and returns the bank after them (players leave at their selling price).</summary>
    private static decimal CheckTransfers(Editable e, List<long> squadIds, List<(long Out, long In)> swaps, IReadOnlyDictionary<long, SquadPlayer> players,
        Dictionary<long, decimal> sell)
    {
        if (swaps.Any(s => !squadIds.Contains(s.Out) || squadIds.Contains(s.In)))
        {
            throw new BusinessRuleException("Each transfer takes one of your players out and brings in a player you don't have.", "transfer_players");
        }
        if (swaps.Any(s => !players.ContainsKey(s.In) || !players.ContainsKey(s.Out) || players[s.In].Position != players[s.Out].Position))
        {
            throw new BusinessRuleException("A player can only be replaced by a player in the same position.", "transfer_position");
        }
        var bank = e.Team.Bank + swaps.Sum(s => sell[s.Out] - players[s.In].Price);
        if (bank < 0)
        {
            throw new BusinessRuleException($"Over budget by {-bank:0.0}M.", "over_budget");
        }
        var map = swaps.ToDictionary(s => s.Out, s => s.In);
        var newSquad = squadIds.Select(id => players[map.GetValueOrDefault(id, id)]).ToList();
        if (SquadRules.CheckSquad(newSquad, decimal.MaxValue, e.Season.MaxPerClub) is { } broken)
        {
            throw new BusinessRuleException(broken.Message, broken.Code);
        }
        return bank;
    }

    private async Task<Editable> EditableAsync(string competition, CancellationToken ct)
    {
        await flags.EnsureOnAsync(KillSwitches.Fantasy, ct);
        var season = await SeasonAsync(competition, ct);
        var team = await repository.TeamAsync(UserId, season.SeasonId, ct)
            ?? throw new NotFoundException("You have no team in this league yet. Pick your 15 players to start.", "no_team");
        var open = season.Open ?? throw SeasonOver();
        return new Editable(season, team, open, await repository.PicksAsync(team.Id, open.Id, ct));
    }

    private async Task<SeasonInfo> SeasonAsync(string competition, CancellationToken ct) =>
        await repository.SeasonAsync(competition.ToUpperInvariant(), clock.UtcNow, ct)
        ?? throw new NotFoundException("This league has no season yet.", "no_season");

    private static FeatureDisabledException SeasonOver() =>
        new("The deadline has passed and no gameweek is open for changes.", "deadline_passed");

    private async Task<TeamDto> ToDtoAsync(SeasonInfo season, FantasyTeam team, CancellationToken ct)
    {
        var picks = season.Open is null ? [] : await repository.PicksAsync(team.Id, season.Open.Id, ct);
        var prices = await repository.PlayersAsync(season.SeasonId, picks.Select(p => p.PlayerId).ToList(), ct);
        var (chip, cost) = season.Open is null ? (null, 0) : await repository.GameweekStateAsync(team.Id, season.Open.Id, ct);
        decimal Sell(Pick p) => prices.TryGetValue(p.PlayerId, out var now) ? SquadRules.SellingPrice(p.PurchasePrice, now.Price) : 0m;
        return new TeamDto(team.Id, team.Name, season.Key, season.Open?.Number, season.Open?.Deadline, team.Bank,
            picks.Sum(Sell) + team.Bank, team.FreeTransfers, cost, team.TotalPoints, team.OverallRank, Chips(team), chip,
            picks.OrderBy(p => p.Slot).Select(p => new PickDto(p.PlayerId, p.Slot, p.IsCaptain, p.IsViceCaptain, p.Multiplier, p.Points,
                Sell(p), p.PurchasePrice ?? (prices.TryGetValue(p.PlayerId, out var now) ? now.Price : 0m))).ToList());
    }

    /// <summary>Chips played, as "wildcard1" … "triplecaptain2" (older rows said "benchboost" / "triplecaptain" for the first half).</summary>
    private static List<string> Chips(FantasyTeam team) =>
        string.IsNullOrWhiteSpace(team.ChipsUsed) ? [] : [.. team.ChipsUsed.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c is FantasyScoring.BenchBoostChip or FantasyScoring.TripleCaptainChip ? c + "1" : c).Distinct()];

    /// <summary>What the team paid for each player in the squad (null = bought before purchase prices were kept).</summary>
    private static Dictionary<long, decimal?> Paid(List<Pick> picks) => picks.ToDictionary(p => p.PlayerId, p => p.PurchasePrice);

    private static Lineup ToLineup(LineupRequest r) => new(r.Starters, r.Bench, r.CaptainId, r.ViceCaptainId);

    private static Lineup CurrentLineup(List<Pick> picks)
    {
        var ordered = picks.OrderBy(p => p.Slot).ToList();
        return new Lineup(ordered.Where(p => p.Slot <= 11).Select(p => p.PlayerId).ToList(), ordered.Where(p => p.Slot > 11).Select(p => p.PlayerId).ToList(),
            ordered.First(p => p.IsCaptain).PlayerId, ordered.First(p => p.IsViceCaptain).PlayerId);
    }

    /// <summary>Picks in slot order: 1–11 the starters, 12–15 the bench; each keeps what the team paid for him.</summary>
    private static List<Pick> Picks(Lineup lineup, long teamId, long gameweekId, IReadOnlyDictionary<long, decimal?> paid) =>
        lineup.Starters.Concat(lineup.Bench).Select((id, i) => new Pick
        {
            TeamId = teamId, GameweekId = gameweekId, PlayerId = id, Slot = (byte)(i + 1),
            IsCaptain = id == lineup.CaptainId, IsViceCaptain = id == lineup.ViceCaptainId, PurchasePrice = paid.GetValueOrDefault(id),
        }).ToList();
}

public sealed class CreateTeamRequestValidator : AbstractValidator<CreateTeamRequest>
{
    public CreateTeamRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Give your team a name.").Length(3, 40);
        RuleFor(x => x.Picks).NotNull().Must(p => p.Count == SquadRules.SquadSize && p.Distinct().Count() == SquadRules.SquadSize)
            .WithMessage("Pick 15 different players.");
        RuleFor(x => x.Lineup!).SetValidator(new LineupRequestValidator()).When(x => x.Lineup is not null);
    }
}

public sealed class LineupRequestValidator : AbstractValidator<LineupRequest>
{
    public LineupRequestValidator()
    {
        RuleFor(x => x.Starters).NotNull().Must(s => s.Count == 11).WithMessage("Pick 11 starters.");
        RuleFor(x => x.Bench).NotNull().Must(b => b.Count == 4).WithMessage("Put 4 players on the bench.");
        RuleFor(x => x.CaptainId).GreaterThan(0).WithMessage("Pick a captain.");
        RuleFor(x => x.ViceCaptainId).GreaterThan(0).WithMessage("Pick a vice-captain.");
    }
}

public sealed class RenameTeamRequestValidator : AbstractValidator<RenameTeamRequest>
{
    public RenameTeamRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Give your team a name.").Length(3, 40);
    }
}

public sealed class TransfersRequestValidator : AbstractValidator<TransfersRequest>
{
    public TransfersRequestValidator()
    {
        RuleFor(x => x.Transfers).NotNull().Must(t => t.Count is >= 1 and <= SquadRules.SquadSize)
            .WithMessage("Make 1 to 15 transfers at a time.")
            .Must(t => t.Select(x => x.Out).Distinct().Count() == t.Count && t.Select(x => x.In).Distinct().Count() == t.Count)
            .WithMessage("Each player once.");
    }
}
