using Microsoft.Extensions.Options;
using SuperApp.Application.Abstractions.Identity;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Options;
using SuperApp.Application.Features.Auth.Dtos;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Auth.Services;

/// <summary>
/// "Continue with Google / Facebook". The server checks the provider's token itself, then finds the linked account,
/// links an account with the same verified email, or opens a new customer account (email already confirmed).
/// Team accounts (admins, the CEO) are refused: they always sign in with their password and authenticator code.
/// </summary>
public sealed class ExternalSignInService(
    IEnumerable<IExternalLoginVerifier> verifiers,
    IIdentityService identity,
    ISessionService session,
    IMeService me,
    ILegalRepository legal,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IRequestInfo requestInfo,
    IOptions<ExternalLoginOptions> options) : IExternalSignInService
{
    private const string TermsDocument = "terms";

    public ExternalProvidersResponse Providers()
    {
        var o = options.Value;
        return new ExternalProvidersResponse(o.Google.Enabled ? o.Google.ClientId : null, o.Facebook.Enabled ? o.Facebook.AppId : null);
    }

    public async Task<SignInResponse> SignInAsync(ExternalSignInRequest request, CancellationToken ct)
    {
        var profile = await VerifyAsync(request, ct);
        var account = await identity.FindExternalAsync(profile, ct) ?? await CreateAccountAsync(profile, request, ct);
        if (account.IsStaff)
        {
            throw new ForbiddenException("Team accounts sign in with their password and authenticator code.", "staff_password_only");
        }
        var outcome = await session.ExternalSignInAsync(account.Id, request.Remember);
        return outcome switch
        {
            SignInOutcome.Succeeded => new SignInResponse(true, false, await me.GetAsync(account.Id, ct)),
            SignInOutcome.LockedOut => throw new LockedException(),
            SignInOutcome.RequiresTwoFactor => throw new ForbiddenException("This account uses an authenticator: sign in with your password.", "password_required"),
            _ => throw new ForbiddenException("This account is suspended. Please contact support.", "account_suspended"),
        };
    }

    private async Task<ExternalProfile> VerifyAsync(ExternalSignInRequest request, CancellationToken ct)
    {
        var verifier = verifiers.FirstOrDefault(v => v.Provider == request.Provider && IsOn(v.Provider))
            ?? throw new BusinessRuleException($"Signing in with {request.Provider} is not available yet.", "provider_off");
        var profile = await verifier.VerifyAsync(request.Token, ct)
            ?? throw new UnauthorizedException($"{request.Provider} could not confirm who you are. Please try again.", "external_failed");
        if (string.IsNullOrWhiteSpace(profile.Email) || !profile.EmailVerified)
        {
            throw new BusinessRuleException($"Your {request.Provider} account has no confirmed email. Sign up with your email instead.", "external_no_email");
        }
        return profile;
    }

    /// <summary>A first sign-in opens the account, but only once the person accepted the terms (the site asks and retries).</summary>
    private async Task<AccountSummary> CreateAccountAsync(ExternalProfile profile, ExternalSignInRequest request, CancellationToken ct)
    {
        var termsVersion = await legal.CurrentVersionIdAsync(TermsDocument, ct)
            ?? throw new BusinessRuleException("Sign-up is not open yet: the terms of use are not published.", "terms_missing");
        if (!request.AcceptTerms)
        {
            throw new BusinessRuleException("Please accept the terms of use to create your account.", "terms_required");
        }
        var userId = await identity.CreateExternalAsync(profile, request.Language, ct);
        legal.AddAcceptance(termsVersion, userId, $"sign-up:{request.Provider.ToLowerInvariant()}", requestInfo.IpAddress);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Site, "account.created", $"New customer account {profile.Email} with {request.Provider}", "User", userId.ToString(), ct: ct);
        return await identity.FindByIdAsync(userId, ct) ?? throw new NotFoundException();
    }

    private bool IsOn(string provider) => provider switch
    {
        ExternalProviders.Google => options.Value.Google.Enabled,
        ExternalProviders.Facebook => options.Value.Facebook.Enabled,
        _ => false,
    };
}
