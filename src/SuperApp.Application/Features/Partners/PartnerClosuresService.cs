using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Partners;

/// <summary>
/// Blocked days or hours of a pitch (API §3.2). Hours already booked can't be blocked: the partner cancels those
/// bookings first (409 <c>bookings_in_range</c>), so no player loses a booking without a refund.
/// </summary>
public interface IPartnerClosuresService
{
    Task<IReadOnlyList<ClosureDto>> ListAsync(long pitchId, CancellationToken ct);

    Task<ClosureDto> CreateAsync(long pitchId, ClosureRequest request, CancellationToken ct);

    Task DeleteAsync(long pitchId, long closureId, CancellationToken ct);
}

public sealed class PartnerClosuresService(
    IPartnerAccess access,
    IPartnerRepository partners,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<ClosureRequest> rules) : IPartnerClosuresService
{
    public async Task<IReadOnlyList<ClosureDto>> ListAsync(long pitchId, CancellationToken ct)
    {
        var scope = await access.ViewAsync(ct);
        _ = await partners.PitchAsync(scope.PartnerId, pitchId, ct) ?? throw new NotFoundException("Pitch not found.");
        return await partners.ClosuresAsync(pitchId, Today(), ct);
    }

    public async Task<ClosureDto> CreateAsync(long pitchId, ClosureRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var scope = await access.ManageAsync(ct);
        var pitch = await partners.PitchAsync(scope.PartnerId, pitchId, ct) ?? throw new NotFoundException("Pitch not found.");
        if (request.ToDate < Today())
        {
            throw new BusinessRuleException("These dates have already passed.", "date_past");
        }
        var booked = await partners.BookingsInRangeAsync(pitchId, request.FromDate, request.ToDate, request.FromHour, request.ToHour, ct);
        if (booked > 0)
        {
            throw new ConflictException($"{booked} booking{(booked == 1 ? " is" : "s are")} in these hours. Cancel {(booked == 1 ? "it" : "them")} first (the players get a full refund), then block the time.", "bookings_in_range");
        }
        var closure = new Closure
        {
            PitchId = pitchId, FromDate = request.FromDate, ToDate = request.ToDate, FromHour = (byte?)request.FromHour, ToHour = (byte?)request.ToHour,
            Reason = request.Reason.Trim(), CreatedById = currentUser.UserId,
        };
        partners.AddClosure(closure);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Partner, "closure.created", $"{pitch.Name} blocked {request.FromDate:yyyy-MM-dd} – {request.ToDate:yyyy-MM-dd}", "Pitch", pitch.Slug, ct: ct);
        return (await partners.ClosuresAsync(pitchId, DateOnly.MinValue, ct)).Single(c => c.Id == closure.Id);
    }

    public async Task DeleteAsync(long pitchId, long closureId, CancellationToken ct)
    {
        var scope = await access.ManageAsync(ct);
        var pitch = await partners.PitchAsync(scope.PartnerId, pitchId, ct) ?? throw new NotFoundException("Pitch not found.");
        var closure = await partners.ClosureAsync(pitchId, closureId, ct) ?? throw new NotFoundException("Blocked time not found.");
        partners.RemoveClosure(closure);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Partner, "closure.removed", $"{pitch.Name} unblocked {closure.FromDate:yyyy-MM-dd} – {closure.ToDate:yyyy-MM-dd}", "Pitch", pitch.Slug, ct: ct);
    }

    private DateOnly Today() => DateOnly.FromDateTime(CairoTime.Now(clock));
}
