using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Bookings;
using SuperApp.Application.Features.Partners;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Tournaments;

/// <summary>
/// The venue runs its tournaments (owner or manager). Creating one reserves every match hour as a tournament booking
/// (409 when an hour is taken), so the hours leave the booking grid. The draw keeps the hours the teams need and frees the
/// rest; results move knockout winners on and finish the tournament; cancelling refunds every paid team and frees all hours.
/// </summary>
public sealed class PartnerTournamentsService(
    IPartnerAccess access,
    IPartnerBookingsRepository partnerBookings,
    IBookingRepository bookings,
    BookingPlanner planner,
    ITournamentStore store,
    TournamentViews views,
    TournamentMoney money,
    INotifier notifier,
    IAuditLogger audit,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<CreateTournamentRequest> createRules)
{
    public async Task<IReadOnlyList<TournamentDto>> MineAsync(CancellationToken ct) =>
        await views.ViewAsync(await store.OfPartnerAsync((await access.ViewAsync(ct)).PartnerId, ct), organiser: true, ct);

    public async Task<TournamentDto> GetAsync(long id, CancellationToken ct) => await views.ViewAsync(await OwnAsync(id, ct), organiser: true, ct);

    public async Task<TournamentDto> CreateAsync(CreateTournamentRequest r, CancellationToken ct)
    {
        await createRules.ValidateAndThrowAsync(r, ct);
        var scope = await access.ManageAsync(ct);
        var pitch = await partnerBookings.PitchAsync(scope.PartnerId, r.PitchId, ct) ?? throw new NotFoundException("Pitch not found.");
        TournamentsService.Check(r.StartsOn.AddDays(-TournamentRules.RegistrationClosesDaysBefore) >= views.Today(),
            $"The first match day must be at least {TournamentRules.RegistrationClosesDaysBefore} days away (registration closes then).", "too_soon");
        var type = r.Type.Equals("league", StringComparison.OrdinalIgnoreCase) ? TournamentType.League : TournamentType.KnockoutCup;
        var rounds = TournamentRules.Rounds(type, r.TeamCount);
        var t = new Tournament
        {
            PartnerId = scope.PartnerId, PitchId = r.PitchId, Name = r.Name.Trim(), Type = type, TeamCount = (byte)r.TeamCount, EntryFee = r.EntryFee,
            PrizeText = r.PrizeText?.Trim(), StartsOn = r.StartsOn, EndsOn = TournamentRules.DayOf(r.StartsOn, rounds.Count), MatchHours = r.Hour.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RulesText = r.RulesText.Trim(), Status = TournamentStatus.Registration,
        };
        var plans = new List<(Booking Booking, IReadOnlyList<(DateOnly, int)> Slots, int Round)>();
        for (var round = 1; round <= rounds.Count; round++)
        {
            for (var slot = 0; slot < rounds[round - 1]; slot++)
            {
                var (booking, slots) = await ReserveAsync(t, pitch.Slug, round, r.Hour + slot, ct);
                plans.Add((booking, slots, round));
            }
        }
        foreach (var (booking, slots, _) in plans)
        {
            await bookings.SaveAsync(booking, slots, ct);
        }
        foreach (var (booking, _, round) in plans)
        {
            t.Matches.Add(new TournamentMatch { Round = (byte)round, BookingId = booking.Id, Booking = booking });
        }
        store.Add(t);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Partner, "tournament.created", $"{t.Name} at {pitch.Name}: {plans.Count} match hours reserved", "Tournament", t.Id.ToString(), ct: ct);
        return await GetAsync(t.Id, ct);
    }

    /// <summary>Plans one match hour with the booking rules (outside hours / taken → 422 / 409) and its booking.</summary>
    private async Task<(Booking, IReadOnlyList<(DateOnly, int)>)> ReserveAsync(Tournament t, string slug, int round, int hour, CancellationToken ct)
    {
        var plan = await planner.PlanAsync(slug, TournamentRules.DayOf(t.StartsOn, round), hour, 1, ct);
        var booking = new Booking
        {
            Code = await bookings.NewCodeAsync(ct), PitchId = t.PitchId, Source = BookingSource.Tournament, WalkInName = t.Name.Length > 80 ? t.Name[..80] : t.Name,
            Date = plan.Lines[0].Date, StartHour = (byte)plan.Lines[0].CalendarHour, Duration = 1, PayAtVenue = true, PolicyKey = plan.Pitch.PolicyKey, Status = BookingStatus.Confirmed,
        };
        return (booking, plan.Slots);
    }

    public async Task<TournamentDto> AddTeamAsync(long id, VenueTeamRequest r, CancellationToken ct)
    {
        var scope = await access.ManageAsync(ct);
        var t = await OwnAsync(id, ct);
        TournamentsService.Check(t.Status == TournamentStatus.Registration, "The fixtures are out — teams can't be added now.", "already_drawn");
        TournamentsService.Check(TournamentRules.Active(t).Count() < t.TeamCount, "All spots are taken.", "full");
        TournamentsService.Check(r.TeamName.Trim().Length is >= 3 and <= 40, "Enter the team name (3–40 characters).", "team_name");
        if (TournamentRules.Active(t).Any(x => string.Equals(x.Name, r.TeamName.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException("That team is already registered.", "team_name_taken");
        }
        t.Teams.Add(new TournamentTeam { Name = r.TeamName.Trim(), CaptainId = scope.OwnerUserId, Phone = r.Phone?.Trim() ?? string.Empty, Status = TournamentTeamStatus.Registered });
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<TournamentDto> RemoveTeamAsync(long id, long teamId, CancellationToken ct)
    {
        var scope = await access.ManageAsync(ct);
        var t = await OwnAsync(id, ct);
        TournamentsService.Check(t.Status == TournamentStatus.Registration, "The fixtures are out — teams can't be removed now.", "already_drawn");
        var team = TournamentRules.Active(t).FirstOrDefault(x => x.Id == teamId) ?? throw new NotFoundException("Team not found.");
        await money.RefundAsync(team, t, scope.OwnerUserId, "Removed by the venue", ct);
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Draws the fixtures for the registered teams (knockout: 4, 8 or 16; league: 3 or more) and frees unneeded hours.</summary>
    public async Task<TournamentDto> DrawAsync(long id, CancellationToken ct)
    {
        await access.ManageAsync(ct);
        var t = await OwnAsync(id, ct);
        TournamentsService.Check(t.Status == TournamentStatus.Registration, "The fixtures are already out.", "already_drawn");
        var ids = TournamentRules.Active(t).Select(x => x.Id).OrderBy(x => HashCode.Combine(t.Id, x)).ToList();
        var knockout = t.Type == TournamentType.KnockoutCup;
        TournamentsService.Check(knockout ? ids.Count is 4 or 8 or 16 : ids.Count >= 3,
            knockout ? $"A knockout cup needs 4, 8 or 16 teams — you have {ids.Count}." : "A league needs at least 3 teams.", "not_enough_teams");
        var need = TournamentRules.Rounds(t.Type, ids.Count);
        var hour = TournamentRules.Hour(t);
        foreach (var m in t.Matches.ToList().Where(m => m.Round > need.Count || m.Booking!.StartHour - hour >= need[m.Round - 1]))
        {
            await FreeAsync(t, m, ct);
        }
        var byRound = t.Matches.GroupBy(m => (int)m.Round).ToDictionary(g => g.Key, g => g.OrderBy(m => m.Booking!.StartHour).ToList());
        var pairs = knockout ? [Enumerable.Range(0, ids.Count / 2).Select(j => (ids[2 * j], ids[2 * j + 1])).ToList()] : TournamentRules.RoundRobin(ids);
        for (var r = 0; r < pairs.Count; r++)
        {
            for (var j = 0; j < pairs[r].Count; j++)
            {
                (byRound[r + 1][j].HomeTeamId, byRound[r + 1][j].AwayTeamId) = pairs[r][j];
            }
        }
        t.EndsOn = TournamentRules.DayOf(t.StartsOn, need.Count);
        t.Status = TournamentStatus.Running;
        await unitOfWork.SaveChangesAsync(ct);
        await TellCaptainsAsync(t, $"{t.Name}: fixtures are out", "See who you play and when.", ct);
        await audit.LogAsync(AuditSystem.Partner, "tournament.drawn", $"Fixtures for {t.Name} ({t.Matches.Count} matches)", "Tournament", t.Id.ToString(), ct: ct);
        return await GetAsync(id, ct);
    }

    public async Task<TournamentDto> ResultAsync(long id, long matchId, MatchResultRequest r, CancellationToken ct)
    {
        await access.ManageAsync(ct);
        var t = await OwnAsync(id, ct);
        TournamentsService.Check(t.Status is TournamentStatus.Running or TournamentStatus.Finished, "Draw the fixtures first.", "not_drawn");
        TournamentsService.Check(r.HomeGoals is >= 0 and <= 40 && r.AwayGoals is >= 0 and <= 40, "Enter whole-number scores (0–40).", "invalid_score");
        var m = t.Matches.FirstOrDefault(x => x.Id == matchId) ?? throw new NotFoundException("Match not found.");
        var hour = TournamentRules.Hour(t);
        TournamentRules.SetResult(t, m, m.Booking!.StartHour - hour, r, x => x.Booking!.StartHour - hour);
        await unitOfWork.SaveChangesAsync(ct);
        if (t.Status == TournamentStatus.Finished)
        {
            var champion = t.Teams.First(x => x.Id == t.ChampionTeamId).Name;
            await TellCaptainsAsync(t, $"{t.Name} is over", $"Champions: {champion} 🏆", ct);
            await audit.LogAsync(AuditSystem.Partner, "tournament.finished", $"{t.Name} finished — champion {champion}", "Tournament", t.Id.ToString(), ct: ct);
        }
        return await GetAsync(id, ct);
    }

    public async Task<TournamentDto> CancelAsync(long id, CancellationToken ct)
    {
        var scope = await access.ManageAsync(ct);
        await CancelAsync(await OwnAsync(id, ct), scope.OwnerUserId, AuditSystem.Partner, ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Refunds every paid team, frees every reserved hour, tells the captains (also used by admins).</summary>
    public async Task CancelAsync(Tournament t, Guid by, AuditSystem system, CancellationToken ct)
    {
        TournamentsService.Check(t.Status is TournamentStatus.Registration or TournamentStatus.Running, "This tournament is already over.", "already_over");
        await TellCaptainsAsync(t, $"{t.Name} was cancelled", t.EntryFee > 0 ? "Your entry fee is back in your Super App wallet." : "Sorry for the change of plans.", ct);
        foreach (var team in TournamentRules.Active(t).ToList())
        {
            await money.RefundAsync(team, t, by, "Tournament cancelled", ct);
        }
        foreach (var m in t.Matches.Where(m => m.HomeGoals is null).ToList())
        {
            await FreeAsync(t, m, ct, keepRow: true);
        }
        t.Status = TournamentStatus.Cancelled;
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(system, "tournament.cancelled", $"{t.Name} cancelled", "Tournament", t.Id.ToString(), ct: ct);
    }

    private async Task FreeAsync(Tournament t, TournamentMatch m, CancellationToken ct, bool keepRow = false)
    {
        if (m.Booking is { } b)
        {
            b.Status = BookingStatus.CancelledByVenue;
            b.CancelledAt = clock.UtcNow;
            await unitOfWork.SaveChangesAsync(ct);
            await bookings.ReleaseSlotsAsync(b.Id, ct);
        }
        if (!keepRow)
        {
            t.Matches.Remove(m);
            store.Remove(m);
        }
    }

    private async Task TellCaptainsAsync(Tournament t, string title, string body, CancellationToken ct)
    {
        foreach (var captain in TournamentRules.Active(t).Select(x => x.CaptainId).Where(c => c != t.Partner.UserId).Distinct())
        {
            await notifier.NotifyUserAsync(captain, new NotificationMessage(NotificationCategory.Bookings, title, body, $"#/tournaments/{t.Id}", NotificationTone.Info), ct);
        }
    }

    private async Task<Tournament> OwnAsync(long id, CancellationToken ct)
    {
        var scope = await access.ViewAsync(ct);
        var t = await store.FindAsync(id, ct);
        return t is not null && t.PartnerId == scope.PartnerId ? t : throw new NotFoundException("Tournament not found.");
    }
}

public sealed class CreateTournamentRequestValidator : AbstractValidator<CreateTournamentRequest>
{
    public CreateTournamentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(4, 80).WithMessage("Give the tournament a name (4–80 characters).");
        RuleFor(x => x.Type).Must(t => t is not null && (t.Equals("knockout", StringComparison.OrdinalIgnoreCase) || t.Equals("league", StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Choose knockout or league.");
        RuleFor(x => x.TeamCount).Must(n => n is 4 or 8 or 16).When(x => string.Equals(x.Type, "knockout", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Knockout cups need 4, 8 or 16 teams.");
        RuleFor(x => x.TeamCount).InclusiveBetween(3, 10).When(x => string.Equals(x.Type, "league", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Leagues can have 3 to 10 teams.");
        RuleFor(x => x.EntryFee).InclusiveBetween(0, 50000);
        RuleFor(x => x.Hour).InclusiveBetween(0, 23);
        RuleFor(x => x.PrizeText).MaximumLength(300);
        RuleFor(x => x.RulesText).NotEmpty().Length(10, 2000).WithMessage("Write a few lines of rules (at least 10 characters).");
    }
}
