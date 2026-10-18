using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Bookings;
using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Waitlist;

/// <summary>POST /waitlist — <c>Date</c> is the grid day and <c>Hour</c> the grid hour (25 = 1 AM), as the hour grid shows them.</summary>
public sealed record JoinWaitlistRequest(string PitchSlug, DateOnly Date, int Hour);

/// <summary>An hour I'm waiting for. Status: Waiting, FreeNow (it was freed — book it before someone else), Booked, Expired.</summary>
public sealed record WaitlistEntryDto(long Id, string PitchSlug, string PitchName, DateOnly Date, int Hour, string Status, int Waiting, DateTime CreatedAt);

public interface IWaitlistRepository
{
    Task<WaitlistEntryDto?> GetAsync(long id, CancellationToken ct);

    Task<bool> ExistsAsync(long pitchId, DateOnly date, int hour, Guid userId, CancellationToken ct);

    void Add(WaitlistEntry entry);

    Task<IReadOnlyList<WaitlistEntryDto>> MineAsync(Guid userId, DateOnly from, CancellationToken ct);

    /// <summary>My entry, tracked for changes.</summary>
    Task<WaitlistEntry?> MineAsync(Guid userId, long id, CancellationToken ct);

    void Remove(WaitlistEntry entry);

    /// <summary>Everyone still waiting for one of these hours, tracked for changes.</summary>
    Task<IReadOnlyList<WaitlistEntry>> WaitingForAsync(long pitchId, IReadOnlyList<(DateOnly Date, int Hour)> hours, CancellationToken ct);

    /// <summary>This person's entries for these hours (they just booked them).</summary>
    Task<IReadOnlyList<WaitlistEntry>> OwnForAsync(long pitchId, Guid userId, IReadOnlyList<(DateOnly Date, int Hour)> hours, CancellationToken ct);
}

/// <summary>
/// The waitlist (API §3.7): a player waits for a booked hour; when a booking is cancelled everyone waiting for its hours
/// is told at once and the first to book gets it. Booking the hour closes the player's own entry.
/// </summary>
public interface IWaitlistService
{
    Task<WaitlistEntryDto> JoinAsync(JoinWaitlistRequest request, CancellationToken ct);

    Task<IReadOnlyList<WaitlistEntryDto>> MineAsync(CancellationToken ct);

    Task LeaveAsync(long id, CancellationToken ct);

    /// <summary>A booking was cancelled and its hours freed: tell everyone waiting for them.</summary>
    Task HoursFreedAsync(Booking booking, string pitchName, string pitchSlug, CancellationToken ct);

    /// <summary>A booking was made: the booker's own entries for its hours are done.</summary>
    Task BookedAsync(Booking booking, CancellationToken ct);
}

public sealed class WaitlistService(
    IWaitlistRepository waitlist,
    BookingPlanner planner,
    IBookingRepository bookings,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<JoinWaitlistRequest> rules) : IWaitlistService
{
    public async Task<WaitlistEntryDto> JoinAsync(JoinWaitlistRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var me = Me();
        try
        {
            await planner.PlanAsync(request.PitchSlug, request.Date, request.Hour, 1, ct);
            throw new BusinessRuleException("This hour is free — book it now.", "available");
        }
        catch (ConflictException)
        {
            // Booked: the waitlist is for exactly this case
        }
        var pitch = await bookings.PitchAsync(request.PitchSlug, ct) ?? throw new NotFoundException("This pitch does not exist.");
        var (date, hour) = request.Hour >= 24 ? (request.Date.AddDays(1), request.Hour - 24) : (request.Date, request.Hour);
        if (await waitlist.ExistsAsync(pitch.Id, date, hour, me, ct))
        {
            throw new ConflictException("You’re already on the waitlist for this hour.", "already_waiting");
        }
        var entry = new WaitlistEntry { PitchId = pitch.Id, UserId = me, Date = date, Hour = (byte)hour, Status = WaitlistStatus.Waiting };
        waitlist.Add(entry);
        await unitOfWork.SaveChangesAsync(ct);
        return (await waitlist.GetAsync(entry.Id, ct))!;
    }

    public async Task<IReadOnlyList<WaitlistEntryDto>> MineAsync(CancellationToken ct) =>
        await waitlist.MineAsync(Me(), DateOnly.FromDateTime(CairoTime.Now(clock)), ct);

    public async Task LeaveAsync(long id, CancellationToken ct)
    {
        var entry = await waitlist.MineAsync(Me(), id, ct) ?? throw new NotFoundException("Waitlist entry not found.");
        waitlist.Remove(entry);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task HoursFreedAsync(Booking booking, string pitchName, string pitchSlug, CancellationToken ct)
    {
        var hours = Hours(booking);
        if (booking.Date.ToDateTime(new TimeOnly(booking.StartHour, 0)) <= CairoTime.Now(clock))
        {
            return;
        }
        var waiting = await waitlist.WaitingForAsync(booking.PitchId, hours, ct);
        foreach (var entry in waiting)
        {
            entry.Status = WaitlistStatus.FreeNow;
            entry.NotifiedAt = clock.UtcNow;
        }
        await unitOfWork.SaveChangesAsync(ct);
        foreach (var entry in waiting)
        {
            await notifier.NotifyUserAsync(entry.UserId, new NotificationMessage(NotificationCategory.Bookings, $"An hour you wanted is free: {pitchName}",
                $"{entry.Date:ddd d MMM} · {entry.Hour:00}:00 — the first to book gets it.", $"#/pitches/p-{pitchSlug}?date={entry.Date:yyyy-MM-dd}", NotificationTone.Success), ct);
        }
    }

    public async Task BookedAsync(Booking booking, CancellationToken ct)
    {
        if (booking.CustomerId is not { } customer)
        {
            return;
        }
        var own = await waitlist.OwnForAsync(booking.PitchId, customer, Hours(booking), ct);
        foreach (var entry in own)
        {
            entry.Status = WaitlistStatus.Booked;
        }
        if (own.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
    }

    /// <summary>The calendar hours of a booking (a game past midnight continues on the next date).</summary>
    private static List<(DateOnly Date, int Hour)> Hours(Booking b) =>
        Enumerable.Range(b.StartHour, b.Duration).Select(h => h >= 24 ? (b.Date.AddDays(1), h - 24) : (b.Date, h)).ToList();

    private Guid Me() => currentUser.UserId ?? throw new UnauthorizedException();
}

public sealed class JoinWaitlistRequestValidator : AbstractValidator<JoinWaitlistRequest>
{
    public JoinWaitlistRequestValidator()
    {
        RuleFor(x => x.PitchSlug).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Hour).InclusiveBetween(0, 29);
    }
}
