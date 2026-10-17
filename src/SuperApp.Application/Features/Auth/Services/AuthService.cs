using Microsoft.Extensions.Options;
using SuperApp.Application.Abstractions.Email;
using SuperApp.Application.Abstractions.Identity;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Options;
using SuperApp.Application.Common.Validation;
using SuperApp.Application.Features.Auth.Dtos;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Auth.Services;

/// <summary>
/// Sign-up, sign-in and the emailed links. Wrong email and wrong password give the same answer, and
/// "forgot password" never tells whether an email exists — so nobody can find out who has an account.
/// </summary>
public sealed class AuthService(
    IIdentityService identity,
    IStaffStore staff,
    ISessionService session,
    IMeService me,
    IEmailService email,
    ILegalRepository legal,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IRequestInfo requestInfo,
    IOptions<SiteOptions> site) : IAuthService
{
    private const string TermsDocument = "terms";

    public async Task<MeResponse> SignUpAsync(SignUpRequest request, CancellationToken ct)
    {
        var termsVersion = await legal.CurrentVersionIdAsync(TermsDocument, ct)
            ?? throw new BusinessRuleException("Sign-up is not open yet: the terms of use are not published.", "terms_missing");
        var account = new NewAccount(request.FullName.Trim(), request.Email.Trim(), EgyptianPhone.Normalize(request.Phone)!, request.Language);
        var userId = await identity.CreateCustomerAsync(account, request.Password, ct);

        legal.AddAcceptance(termsVersion, userId, "sign-up", requestInfo.IpAddress);
        await unitOfWork.SaveChangesAsync(ct);
        await SendConfirmationAsync(userId, ct);
        await session.PasswordSignInAsync(userId, request.Password, remember: true);
        await audit.LogAsync(AuditSystem.Site, "account.created", $"New customer account {account.Email}", "User", userId.ToString(), ct: ct);
        return await me.GetAsync(userId, ct);
    }

    public async Task<SignInResponse> SignInAsync(SignInRequest request, CancellationToken ct)
    {
        var account = await identity.FindByLoginAsync(request.Login.Trim(), ct) ?? throw WrongLogin();
        var outcome = await session.PasswordSignInAsync(account.Id, request.Password, request.Remember);
        return outcome switch
        {
            SignInOutcome.Succeeded => new SignInResponse(true, false, await me.GetAsync(account.Id, ct)),
            SignInOutcome.RequiresTwoFactor => new SignInResponse(false, true, null),
            SignInOutcome.LockedOut => throw new LockedException(),
            SignInOutcome.NotAllowed => throw new ForbiddenException("This account is suspended. Please contact support.", "account_suspended"),
            _ => throw WrongLogin(),
        };
    }

    public Task SignOutAsync(CancellationToken ct) => session.SignOutAsync();

    public async Task SignOutEverywhereAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        await session.SignOutEverywhereAsync(userId);
        await audit.LogAsync(AuditSystem.Site, "account.signout_everywhere", "Signed out on every device", "User", userId.ToString(), ct: ct);
    }

    public Task ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct) => identity.ConfirmEmailAsync(request.UserId, request.Token);

    public async Task ResendConfirmationAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var account = await identity.FindByIdAsync(userId, ct) ?? throw new UnauthorizedException();
        if (account.EmailConfirmed)
        {
            throw new ConflictException("Your email is already confirmed.", "already_confirmed");
        }
        await SendConfirmationAsync(userId, ct);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        var account = await identity.FindByLoginAsync(request.Email.Trim(), ct);
        if (account is null)
        {
            return; // same answer either way
        }
        var token = await identity.CreatePasswordResetTokenAsync(account.Id);
        var link = site.Value.Link("auth/reset", ("u", account.Id.ToString()), ("t", token));
        await email.SendAsync(new EmailMessage(account.Email, "reset-password", Model(account.FullName, link), account.Language, account.Id), ct);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        await identity.ResetPasswordAsync(request.UserId, request.Token, request.NewPassword);
        await session.SignOutEverywhereAsync(request.UserId);
        await audit.LogAsync(AuditSystem.Site, "account.password_reset", "Password reset by email link", "User", request.UserId.ToString(), ct: ct);
    }

    public async Task AcceptInviteAsync(AcceptInviteRequest request, CancellationToken ct)
    {
        await staff.AcceptInviteAsync(request.UserId, request.Token, request.Password);
        await audit.LogAsync(AuditSystem.Admin, "admin.invite_accepted", "Invitation accepted, password chosen", "User", request.UserId.ToString(), ct: ct);
    }

    private async Task SendConfirmationAsync(Guid userId, CancellationToken ct)
    {
        var account = await identity.FindByIdAsync(userId, ct) ?? throw new NotFoundException();
        var token = await identity.CreateEmailConfirmationTokenAsync(userId);
        var link = site.Value.Link("auth/confirm", ("u", userId.ToString()), ("t", token));
        await email.SendAsync(new EmailMessage(account.Email, "confirm-email", Model(account.FullName, link), account.Language, userId), ct);
    }

    private static Dictionary<string, string> Model(string name, string link) => new() { ["name"] = name, ["link"] = link };

    private static UnauthorizedException WrongLogin() => new("The email, mobile number or password is not correct.", "wrong_login");
}
