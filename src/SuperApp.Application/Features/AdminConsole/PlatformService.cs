using System.Text.Json;
using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.CeoSystem;
using SuperApp.Domain.Entities.Notifications;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.AdminConsole;

/// <summary>
/// Platform settings (non-money), maintenance mode and broadcasts (API §7.2 "Settings", "Marketing"). Money settings are
/// listed read-only (403 <c>money_setting</c>) until the CEO limits screen comes with Phase 5.
/// </summary>
public interface IPlatformService
{
    Task<IReadOnlyList<SettingDto>> SettingsAsync(string? group, CancellationToken ct);

    Task<IReadOnlyList<SettingDto>> UpdateSettingsAsync(string group, UpdateSettingsRequest request, CancellationToken ct);

    Task<MaintenanceDto> MaintenanceAsync(CancellationToken ct);

    Task<MaintenanceDto> SetMaintenanceAsync(MaintenanceRequest request, CancellationToken ct);

    Task<PagedResult<BroadcastDto>> BroadcastsAsync(int page, int pageSize, CancellationToken ct);

    Task<(BroadcastDto Broadcast, bool Scheduled)> BroadcastAsync(BroadcastRequest request, CancellationToken ct);

    /// <summary>Sends scheduled broadcasts whose time has come (called by the scheduler).</summary>
    Task<int> SendDueAsync(CancellationToken ct);
}

