using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Support;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Cancellations;

/// <summary>
/// Cancellation requests for the customer (owner's rule, 2026-11-07): "Cancel" on anything paid online creates a request
/// with its own number; the team (or the AI agent, through the n8n event cancellation.requested) answers within 2 hours.
/// </summary>
public interface ICancellationsService
{
    Task<CancellationDto> RequestAsync(CancellationRequestBody body, CancellationToken ct);

    Task<IReadOnlyList<CancellationDto>> MineAsync(CancellationToken ct);
}

public sealed class CancellationsService(
    ICancellationRepository repository,
    CancellationTargets targets,
    CancellationNotices notices,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<CancellationRequestBody> rules) : ICancellationsService
{
    public const int ReplyHours = 2;

    private Guid Me => currentUser.UserId ?? throw new UnauthorizedException();

    public async Task<CancellationDto> RequestAsync(CancellationRequestBody body, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(body, ct);
        var kind = Enum.Parse<CancellationKind>(body.Kind, true);
        var reference = body.Reference.Trim().ToUpperInvariant();
        if (await repository.OpenExistsAsync(kind, reference, ct))
        {
            throw new ConflictException($"You already asked to cancel this. We’ll reply within {ReplyHours} hours.", "already_requested");
        }
        var subject = await targets.DescribeAsync(kind, reference, Me, ct);
        var request = new CancellationRequest
        {
            Code = await repository.NewCodeAsync(ct), CustomerId = Me, Kind = kind, Reference = reference, Label = subject.Label,
            PaidOnline = subject.Paid, RefundIfApproved = subject.Refund, Reason = string.IsNullOrWhiteSpace(body.Reason) ? null : body.Reason.Trim(),
            Status = CancellationStatus.Pending, DueAt = clock.UtcNow.AddHours(ReplyHours),
        };
        repository.Add(request);
        await unitOfWork.SaveChangesAsync(ct);
        await notices.RequestedAsync(request, subject, ct);
        return (await repository.DtoAsync(request.Code, false, ct))!;
    }

    public Task<IReadOnlyList<CancellationDto>> MineAsync(CancellationToken ct) => repository.MineAsync(Me, ct);
}
