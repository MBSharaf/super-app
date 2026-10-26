using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Bookings;
using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Tournaments;

/// <summary>Builds the site's view of a tournament.</summary>
public sealed class TournamentViews(ITournamentStore store, ICurrentUser currentUser, IClock clock)
{
    public async Task<TournamentDto> ViewAsync(Tournament t, bool organiser, CancellationToken ct, string? paymentUrl = null) =>
        (await ViewAsync([t], organiser, ct))[0] with { PaymentUrl = paymentUrl };

    public async Task<IReadOnlyList<TournamentDto>> ViewAsync(IReadOnlyList<Tournament> list, bool organiser, CancellationToken ct)
    {
        var names = await store.NamesAsync(list.SelectMany(t => t.Teams).Select(x => x.CaptainId).Distinct(), ct);
        return list.Select(t => ToDto(t, organiser, names)).ToList();
    }

    private TournamentDto ToDto(Tournament t, bool organiser, IReadOnlyDictionary<Guid, string> names)
    {
        var hour = TournamentRules.Hour(t);
        var me = currentUser.UserId;
        var active = TournamentRules.Active(t).ToList();
        var teams = t.Teams.Where(x => x.Status == TournamentTeamStatus.Registered || organiser).OrderBy(x => x.Id).Select(x => new TournamentTeamDto(x.Id, x.Name,
            names.GetValueOrDefault(x.CaptainId, "Captain"), organiser ? x.Phone : null, x.PaymentId is not null && x.Payment?.Status == PaymentStatus.Paid,
            me is not null && x.CaptainId == me && x.CaptainId != t.Partner.UserId, x.Status.ToString())).ToList();
        var matches = t.Matches.Where(m => m.Booking is not null).OrderBy(m => m.Round).ThenBy(m => m.Booking!.StartHour)
            .Select(m => new TournamentMatchDto(m.Id, m.Round, m.Booking!.StartHour - hour, m.Booking.Date, m.Booking.StartHour, m.HomeTeamId, m.AwayTeamId,
                m.HomeGoals, m.AwayGoals, m.HomePens is null ? null : m.HomePens > m.AwayPens ? m.HomeTeamId : m.AwayTeamId, m.WinnerTeamId, m.HomeGoals is not null)).ToList();
        return new TournamentDto(t.Id, t.Name, t.Type == TournamentType.KnockoutCup ? "knockout" : "league", t.PitchId, t.Pitch.Slug, t.Pitch.Name, t.Partner.BusinessName,
            t.TeamCount, t.EntryFee, t.PrizeText, t.StartsOn, t.EndsOn, hour, t.RulesText, Status(t, active.Count), TournamentRules.RegistrationClosesOn(t),
            Math.Max(0, t.TeamCount - active.Count), t.ChampionTeamId, teams, matches, t.Type == TournamentType.League ? TournamentRules.Table(t) : []);
    }

    private static string Status(Tournament t, int teams) => t.Status switch
    {
        TournamentStatus.Running => "running",
        TournamentStatus.Finished => "finished",
        TournamentStatus.Cancelled => "cancelled",
        _ => teams >= t.TeamCount ? "full" : "open",
    };

    public DateOnly Today() => DateOnly.FromDateTime(CairoTime.Now(clock));
}

