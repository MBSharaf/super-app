using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Factory;

/// <summary>The factory the caller works for, who they are and what their role may do (FR-FAC-03).</summary>
public sealed record FactoryScope(long FactoryId, string FactoryName, Guid OwnerUserId, long? StaffId, string Name, FactoryRole Role, FactoryStatus Status, bool IntakePaused, short SlaHours)
{
    public bool Can(string action) => FactoryRoles.Can(Role, action);

    /// <summary>Throws 403 role when the role may not do this.</summary>
    public FactoryScope Need(string action) =>
        Can(action) ? this : throw new ForbiddenException("Your role can’t do this. Ask your supervisor.", "role");
}

/// <summary>What each factory role may do (same table as the factory site).</summary>
public static class FactoryRoles
{
    private static readonly Dictionary<FactoryRole, string[]> Actions = new()
    {
        [FactoryRole.Owner] = ["print", "qc", "pack", "assign", "ship", "reports", "team", "money", "settings", "intake"],
        [FactoryRole.Supervisor] = ["print", "qc", "pack", "assign", "ship", "reports", "intake"],
        [FactoryRole.Printer] = ["print"],
        [FactoryRole.QualityCheck] = ["qc"],
        [FactoryRole.Packer] = ["pack", "ship"],
    };

    public static bool Can(FactoryRole role, string action) => Actions[role].Contains(action);

    public static IReadOnlyList<string> Of(FactoryRole role) => Actions[role];
}

/// <summary>The request's factory headers (device token and staff session); empty when the owner calls with the account.</summary>
public interface IFactoryHeaders
{
    string? DeviceToken { get; }

    string? SessionToken { get; }
}

/// <summary>Who may use the factory system: the owner (account) or staff (PIN session on a registered device).</summary>
public interface IFactoryAccess
{
    Task<FactoryScope> CurrentAsync(CancellationToken ct);

    /// <summary>The factory owned by the signed-in account (device registration, team, money, settings).</summary>
    Task<FactoryScope> OwnerAsync(CancellationToken ct);
}

public sealed class FactoryAccess(IFactoryRepository factories, IFactoryTokens tokens, IFactoryHeaders headers, ICurrentUser currentUser) : IFactoryAccess
{
    private FactoryScope? scope;

    public async Task<FactoryScope> CurrentAsync(CancellationToken ct)
    {
        if (scope is not null)
        {
            return scope;
        }
        if (!string.IsNullOrEmpty(headers.SessionToken))
        {
            var session = tokens.ReadSession(headers.SessionToken) ?? throw new UnauthorizedException("Your factory session ended. Sign in with your PIN.", "factory_session");
            var device = await factories.DeviceAsync(headers.DeviceToken ?? "", ct);
            if (device is null || device.Id != session.DeviceId)
            {
                throw new UnauthorizedException("This device is not registered.", "factory_device");
            }
            scope = await factories.StaffScopeAsync(session.StaffId, device.FactoryId, ct) ?? throw new UnauthorizedException("Your factory session ended. Sign in with your PIN.", "factory_session");
        }
        else
        {
            scope = await OwnerAsync(ct);
        }
        return scope.Status == FactoryStatus.Suspended
            ? throw new FeatureDisabledException("This factory is suspended. Contact Super App.", "factory_suspended")
            : scope;
    }

    public async Task<FactoryScope> OwnerAsync(CancellationToken ct)
    {
        var me = currentUser.UserId ?? throw new UnauthorizedException();
        return await factories.OwnerScopeAsync(me, ct) ?? throw new ForbiddenException("This page is for print factories.", "not_factory");
    }
}
