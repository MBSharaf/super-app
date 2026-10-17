using FluentValidation;
using SuperApp.Application.Abstractions.Identity;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Partners;

/// <summary>A staff member of the partner. Role: Manager, CheckIn, Accountant.</summary>
public sealed record PartnerStaffDto(long Id, Guid UserId, string FullName, string Email, string? Phone, string Role, bool IsActive, DateTime CreatedAt);

/// <summary>POST /partner/staff — the person signs up on Super App first; they are found by email or mobile.</summary>
public sealed record AddStaffRequest(string Login, string Role);

/// <summary>PUT /partner/staff/{id}</summary>
public sealed record UpdateStaffRequest(string Role, bool IsActive);

public interface IPartnerStaffRepository
{
    Task<IReadOnlyList<PartnerStaffDto>> ListAsync(long partnerId, CancellationToken ct);

    Task<PartnerStaffMember?> FindAsync(long partnerId, long id, CancellationToken ct);

    /// <summary>True when the person already owns a partner business or is staff of one (one business per person).</summary>
    Task<bool> WorksForAPartnerAsync(Guid userId, CancellationToken ct);

    void Add(PartnerStaffMember member);

    void Remove(PartnerStaffMember member);
}

/// <summary>
/// The partner's staff (API §3.2). Owner and managers manage check-in staff and accountants; only the owner adds,
/// changes or removes managers. Each change tells the staff member.
/// </summary>
public interface IPartnerStaffService
{
    Task<IReadOnlyList<PartnerStaffDto>> ListAsync(CancellationToken ct);

    Task<PartnerStaffDto> AddAsync(AddStaffRequest request, CancellationToken ct);

    Task<PartnerStaffDto> UpdateAsync(long id, UpdateStaffRequest request, CancellationToken ct);

    Task RemoveAsync(long id, CancellationToken ct);
}

public sealed class PartnerStaffService(
    IPartnerAccess access,
    IPartnerStaffRepository staff,
    IIdentityService identity,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IAuditLogger audit,
    IValidator<AddStaffRequest> addRules,
    IValidator<UpdateStaffRequest> updateRules) : IPartnerStaffService
{
    public async Task<IReadOnlyList<PartnerStaffDto>> ListAsync(CancellationToken ct) =>
        await staff.ListAsync((await access.ManageAsync(ct)).PartnerId, ct);

    public async Task<PartnerStaffDto> AddAsync(AddStaffRequest request, CancellationToken ct)
    {
        await addRules.ValidateAndThrowAsync(request, ct);
        var scope = await access.ManageAsync(ct);
        var role = Enum.Parse<PartnerStaffRole>(request.Role);
        OwnerForManagers(scope, role);
        var person = await identity.FindByLoginAsync(request.Login.Trim(), ct)
            ?? throw new NotFoundException("No Super App account uses this email or mobile. Ask them to sign up first.", "no_account");
        if (await staff.WorksForAPartnerAsync(person.Id, ct))
        {
            throw new ConflictException($"{person.FullName} already works for a pitch partner on Super App.", "already_staff");
        }
        var member = new PartnerStaffMember { PartnerId = scope.PartnerId, UserId = person.Id, Role = role };
        staff.Add(member);
        await unitOfWork.SaveChangesAsync(ct);
        await TellAsync(person.Id, $"You joined {scope.BusinessName} as {Label(role)}", "Open the partner dashboard to start.", NotificationTone.Success, ct);
        await audit.LogAsync(AuditSystem.Partner, "staff.added", $"{person.FullName} added as {role}", "PartnerStaff", member.Id.ToString(), ct: ct);
        return (await staff.ListAsync(scope.PartnerId, ct)).Single(s => s.Id == member.Id);
    }

    public async Task<PartnerStaffDto> UpdateAsync(long id, UpdateStaffRequest request, CancellationToken ct)
    {
        await updateRules.ValidateAndThrowAsync(request, ct);
        var scope = await access.ManageAsync(ct);
        var member = await staff.FindAsync(scope.PartnerId, id, ct) ?? throw new NotFoundException("Staff member not found.");
        var role = Enum.Parse<PartnerStaffRole>(request.Role);
        OwnerForManagers(scope, member.Role);
        OwnerForManagers(scope, role);
        member.Role = role;
        member.IsActive = request.IsActive;
        await unitOfWork.SaveChangesAsync(ct);
        await TellAsync(member.UserId, request.IsActive ? $"Your role at {scope.BusinessName}: {Label(role)}" : $"Your access to {scope.BusinessName} was paused",
            request.IsActive ? "Your dashboard shows what this role can do." : "Ask the owner if you think this is a mistake.", NotificationTone.Info, ct);
        await audit.LogAsync(AuditSystem.Partner, "staff.updated", $"Staff {id}: {role}, {(request.IsActive ? "active" : "paused")}", "PartnerStaff", id.ToString(), ct: ct);
        return (await staff.ListAsync(scope.PartnerId, ct)).Single(s => s.Id == id);
    }

    public async Task RemoveAsync(long id, CancellationToken ct)
    {
        var scope = await access.ManageAsync(ct);
        var member = await staff.FindAsync(scope.PartnerId, id, ct) ?? throw new NotFoundException("Staff member not found.");
        OwnerForManagers(scope, member.Role);
        staff.Remove(member);
        await unitOfWork.SaveChangesAsync(ct);
        await TellAsync(member.UserId, $"You were removed from {scope.BusinessName}", "You no longer have access to its partner dashboard.", NotificationTone.Warning, ct);
        await audit.LogAsync(AuditSystem.Partner, "staff.removed", $"Staff {id} removed", "PartnerStaff", id.ToString(), ct: ct);
    }

    private static void OwnerForManagers(PartnerScope scope, PartnerStaffRole role)
    {
        if (role == PartnerStaffRole.Manager && !scope.IsOwner)
        {
            throw new ForbiddenException("Only the owner can add, change or remove managers.", "owner_only");
        }
    }

    private static string Label(PartnerStaffRole role) => role switch
    {
        PartnerStaffRole.CheckIn => "check-in staff",
        PartnerStaffRole.Accountant => "accountant",
        _ => "manager",
    };

    private Task TellAsync(Guid userId, string title, string body, NotificationTone tone, CancellationToken ct) =>
        notifier.NotifyUserAsync(userId, new NotificationMessage(NotificationCategory.Account, title, body, "#/dash", tone), ct);
}

public sealed class AddStaffRequestValidator : AbstractValidator<AddStaffRequest>
{
    public AddStaffRequestValidator()
    {
        RuleFor(x => x.Login).NotEmpty().WithMessage("Enter their email or mobile.").MaximumLength(256);
        RuleFor(x => x.Role).NotEmpty().IsEnumName(typeof(PartnerStaffRole), caseSensitive: true).WithMessage("Choose Manager, CheckIn or Accountant.");
    }
}

public sealed class UpdateStaffRequestValidator : AbstractValidator<UpdateStaffRequest>
{
    public UpdateStaffRequestValidator()
    {
        RuleFor(x => x.Role).NotEmpty().IsEnumName(typeof(PartnerStaffRole), caseSensitive: true).WithMessage("Choose Manager, CheckIn or Accountant.");
    }
}
