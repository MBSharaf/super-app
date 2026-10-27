using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.AdminConsole;

/// <summary>
/// Customers, partners and sellers in the admin console (API §7.2 "Customers", "Partners", "Sellers"): search, drawer and
/// actions. The person concerned is always told; every action is audited. Money actions (wallet credit, partner balance
/// adjustment) check the caller's money limits; over the limit → 403 for someone with a higher limit or the CEO.
/// </summary>
public interface IAdminPeopleService
{
    Task<PagedResult<CustomerRowDto>> CustomersAsync(PeopleQuery query, CancellationToken ct);

    Task<CustomerDetailDto> CustomerAsync(Guid id, CancellationToken ct);

    Task<CustomerDetailDto> BlockAsync(Guid id, ReasonInput request, CancellationToken ct);

    Task<CustomerDetailDto> UnblockAsync(Guid id, CancellationToken ct);

    Task<CustomerDetailDto> PrepayAsync(Guid id, PrepayRequest request, CancellationToken ct);

    Task<CustomerDetailDto> NoteAsync(Guid id, NoteRequest request, CancellationToken ct);

    Task<PagedResult<PartnerRowDto>> PartnersAsync(PeopleQuery query, CancellationToken ct);

    Task<PartnerDetailDto> PartnerAsync(long id, CancellationToken ct);

    Task<PartnerDetailDto> SetPartnerStatusAsync(long id, bool suspend, string? reason, CancellationToken ct);

    Task<PagedResult<SellerRowDto>> SellersAsync(PeopleQuery query, CancellationToken ct);

    Task<SellerDetailDto> SellerAsync(long id, CancellationToken ct);

    Task<SellerDetailDto> SetSellerStatusAsync(long id, bool suspend, string? reason, CancellationToken ct);

    Task<SellerDetailDto> FeatureSellerAsync(long id, FeatureRequest request, CancellationToken ct);

    /// <summary>Goodwill money to the customer's Super App wallet, paid by Super App, with a receipt.</summary>
    Task<CustomerDetailDto> CreditAsync(Guid id, CreditRequest request, CancellationToken ct);

    /// <summary>Super Points + or −; the balance never goes below 0 (422 <c>not_enough_points</c>).</summary>
    Task<CustomerDetailDto> PointsAsync(Guid id, PointsRequest request, CancellationToken ct);

    /// <summary>Clears refused cash-on-delivery parcels (409 <c>no_refusals</c> when there are none).</summary>
    Task<CustomerDetailDto> ResetRefusalsAsync(Guid id, CancellationToken ct);

    /// <summary>The partner's own commission (percent); empty = the platform rate. New bookings use it.</summary>
    Task<PartnerDetailDto> CommissionAsync(long id, CommissionDealRequest request, CancellationToken ct);

    Task<PartnerDetailDto> AdjustBalanceAsync(long id, BalanceAdjustRequest request, CancellationToken ct);

    /// <summary>Closes a live pitch (no new bookings) or reopens one Super App closed; the owner is told.</summary>
    Task<PartnerDetailDto> SetPitchClosedAsync(long pitchId, bool close, string? reason, CancellationToken ct);
}

