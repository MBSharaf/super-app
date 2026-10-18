using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Fantasy;

/// <summary>
/// My fantasy team (API §6.1): create the squad, set the lineup, make transfers, play chips, see the history. Changes
/// always go to the gameweek open for changes; when the season has none left the answer is 423.
/// </summary>
public interface IFantasyTeamService
{
    Task<TeamDto> GetAsync(string competition, CancellationToken ct);

    Task<TeamDto> CreateAsync(string competition, CreateTeamRequest request, CancellationToken ct);

    Task<TeamDto> LineupAsync(string competition, LineupRequest request, CancellationToken ct);

    Task<TeamDto> RenameAsync(string competition, RenameTeamRequest request, CancellationToken ct);

    Task<TransfersResultDto> TransfersAsync(string competition, TransfersRequest request, CancellationToken ct);

    Task<TeamDto> ChipAsync(string competition, string chip, CancellationToken ct);

    Task<TeamHistoryDto> HistoryAsync(string competition, CancellationToken ct);
}

public sealed partial class FantasyTeamService(
    IFantasyTeamRepository repository,
    IUnitOfWork unitOfWork,
    IFeatureFlags flags,
    INotifier notifier,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<CreateTeamRequest> createRules,
    IValidator<LineupRequest> lineupRules,
    IValidator<RenameTeamRequest> renameRules,
    IValidator<TransfersRequest> transferRules) : IFantasyTeamService
{
    private sealed record Editable(SeasonInfo Season, FantasyTeam Team, OpenGameweek Open, List<Pick> Picks);

    private Guid UserId => currentUser.UserId ?? throw new UnauthorizedException();

    public async Task<TeamDto> GetAsync(string competition, CancellationToken ct)
    {
        var season = await SeasonAsync(competition, ct);
        var team = await repository.TeamAsync(UserId, season.SeasonId, ct)
            ?? throw new NotFoundException("You have no team in this league yet. Pick your 15 players to start.", "no_team");
        return await ToDtoAsync(season, team, ct);
    }

    public async Task<TeamDto> CreateAsync(string competition, CreateTeamRequest request, CancellationToken ct)
    {
        await createRules.ValidateAndThrowAsync(request, ct);
        await flags.EnsureOnAsync(KillSwitches.Fantasy, ct);
        var season = await SeasonAsync(competition, ct);
        var open = season.Open ?? throw SeasonOver();
        var players = await repository.PlayersAsync(season.SeasonId, request.Picks, ct);
        var squad = request.Picks.Where(players.ContainsKey).Select(id => players[id]).ToList();
        if (SquadRules.CheckSquad(squad, season.Budget, season.MaxPerClub) is { } broken)
        {
            throw new BusinessRuleException(broken.Message, broken.Code);
        }
        var lineup = request.Lineup is null ? SquadRules.DefaultLineup(squad) : ToLineup(request.Lineup);
        if (SquadRules.CheckLineup(lineup, players) is { } badLineup)
        {
            throw new BusinessRuleException(badLineup.Message, badLineup.Code);
        }
        var team = new FantasyTeam { UserId = UserId, SeasonId = season.SeasonId, Name = request.Name.Trim(), Bank = season.Budget - squad.Sum(p => p.Price) };
        await repository.AddTeamAsync(team, Picks(lineup, 0, open.Id), ct);
        await notifier.NotifyUserAsync(UserId, new NotificationMessage(NotificationCategory.Fantasy, "Your fantasy team is ready",
            $"{team.Name} plays from gameweek {open.Number}. Join a group to play your friends.", "#/fantasy", NotificationTone.Success), ct);
        return await ToDtoAsync(season, team, ct);
    }

    public async Task<TeamDto> LineupAsync(string competition, LineupRequest request, CancellationToken ct)
    {
        await lineupRules.ValidateAndThrowAsync(request, ct);
        var e = await EditableAsync(competition, ct);
        var squad = await repository.PlayersAsync(e.Season.SeasonId, e.Picks.Select(p => p.PlayerId).ToList(), ct);
        var lineup = ToLineup(request);
        if (SquadRules.CheckLineup(lineup, squad) is { } broken)
        {
            throw new BusinessRuleException(broken.Message, broken.Code);
        }
        await repository.ReplacePicksAsync(e.Team.Id, e.Open.Id, Picks(lineup, e.Team.Id, e.Open.Id), ct);
        return await ToDtoAsync(e.Season, e.Team, ct);
    }

    public async Task<TeamDto> RenameAsync(string competition, RenameTeamRequest request, CancellationToken ct)
    {
        await renameRules.ValidateAndThrowAsync(request, ct);
        var season = await SeasonAsync(competition, ct);
        var team = await repository.TeamAsync(UserId, season.SeasonId, ct) ?? throw new NotFoundException("You have no team in this league yet.", "no_team");
        team.Name = request.Name.Trim();
        await unitOfWork.SaveChangesAsync(ct);
        return await ToDtoAsync(season, team, ct);
    }

    public async Task<TeamDto> ChipAsync(string competition, string chip, CancellationToken ct)
    {
        var e = await EditableAsync(competition, ct);
        var key = chip.ToLowerInvariant() switch
        {
            FantasyScoring.WildcardChip => e.Open.Number < 20 ? "wildcard1" : "wildcard2",
            FantasyScoring.BenchBoostChip or FantasyScoring.TripleCaptainChip => chip.ToLowerInvariant(),
            _ => throw new BadRequestException("Chips: wildcard, benchboost or triplecaptain.", "unknown_chip"),
        };
        var used = Chips(e.Team);
        var row = await repository.GameweekRowAsync(e.Team.Id, e.Open.Id, ct);
        if (used.Contains(key))
        {
            throw new ConflictException("You have already played this chip.", "chip_used");
        }
        if (row.ChipPlayed is not null)
        {
            throw new ConflictException($"You already play the {row.ChipPlayed} chip this gameweek.", "chip_this_week");
        }
        row.ChipPlayed = chip.ToLowerInvariant();
        e.Team.ChipsUsed = string.Join(',', used.Append(key));
        await unitOfWork.SaveChangesAsync(ct);
        return await ToDtoAsync(e.Season, e.Team, ct);
    }

    public async Task<TeamHistoryDto> HistoryAsync(string competition, CancellationToken ct)
    {
        var season = await SeasonAsync(competition, ct);
        var team = await repository.TeamAsync(UserId, season.SeasonId, ct) ?? throw new NotFoundException("You have no team in this league yet.", "no_team");
        return new TeamHistoryDto(team.TotalPoints, team.OverallRank, await repository.ManagersAsync(season.SeasonId, ct), await repository.HistoryAsync(team.Id, ct));
    }
}
