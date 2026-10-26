using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Bookings;
using SuperApp.Application.Features.Partners;
using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.FreeSlots;

/// <summary>POST /partner/free-slots — <c>Date</c> the grid day, <c>Hour</c> the grid hour (25 = 1 AM); 5–90% off.</summary>
public sealed record PostFreeSlotRequest(long PitchId, DateOnly Date, int Hour, decimal DiscountPercent);

/// <summary>A live free-hour card. <c>Date</c>/<c>Hour</c> are the grid day and hour, as the pitch page shows them.</summary>
public sealed record FreeSlotDto(long Id, string PitchSlug, string PitchName, string AreaEn, int AreaId, DateOnly Date, int Hour, decimal DiscountPercent, decimal Price, decimal OldPrice);

public sealed record FreeSlotsQuery(int? AreaId = null, DateOnly? From = null, DateOnly? To = null);

/// <summary>POST /alerts — follow a pitch (slug) or an area, optionally only between two hours.</summary>
public sealed record AlertRequest(string? PitchSlug, int? AreaId, int? FromHour, int? ToHour);

public sealed record AlertDto(long Id, string? PitchSlug, string? PitchName, int? AreaId, string? AreaEn, int? FromHour, int? ToHour, DateTime CreatedAt);

public interface IFreeSlotRepository
{
    Task<int> PostedTodayAsync(long partnerId, DateTime sinceUtc, CancellationToken ct);

    Task<bool> PostingBlockedAsync(long partnerId, CancellationToken ct);

    Task<bool> LiveExistsAsync(long pitchId, DateOnly date, int hour, CancellationToken ct);

    void Add(FreeSlot slot, Offer offer);

    /// <summary>A live free slot of the partner with its offer, tracked.</summary>
    Task<FreeSlot?> PartnerSlotAsync(long partnerId, long id, CancellationToken ct);

    Task<IReadOnlyList<FreeSlotDto>> LiveAsync(FreeSlotsQuery query, DateOnly today, long? partnerId, CancellationToken ct);

    Task<FreeSlotDto?> GetAsync(long id, CancellationToken ct);

    /// <summary>People following this pitch or its area whose time window covers the hour (calendar hour).</summary>
    Task<IReadOnlyList<Guid>> FollowersAsync(long pitchId, int hour, CancellationToken ct);

    Task<int?> PitchAreaAsync(long pitchId, CancellationToken ct);

    Task<bool> AreaExistsAsync(int areaId, CancellationToken ct);

    void AddAlert(SlotAlert alert);

    Task<IReadOnlyList<AlertDto>> AlertsAsync(Guid userId, CancellationToken ct);

    Task<SlotAlert?> AlertAsync(Guid userId, long id, CancellationToken ct);

    void RemoveAlert(SlotAlert alert);
}

/// <summary>
/// Free slots and alerts (API §3.7). A partner posts a free hour with a discount (it becomes a one-hour offer on the grid);
/// a daily limit per partner (setting <c>freeSlots.dailyLimit</c>, default 5) and the admin's posting block apply.
/// Players following the pitch or its area are told at once.
/// </summary>
public interface IFreeSlotsService
{
    Task<FreeSlotDto> PostAsync(PostFreeSlotRequest request, CancellationToken ct);

    Task RemoveAsync(long id, CancellationToken ct);

    Task<IReadOnlyList<FreeSlotDto>> PartnerListAsync(CancellationToken ct);

    Task<IReadOnlyList<FreeSlotDto>> LiveAsync(FreeSlotsQuery query, CancellationToken ct);

    Task<AlertDto> FollowAsync(AlertRequest request, CancellationToken ct);

    Task<IReadOnlyList<AlertDto>> AlertsAsync(CancellationToken ct);

    Task UnfollowAsync(long id, CancellationToken ct);
}

