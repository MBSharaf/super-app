using FluentValidation;
using SuperApp.Application.Common.Validation;
using SuperApp.Application.Features.Ceo.Dtos;
using SuperApp.Application.Security;

namespace SuperApp.Application.Features.Ceo.Validators;

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
        RuleFor(x => x.Roles).NotEmpty().WithMessage("Choose at least one role.");
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
        RuleFor(x => x.Roles).NotEmpty().WithMessage("Choose at least one role.");
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

public sealed class RoleRequestValidator : AbstractValidator<RoleRequest>
{
    public RoleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Write the role's name.").Length(2, 40)
            .Matches(@"^[A-Za-z][A-Za-z0-9 _-]*$").WithMessage("Use English letters, numbers, spaces, - or _ and start with a letter.");
        RuleFor(x => x.Description).MaximumLength(300);
        RuleFor(x => x.Permissions).NotNull();
        RuleForEach(x => x.Permissions).Must(Permissions.Exists).WithMessage("Unknown permission: {PropertyValue}.");
    }
}
