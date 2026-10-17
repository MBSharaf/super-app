using FluentValidation;
using SuperApp.Application.Common.Validation;
using SuperApp.Application.Features.Ceo.Dtos;
using SuperApp.Application.Security;

namespace SuperApp.Application.Features.Ceo.Validators;

/// <summary>Roles the CEO can give to an admin (never CEO, partner, seller or factory roles).</summary>
public static class AdminRoles
{
    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>
    {
        SystemRoles.AdminManager, SystemRoles.Support, SystemRoles.Finance, SystemRoles.Content, SystemRoles.Marketing, SystemRoles.Moderator,
    };
}

public sealed class StaffLimitsDtoValidator : AbstractValidator<StaffLimitsDto>
{
    public StaffLimitsDtoValidator()
    {
        RuleFor(x => x.Refund).InclusiveBetween(0, 1_000_000).WithMessage("The refund limit must be 0 – 1,000,000 EGP.");
        RuleFor(x => x.Credit).InclusiveBetween(0, 1_000_000).WithMessage("The credit limit must be 0 – 1,000,000 EGP.");
        RuleFor(x => x.Payout).InclusiveBetween(0, 10_000_000).WithMessage("The payout limit must be 0 – 10,000,000 EGP.");
    }
}

public sealed class CreateAdminRequestValidator : AbstractValidator<CreateAdminRequest>
{
    public CreateAdminRequestValidator()
    {
        RuleFor(x => x.FullName).PersonName();
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.Roles).NotEmpty().WithMessage("Choose at least one role.")
            .Must(r => r.All(AdminRoles.Allowed.Contains)).WithMessage("Only admin roles can be given here.");
        RuleForEach(x => x.Permissions).Must(Permissions.Exists).WithMessage("Unknown permission: {PropertyValue}.");
        RuleFor(x => x.Limits!).SetValidator(new StaffLimitsDtoValidator()).When(x => x.Limits is not null);
        RuleFor(x => x.Language).Language();
    }
}

public sealed class UpdateAdminRequestValidator : AbstractValidator<UpdateAdminRequest>
{
    public UpdateAdminRequestValidator()
    {
        RuleFor(x => x.FullName).PersonName();
        RuleFor(x => x.Roles).NotEmpty().WithMessage("Choose at least one role.")
            .Must(r => r.All(AdminRoles.Allowed.Contains)).WithMessage("Only admin roles can be given here.");
    }
}

public sealed class SetPermissionsRequestValidator : AbstractValidator<SetPermissionsRequest>
{
    public SetPermissionsRequestValidator()
    {
        RuleFor(x => x.Permissions).NotNull();
        RuleForEach(x => x.Permissions).Must(Permissions.Exists).WithMessage("Unknown permission: {PropertyValue}.");
        RuleFor(x => x.ExpiresAt).GreaterThan(_ => DateTime.UtcNow).WithMessage("The end date must be in the future.").When(x => x.ExpiresAt is not null);
    }
}

public sealed class SuspendRequestValidator : AbstractValidator<SuspendRequest>
{
    public SuspendRequestValidator() =>
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Please write the reason.").Length(3, 400);
}
