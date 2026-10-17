using FluentValidation;
using SuperApp.Application.Features.Auth.Dtos;

namespace SuperApp.Application.Features.Auth.Validators;

public sealed class TwoFactorCodeRequestValidator : AbstractValidator<TwoFactorCodeRequest>
{
    public TwoFactorCodeRequestValidator() =>
        RuleFor(x => x.Code).NotEmpty().WithMessage("Enter the 6-digit code from your app.")
            .Matches("^[0-9 ]{6,7}$").WithMessage("The code is 6 digits.");
}

public sealed class TwoFactorVerifyRequestValidator : AbstractValidator<TwoFactorVerifyRequest>
{
    public TwoFactorVerifyRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Enter the code.").MaximumLength(20);
        RuleFor(x => x.Code).Matches("^[0-9 ]{6,7}$").WithMessage("The code is 6 digits.").When(x => !x.IsRecoveryCode);
    }
}