public sealed partial class AdminPeopleService(
    IAdminReader reader,
    IAdminPeopleRepository people,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IAuditLogger audit,
    IClock clock,
    Money.ILedger ledger,
    Money.ILoyaltyStore loyalty,
    Money.MoneyLimits limits) : IAdminPeopleService
{
    public Task<PagedResult<CustomerRowDto>> CustomersAsync(PeopleQuery query, CancellationToken ct) => reader.CustomersAsync(query, ct);

    public async Task<CustomerDetailDto> CustomerAsync(Guid id, CancellationToken ct) =>
        await reader.CustomerAsync(id, ct) ?? throw new NotFoundException("This customer does not exist.");

    /// <summary>Blocked from booking and ordering (sign-in still works, so they can read their history and contact support).</summary>
    public async Task<CustomerDetailDto> BlockAsync(Guid id, ReasonInput request, CancellationToken ct)
    {
        var user = await CustomerEntityAsync(id, ct);
        (user.Status, user.SuspendedReason) = (UserStatus.BlockedFromBookingAndOrdering, request.Reason.Trim());
        await unitOfWork.SaveChangesAsync(ct);
        await TellAsync(user, "Your account can no longer book or order", request.Reason.Trim(), NotificationTone.Warning, ct);
        await audit.LogAsync(AuditSystem.Admin, "customer.blocked", $"{user.FullName} blocked: {request.Reason}", "User", id.ToString(), ct: ct);
        return await CustomerAsync(id, ct);
    }

    public async Task<CustomerDetailDto> UnblockAsync(Guid id, CancellationToken ct)
    {
        var user = await CustomerEntityAsync(id, ct);
        if (user.Status == UserStatus.Active)
        {
            throw new ConflictException("This customer is not blocked.", "not_blocked");
        }
        (user.Status, user.SuspendedReason) = (UserStatus.Active, null);
        await unitOfWork.SaveChangesAsync(ct);
        await TellAsync(user, "Your account is active again", "You can book and order again.", NotificationTone.Success, ct);
        await audit.LogAsync(AuditSystem.Admin, "customer.unblocked", $"{user.FullName} unblocked", "User", id.ToString(), ct: ct);
        return await CustomerAsync(id, ct);
    }

    /// <summary>Cash-on-delivery rule for printed items: Automatic (after refusals), Always prepay, or Never.</summary>
    public async Task<CustomerDetailDto> PrepayAsync(Guid id, PrepayRequest request, CancellationToken ct)
    {
        var user = await CustomerEntityAsync(id, ct);
        user.MustPrepay = Enum.Parse<PrepayRule>(request.Rule, true);
        await unitOfWork.SaveChangesAsync(ct);
        if (user.MustPrepay == PrepayRule.Always)
        {
            await TellAsync(user, "Online payment needed for printed items", "Cash on delivery is off for printed items on your account.", NotificationTone.Info, ct);
        }
        await audit.LogAsync(AuditSystem.Admin, "customer.prepay", $"{user.FullName}: prepay {user.MustPrepay}", "User", id.ToString(), ct: ct);
        return await CustomerAsync(id, ct);
    }

    /// <summary>A note only the team sees (not told to the customer).</summary>
    public async Task<CustomerDetailDto> NoteAsync(Guid id, NoteRequest request, CancellationToken ct)
    {
        var user = await CustomerEntityAsync(id, ct);
        user.AdminNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, "customer.note", $"Note on {user.FullName}", "User", id.ToString(), ct: ct);
        return await CustomerAsync(id, ct);
    }

    private Task TellAsync(User user, string title, string body, NotificationTone tone, CancellationToken ct) =>
        notifier.NotifyUserAsync(user.Id, new NotificationMessage(NotificationCategory.Account, title, body, "#/profile", tone), ct);

    private async Task<User> CustomerEntityAsync(Guid id, CancellationToken ct) =>
        await people.CustomerAsync(id, ct) ?? throw new NotFoundException("This customer does not exist.");
}

public sealed class ReasonInputValidator : AbstractValidator<ReasonInput>
{
    public ReasonInputValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Write the reason (the person sees it).").MaximumLength(300);
    }
}

public sealed class PrepayRequestValidator : AbstractValidator<PrepayRequest>
{
    public PrepayRequestValidator()
    {
        RuleFor(x => x.Rule).Must(r => Enum.TryParse<PrepayRule>(r, true, out _) && !int.TryParse(r, out _)).WithMessage("Rule: Automatic, Always or Never.");
    }
}

public sealed class NoteRequestValidator : AbstractValidator<NoteRequest>
{
    public NoteRequestValidator()
    {
        RuleFor(x => x.Note).NotNull().MaximumLength(1000);
    }
}

public sealed class PeopleQueryValidator : AbstractValidator<PeopleQuery>
{
    public PeopleQueryValidator()
    {
        RuleFor(x => x.Q).MaximumLength(100);
        RuleFor(x => x.Status).MaximumLength(40);
        RuleFor(x => x.Level).MaximumLength(40);
        RuleFor(x => x.Page).InclusiveBetween(1, 100_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
