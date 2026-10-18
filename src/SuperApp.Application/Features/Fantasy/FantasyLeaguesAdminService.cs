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
/// Admin: sponsors, official and paid groups with their prizes, and winners (API §7.2, FR-FAN-09/11). Prizes are sponsor
/// rewards and shop vouchers — never cash and never a pool of entry fees. Paid groups need fantasy.groups.paid and a
/// live payment gateway (423 until then).
/// </summary>
public interface IFantasyLeaguesAdminService
{
    Task<IReadOnlyList<SponsorDto>> SponsorsAsync(CancellationToken ct);

    Task<SponsorDto> SaveSponsorAsync(long? id, SponsorRequest request, CancellationToken ct);

    Task DeleteSponsorAsync(long id, CancellationToken ct);

    Task<IReadOnlyList<AdminGroupDto>> GroupsAsync(string? competition, CancellationToken ct);

    Task<AdminGroupDto> SaveGroupAsync(long? id, AdminGroupRequest request, CancellationToken ct);

    Task<IReadOnlyList<WinnerDto>> WinnersAsync(WinnersRequest request, CancellationToken ct);
}

public sealed class FantasyLeaguesAdminService(
    ISponsorRepository sponsors,
    IFantasyGroupRepository groups,
    IFantasyPassRepository passes,
    IPrizeVouchers vouchers,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IValidator<SponsorRequest> sponsorRules,
    IValidator<AdminGroupRequest> groupRules,
    IValidator<WinnersRequest> winnerRules) : IFantasyLeaguesAdminService
{
    private const string CodeLetters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public Task<IReadOnlyList<SponsorDto>> SponsorsAsync(CancellationToken ct) => sponsors.ListAsync(ct);

    public async Task<SponsorDto> SaveSponsorAsync(long? id, SponsorRequest request, CancellationToken ct)
    {
        await sponsorRules.ValidateAndThrowAsync(request, ct);
        if (await sponsors.NameTakenAsync(request.Name.Trim(), id, ct))
        {
            throw new ConflictException("A sponsor with this name exists.", "sponsor_exists");
        }
        var sponsor = id is null ? new Sponsor() : await sponsors.FindAsync(id.Value, ct) ?? throw new NotFoundException("Sponsor not found.");
        if (id is null)
        {
            sponsors.Add(sponsor);
        }
        (sponsor.Name, sponsor.Tier, sponsor.ContactEmail, sponsor.ContractValue) = (request.Name.Trim(), Enum.Parse<SponsorTier>(request.Tier, true), request.ContactEmail, request.ContractValue);
        (sponsor.StartsOn, sponsor.EndsOn, sponsor.Color) = (request.StartsOn, request.EndsOn, request.Color.ToUpperInvariant());
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, id is null ? "fantasy.sponsor.created" : "fantasy.sponsor.updated", $"Sponsor {sponsor.Name}", "Sponsor", sponsor.Id.ToString(), ct: ct);
        return new SponsorDto(sponsor.Id, sponsor.Name, sponsor.Tier.ToString(), sponsor.ContactEmail, sponsor.ContractValue, sponsor.StartsOn, sponsor.EndsOn, sponsor.Color);
    }

    public async Task DeleteSponsorAsync(long id, CancellationToken ct)
    {
        var sponsor = await sponsors.FindAsync(id, ct) ?? throw new NotFoundException("Sponsor not found.");
        sponsor.IsDeleted = true;
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, "fantasy.sponsor.deleted", $"Sponsor {sponsor.Name} removed", "Sponsor", id.ToString(), ct: ct);
    }

    public Task<IReadOnlyList<AdminGroupDto>> GroupsAsync(string? competition, CancellationToken ct) => groups.AdminGroupsAsync(competition, ct);

    public async Task<AdminGroupDto> SaveGroupAsync(long? id, AdminGroupRequest request, CancellationToken ct)
    {
        await groupRules.ValidateAndThrowAsync(request, ct);
        var type = Enum.Parse<FantasyGroupType>(request.Type, true);
        await CheckPaidAsync(type, ct);
        var seasonId = await groups.CurrentSeasonIdAsync(request.Competition.ToUpperInvariant(), ct) ?? throw new NotFoundException("This league has no season yet.", "no_season");
        if (request.SponsorId is { } sponsorId && await sponsors.FindAsync(sponsorId, ct) is null)
        {
            throw new BadRequestException("Sponsor not found.", "sponsor");
        }
        var group = id is null ? new FantasyGroup { InviteCode = await NewCodeAsync(ct) } : await groups.GroupAsync(id.Value, ct) ?? throw new NotFoundException("Group not found.");
        if (group.Type == FantasyGroupType.Private && id is not null)
        {
            throw new BusinessRuleException("Private groups belong to their players.", "private_group");
        }
        if (id is null)
        {
            groups.Add(group);
        }
        (group.SeasonId, group.Name, group.Type, group.SponsorId) = (seasonId, request.Name.Trim(), type, request.SponsorId);
        (group.PrizeText, group.PrizeValue, group.MonthlyPrice, group.IsHidden) = (request.PrizeText?.Trim(), request.PrizeValue, type == FantasyGroupType.Paid ? request.MonthlyPrice : null, false);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, id is null ? "fantasy.group.created" : "fantasy.group.updated", $"{type} group {group.Name}", "FantasyGroup", group.Id.ToString(), ct: ct);
        return (await groups.AdminGroupsAsync(null, ct)).Single(g => g.Id == group.Id);
    }

    private async Task CheckPaidAsync(FantasyGroupType type, CancellationToken ct)
    {
        if (type == FantasyGroupType.Private)
        {
            throw new BadRequestException("Admins create official or paid groups.", "type");
        }
        if (type == FantasyGroupType.Paid && !currentUser.HasPermission("fantasy.groups.paid"))
        {
            throw new ForbiddenException("Paid groups need the fantasy.groups.paid permission.", "forbidden");
        }
        if (type == FantasyGroupType.Paid && !await passes.PaymentsLiveAsync(ct))
        {
            throw new FeatureDisabledException("Paid groups open when online payments are live.", "payments_not_live");
        }
    }

    public async Task<IReadOnlyList<WinnerDto>> WinnersAsync(WinnersRequest request, CancellationToken ct)
    {
        await winnerRules.ValidateAndThrowAsync(request, ct);
        var group = await groups.GroupAsync(request.GroupId, ct) ?? throw new NotFoundException("Group not found.");
        var prefix = $"FP{group.Id}-";
        if (await vouchers.GivenAsync(prefix, ct))
        {
            throw new ConflictException("The winners of this group were already announced.", "winners_announced");
        }
        var places = request.Places.ToDictionary(p => p.Rank, p => p.Amount);
        var winners = new List<WinnerDto>();
        foreach (var w in await groups.AtRanksAsync(group.Id, places.Keys, ct))
        {
            var code = await vouchers.IssueAsync(w.UserId, places[w.Rank], prefix, ct);
            winners.Add(new WinnerDto(w.Rank, w.Team, w.Manager, places[w.Rank], code));
            await notifier.NotifyUserAsync(w.UserId, new NotificationMessage(NotificationCategory.Fantasy, $"You won in {group.Name}!",
                $"Place {w.Rank}: a {places[w.Rank]:0} EGP shop voucher ({code}).", "#/account", NotificationTone.Success), ct);
        }
        await audit.LogAsync(AuditSystem.Admin, "fantasy.winners", $"{group.Name}: {winners.Count} winner(s) given vouchers", "FantasyGroup", group.Id.ToString(), ct: ct);
        return winners;
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
}

