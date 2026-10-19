using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.AdminConsole;

/// <summary>Partners and sellers.</summary>
public sealed partial class AdminPeopleService
{
    public Task<PagedResult<PartnerRowDto>> PartnersAsync(PeopleQuery query, CancellationToken ct) => reader.PartnersAsync(query, clock.UtcNow, ct);

    public async Task<PartnerDetailDto> PartnerAsync(long id, CancellationToken ct) =>
        await reader.PartnerAsync(id, clock.UtcNow, ct) ?? throw new NotFoundException("This partner does not exist.");

    public async Task<PartnerDetailDto> SetPartnerStatusAsync(long id, bool suspend, string? reason, CancellationToken ct)
    {
        var partner = await people.PartnerAsync(id, ct) ?? throw new NotFoundException("This partner does not exist.");
        var target = suspend ? PartnerStatus.Suspended : PartnerStatus.Active;
        if (partner.Status == target)
        {
            throw new ConflictException(suspend ? "This partner is already suspended." : "This partner is already active.", "same_status");
        }
        (partner.Status, partner.SuspendedReason) = (target, suspend ? reason!.Trim() : null);
        await unitOfWork.SaveChangesAsync(ct);
        var (title, body) = suspend ? ("Your partner account is suspended", reason!.Trim()) : ("Your partner account is active again", "Your pitches are back in Super App.");
        await notifier.NotifyUserAsync(partner.UserId, new NotificationMessage(NotificationCategory.Account, title, body, "#/dash",
            suspend ? NotificationTone.Warning : NotificationTone.Success), ct);
        await audit.LogAsync(AuditSystem.Admin, suspend ? "partner.suspended" : "partner.reactivated", $"{partner.BusinessName}: {body}", "Partner", id.ToString(), ct: ct);
        return await PartnerAsync(id, ct);
    }

    public Task<PagedResult<SellerRowDto>> SellersAsync(PeopleQuery query, CancellationToken ct) => reader.SellersAsync(query, ct);

    public async Task<SellerDetailDto> SellerAsync(long id, CancellationToken ct) =>
        await reader.SellerAsync(id, ct) ?? throw new NotFoundException("This seller does not exist.");

    public async Task<SellerDetailDto> SetSellerStatusAsync(long id, bool suspend, string? reason, CancellationToken ct)
    {
        var seller = await people.SellerAsync(id, ct) ?? throw new NotFoundException("This seller does not exist.");
        var target = suspend ? SellerStatus.Suspended : SellerStatus.Active;
        if (seller.Status == target)
        {
            throw new ConflictException(suspend ? "This seller is already suspended." : "This seller is already active.", "same_status");
        }
        seller.Status = target;
        await unitOfWork.SaveChangesAsync(ct);
        var (title, body) = suspend ? ("Your seller account is suspended", reason!.Trim()) : ("Your seller account is active again", "Your designs are back on sale.");
        await notifier.NotifyUserAsync(seller.UserId, new NotificationMessage(NotificationCategory.Store, title, body, "#/seller",
            suspend ? NotificationTone.Warning : NotificationTone.Success), ct);
        await audit.LogAsync(AuditSystem.Admin, suspend ? "seller.suspended" : "seller.reactivated", body, "Seller", id.ToString(), ct: ct);
        return await SellerAsync(id, ct);
    }

    public async Task<SellerDetailDto> FeatureSellerAsync(long id, FeatureRequest request, CancellationToken ct)
    {
        var seller = await people.SellerAsync(id, ct) ?? throw new NotFoundException("This seller does not exist.");
        seller.FeaturedByAdmin = request.Featured;
        await unitOfWork.SaveChangesAsync(ct);
        if (request.Featured)
        {
            await notifier.NotifyUserAsync(seller.UserId, new NotificationMessage(NotificationCategory.Store, "You are a featured seller",
                "Super App now shows your store on the shop's front page.", "#/seller", NotificationTone.Success), ct);
        }
        await audit.LogAsync(AuditSystem.Admin, "seller.featured", request.Featured ? "Featured" : "No longer featured", "Seller", id.ToString(), ct: ct);
        return await SellerAsync(id, ct);
    }
}
