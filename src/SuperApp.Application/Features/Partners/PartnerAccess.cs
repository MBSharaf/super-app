using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Partners;

/// <summary>Who may use the partner dashboard, and for what.</summary>
public interface IPartnerAccess
{
    /// <summary>Any active member of the business (owner or staff). Not a partner → 403 <c>not_partner</c>.</summary>
    Task<PartnerScope> ViewAsync(CancellationToken ct);

    /// <summary>The owner or a manager, and the business is not suspended (→ 423 <c>partner_suspended</c>).</summary>
    Task<PartnerScope> ManageAsync(CancellationToken ct);

    /// <summary>Work at the venue (walk-ins, no-shows, check-in): owner, manager or check-in staff; not accountants.</summary>
    Task<PartnerScope> DeskAsync(CancellationToken ct);
}

public sealed class PartnerAccess(IPartnerRepository partners, ICurrentUser currentUser) : IPartnerAccess
{
    private PartnerScope? scope;

    public async Task<PartnerScope> ViewAsync(CancellationToken ct)
    {
        var me = currentUser.UserId ?? throw new UnauthorizedException();
        scope ??= await partners.ScopeAsync(me, ct)
            ?? throw new ForbiddenException("This page is for pitch partners. Apply to list your pitch first.", "not_partner");
        return scope;
    }

    public async Task<PartnerScope> ManageAsync(CancellationToken ct)
    {
        var s = await ViewAsync(ct);
        if (!s.CanManage)
        {
            throw new ForbiddenException("Only the owner or a manager can change this.", "manager_only");
        }
        return NotSuspended(s);
    }

    public async Task<PartnerScope> DeskAsync(CancellationToken ct)
    {
        var s = await ViewAsync(ct);
        if (s.Role == PartnerStaffRole.Accountant)
        {
            throw new ForbiddenException("Accountants can see bookings but not change them.", "desk_only");
        }
        return NotSuspended(s);
    }

    private static PartnerScope NotSuspended(PartnerScope s) => s.Status == PartnerStatus.Suspended
        ? throw new FeatureDisabledException("Your partner account is suspended. Contact Super App support.", "partner_suspended")
        : s;
}
