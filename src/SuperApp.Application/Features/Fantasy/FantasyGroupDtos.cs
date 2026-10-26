using SuperApp.Domain.Entities.Fantasy;

namespace SuperApp.Application.Features.Fantasy;

/* ---------- Groups ---------- */

public sealed record SponsorBadgeDto(long Id, string Name, string Tier, string Color);

/// <summary>
/// A fantasy group. Type Official (sponsor prize league, needs the Fantasy Pass), Private (invite code) or Paid (monthly,
/// hidden until a payment gateway is live). <c>InviteCode</c> only for members (and official groups).
/// </summary>
public sealed record GroupDto(long Id, string Competition, string Name, string Type, string? InviteCode, int Members, bool Joined, bool IsOwner,
    int? MyRank, bool NeedsPass, decimal? MonthlyPrice, SponsorBadgeDto? Sponsor, string? PrizeText, decimal PrizeValue, DateTime CreatedAt);

/// <summary>The grand league: every team of the season, by total points.</summary>
public sealed record OverallDto(int? Rank, int TotalPoints, int Managers, bool HasTeam);

/// <summary>GET /fantasy/{comp}/groups — paid groups stay empty until payments are live.</summary>
public sealed record GroupsOverviewDto(OverallDto Overall, IReadOnlyList<GroupDto> Mine, IReadOnlyList<GroupDto> Official, IReadOnlyList<GroupDto> Paid);

public sealed record CreateGroupRequest(string Name);

public sealed record JoinGroupRequest(string InviteCode);

/// <summary>One row of a group table. Managers are shown by first name and initial only.</summary>
public sealed record StandingRowDto(int Rank, long TeamId, string Team, string Manager, int TotalPoints, int LastGameweekPoints, bool IsMe);

public sealed record GroupTableDto(GroupDto Group, IReadOnlyList<StandingRowDto> Rows);

/* ---------- Fantasy Pass ---------- */

/// <summary>A plan: <c>egy</c>, <c>epl</c> or <c>both</c>; price after the seller-level discount (Pro 50%, Elite free).</summary>
public sealed record PassPlanDto(string Plan, decimal ListPrice, decimal Price, decimal Discount);

public sealed record ActivePassDto(string Plan, DateTime StartsAt, DateTime EndsAt);

/// <summary>My passes, what they cover now, and the plans to buy (30 days each; buying early adds to the end).</summary>
public sealed record PassStatusDto(IReadOnlyList<ActivePassDto> Active, IReadOnlyDictionary<string, DateTime?> CoversUntil, IReadOnlyList<PassPlanDto> Plans, string? PaymentUrl = null);

/// <summary>POST /fantasy/pass — header Idempotency-Key required. Paid straight away until payments arrive (Phase 5).</summary>
public sealed record BuyPassRequest(string Plan);

/* ---------- Admin: sponsors, groups, winners ---------- */

public sealed record SponsorDto(long Id, string Name, string Tier, string? ContactEmail, decimal ContractValue, DateOnly StartsOn, DateOnly EndsOn, string Color);

public sealed record SponsorRequest(string Name, string Tier, string? ContactEmail, decimal ContractValue, DateOnly StartsOn, DateOnly EndsOn, string Color);

public sealed record AdminGroupDto(long Id, string Competition, string Name, string Type, string InviteCode, int Members, bool IsHidden, decimal? MonthlyPrice,
    SponsorBadgeDto? Sponsor, string? PrizeText, decimal PrizeValue, DateTime CreatedAt);

/// <summary>Type Official or Paid (paid: permission fantasy.groups.paid, and only once payments are live — else 423).</summary>
public sealed record AdminGroupRequest(string Competition, string Name, string Type, long? SponsorId, string? PrizeText, decimal PrizeValue, decimal? MonthlyPrice);

public sealed record PrizePlace(int Rank, decimal Amount);

/// <summary>POST /admin/fantasy/winners — each place gets a shop voucher (100 EGP or more; never cash).</summary>
public sealed record WinnersRequest(long GroupId, IReadOnlyList<PrizePlace> Places);

