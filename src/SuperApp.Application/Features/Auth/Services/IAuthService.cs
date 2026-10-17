using SuperApp.Application.Features.Auth.Dtos;

namespace SuperApp.Application.Features.Auth.Services;

/// <summary>Sign-up, sign-in and the emailed links (API §2.1).</summary>
public interface IAuthService
{
    Task<MeResponse> SignUpAsync(SignUpRequest request, CancellationToken ct);

    Task<SignInResponse> SignInAsync(SignInRequest request, CancellationToken ct);

    Task SignOutAsync(CancellationToken ct);

    Task SignOutEverywhereAsync(CancellationToken ct);

    Task ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct);

    Task ResendConfirmationAsync(CancellationToken ct);

    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct);

    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct);
}
