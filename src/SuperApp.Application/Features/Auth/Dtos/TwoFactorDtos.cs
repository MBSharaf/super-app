namespace SuperApp.Application.Features.Auth.Dtos;

/// <summary>POST /auth/2fa/setup — scan the QR (or type the key) in the authenticator app.</summary>
public sealed record TwoFactorSetupResponse(string SharedKey, string AuthenticatorUri, string QrSvg);

/// <summary>Recovery codes, shown once — each one replaces a code from the app a single time.</summary>
public sealed record RecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes);

/// <summary>POST /auth/2fa/enable, /disable, /recovery-codes — a current 6-digit code from the app.</summary>
public sealed record TwoFactorCodeRequest(string Code);

/// <summary>POST /auth/2fa/verify — second step of sign-in; a 6-digit app code or a recovery code.</summary>
public sealed record TwoFactorVerifyRequest(string Code, bool IsRecoveryCode = false, bool Remember = true);
