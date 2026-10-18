using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Fantasy;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Fantasy groups, their members and tables.</summary>
public sealed class FantasyGroupRepository(AppDbContext db) : IFantasyGroupRepository
{
    public Task<FantasyGroup?> GroupAsync(long id, CancellationToken ct) => db.Set<FantasyGroup>().FirstOrDefaultAsync(g => g.Id == id, ct);

    public Task<FantasyGroup?> GroupByCodeAsync(string inviteCode, CancellationToken ct) =>
        db.Set<FantasyGroup>().FirstOrDefaultAsync(g => g.InviteCode == inviteCode, ct);

    public Task<string> CompetitionKeyAsync(long seasonId, CancellationToken ct) =>
        db.Set<Season>().Where(s => s.Id == seasonId).Select(s => s.Competition.Key).SingleAsync(ct);

    public Task<bool> CodeTakenAsync(string inviteCode, CancellationToken ct) =>
        db.Set<FantasyGroup>().IgnoreQueryFilters().AnyAsync(g => g.InviteCode == inviteCode, ct);

    public void Add(FantasyGroup group) => db.Set<FantasyGroup>().Add(group);

    public Task<GroupMember?> MemberAsync(long groupId, long teamId, CancellationToken ct) =>
        db.Set<GroupMember>().FirstOrDefaultAsync(m => m.GroupId == groupId && m.TeamId == teamId, ct);

    public void AddMember(GroupMember member) => db.Set<GroupMember>().Add(member);

    public void RemoveMember(GroupMember member) => db.Set<GroupMember>().Remove(member);

    public async Task<IReadOnlyList<GroupDto>> GroupsAsync(long seasonId, long? teamId, Guid? userId, bool withPaid, CancellationToken ct) =>
        await Project(db.Set<FantasyGroup>().Where(g => g.SeasonId == seasonId && (g.Type == FantasyGroupType.Official
                || (withPaid && g.Type == FantasyGroupType.Paid && !g.IsHidden) || g.Members.Any(m => m.TeamId == teamId)))
            .OrderBy(g => g.Type).ThenByDescending(g => g.PrizeValue).ThenBy(g => g.Name), teamId, userId).ToListAsync(ct);

    public Task<GroupDto?> GroupDtoAsync(long groupId, long? teamId, Guid? userId, CancellationToken ct) =>
        Project(db.Set<FantasyGroup>().Where(g => g.Id == groupId), teamId, userId).FirstOrDefaultAsync(ct);

    private static IQueryable<GroupDto> Project(IQueryable<FantasyGroup> query, long? teamId, Guid? userId) =>
        query.AsNoTracking().Select(g => new GroupDto(g.Id, g.Season.Competition.Key, g.Name, g.Type.ToString(),
            g.Type == FantasyGroupType.Official || g.Members.Any(m => m.TeamId == teamId) ? g.InviteCode : null,
            g.Members.Count, g.Members.Any(m => m.TeamId == teamId), g.OwnerUserId == userId,
            g.Members.Where(m => m.TeamId == teamId).Select(m => m.Rank).FirstOrDefault(), g.Type == FantasyGroupType.Official, g.MonthlyPrice,
            g.Sponsor == null ? null : new SponsorBadgeDto(g.Sponsor.Id, g.Sponsor.Name, g.Sponsor.Tier.ToString(), g.Sponsor.Color),
            g.PrizeText, g.PrizeValue, g.CreatedAt));

    public async Task<IReadOnlyList<StandingRowDto>> TableAsync(long groupId, long? teamId, int take, CancellationToken ct)
    {
        var rows = await RankedAsync(groupId, ct);
        return rows.Where((r, i) => i < take || r.TeamId == teamId)
            .Select(r => new StandingRowDto(r.Rank, r.TeamId, r.Team, Short(r.Manager), r.Total, r.Last, r.TeamId == teamId)).ToList();
    }

    public async Task<IReadOnlyList<(int Rank, long TeamId, string Team, Guid UserId, string Manager)>> AtRanksAsync(long groupId, IReadOnlyCollection<int> ranks, CancellationToken ct) =>
        (await RankedAsync(groupId, ct)).Where(r => ranks.Contains(r.Rank)).Select(r => (r.Rank, r.TeamId, r.Team, r.UserId, Short(r.Manager))).ToList();

    private sealed record Ranked(int Rank, long TeamId, string Team, Guid UserId, string Manager, int Total, int Last);

    /// <summary>Members by total points (equal totals share a rank), with the points of the last scored gameweek.</summary>
    private async Task<List<Ranked>> RankedAsync(long groupId, CancellationToken ct)
    {
        var seasonId = await db.Set<FantasyGroup>().Where(g => g.Id == groupId).Select(g => g.SeasonId).SingleAsync(ct);
        var last = await db.Set<Gameweek>().Where(g => g.SeasonId == seasonId && g.ScoredAt != null).OrderByDescending(g => g.Number).Select(g => (long?)g.Id).FirstOrDefaultAsync(ct);
        var members = await db.Set<GroupMember>().AsNoTracking().Where(m => m.GroupId == groupId)
            .Select(m => new
            {
                m.TeamId, m.Team.Name, m.Team.UserId, Manager = m.Team.User.FullName, m.Team.TotalPoints,
                Last = m.Team.GameweekPoints.Where(p => p.GameweekId == last).Select(p => (int)p.Points).FirstOrDefault(),
            })
            .OrderByDescending(m => m.TotalPoints).ThenBy(m => m.TeamId).ToListAsync(ct);
        var ranked = new List<Ranked>();
        for (var i = 0; i < members.Count; i++)
        {
            var rank = i > 0 && members[i].TotalPoints == members[i - 1].TotalPoints ? ranked[i - 1].Rank : i + 1;
            ranked.Add(new Ranked(rank, members[i].TeamId, members[i].Name, members[i].UserId, members[i].Manager, members[i].TotalPoints, members[i].Last));
        }
        return ranked;
    }

    /// <summary>"Karim Adel" → "Karim A." (other managers' full names are not shown).</summary>
    private static string Short(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch { 0 => "Manager", 1 => parts[0], _ => $"{parts[0]} {parts[^1][0]}." };
    }

    public async Task<IReadOnlyList<AdminGroupDto>> AdminGroupsAsync(string? competition, CancellationToken ct) =>
        await db.Set<FantasyGroup>().AsNoTracking()
            .Where(g => g.Type != FantasyGroupType.Private && g.Season.IsCurrent && (competition == null || g.Season.Competition.Key == competition))
            .OrderBy(g => g.Season.Competition.SortOrder).ThenBy(g => g.Type).ThenBy(g => g.Name)
            .Select(g => new AdminGroupDto(g.Id, g.Season.Competition.Key, g.Name, g.Type.ToString(), g.InviteCode, g.Members.Count, g.IsHidden, g.MonthlyPrice,
                g.Sponsor == null ? null : new SponsorBadgeDto(g.Sponsor.Id, g.Sponsor.Name, g.Sponsor.Tier.ToString(), g.Sponsor.Color),
                g.PrizeText, g.PrizeValue, g.CreatedAt))
            .ToListAsync(ct);

    public Task<long?> CurrentSeasonIdAsync(string competition, CancellationToken ct) =>
        db.Set<Season>().Where(s => s.IsCurrent && s.Competition.Key == competition).Select(s => (long?)s.Id).FirstOrDefaultAsync(ct);
}