public sealed class PlatformService(
    IPlatformSettingsRepository settings,
    ISwitchRepository switches,
    IBroadcastRepository broadcasts,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IClock clock) : IPlatformService
{
    private static readonly HashSet<string> MoneyGroups = ["money", "limits"];
    private static readonly HashSet<string> MoneyKeys = ["pod.minWithdraw", "pod.holdDays", "delivery.fees", "fantasy.passPrice", "loyalty.earnPer", "loyalty.pointValue", "loyalty.minRedeem"];

    public static bool Editable(string group, string key) => !MoneyGroups.Contains(group) && !MoneyKeys.Contains(key) && !key.StartsWith("loyalty.", StringComparison.Ordinal);

    public async Task<IReadOnlyList<SettingDto>> SettingsAsync(string? group, CancellationToken ct) =>
        (await settings.ListAsync(group, ct)).Select(s => new SettingDto(s.Key, s.Group, Parse(s.Value), Editable(s.Group, s.Key), s.UpdatedAt)).ToList();

    public async Task<IReadOnlyList<SettingDto>> UpdateSettingsAsync(string group, UpdateSettingsRequest request, CancellationToken ct)
    {
        var rows = (await settings.ListAsync(group, ct)).ToDictionary(s => s.Key);
        if (rows.Count == 0)
        {
            throw new NotFoundException("This settings group does not exist.");
        }
        foreach (var (key, value) in request.Values)
        {
            var row = rows.GetValueOrDefault(key) ?? throw new BadRequestException($"{key} is not in the {group} settings.", key);
            if (!Editable(group, key))
            {
                throw new ForbiddenException("Money settings are changed from the CEO limits.", "money_setting");
            }
            (row.Value, row.UpdatedAt, row.UpdatedById) = (value.GetRawText(), clock.UtcNow, currentUser.UserId);
        }
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, "settings.updated", $"{group}: {string.Join(", ", request.Values.Keys)}", "Setting", group,
            request.Values.ToDictionary(v => v.Key, v => v.Value.GetRawText()), ct);
        return await SettingsAsync(group, ct);
    }

    public async Task<MaintenanceDto> MaintenanceAsync(CancellationToken ct)
    {
        var bookings = await switches.GetAsync(KillSwitches.Bookings, ct);
        var shop = await switches.GetAsync(KillSwitches.Shop, ct);
        return new MaintenanceDto(bookings?.IsEnabled == false, shop?.IsEnabled == false, bookings?.Reason ?? shop?.Reason);
    }

    /// <summary>Pauses bookings and / or the shop with a message (the CEO's kill switches kill.bookings / kill.shop).</summary>
    public async Task<MaintenanceDto> SetMaintenanceAsync(MaintenanceRequest request, CancellationToken ct)
    {
        var message = string.IsNullOrWhiteSpace(request.Message) ? "Back soon — we are doing some maintenance." : request.Message.Trim();
        await switches.SetAsync(KillSwitches.Bookings, !request.PauseBookings, request.PauseBookings ? message : null, currentUser.UserId, ct);
        await switches.SetAsync(KillSwitches.Shop, !request.PauseShop, request.PauseShop ? message : null, currentUser.UserId, ct);
        await audit.LogAsync(AuditSystem.Admin, "maintenance.changed", $"Bookings {(request.PauseBookings ? "paused" : "on")}, shop {(request.PauseShop ? "paused" : "on")}", ct: ct);
        return await MaintenanceAsync(ct);
    }

    public Task<PagedResult<BroadcastDto>> BroadcastsAsync(int page, int pageSize, CancellationToken ct) => broadcasts.ListAsync(page, pageSize, ct);

    public async Task<(BroadcastDto Broadcast, bool Scheduled)> BroadcastAsync(BroadcastRequest request, CancellationToken ct)
    {
        var scheduled = request.ScheduledAt is { } at && at > clock.UtcNow.AddMinutes(1);
        var row = new Broadcast
        {
            Title = request.Title.Trim(), Body = request.Body.Trim(), Link = request.Link?.Trim(), Segment = ReportKinds.Segment(request.Segment)!.Value,
            SendEmail = request.SendEmail, ScheduledAt = scheduled ? request.ScheduledAt : null, CreatedById = currentUser.UserId,
        };
        broadcasts.Add(row);
        await unitOfWork.SaveChangesAsync(ct);
        if (!scheduled)
        {
            await SendAsync(row, ct);
        }
        await audit.LogAsync(AuditSystem.Admin, scheduled ? "broadcast.scheduled" : "broadcast.sent", $"{row.Title} → {row.Segment} ({row.RecipientsCount})", "Broadcast", row.Id.ToString(), ct: ct);
        return (ToDto(row), scheduled);
    }

    public async Task<int> SendDueAsync(CancellationToken ct)
    {
        var due = await broadcasts.DueAsync(clock.UtcNow, ct);
        foreach (var row in due)
        {
            await SendAsync(row, ct);
        }
        return due.Count;
    }

    private async Task SendAsync(Broadcast row, CancellationToken ct)
    {
        row.RecipientsCount = await broadcasts.DeliverAsync(row, ct);
        row.SentAt = clock.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static BroadcastDto ToDto(Broadcast b) =>
        new(b.Id, b.Title, b.Body, b.Link, b.Segment.ToString(), b.SendEmail, b.ScheduledAt, b.SentAt, b.RecipientsCount, b.CreatedAt);

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
}

public sealed class UpdateSettingsRequestValidator : AbstractValidator<UpdateSettingsRequest>
{
    public UpdateSettingsRequestValidator()
    {
        RuleFor(x => x.Values).NotNull().Must(v => v.Count is > 0 and <= 30).WithMessage("Send 1–30 settings.")
            .Must(v => v.Values.All(j => j.ValueKind != JsonValueKind.Undefined && j.GetRawText().Length <= 4000)).WithMessage("A value is too long.");
    }
}

public sealed class MaintenanceRequestValidator : AbstractValidator<MaintenanceRequest>
{
    public MaintenanceRequestValidator()
    {
        RuleFor(x => x.Message).MaximumLength(300);
    }
}

public sealed class BroadcastRequestValidator : AbstractValidator<BroadcastRequest>
{
    public BroadcastRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Write a title.").MaximumLength(140);
        RuleFor(x => x.Body).NotEmpty().WithMessage("Write the message.").MaximumLength(1000);
        RuleFor(x => x.Link).Must(l => l is null || l.StartsWith("#/", StringComparison.Ordinal)).WithMessage("Links start with #/.").MaximumLength(300);
        RuleFor(x => x.Segment).Must(s => ReportKinds.Segment(s) is not null).WithMessage("Segment: Everyone, Customers, Partners, Sellers, Factories or FantasyPlayers.");
    }
}
