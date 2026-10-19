using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.CeoSystem;

/// <summary>
/// The CEO's controls (API §8): the 9 kill switches, the API catalogue with its group switches, and the AI panel summary.
/// Turning a switch off makes the area answer 423 at once; everyone with the audit permission is told.
/// </summary>
public interface ICeoControlService
{
    Task<IReadOnlyList<SwitchDto>> SwitchesAsync(CancellationToken ct);

    Task<SwitchDto> SetSwitchAsync(string key, SetSwitchRequest request, CancellationToken ct);

    Task<IReadOnlyList<ApiGroupDto>> CatalogueAsync(CancellationToken ct);

    Task<ApiGroupDto> SetApiGroupAsync(string group, SetApiGroupRequest request, CancellationToken ct);

    Task<AiPanelDto> AiPanelAsync(CancellationToken ct);
}

public sealed class CeoControlService(
    ISwitchRepository switches,
    IAiPanelReader ai,
    IAuditLogger audit,
    INotifier notifier,
    ICurrentUser currentUser,
    IClock clock) : ICeoControlService
{
    public Task<IReadOnlyList<SwitchDto>> SwitchesAsync(CancellationToken ct) => switches.ListAsync(FeatureFlagKind.KillSwitch, ct);

    public async Task<SwitchDto> SetSwitchAsync(string key, SetSwitchRequest request, CancellationToken ct)
    {
        var current = await switches.GetAsync(key, ct);
        if (current is null || current.Kind != nameof(FeatureFlagKind.KillSwitch))
        {
            throw new NotFoundException("This switch does not exist.");
        }
        return await FlipAsync(key, request.Enabled, request.Reason, ct);
    }

    public async Task<IReadOnlyList<ApiGroupDto>> CatalogueAsync(CancellationToken ct)
    {
        var flags = (await switches.ListAsync(null, ct)).ToDictionary(f => f.Key, f => f.IsEnabled);
        var calls = await ai.AgentCallsByGroupAsync(clock.UtcNow.AddHours(-24), ct);
        return ApiCatalogue.Groups.Select(g => new ApiGroupDto(g.Group, g.Name, g.Purpose, g.Permission, g.Callers, "/api/v1" + g.Path, g.SwitchKey,
            g.SwitchKey is null ? null : flags.GetValueOrDefault(g.SwitchKey, true), calls.GetValueOrDefault(g.Group))).ToList();
    }

    public async Task<ApiGroupDto> SetApiGroupAsync(string group, SetApiGroupRequest request, CancellationToken ct)
    {
        var info = ApiCatalogue.Groups.FirstOrDefault(g => g.Group == group) ?? throw new NotFoundException("This API group does not exist.");
        if (info.SwitchKey is null)
        {
            throw new BusinessRuleException("This group has no switch.", "no_switch");
        }
        await FlipAsync(info.SwitchKey, request.Enabled, request.Reason, ct);
        return (await CatalogueAsync(ct)).Single(g => g.Group == group);
    }

    public async Task<AiPanelDto> AiPanelAsync(CancellationToken ct) =>
        await ai.PanelAsync(await switches.IsOnAsync(KillSwitches.Agents, ct), clock.UtcNow, ct);

    private async Task<SwitchDto> FlipAsync(string key, bool enabled, string? reason, CancellationToken ct)
    {
        if (!enabled && string.IsNullOrWhiteSpace(reason))
        {
            throw new BadRequestException("Write why it is switched off — people will see it.", "reason");
        }
        await switches.SetAsync(key, enabled, enabled ? null : reason!.Trim(), currentUser.UserId, ct);
        var state = enabled ? "on" : "off";
        await audit.LogAsync(AuditSystem.Ceo, "switch.changed", $"{key} switched {state}{(enabled ? "" : ": " + reason)}", "FeatureFlag", key, ct: ct);
        await notifier.NotifyStaffAsync("audit.read", new NotificationMessage(NotificationCategory.Account, $"{key} switched {state}",
            enabled ? null : reason, "#/ceo/control", enabled ? NotificationTone.Info : NotificationTone.Warning), ct);
        return (await switches.GetAsync(key, ct))!;
    }
}

public sealed class SetSwitchRequestValidator : AbstractValidator<SetSwitchRequest>
{
    public SetSwitchRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(300);
    }
}

public sealed class SetApiGroupRequestValidator : AbstractValidator<SetApiGroupRequest>
{
    public SetApiGroupRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(300);
    }
}