public sealed class FreeSlotsService(
    IPartnerAccess access,
    IPartnerBookingsRepository partnerPitches,
    IFreeSlotRepository slots,
    IBookingRepository bookings,
    BookingPlanner planner,
    ISettingsReader settings,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<PostFreeSlotRequest> postRules,
    IValidator<AlertRequest> alertRules) : IFreeSlotsService
{
    public async Task<FreeSlotDto> PostAsync(PostFreeSlotRequest request, CancellationToken ct)
    {
        await postRules.ValidateAndThrowAsync(request, ct);
        var scope = await access.ManageAsync(ct);
        var pitch = await partnerPitches.PitchAsync(scope.PartnerId, request.PitchId, ct) ?? throw new NotFoundException("Pitch not found.");
        await CheckLimitsAsync(scope, ct);
        var plan = await planner.PlanAsync(pitch.Slug, request.Date, request.Hour, 1, ct);
        var line = plan.Lines[0];
        if (await slots.LiveExistsAsync(plan.Pitch.Id, line.Date, line.CalendarHour, ct))
        {
            throw new ConflictException("This hour is already posted.", "already_posted");
        }
        var offer = new Offer
        {
            PitchId = plan.Pitch.Id, DaysMask = (byte)(1 << (int)request.Date.DayOfWeek), FromHour = (byte)(request.Hour % 24), ToHour = (byte)(request.Hour % 24 + 1),
            DiscountPercent = request.DiscountPercent, StartsOn = request.Date, EndsOn = request.Date, IsActive = true, CreatedById = currentUser.UserId,
        };
        var slot = new FreeSlot { PitchId = plan.Pitch.Id, PostedById = currentUser.UserId!.Value, Date = line.Date, Hour = (byte)line.CalendarHour,
            DiscountPercent = request.DiscountPercent, Offer = offer, Status = FreeSlotStatus.Live };
        slots.Add(slot, offer);
        await unitOfWork.SaveChangesAsync(ct);
        await TellFollowersAsync(plan.Pitch.Id, pitch.Name, request, line.CalendarHour, ct);
        await audit.LogAsync(AuditSystem.Partner, "free_slot.posted", $"{pitch.Name} {line.Date:yyyy-MM-dd} {line.CalendarHour:00}:00 at {request.DiscountPercent:0}% off", "Pitch", pitch.Slug, ct: ct);
        return (await slots.GetAsync(slot.Id, ct))!;
    }

    private async Task CheckLimitsAsync(PartnerScope scope, CancellationToken ct)
    {
        if (await slots.PostingBlockedAsync(scope.PartnerId, ct))
        {
            throw new FeatureDisabledException("Posting free slots is paused for your account. Contact Super App support.", "posting_blocked");
        }
        var limit = (int)await settings.DecimalAsync("freeSlots.dailyLimit", 5, ct);
        var since = CairoTime.ToUtc(CairoTime.Now(clock).Date);
        if (await slots.PostedTodayAsync(scope.PartnerId, since, ct) >= limit)
        {
            throw new BusinessRuleException($"You can post up to {limit} free slots a day.", "daily_limit");
        }
    }

    private async Task TellFollowersAsync(long pitchId, string pitchName, PostFreeSlotRequest r, int hour, CancellationToken ct)
    {
        var followers = await slots.FollowersAsync(pitchId, hour, ct);
        if (followers.Count == 0)
        {
            return;
        }
        var message = new NotificationMessage(NotificationCategory.Bookings, $"Free slot at {pitchName}: {r.DiscountPercent:0}% off",
            $"{r.Date:ddd d MMM} · {hour:00}:00", $"#/pitches/p-{(await bookings.PitchInfoAsync(pitchId, ct)).Slug}?date={r.Date:yyyy-MM-dd}", NotificationTone.Success);
        await notifier.NotifyManyAsync(followers.Select(u => (u, message)).ToList(), ct);
    }

    public async Task RemoveAsync(long id, CancellationToken ct)
    {
        var scope = await access.ManageAsync(ct);
        var slot = await slots.PartnerSlotAsync(scope.PartnerId, id, ct) ?? throw new NotFoundException("Free slot not found.");
        slot.Status = FreeSlotStatus.Expired;
        if (slot.Offer is not null)
        {
            slot.Offer.IsActive = false;
        }
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FreeSlotDto>> PartnerListAsync(CancellationToken ct) =>
        await slots.LiveAsync(new FreeSlotsQuery(), Today(), (await access.ViewAsync(ct)).PartnerId, ct);

    public Task<IReadOnlyList<FreeSlotDto>> LiveAsync(FreeSlotsQuery query, CancellationToken ct) => slots.LiveAsync(query, Today(), null, ct);

    public async Task<AlertDto> FollowAsync(AlertRequest request, CancellationToken ct)
    {
        await alertRules.ValidateAndThrowAsync(request, ct);
        long? pitchId = null;
        if (!string.IsNullOrWhiteSpace(request.PitchSlug))
        {
            pitchId = (await bookings.PitchAsync(request.PitchSlug, ct) ?? throw new NotFoundException("This pitch does not exist.")).Id;
        }
        if (request.AreaId is { } area && !await slots.AreaExistsAsync(area, ct))
        {
            throw new BadRequestException("Choose an area from the list.", "unknown_area");
        }
        var alert = new SlotAlert { UserId = Me(), PitchId = pitchId, AreaId = request.AreaId, FromHour = (byte?)request.FromHour, ToHour = (byte?)request.ToHour, IsActive = true };
        slots.AddAlert(alert);
        await unitOfWork.SaveChangesAsync(ct);
        return (await slots.AlertsAsync(Me(), ct)).Single(a => a.Id == alert.Id);
    }

    public Task<IReadOnlyList<AlertDto>> AlertsAsync(CancellationToken ct) => slots.AlertsAsync(Me(), ct);

    public async Task UnfollowAsync(long id, CancellationToken ct)
    {
        var alert = await slots.AlertAsync(Me(), id, ct) ?? throw new NotFoundException("Alert not found.");
        slots.RemoveAlert(alert);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private DateOnly Today() => DateOnly.FromDateTime(CairoTime.Now(clock));

    private Guid Me() => currentUser.UserId ?? throw new UnauthorizedException();
}

public sealed class PostFreeSlotRequestValidator : AbstractValidator<PostFreeSlotRequest>
{
    public PostFreeSlotRequestValidator()
    {
        RuleFor(x => x.PitchId).GreaterThan(0);
        RuleFor(x => x.Hour).InclusiveBetween(0, 29);
        RuleFor(x => x.DiscountPercent).InclusiveBetween(5, 90).WithMessage("Give 5% to 90% off.");
    }
}

public sealed class AlertRequestValidator : AbstractValidator<AlertRequest>
{
    public AlertRequestValidator()
    {
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.PitchSlug) || x.AreaId is not null).WithName("pitchSlug").WithMessage("Follow a pitch or an area.");
        RuleFor(x => x.FromHour).InclusiveBetween(0, 23).When(x => x.FromHour is not null);
        RuleFor(x => x.ToHour).InclusiveBetween(1, 24).When(x => x.ToHour is not null)
            .GreaterThan(x => x.FromHour).When(x => x.ToHour is not null && x.FromHour is not null);
    }
}