public sealed class SponsorRequestValidator : AbstractValidator<SponsorRequest>
{
    public SponsorRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 100);
        RuleFor(x => x.Tier).Must(t => Enum.TryParse<SponsorTier>(t, true, out _)).WithMessage("Tier: Gold, Silver or Bronze.");
        RuleFor(x => x.ContactEmail).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
        RuleFor(x => x.ContractValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EndsOn).GreaterThanOrEqualTo(x => x.StartsOn).WithMessage("The contract ends after it starts.");
        RuleFor(x => x.Color).Matches("^#[0-9A-Fa-f]{6}$").WithMessage("A colour like #0B6E4F.");
    }
}

public sealed class AdminGroupRequestValidator : AbstractValidator<AdminGroupRequest>
{
    public AdminGroupRequestValidator()
    {
        RuleFor(x => x.Competition).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Name).NotEmpty().Length(3, 60);
        RuleFor(x => x.Type).Must(t => Enum.TryParse<FantasyGroupType>(t, true, out _)).WithMessage("Type: Official or Paid.");
        RuleFor(x => x.PrizeText).MaximumLength(200)
            .Must(t => t is null || !t.Contains("cash", StringComparison.OrdinalIgnoreCase) && !t.Contains("نقد", StringComparison.Ordinal))
            .WithMessage("Prizes are sponsor rewards and vouchers — never cash.");
        RuleFor(x => x.PrizeValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MonthlyPrice).NotNull().GreaterThan(0).When(x => string.Equals(x.Type, "Paid", StringComparison.OrdinalIgnoreCase))
            .WithMessage("A paid group needs its monthly price.");
    }
}

public sealed class WinnersRequestValidator : AbstractValidator<WinnersRequest>
{
    public WinnersRequestValidator()
    {
        RuleFor(x => x.GroupId).GreaterThan(0);
        RuleFor(x => x.Places).NotNull().Must(p => p.Count is >= 1 and <= 100 && p.Select(x => x.Rank).Distinct().Count() == p.Count)
            .WithMessage("1 to 100 places, each rank once.");
        RuleForEach(x => x.Places).ChildRules(p =>
        {
            p.RuleFor(x => x.Rank).InclusiveBetween(1, 1000);
            p.RuleFor(x => x.Amount).GreaterThanOrEqualTo(100).WithMessage("A voucher is 100 EGP or more.");
        });
    }
}
