using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Abstractions.Storage;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Applications.Dtos;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Applications.Services;

/// <summary>Apply to become a pitch partner, seller or factory (API §2.3).</summary>
public interface IApplicationsService
{
    Task<ApplicationResponse> ApplyPitchOwnerAsync(PitchOwnerAnswers answers, ApplicationFiles upload, CancellationToken ct);

    Task<ApplicationResponse> ApplySellerAsync(SellerAnswers answers, ApplicationFiles upload, CancellationToken ct);

    Task<ApplicationResponse> ApplyFactoryAsync(FactoryAnswers answers, ApplicationFiles upload, CancellationToken ct);

    Task<IReadOnlyList<ApplicationResponse>> MineAsync(CancellationToken ct);

    /// <summary>Fix and resend a rejected application: new answers, and new files only where they are sent.</summary>
    Task<ApplicationResponse> ResubmitAsync(string code, JsonElement answers, ApplicationFiles upload, CancellationToken ct);
}

public sealed class ApplicationsService(
    IApplicationRepository applications,
    ILegalRepository legal,
    IFileStorage files,
    IFeatureFlags flags,
    INotifier notifier,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IRequestInfo requestInfo,
    IValidator<PitchOwnerAnswers> pitchOwnerRules,
    IValidator<SellerAnswers> sellerRules,
    IValidator<FactoryAnswers> factoryRules) : IApplicationsService
{
    public async Task<ApplicationResponse> ApplyPitchOwnerAsync(PitchOwnerAnswers answers, ApplicationFiles upload, CancellationToken ct)
    {
        await pitchOwnerRules.ValidateAndThrowAsync(answers, ct);
        await EnsureAreaAsync(answers.AreaId, ct);
        return await SubmitAsync(ApplicationType.PitchOwner, answers.NationalId, answers with { NationalId = ApplicationJson.Mask(answers.NationalId) }, upload, ct);
    }

    public async Task<ApplicationResponse> ApplySellerAsync(SellerAnswers answers, ApplicationFiles upload, CancellationToken ct)
    {
        await sellerRules.ValidateAndThrowAsync(answers, ct);
        if (!string.IsNullOrEmpty(answers.Handle) && await applications.StoreHandleTakenAsync(answers.Handle, ct))
        {
            throw Field("handle", "That store link is taken — try another.");
        }
        return await SubmitAsync(ApplicationType.Seller, answers.NationalId, answers with { NationalId = ApplicationJson.Mask(answers.NationalId) }, upload, ct);
    }

    public async Task<ApplicationResponse> ApplyFactoryAsync(FactoryAnswers answers, ApplicationFiles upload, CancellationToken ct)
    {
        await factoryRules.ValidateAndThrowAsync(answers, ct);
        await EnsureAreaAsync(answers.AreaId, ct);
        if (await applications.FactoryNameTakenAsync(answers.FactoryName.Trim(), ct))
        {
            throw Field("factoryName", "A factory with this name is already on Super App.");
        }
        return await SubmitAsync(ApplicationType.Factory, answers.NationalId, answers with { NationalId = ApplicationJson.Mask(answers.NationalId) }, upload, ct);
    }

    public async Task<IReadOnlyList<ApplicationResponse>> MineAsync(CancellationToken ct)
    {
        var me = Me();
        var applicant = await applications.ApplicantAsync(me, ct);
        return (await applications.MineAsync(me, ct)).Select(a => ApplicationJson.ToResponse(a, applicant)).ToList();
    }

    public async Task<ApplicationResponse> ResubmitAsync(string code, JsonElement answers, ApplicationFiles upload, CancellationToken ct)
    {
        await flags.EnsureOnAsync(KillSwitches.Applications, ct);
        var app = await applications.FindAsync(code, ct);
        if (app is null || app.ApplicantId != Me())
        {
            throw new NotFoundException("This application does not exist.");
        }
        if (app.Status != ApplicationStatus.Rejected)
        {
            throw new BusinessRuleException("Only a rejected application can be fixed and sent again.", "not_rejected");
        }
        var nationalId = await ValidateResubmitAsync(app, answers, ct);
        await ReplaceFilesAsync(app, upload, nationalId, ct);
        app.Status = ApplicationStatus.Pending;
        app.RejectReason = null;
        await unitOfWork.SaveChangesAsync(ct);
        await AnnounceAsync(app, resent: true, ct);
        return ApplicationJson.ToResponse(app, await applications.ApplicantAsync(app.ApplicantId, ct));
    }

    private async Task<ApplicationResponse> SubmitAsync<T>(ApplicationType type, string nationalId, T maskedAnswers, ApplicationFiles upload, CancellationToken ct)
    {
        await flags.EnsureOnAsync(KillSwitches.Applications, ct);
        var me = Me();
        await EnsureCanApplyAsync(me, type, ct);
        CheckFiles(type, upload);
        var (agreement, _, _) = ApplicationJson.RulesOf(type);
        var agreementVersion = await legal.CurrentVersionIdAsync(agreement, ct)
            ?? throw new BusinessRuleException("Applications are not open yet: the agreement is not published.", "agreement_missing");

        var app = new PartnerApplication
        {
            Code = await applications.NewCodeAsync(ct), ApplicantId = me, Type = type, AnswersJson = ApplicationJson.Write(maskedAnswers),
            AgreementVersionId = agreementVersion, Status = ApplicationStatus.Pending,
        };
        await AttachFilesAsync(app, upload, me, ct);
        applications.Add(app);
        legal.AddAcceptance(agreementVersion, me, $"application.{type}", requestInfo.IpAddress);
        await unitOfWork.SaveChangesAsync(ct);
        await applications.SaveKycAsync(me, nationalId, IdFile(app, ApplicationFilePurpose.IdFront), IdFile(app, ApplicationFilePurpose.IdBack), ct);
        await AnnounceAsync(app, resent: false, ct);
        return ApplicationJson.ToResponse(app, await applications.ApplicantAsync(me, ct));
    }

    private async Task EnsureCanApplyAsync(Guid me, ApplicationType type, CancellationToken ct)
    {
        if (await applications.IsAlreadyMemberAsync(me, type, ct))
        {
            throw new ConflictException("Your account already has this.", "already_member");
        }
        if (await applications.HasOpenAsync(me, type, ct))
        {
            throw new ConflictException("Your application is already being reviewed.", "application_open");
        }
    }

    private static void CheckFiles(ApplicationType type, ApplicationFiles upload)
    {
        var errors = new List<ValidationFailure>();
        if (upload.IdFront is null) { errors.Add(new("idFront", "Add a photo of the front of your ID.")); }
        if (upload.IdBack is null) { errors.Add(new("idBack", "Add a photo of the back of your ID.")); }
        if (type == ApplicationType.PitchOwner && upload.Photos.Count is < 1 or > 4) { errors.Add(new("photos", "Add 1 to 4 photos of the pitch.")); }
        if (upload.Documents.Count > 4) { errors.Add(new("documents", "Up to 4 documents.")); }
        if (errors.Count > 0) { throw new ValidationException(errors); }
    }

    private async Task AttachFilesAsync(PartnerApplication app, ApplicationFiles upload, Guid owner, CancellationToken ct)
    {
        async Task Add(UploadedFile file, UploadKind kind, ApplicationFilePurpose purpose, string field) =>
            app.Files.Add(new ApplicationFile { FileId = await files.SaveAsync(file, kind, owner, field, ct), Purpose = purpose });

        if (upload.IdFront is not null) { await Add(upload.IdFront, UploadKind.IdDocument, ApplicationFilePurpose.IdFront, "idFront"); }
        if (upload.IdBack is not null) { await Add(upload.IdBack, UploadKind.IdDocument, ApplicationFilePurpose.IdBack, "idBack"); }
        foreach (var photo in upload.Photos.Take(4)) { await Add(photo, UploadKind.Photo, ApplicationFilePurpose.VenuePhoto, "photos"); }
        foreach (var doc in upload.Documents.Take(4)) { await Add(doc, UploadKind.Document, ApplicationFilePurpose.Document, "documents"); }
    }

    /// <summary>Validates the new answers for the application's type; returns the full national ID for the ID check.</summary>
    private async Task<string> ValidateResubmitAsync(PartnerApplication app, JsonElement answers, CancellationToken ct)
    {
        switch (app.Type)
        {
            case ApplicationType.PitchOwner:
                var p = ApplicationJson.Read<PitchOwnerAnswers>(answers);
                await pitchOwnerRules.ValidateAndThrowAsync(p, ct);
                await EnsureAreaAsync(p.AreaId, ct);
                app.AnswersJson = ApplicationJson.Write(p with { NationalId = ApplicationJson.Mask(p.NationalId) });
                return p.NationalId;
            case ApplicationType.Seller:
                var s = ApplicationJson.Read<SellerAnswers>(answers);
                await sellerRules.ValidateAndThrowAsync(s, ct);
                app.AnswersJson = ApplicationJson.Write(s with { NationalId = ApplicationJson.Mask(s.NationalId) });
                return s.NationalId;
            default:
                var f = ApplicationJson.Read<FactoryAnswers>(answers);
                await factoryRules.ValidateAndThrowAsync(f, ct);
                await EnsureAreaAsync(f.AreaId, ct);
                app.AnswersJson = ApplicationJson.Write(f with { NationalId = ApplicationJson.Mask(f.NationalId) });
                return f.NationalId;
        }
    }

    /// <summary>Files sent again replace the old ones of the same kind; the ID check is reopened with the latest ID photos.</summary>
    private async Task ReplaceFilesAsync(PartnerApplication app, ApplicationFiles upload, string nationalId, CancellationToken ct)
    {
        var replaced = new HashSet<ApplicationFilePurpose>();
        if (upload.IdFront is not null) { replaced.Add(ApplicationFilePurpose.IdFront); }
        if (upload.IdBack is not null) { replaced.Add(ApplicationFilePurpose.IdBack); }
        if (upload.Photos.Count > 0) { replaced.Add(ApplicationFilePurpose.VenuePhoto); }
        if (upload.Documents.Count > 0) { replaced.Add(ApplicationFilePurpose.Document); }
        foreach (var old in app.Files.Where(f => replaced.Contains(f.Purpose)).ToList())
        {
            app.Files.Remove(old);
        }
        await AttachFilesAsync(app, upload, app.ApplicantId, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await applications.SaveKycAsync(app.ApplicantId, nationalId, IdFile(app, ApplicationFilePurpose.IdFront), IdFile(app, ApplicationFilePurpose.IdBack), ct);
    }

    private async Task AnnounceAsync(PartnerApplication app, bool resent, CancellationToken ct)
    {
        var (_, permission, label) = ApplicationJson.RulesOf(app.Type);
        var applicant = await applications.ApplicantAsync(app.ApplicantId, ct);
        var verb = resent ? "sent again" : "received";
        await notifier.NotifyUserAsync(app.ApplicantId, new NotificationMessage(NotificationCategory.Account,
            $"We {verb} your {label} application", $"Application {app.Code} — the Super App team reviews it within 2 working days.", "#/account", NotificationTone.Info), ct);
        await notifier.NotifyStaffAsync(permission, new NotificationMessage(NotificationCategory.Store,
            $"New {label} application", $"{applicant.FullName} · {app.Code}", "#/admin/approvals", NotificationTone.Warning), ct);
        await audit.LogAsync(AuditSystem.Site, resent ? "application.resent" : "application.submitted", $"{label} application {app.Code}", "Application", app.Code, ct: ct);
    }

    private async Task EnsureAreaAsync(int areaId, CancellationToken ct)
    {
        if (!await applications.AreaExistsAsync(areaId, ct))
        {
            throw Field("areaId", "Choose the area from the list.");
        }
    }

    private static long IdFile(PartnerApplication app, ApplicationFilePurpose purpose) => app.Files.Last(f => f.Purpose == purpose).FileId;

    private static ValidationException Field(string field, string message) => new([new ValidationFailure(field, message)]);

    private Guid Me() => currentUser.UserId ?? throw new UnauthorizedException();
}
