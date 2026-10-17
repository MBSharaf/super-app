using FluentValidation;

namespace SuperApp.Application.Common.Validation;

/// <summary>Shared field rules so every form checks names, emails, phones and passwords the same way.</summary>
public static class RuleExtensions
{
    public static IRuleBuilderOptions<T, string> PersonName<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("Please enter your name.")
        .Length(2, 120).WithMessage("The name must be 2–120 characters.")
        .Must(NoMarkup).WithMessage("The name cannot contain < or >.");

    public static IRuleBuilderOptions<T, string> Email<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("Please enter your email.")
        .MaximumLength(256).WithMessage("The email is too long.")
        .EmailAddress().WithMessage("Please enter a valid email.");

    public static IRuleBuilderOptions<T, string> EgyptianMobile<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("Please enter your mobile number.")
        .Must(EgyptianPhone.IsValid).WithMessage("Please enter a valid Egyptian mobile number (010, 011, 012 or 015).");

    public static IRuleBuilderOptions<T, string> NewPassword<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("Please choose a password.")
        .Length(8, 128).WithMessage("The password must be at least 8 characters.")
        .Matches("[0-9]").WithMessage("The password must contain at least one number.");

    public static IRuleBuilderOptions<T, string> Language<T>(this IRuleBuilder<T, string> rule) => rule
        .Must(l => l is "ar" or "en").WithMessage("The language must be ar or en.");

    private static bool NoMarkup(string? value) => value is null || (!value.Contains('<', StringComparison.Ordinal) && !value.Contains('>', StringComparison.Ordinal));
}
