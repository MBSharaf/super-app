using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;

namespace SuperApp.Infrastructure.Identity;

/// <summary>Turns ASP.NET Identity errors into the app's 400 response, attached to the form field they belong to.</summary>
internal static class IdentityErrors
{
    public static void Check(IdentityResult result, string field = "request")
    {
        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors.Select(e => new ValidationFailure(field, Friendly(e))));
        }
    }

    public static ValidationException Field(string field, string message) => new([new ValidationFailure(field, message)]);

    /// <summary>Identity's own wording is replaced where it would confuse a customer.</summary>
    private static string Friendly(IdentityError error) => error.Code switch
    {
        nameof(IdentityErrorDescriber.PasswordTooShort) => "The password must be at least 8 characters.",
        nameof(IdentityErrorDescriber.PasswordRequiresDigit) => "The password must contain at least one number.",
        nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName) => "An account with this email already exists.",
        nameof(IdentityErrorDescriber.InvalidEmail) => "Please enter a valid email.",
        _ => error.Description,
    };
}
