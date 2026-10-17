namespace SuperApp.Application.Features.Auth.Dtos;

/// <summary>POST /auth/sign-up — a new customer account. <see cref="AcceptTerms"/> must be true.</summary>
public sealed record SignUpRequest(string FullName, string Email, string Phone, string Password, bool AcceptTerms, string Language = "ar");

/// <summary>POST /auth/sign-in — email or Egyptian mobile number, and the password.</summary>
public sealed record SignInRequest(string Login, string Password, bool Remember = true);

/// <summary>POST /auth/confirm-email — the values from the emailed link.</summary>
public sealed record ConfirmEmailRequest(Guid UserId, string Token);

/// <summary>POST /auth/forgot-password.</summary>
public sealed record ForgotPasswordRequest(string Email);

/// <summary>POST /auth/reset-password — the values from the emailed link and the new password.</summary>
public sealed record ResetPasswordRequest(Guid UserId, string Token, string NewPassword);

/// <summary>PUT /me/password.</summary>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>PATCH /me — only the fields that are sent change.</summary>
public sealed record UpdateMeRequest(string? FullName, string? Phone, string? Language);
