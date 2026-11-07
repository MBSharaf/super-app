using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Entities.Support;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Cancellations;

/// <summary>The team's (and the AI agent's) queue: list, approve (the item is cancelled and refunded), refuse with a reason.</summary>
public interface ICancellationsAdminService
{
    Task<PagedResult<CancellationDto>> ListAsync(CancellationsQuery query, CancellationToken ct);

    Task<CancellationDto> GetAsync(string code, CancellationToken ct);

    Task<CancellationDto> ApproveAsync(string code, CancellationDecisionBody body, CancellationToken ct);

    Task<CancellationDto> RejectAsync(string code, CancellationDecisionBody body, CancellationToken ct);
}

public sealed class CancellationsAdminService(
    ICancellationRepository repository,
    CancellationTargets targets,
    CancellationNotices notices,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<CancellationsQuery> queryRules,
    IValidator<CancellationDecisionBody> decisionRules) : ICancellationsAdminService
{
    public async Task<PagedResult<CancellationDto>> ListAsync(CancellationsQuery query, CancellationToken ct)
    {
        await queryRules.ValidateAndThrowAsync(query, ct);
        return await repository.ListAsync(query.Status ?? "pending", new PageRequest(query.Page, query.PageSize), ct);
    }

    public async Task<CancellationDto> GetAsync(string code, CancellationToken ct) =>
        await repository.DtoAsync(code, true, ct) ?? throw new NotFoundException("Cancellation request not found.");

    public async Task<CancellationDto> ApproveAsync(string code, CancellationDecisionBody body, CancellationToken ct)
    {
        await decisionRules.ValidateAndThrowAsync(body, ct);
        var request = await PendingAsync(code, ct);
        var refunded = await targets.CancelAsync(request.Kind, request.Reference, request.CustomerId, ct);
        Decide(request, CancellationStatus.Approved, body.Note);
        request.Refunded = refunded;
        await unitOfWork.SaveChangesAsync(ct);
        await notices.DecidedAsync(request, ct);
        return await GetAsync(code, ct);
    }

    public async Task<CancellationDto> RejectAsync(string code, CancellationDecisionBody body, CancellationToken ct)
    {
        await decisionRules.ValidateAndThrowAsync(body, ct);
        if ((body.Note?.Trim().Length ?? 0) < 5)
        {
            throw new ValidationException([new FluentValidation.Results.ValidationFailure("note", "Write the reason the customer sees (5+ characters).")]);
        }
        var request = await PendingAsync(code, ct);
        Decide(request, CancellationStatus.Rejected, body.Note);
        await unitOfWork.SaveChangesAsync(ct);
        await notices.DecidedAsync(request, ct);
        return await GetAsync(code, ct);
    }

    private async Task<CancellationRequest> PendingAsync(string code, CancellationToken ct)
    {
        var request = await repository.FindAsync(code, ct) ?? throw new NotFoundException("Cancellation request not found.");
        return request.Status == CancellationStatus.Pending ? request
            : throw new BusinessRuleException("This request is already answered.", "already_answered");
    }

    private void Decide(CancellationRequest request, CancellationStatus status, string? note)
    {
        request.Status = status;
        request.DecidedAt = clock.UtcNow;
        request.DecidedById = currentUser.UserId;
        request.DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
