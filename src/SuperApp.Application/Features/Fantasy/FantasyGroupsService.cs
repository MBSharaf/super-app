using System.Security.Cryptography;
using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Fantasy;

/// <summary>
/// Fantasy groups (API §6.1, FR-FAN-06/07): the grand league (every team), official sponsor leagues (Fantasy Pass),
/// private groups with an invite code, paid groups hidden until payments are live. Tables follow total points.
/// </summary>
public interface IFantasyGroupsService
{
    Task<GroupsOverviewDto> OverviewAsync(string competition, CancellationToken ct);

    Task<GroupDto> CreateAsync(string competition, CreateGroupRequest request, CancellationToken ct);

    Task<GroupDto> JoinAsync(JoinGroupRequest request, CancellationToken ct);

    Task LeaveAsync(long groupId, CancellationToken ct);

    Task<GroupTableDto> TableAsync(long groupId, CancellationToken ct);
}

public sealed class FantasyGroupsService(
    IFantasyGroupRepository groups,
    IFantasyTeamRepository teams,
    IFantasyPassRepository passes,
    IUnitOfWork unitOfWork,
    IFeatureFlags flags,
    INotifier notifier,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<CreateGroupRequest> createRules,
    IValidator<JoinGroupRequest> joinRules) : IFantasyGroupsService
{
    private const string CodeLetters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private Guid UserId => currentUser.UserId ?? throw new UnauthorizedException();

    public async Task<GroupsOverviewDto> OverviewAsync(string competition, CancellationToken ct)
    {
        var season = await teams.SeasonAsync(competition.ToUpperInvariant(), clock.UtcNow, ct) ?? throw new NotFoundException("This league has no season yet.", "no_season");
        var team = await teams.TeamAsync(UserId, season.SeasonId, ct);
        var all = await groups.GroupsAsync(season.SeasonId, team?.Id, UserId, await passes.PaymentsLiveAsync(ct), ct);
        var overall = new OverallDto(team?.OverallRank, team?.TotalPoints ?? 0, await teams.ManagersAsync(season.SeasonId, ct), team is not null);
        return new GroupsOverviewDto(overall, all.Where(g => g.Joined).ToList(), all.Where(g => g.Type == "Official").ToList(), all.Where(g => g.Type == "Paid").ToList());
    }

    public async Task<GroupDto> CreateAsync(string competition, CreateGroupRequest request, CancellationToken ct)
    {
        await createRules.ValidateAndThrowAsync(request, ct);
        await flags.EnsureOnAsync(KillSwitches.Fantasy, ct);
        var season = await teams.SeasonAsync(competition.ToUpperInvariant(), clock.UtcNow, ct) ?? throw new NotFoundException("This league has no season yet.", "no_season");
        var team = await teams.TeamAsync(UserId, season.SeasonId, ct) ?? throw NoTeam();
        var group = new FantasyGroup { SeasonId = season.SeasonId, Name = request.Name.Trim(), Type = FantasyGroupType.Private, OwnerUserId = UserId, InviteCode = await NewCodeAsync(ct) };
        groups.Add(group);
        group.Members.Add(new GroupMember { TeamId = team.Id, Rank = 1 });
        await unitOfWork.SaveChangesAsync(ct);
        return (await groups.GroupDtoAsync(group.Id, team.Id, UserId, ct))!;
    }

    public async Task<GroupDto> JoinAsync(JoinGroupRequest request, CancellationToken ct)
    {
        await joinRules.ValidateAndThrowAsync(request, ct);
        await flags.EnsureOnAsync(KillSwitches.Fantasy, ct);
        var group = await groups.GroupByCodeAsync(request.InviteCode.Trim().ToUpperInvariant(), ct);
        if (group is null || (group.Type == FantasyGroupType.Paid && group.IsHidden))
        {
            throw new NotFoundException("No group uses that code. Check it with the person who invited you.", "not_found");
        }
        var team = await teams.TeamAsync(UserId, group.SeasonId, ct) ?? throw NoTeam();
        if (await groups.MemberAsync(group.Id, team.Id, ct) is not null)
        {
            throw new ConflictException($"You're already in {group.Name}.", "already_member");
        }
        await CheckEntryAsync(group, ct);
        groups.AddMember(new GroupMember { GroupId = group.Id, TeamId = team.Id });
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyUserAsync(UserId, new NotificationMessage(NotificationCategory.Fantasy, $"You joined {group.Name}",
            group.PrizeText, $"#/fantasy/league/{group.Id}", NotificationTone.Success), ct);
        return (await groups.GroupDtoAsync(group.Id, team.Id, UserId, ct))!;
    }

    /// <summary>Official leagues need a Fantasy Pass for the league; paid groups need a payment (Phase 5).</summary>
    private async Task CheckEntryAsync(FantasyGroup group, CancellationToken ct)
    {
        if (group.Type == FantasyGroupType.Paid)
        {
            throw new BusinessRuleException("This group has a monthly fee; paying for groups opens with online payments.", "payment_required");
        }
        if (group.Type != FantasyGroupType.Official)
        {
            return;
        }
        var key = await groups.CompetitionKeyAsync(group.SeasonId, ct);
        var competitionId = (await passes.CompetitionsAsync(ct)).First(c => c.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Id;
        var active = await passes.ActiveAsync(UserId, clock.UtcNow, ct);
        if (!active.Any(p => p.CompetitionId is null || p.CompetitionId == competitionId))
        {
            throw new ForbiddenException("Prize leagues need a Fantasy Pass for this league.", "pass_required");
        }
    }

    public async Task LeaveAsync(long groupId, CancellationToken ct)
    {
        var group = await groups.GroupAsync(groupId, ct) ?? throw new NotFoundException("Group not found.");
        var team = await teams.TeamAsync(UserId, group.SeasonId, ct) ?? throw NoTeam();
        var member = await groups.MemberAsync(groupId, team.Id, ct) ?? throw new NotFoundException("You're not in this group.", "not_member");
        groups.RemoveMember(member);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<GroupTableDto> TableAsync(long groupId, CancellationToken ct)
    {
        var group = await groups.GroupAsync(groupId, ct);
        if (group is null || (group.Type == FantasyGroupType.Paid && group.IsHidden))
        {
            throw new NotFoundException("Group not found.");
        }
        var team = currentUser.UserId is { } me ? await teams.TeamAsync(me, group.SeasonId, ct) : null;
        var isMember = team is not null && await groups.MemberAsync(groupId, team.Id, ct) is not null;
        if (group.Type != FantasyGroupType.Official && !isMember)
        {
            throw new ForbiddenException("Only members see this group's table.", "not_member");
        }
        var dto = (await groups.GroupDtoAsync(groupId, team?.Id, currentUser.UserId, ct))!;
        return new GroupTableDto(dto, await groups.TableAsync(groupId, team?.Id, 50, ct));
    }

    private async Task<string> NewCodeAsync(CancellationToken ct)
    {
        while (true)
        {
            var code = "SA-" + new string(Enumerable.Range(0, 6).Select(_ => CodeLetters[RandomNumberGenerator.GetInt32(CodeLetters.Length)]).ToArray());
            if (!await groups.CodeTakenAsync(code, ct))
            {
                return code;
            }
        }
    }

    private static BusinessRuleException NoTeam() => new("Create your fantasy team for this league first.", "no_team");
}

public sealed class CreateGroupRequestValidator : AbstractValidator<CreateGroupRequest>
{
    public CreateGroupRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Give your group a name.").Length(3, 60);
    }
}

public sealed class JoinGroupRequestValidator : AbstractValidator<JoinGroupRequest>
{
    public JoinGroupRequestValidator()
    {
        RuleFor(x => x.InviteCode).NotEmpty().WithMessage("Type the invite code.").MaximumLength(10);
    }
}
