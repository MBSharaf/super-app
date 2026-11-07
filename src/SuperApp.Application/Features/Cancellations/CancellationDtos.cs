using FluentValidation;
using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Entities.Support;

namespace SuperApp.Application.Features.Cancellations;

/// <summary>{ kind: booking | package | order | game, reference: its code, reason? }</summary>
public sealed record CancellationRequestBody(string Kind, string Reference, string? Reason);

/// <summary>{ note } — optional on approve, the reason the customer sees (5+ characters) on refuse.</summary>
public sealed record CancellationDecisionBody(string? Note);

/// <summary>?status = pending (default) | answered | all, page, pageSize.</summary>
public sealed record CancellationsQuery(string? Status, int Page = 1, int PageSize = 50);

public sealed record CancellationCustomerDto(Guid Id, string Name, string? Email, string? Phone);

public sealed record CancellationDto(
    string Code, string Kind, string Reference, string Label, string Link, decimal PaidOnline, decimal RefundIfApproved, decimal? Refunded,
    string? Reason, string Status, string? Note, DateTime CreatedAt, DateTime DueAt, DateTime? DecidedAt, CancellationCustomerDto? Customer);

/// <summary>What the request is about, worked out from the item (label, link, money, and for n8n the pitch).</summary>
public sealed record CancellationSubject(string Label, string Link, decimal Paid, decimal Refund, string? Policy, string? PitchSlug);

/// <summary>Reads and saves cancellation requests.</summary>
public interface ICancellationRepository
{
    void Add(CancellationRequest request);

    Task<string> NewCodeAsync(CancellationToken ct);

    Task<bool> OpenExistsAsync(Domain.Enums.CancellationKind kind, string reference, CancellationToken ct);

    Task<CancellationRequest?> FindAsync(string code, CancellationToken ct);

    Task<IReadOnlyList<CancellationDto>> MineAsync(Guid customerId, CancellationToken ct);

    Task<PagedResult<CancellationDto>> ListAsync(string status, PageRequest page, CancellationToken ct);

    Task<CancellationDto?> DtoAsync(string code, bool withCustomer, CancellationToken ct);

    /// <summary>The package's games still to play (confirmed, from today), with the pitch name; null when it isn't the customer's.</summary>
    Task<(string PitchName, IReadOnlyList<string> BookingCodes)?> PackageAsync(string code, Guid customerId, DateOnly today, CancellationToken ct);
}

public sealed class CancellationRequestBodyValidator : AbstractValidator<CancellationRequestBody>
{
    public CancellationRequestBodyValidator()
    {
        RuleFor(x => x.Kind).NotEmpty().Must(k => Enum.TryParse<Domain.Enums.CancellationKind>(k, true, out var v) && Enum.IsDefined(v))
            .WithMessage("Choose booking, package, order or game.");
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(30).Matches("^[A-Za-z0-9-]+$").WithMessage("Enter the code of what you want to cancel.");
        RuleFor(x => x.Reason).MaximumLength(300);
    }
}

public sealed class CancellationDecisionBodyValidator : AbstractValidator<CancellationDecisionBody>
{
    public CancellationDecisionBodyValidator() => RuleFor(x => x.Note).MaximumLength(300);
}

public sealed class CancellationsQueryValidator : AbstractValidator<CancellationsQuery>
{
    public CancellationsQueryValidator()
    {
        RuleFor(x => x.Status).Must(s => s is null or "pending" or "answered" or "all").WithMessage("Use pending, answered or all.");
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