/// <summary>
/// Tournaments for players (API §3.9): list, detail, register a team and pay the entry fee, withdraw before registration
/// closes (refund to the wallet). The captain is the signed-in person; one team per captain per tournament.
/// </summary>
public sealed class TournamentsService(
    ITournamentStore store,
    TournamentViews views,
    TournamentMoney money,
    ILegalRepository legal,
    INotifier notifier,
    IAuditLogger audit,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IRequestInfo requestInfo,
    IValidator<RegisterTeamRequest> rules,
    Payments.IPaymentsService payments)
{
    public async Task<IReadOnlyList<TournamentDto>> ListAsync(string? status, CancellationToken ct)
    {
        var all = await views.ViewAsync(await store.ListAsync(includeFinished: true, ct), organiser: false, ct);
        return all.Where(t => status is null || t.Status == status).ToList();
    }

    public async Task<TournamentDto> GetAsync(long id, CancellationToken ct) =>
        await views.ViewAsync(await FindAsync(id, ct), organiser: false, ct);

    public async Task<TournamentDto> RegisterAsync(long id, RegisterTeamRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var me = currentUser.UserId ?? throw new UnauthorizedException();
        var t = await FindAsync(id, ct);
        Check(t.Status == TournamentStatus.Registration && views.Today() <= TournamentRules.RegistrationClosesOn(t), "Registration has closed.", "registration_closed");
        Check(TournamentRules.Active(t).Count() < t.TeamCount, "This tournament is full.", "full");
        if (TournamentRules.Active(t).Any(x => x.CaptainId == me))
        {
            throw new ConflictException("You already registered a team here.", "already_registered");
        }
        if (TournamentRules.Active(t).Any(x => string.Equals(x.Name, request.TeamName.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException("A team with this name is already registered.", "team_name_taken");
        }
        var team = new TournamentTeam { Tournament = t, Name = request.TeamName.Trim(), CaptainId = me, Phone = request.Phone.Trim(),
            WaiverVersionId = await legal.CurrentVersionIdAsync("waiver", ct), Status = TournamentTeamStatus.Registered };
        t.Teams.Add(team);
        var url = await PayAsync(team, t, me, ct);
        if (team.WaiverVersionId is { } waiver)
        {
            legal.AddAcceptance(waiver, me, "tournament", requestInfo.IpAddress);
        }
        await unitOfWork.SaveChangesAsync(ct);
        if (url is null)
        {
            await money.PaidAsync(team, t, ct);
        }
        await notifier.NotifyUserAsync(t.Partner.UserId, new NotificationMessage(NotificationCategory.Bookings, $"{team.Name} registered for {t.Name}",
            $"{TournamentRules.Active(t).Count()} of {t.TeamCount} teams", "#/dash/tournaments", NotificationTone.Info), ct);
        await audit.LogAsync(AuditSystem.Site, "tournament.registered", $"{team.Name} registered for {t.Name}", "Tournament", t.Id.ToString(), ct: ct);
        return await views.ViewAsync(t, organiser: false, ct, url);
    }

    /// <summary>The captain withdraws before registration closes; a paid fee goes back to the wallet.</summary>
    public async Task<TournamentDto> WithdrawAsync(long id, long teamId, CancellationToken ct)
    {
        var me = currentUser.UserId ?? throw new UnauthorizedException();
        var t = await FindAsync(id, ct);
        var team = TournamentRules.Active(t).FirstOrDefault(x => x.Id == teamId && x.CaptainId == me) ?? throw new NotFoundException("Team not found.");
        Check(t.Status == TournamentStatus.Registration && views.Today() <= TournamentRules.RegistrationClosesOn(t), "Registration has closed — ask the venue.", "registration_closed");
        await money.RefundAsync(team, t, me, "Team withdrawn", ct);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Site, "tournament.withdrawn", $"{team.Name} withdrew from {t.Name}", "Tournament", t.Id.ToString(), ct: ct);
        return await views.ViewAsync(t, organiser: false, ct);
    }

    /// <summary>Starts the payment (null = nothing to pay or paid at once by the simulated gateway).</summary>
    private async Task<string?> PayAsync(TournamentTeam team, Tournament t, Guid captain, CancellationToken ct)
    {
        if (t.EntryFee <= 0)
        {
            return null;
        }
        var started = await payments.StartAsync(new Payments.PaymentRequest(PaymentPurpose.TournamentFee, t.EntryFee, captain, $"{t.Name} entry — {team.Name}", $"#/tournaments/{t.Id}"), ct);
        team.Payment = started.Payment;
        return started.RedirectUrl;
    }

    private async Task<Tournament> FindAsync(long id, CancellationToken ct)
    {
        var t = await store.FindAsync(id, ct);
        return t is null || t.Status == TournamentStatus.Cancelled ? throw new NotFoundException("This tournament isn't available.") : t;
    }

    internal static void Check(bool ok, string message, string code)
    {
        if (!ok)
        {
            throw new BusinessRuleException(message, code);
        }
    }
}

/// <summary>Entry fees: the ledger when a team paid, a refund to the captain's wallet.</summary>
public sealed class TournamentMoney(Money.ILedger ledger, ITournamentStore store, ISettingsReader settings, INotifier notifier)
{
    /// <summary>Books a paid entry fee into the ledger (the venue's share is held until the day after the first match day).</summary>
    public async Task PaidAsync(TournamentTeam team, Tournament t, CancellationToken ct)
    {
        if (team.Payment is not { Status: PaymentStatus.Paid } payment)
        {
            return;
        }
        var rate = await store.PartnerCommissionAsync(t.PartnerId, ct) ?? await settings.DecimalAsync("commission.bookings", 0.10m, ct) * 100;
        var release = CairoTime.ToUtc(t.StartsOn.AddDays(1).ToDateTime(TimeOnly.MinValue));
        await ledger.EntryFeeAsync(payment, t.Partner.UserId, rate, "TournamentTeam", team.Id, release, ct);
        await notifier.NotifyUserAsync(team.CaptainId, new NotificationMessage(NotificationCategory.Bookings, $"{team.Name} is in!",
            $"You're registered for {t.Name}. Fixtures come out after registration closes.", $"#/tournaments/{t.Id}", NotificationTone.Success), ct);
    }

    /// <summary>Takes the team out and gives a paid fee back to the captain's wallet.</summary>
    public async Task RefundAsync(TournamentTeam team, Tournament t, Guid requestedBy, string reason, CancellationToken ct)
    {
        var paid = team.Payment is { Status: PaymentStatus.Paid };
        team.Status = paid ? TournamentTeamStatus.Refunded : TournamentTeamStatus.Withdrawn;
        if (paid)
        {
            await ledger.ReverseAsync(team.Payment!, team.CaptainId, "TournamentTeam", team.Id, requestedBy, reason, ct);
            await notifier.NotifyUserAsync(team.CaptainId, new NotificationMessage(NotificationCategory.Money, $"{t.EntryFee:0} EGP back in your wallet",
                $"{team.Name} · {t.Name}: {reason}", "#/wallet", NotificationTone.Info), ct);
        }
    }
}

public sealed class RegisterTeamRequestValidator : AbstractValidator<RegisterTeamRequest>
{
    public RegisterTeamRequestValidator()
    {
        RuleFor(x => x.TeamName).NotEmpty().Length(3, 40).WithMessage("Enter your team name (3–40 characters).");
        RuleFor(x => x.Phone).NotEmpty().Matches(@"^\+?[0-9 ]{8,16}$").WithMessage("Enter a valid phone number.");
        RuleFor(x => x.AcceptWaiver).Equal(true).WithMessage("Please accept the injury waiver.");
    }
}

/// <summary>The gateway confirmed (or refused) a team's entry fee.</summary>
public sealed class TournamentPaymentListener(ITournamentStore store, TournamentMoney money, IUnitOfWork unitOfWork) : Payments.IPaymentListener
{
    public PaymentPurpose Purpose => PaymentPurpose.TournamentFee;

    public async Task PaidAsync(Payment payment, CancellationToken ct)
    {
        var t = await store.ByTeamPaymentAsync(payment.Id, ct);
        var team = t?.Teams.FirstOrDefault(x => x.PaymentId == payment.Id);
        if (t is null || team is null)
        {
            return;
        }
        team.Payment = payment;
        await money.PaidAsync(team, t, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task FailedAsync(Payment payment, CancellationToken ct)
    {
        var t = await store.ByTeamPaymentAsync(payment.Id, ct);
        if (t?.Teams.FirstOrDefault(x => x.PaymentId == payment.Id) is { Status: TournamentTeamStatus.Registered } team)
        {
            team.Status = TournamentTeamStatus.Withdrawn;
            await unitOfWork.SaveChangesAsync(ct);
        }
    }
}