public sealed record WinnerDto(int Rank, string Team, string Manager, decimal Amount, string VoucherCode);

/* ---------- Persistence ---------- */

/// <summary>Groups, members and tables.</summary>
public interface IFantasyGroupRepository
{
    Task<FantasyGroup?> GroupAsync(long id, CancellationToken ct);

    Task<FantasyGroup?> GroupByCodeAsync(string inviteCode, CancellationToken ct);

    Task<string> CompetitionKeyAsync(long seasonId, CancellationToken ct);

    Task<bool> CodeTakenAsync(string inviteCode, CancellationToken ct);

    void Add(FantasyGroup group);

    Task<GroupMember?> MemberAsync(long groupId, long teamId, CancellationToken ct);

    void AddMember(GroupMember member);

    void RemoveMember(GroupMember member);

    /// <summary>Official groups (and paid ones when visible) of the season, plus every group the team is in.</summary>
    Task<IReadOnlyList<GroupDto>> GroupsAsync(long seasonId, long? teamId, Guid? userId, bool withPaid, CancellationToken ct);

    Task<GroupDto?> GroupDtoAsync(long groupId, long? teamId, Guid? userId, CancellationToken ct);

    /// <summary>The top rows by total points, plus the caller's row when lower down.</summary>
    Task<IReadOnlyList<StandingRowDto>> TableAsync(long groupId, long? teamId, int take, CancellationToken ct);

    /// <summary>The teams at the given ranks of a group (ties share a rank), with their manager.</summary>
    Task<IReadOnlyList<(int Rank, long TeamId, string Team, Guid UserId, string Manager)>> AtRanksAsync(long groupId, IReadOnlyCollection<int> ranks, CancellationToken ct);

    Task<IReadOnlyList<AdminGroupDto>> AdminGroupsAsync(string? competition, CancellationToken ct);

    Task<long?> CurrentSeasonIdAsync(string competition, CancellationToken ct);
}

/// <summary>Fantasy Pass rows, prices and the seller-level discount.</summary>
public interface IFantasyPassRepository
{
    Task<IReadOnlyList<(int Id, string Key)>> CompetitionsAsync(CancellationToken ct);

    Task<IReadOnlyList<FantasyPass>> ActiveAsync(Guid userId, DateTime now, CancellationToken ct);

    /// <summary>The same plan bought by the same person in the last minutes (a repeated request).</summary>
    Task<FantasyPass?> RecentAsync(Guid userId, int? competitionId, DateTime since, CancellationToken ct);

    void Add(FantasyPass pass);

    Task<FantasyPass?> ByPaymentAsync(long paymentId, CancellationToken ct);

    /// <summary>Setting <c>fantasy.passPrice</c> (one league / both).</summary>
    Task<(decimal One, decimal Both)> PricesAsync(CancellationToken ct);

    /// <summary>The seller level's fantasy discount (0–1), 0 for everyone else.</summary>
    Task<decimal> SellerDiscountAsync(Guid userId, CancellationToken ct);

    /// <summary>True when the primary payment gateway is live (paid groups appear then).</summary>
    Task<bool> PaymentsLiveAsync(CancellationToken ct);
}

/// <summary>Sponsors (admin).</summary>
public interface ISponsorRepository
{
    Task<IReadOnlyList<SponsorDto>> ListAsync(CancellationToken ct);

    Task<Sponsor?> FindAsync(long id, CancellationToken ct);

    Task<bool> NameTakenAsync(string name, long? exceptId, CancellationToken ct);

    void Add(Sponsor sponsor);
}

/// <summary>
/// Prize vouchers (money.Vouchers, source "fantasy prize"). A seam for Phase 5: money is built later by the main window;
/// until then the voucher row is written directly and spent through the shop's voucher code.
/// </summary>
public interface IPrizeVouchers
{
    /// <summary>True when vouchers with this code prefix were already given (winners announced once).</summary>
    Task<bool> GivenAsync(string codePrefix, CancellationToken ct);

    Task<string> IssueAsync(Guid userId, decimal amount, string codePrefix, CancellationToken ct);
}
