using SuperApp.Application.Abstractions.Email;
using SuperApp.Application.Abstractions.Identity;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Applications.Dtos;
using SuperApp.Application.Security;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Applications.Services;

/// <summary>The team's approvals queue for applications (API §7.2 "Approvals"). Each type needs its own permission.</summary>
public interface IApprovalsService
{
    Task<PagedResult<ApplicationResponse>> ListAsync(ApprovalsQuery query, CancellationToken ct);

    Task<ApplicationResponse> GetAsync(string code, CancellationToken ct);

    Task<ApplicationResponse> ApproveAsync(string code, CancellationToken ct);

    Task<ApplicationResponse> RejectAsync(string code, RejectRequest request, CancellationToken ct);
}

public sealed class ApprovalsService(
    IApplicationRepository applications,
    IOnboardingRepository onboarding,
    IIdentityService identity,
    INotifier notifier,
    IEmailService email,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IClock clock) : IApprovalsService
{
    public async Task<PagedResult<ApplicationResponse>> ListAsync(ApprovalsQuery query, CancellationToken ct)
    {
        var allowed = Enum.GetValues<ApplicationType>().Where(t => currentUser.HasPermission(ApplicationJson.RulesOf(t).Permission)).ToList();
        if (Enum.TryParse<ApplicationType>(query.Type, ignoreCase: true, out var only))
        {
            allowed = allowed.Where(t => t == only).ToList();
        }
        if (allowed.Count == 0)
        {
            throw new ForbiddenException();
        }
        ApplicationStatus? status = Enum.TryParse<ApplicationStatus>(query.Status, ignoreCase: true, out var s) ? s : null;
        var page = new PageRequest(query.Page, query.PageSize);
        var result = await applications.ListAsync(allowed, status, page.SafePage, page.SafePageSize, ct);
        var items = new List<ApplicationResponse>();
        foreach (var app in result.Items)
        {
            items.Add(ApplicationJson.ToResponse(app, await applications.ApplicantAsync(app.ApplicantId, ct)));
        }
        return new PagedResult<ApplicationResponse>(items, result.Page, result.PageSize, result.Total);
    }

    public async Task<ApplicationResponse> GetAsync(string code, CancellationToken ct)
    {
        var app = await FindAllowedAsync(code, ct);
        return ApplicationJson.ToResponse(app, await applications.ApplicantAsync(app.ApplicantId, ct));
    }

    public async Task<ApplicationResponse> ApproveAsync(string code, CancellationToken ct)
    {
        var app = await FindPendingAsync(code, ct);
        await OnboardAsync(app, ct);
        await identity.AddRoleAsync(app.ApplicantId, RoleOf(app.Type));
        await applications.DecideKycAsync(app.ApplicantId, verified: true, Reviewer(), null, ct);
        Decide(app, ApplicationStatus.Approved, null);
        await unitOfWork.SaveChangesAsync(ct);
        await TellApplicantAsync(app, approved: true, ct);
        return await GetAsync(code, ct);
    }

    public async Task<ApplicationResponse> RejectAsync(string code, RejectRequest request, CancellationToken ct)
    {
        var app = await FindPendingAsync(code, ct);
        var reason = request.Reason.Trim();
        await applications.DecideKycAsync(app.ApplicantId, verified: false, Reviewer(), reason, ct);
        Decide(app, ApplicationStatus.Rejected, reason);
        await unitOfWork.SaveChangesAsync(ct);
        await TellApplicantAsync(app, approved: false, ct);
        return await GetAsync(code, ct);
    }

    private Task OnboardAsync(PartnerApplication app, CancellationToken ct) => app.Type switch
    {
        ApplicationType.PitchOwner => onboarding.OnboardPitchOwnerAsync(app.ApplicantId, ApplicationJson.Read<PitchOwnerAnswers>(app.AnswersJson),
            app.Files.Where(f => f.Purpose == ApplicationFilePurpose.VenuePhoto).Select(f => f.FileId).ToList(), ct),
        ApplicationType.Seller => onboarding.OnboardSellerAsync(app.ApplicantId, ApplicationJson.Read<SellerAnswers>(app.AnswersJson), ct),
        _ => onboarding.OnboardFactoryAsync(app.ApplicantId, ApplicationJson.Read<FactoryAnswers>(app.AnswersJson), ct),
    };

    private void Decide(PartnerApplication app, ApplicationStatus status, string? reason)
    {
        app.Status = status;
        app.RejectReason = reason;
        app.ReviewedById = Reviewer();
        app.ReviewedAt = clock.UtcNow;
    }

    /// <summary>Notification + email to the applicant, and an audit line for the decision.</summary>
    private async Task TellApplicantAsync(PartnerApplication app, bool approved, CancellationToken ct)
    {
        var (_, _, label) = ApplicationJson.RulesOf(app.Type);
        var applicant = await identity.FindByIdAsync(app.ApplicantId, ct) ?? throw new NotFoundException();
        var title = approved ? $"You are now a Super App {label}!" : $"Your {label} application needs changes";
        var body = approved ? "Open your dashboard from your profile to get started." : $"Reason: {app.RejectReason}. Fix it and send it again from your profile.";
        await notifier.NotifyUserAsync(app.ApplicantId, new NotificationMessage(NotificationCategory.Account, title, body, "#/account",
            approved ? NotificationTone.Success : NotificationTone.Warning), ct);
        var model = new Dictionary<string, string> { ["name"] = applicant.FullName, ["kind"] = label, ["code"] = app.Code, ["reason"] = app.RejectReason ?? "" };
        await email.SendAsync(new EmailMessage(applicant.Email, approved ? "application-approved" : "application-rejected", model, applicant.Language, app.ApplicantId), ct);
        await audit.LogAsync(AuditSystem.Admin, approved ? "application.approved" : "application.rejected",
            $"{label} application {app.Code} {(approved ? "approved" : "rejected: " + app.RejectReason)}", "Application", app.Code, ct: ct);
    }

    private async Task<PartnerApplication> FindPendingAsync(string code, CancellationToken ct)
    {
        var app = await FindAllowedAsync(code, ct);
        if (app.Status != ApplicationStatus.Pending)
        {
            throw new ConflictException("This application was already decided.", "already_decided");
        }
        return app;
    }

    /// <summary>Someone without the permission for this type gets 404, as if it did not exist.</summary>
    private async Task<PartnerApplication> FindAllowedAsync(string code, CancellationToken ct)
    {
        var app = await applications.FindAsync(code, ct);
        if (app is null || !currentUser.HasPermission(ApplicationJson.RulesOf(app.Type).Permission))
        {
            throw new NotFoundException("This application does not exist.");
        }
        return app;
    }

    private static string RoleOf(ApplicationType type) => type switch
    {
        ApplicationType.PitchOwner => SystemRoles.PitchOwner,
        ApplicationType.Seller => SystemRoles.Seller,
        _ => SystemRoles.FactoryOwner,
    };

    private Guid Reviewer() => currentUser.UserId ?? throw new UnauthorizedException();
}
