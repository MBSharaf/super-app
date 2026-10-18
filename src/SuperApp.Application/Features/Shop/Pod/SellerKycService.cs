using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Storage;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Pod;

/// <summary>The seller's ID check (API §4.4 /seller/kyc): its status, and sending the national ID with both sides of the card again.</summary>
public interface ISellerKycService
{
    Task<KycStatusDto> GetAsync(CancellationToken ct);

    Task<KycStatusDto> SubmitAsync(KycRequest request, UploadedFile front, UploadedFile back, CancellationToken ct);
}

public sealed class SellerKycService(
    ISellerAccess access,
    ISellerInsightsRepository insights,
    IApplicationRepository applications,
    IFileStorage files,
    INotifier notifier,
    IValidator<KycRequest> rules) : ISellerKycService
{
    public async Task<KycStatusDto> GetAsync(CancellationToken ct) => await insights.KycAsync((await access.CurrentAsync(ct)).UserId, ct);

    public async Task<KycStatusDto> SubmitAsync(KycRequest request, UploadedFile front, UploadedFile back, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var me = await access.CurrentAsync(ct);
        var frontId = await files.SaveAsync(front, UploadKind.IdDocument, me.UserId, "idFront", ct);
        var backId = await files.SaveAsync(back, UploadKind.IdDocument, me.UserId, "idBack", ct);
        await applications.SaveKycAsync(me.UserId, request.NationalId.Trim(), frontId, backId, ct);
        await notifier.NotifyStaffAsync("kyc.verify", new NotificationMessage(NotificationCategory.Account, "ID to check", $"Seller {me.StoreName} sent their ID card.", "#/admin/people"), ct);
        return await insights.KycAsync(me.UserId, ct);
    }
}

public sealed class KycRequestValidator : AbstractValidator<KycRequest>
{
    public KycRequestValidator() => RuleFor(x => x.NationalId).NotEmpty().Matches("^[23][0-9]{13}$").WithMessage("The national ID has 14 digits.");
}
