using FluentValidation;
using SuperApp.Application.Common.Validation;
using SuperApp.Application.Features.Auth.Dtos;

namespace SuperApp.Application.Features.Auth.Validators;

public sealed class SignUpRequestValidator : AbstractValidator<SignUpRequest>
{
    public SignUpRequestValidator()
    {
        RuleFor(x => x.FullName).PersonName();
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.Phone).EgyptianMobile();
        RuleFor(x => x.Password).NewPassword();
        RuleFor(x => x.AcceptTerms).Equal(true).WithMessage("Please accept the terms of use to continue.");
        RuleFor(x => x.Language).Language();
    }
}

public sealed class SignInRequestValidator : AbstractValidator<SignInRequest>
{
    public SignInRequestValidator()
    {
        RuleFor(x => x.Login).NotEmpty().WithMessage("Please enter your email or mobile number.").MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().WithMessage("Please enter your password.").MaximumLength(128);
    }
}

public sealed class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Token).NotEmpty().MaximumLength(2000);
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator() => RuleFor(x => x.Email).Email();
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Token).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.NewPassword).NewPassword();
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Please enter your current password.").MaximumLength(128);
        RuleFor(x => x.NewPassword).NewPassword().NotEqual(x => x.CurrentPassword).WithMessage("The new password must be different.");
    }
}

public sealed class UpdateMeRequestValidator : AbstractValidator<UpdateMeRequest>
{
    public UpdateMeRequestValidator()
    {
        RuleFor(x => x.FullName!).PersonName().When(x => x.FullName is not null);
        RuleFor(x => x.Phone!).EgyptianMobile().When(x => x.Phone is not null);
        RuleFor(x => x.Language!).Language().When(x => x.Language is not null);
        RuleFor(x => x.Position).Must(v => OneOf(v, Positions)).WithMessage("Choose a position from the list.");
        RuleFor(x => x.Level).Must(v => OneOf(v, Levels)).WithMessage("Choose a level from the list.");
        RuleFor(x => x.Foot).Must(v => OneOf(v, Feet)).WithMessage("Choose Right, Left or Both.");
        RuleFor(x => x.Area).MaximumLength(80).WithMessage("Area: up to 80 characters.");
    }

    public static readonly string[] Positions = ["Goalkeeper", "Defender", "Midfielder", "Winger", "Forward"];
    public static readonly string[] Levels = ["Beginner", "Intermediate", "Advanced", "Competitive"];
    public static readonly string[] Feet = ["Right", "Left", "Both"];

    /// <summary>Not sent (null), cleared ("") or one of the allowed values.</summary>
    private static bool OneOf(string? value, string[] allowed) => string.IsNullOrEmpty(value) || allowed.Contains(value, StringComparer.Ordinal);
}

public sealed class AcceptInviteRequestValidator : AbstractValidator<AcceptInviteRequest>
{
    public AcceptInviteRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Token).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Password).NewPassword();
    }
}

public sealed class ExternalSignInRequestValidator : AbstractValidator<ExternalSignInRequest>
{
    public ExternalSignInRequestValidator()
    {
        RuleFor(x => x.Provider).Must(p => SuperApp.Application.Abstractions.Identity.ExternalProviders.All.Contains(p))
            .WithMessage("Choose Google or Facebook.");
        RuleFor(x => x.Token).NotEmpty().WithMessage("The sign-in was cancelled. Please try again.").MaximumLength(4096);
        RuleFor(x => x.Language).Language();
    }
}
