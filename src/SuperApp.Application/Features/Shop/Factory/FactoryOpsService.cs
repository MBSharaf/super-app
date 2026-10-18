using FluentValidation;
using SuperApp.Application.Abstractions.Documents;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Factory;

/// <summary>Factory shipping, team, money, reports and settings (API §4.6, FR-FAC-03, 07, 08). Each part checks the role.</summary>
public interface IFactoryOpsService
{
    Task<IReadOnlyList<FactoryShipmentDto>> ShippingAsync(string? status, CancellationToken ct);

    Task<FactoryShipmentDto> ShipmentAsync(long id, CancellationToken ct);

    Task<byte[]> LabelAsync(long id, CancellationToken ct);

    Task<byte[]> TicketAsync(string code, CancellationToken ct);

    Task<IReadOnlyList<StaffDto>> TeamAsync(CancellationToken ct);

    Task<StaffDto> AddStaffAsync(StaffRequest request, CancellationToken ct);

    Task<StaffDto> UpdateStaffAsync(long id, StaffRequest request, CancellationToken ct);

    Task ResetPinAsync(long id, ResetPinRequest request, CancellationToken ct);

    Task<FactoryMoneyDto> MoneyAsync(CancellationToken ct);

    Task<(byte[] File, string Name)> StatementAsync(CancellationToken ct);

    Task<FactoryReportDto> ReportAsync(DateOnly? from, DateOnly? to, CancellationToken ct);

    Task<FactorySettingsDto> SettingsAsync(CancellationToken ct);

    Task<FactorySettingsDto> SaveSettingsAsync(FactorySettingsRequest request, CancellationToken ct);
}

public sealed class FactoryOpsService(
    IFactoryAccess access,
    IFactoryRepository factories,
    IPinHasher pins,
    IExcelExporter excel,
    IFactoryDocuments documents,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock,
    IValidator<StaffRequest> staffRules,
    IValidator<FactorySettingsRequest> settingsRules) : IFactoryOpsService
{
    public async Task<IReadOnlyList<FactoryShipmentDto>> ShippingAsync(string? status, CancellationToken ct) =>
        await factories.ShipmentsAsync((await access.CurrentAsync(ct)).Need("ship").FactoryId, status, ct);

    public async Task<FactoryShipmentDto> ShipmentAsync(long id, CancellationToken ct) =>
        await factories.ShipmentAsync((await access.CurrentAsync(ct)).Need("ship").FactoryId, id, ct) ?? throw new NotFoundException("Shipment not found.");

    public async Task<byte[]> LabelAsync(long id, CancellationToken ct)
    {
        var me = (await access.CurrentAsync(ct)).Need("ship");
        return documents.ShippingLabel(await factories.LabelAsync(me.FactoryId, id, ct) ?? throw new NotFoundException("Shipment not found."));
    }

    public async Task<byte[]> TicketAsync(string code, CancellationToken ct)
    {
        var me = await access.CurrentAsync(ct);
        return documents.JobTicket(await factories.JobDtoAsync(me.FactoryId, code, clock.UtcNow, ct) ?? throw new NotFoundException("Job not found."), me.FactoryName);
    }

    public async Task<IReadOnlyList<StaffDto>> TeamAsync(CancellationToken ct) => await factories.TeamAsync((await TeamOwnerAsync(ct)).FactoryId, clock.UtcNow, ct);

    public async Task<StaffDto> AddStaffAsync(StaffRequest request, CancellationToken ct)
    {
        await staffRules.ValidateAndThrowAsync(request, ct);
        var me = await TeamOwnerAsync(ct);
        if (string.IsNullOrEmpty(request.Pin))
        {
            throw new FluentValidation.ValidationException([new FluentValidation.Results.ValidationFailure("pin", "Give the new member a 4-digit PIN.")]);
        }
        await EnsureFreeNameAsync(me.FactoryId, request.Name, null, ct);
        var staff = new FactoryStaffMember { FactoryId = me.FactoryId, Name = request.Name.Trim(), Role = Enum.Parse<FactoryRole>(request.Role, true), PinHash = pins.Hash(request.Pin), IsActive = request.IsActive };
        factories.AddStaff(staff);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Factory, "staff.added", $"{me.FactoryName} added {staff.Name} ({staff.Role})", "FactoryStaff", staff.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), ct: ct);
        return (await factories.TeamAsync(me.FactoryId, clock.UtcNow, ct)).Single(s => s.Id == staff.Id);
    }

    public async Task<StaffDto> UpdateStaffAsync(long id, StaffRequest request, CancellationToken ct)
    {
        await staffRules.ValidateAndThrowAsync(request, ct);
        var me = await TeamOwnerAsync(ct);
        var staff = await factories.StaffAsync(me.FactoryId, id, ct) ?? throw new NotFoundException("Team member not found.");
        await EnsureFreeNameAsync(me.FactoryId, request.Name, id, ct);
        (staff.Name, staff.Role, staff.IsActive) = (request.Name.Trim(), Enum.Parse<FactoryRole>(request.Role, true), request.IsActive);
        staff.PinHash = string.IsNullOrEmpty(request.Pin) ? staff.PinHash : pins.Hash(request.Pin);
        await unitOfWork.SaveChangesAsync(ct);
        return (await factories.TeamAsync(me.FactoryId, clock.UtcNow, ct)).Single(s => s.Id == id);
    }

    public async Task ResetPinAsync(long id, ResetPinRequest request, CancellationToken ct)
    {
        if (request.Pin is not { Length: 4 } || !request.Pin.All(char.IsAsciiDigit))
        {
            throw new FluentValidation.ValidationException([new FluentValidation.Results.ValidationFailure("pin", "The PIN has 4 digits.")]);
        }
        var me = await TeamOwnerAsync(ct);
        var staff = await factories.StaffAsync(me.FactoryId, id, ct) ?? throw new NotFoundException("Team member not found.");
        (staff.PinHash, staff.FailedPins, staff.LockedUntil) = (pins.Hash(request.Pin), 0, null);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Factory, "staff.pin_reset", $"{me.FactoryName} reset the PIN of {staff.Name}", "FactoryStaff", id.ToString(System.Globalization.CultureInfo.InvariantCulture), ct: ct);
    }

    public async Task<FactoryMoneyDto> MoneyAsync(CancellationToken ct) => await factories.MoneyAsync((await access.CurrentAsync(ct)).Need("money").FactoryId, ct);

    public async Task<(byte[] File, string Name)> StatementAsync(CancellationToken ct)
    {
        var me = (await access.CurrentAsync(ct)).Need("money");
        var money = await factories.MoneyAsync(me.FactoryId, ct);
        var file = excel.Build(
            new ExcelDashboard($"{me.FactoryName} — statement", $"Made {clock.UtcNow:yyyy-MM-dd} · payouts arrive with the money system",
                [new ExcelFigure("Pieces", money.Pieces), new ExcelFigure("Earned", money.Earned, true), new ExcelFigure("Deductions", money.Deductions, true), new ExcelFigure("Net", money.Net, true)]),
            new ExcelSheet("Jobs", ["Job", "Order", "Date", "Item", "Qty", "Earned (EGP)", "Deduction (EGP)", "Sample"],
                money.Lines.Select(l => (IReadOnlyList<object?>)[l.JobCode, l.OrderCode, l.Date, l.Title, l.Qty, l.Earning, l.Deduction, l.IsSample ? "Yes" : ""]).ToList()));
        return (file, $"statement-{clock.UtcNow:yyyyMMdd}.xlsx");
    }

    public async Task<FactoryReportDto> ReportAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var me = (await access.CurrentAsync(ct)).Need("reports");
        var end = to ?? DateOnly.FromDateTime(clock.UtcNow);
        var start = from ?? end.AddDays(-29);
        if (start > end || end.DayNumber - start.DayNumber > 366)
        {
            throw new BadRequestException("Choose a range of up to one year, oldest date first.", "bad_range");
        }
        return await factories.ReportAsync(me.FactoryId, start, end, ct);
    }

    public async Task<FactorySettingsDto> SettingsAsync(CancellationToken ct)
    {
        var me = await access.CurrentAsync(ct);
        var f = await factories.FactoryAsync(me.FactoryId, ct) ?? throw new NotFoundException();
        return new FactorySettingsDto(f.Name, f.Address, f.CapacityPerDay, f.SlaHours, f.Techniques, f.IntakePaused, f.Status.ToString());
    }

    public async Task<FactorySettingsDto> SaveSettingsAsync(FactorySettingsRequest request, CancellationToken ct)
    {
        await settingsRules.ValidateAndThrowAsync(request, ct);
        var me = (await access.CurrentAsync(ct)).Need("intake");
        var f = await factories.FactoryAsync(me.FactoryId, ct) ?? throw new NotFoundException();
        f.IntakePaused = request.IntakePaused;
        if (me.Can("settings"))
        {
            (f.CapacityPerDay, f.SlaHours) = (request.CapacityPerDay, (short)request.SlaHours);
        }
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Factory, "factory.settings", $"{me.Name} saved the settings of {me.FactoryName} (intake {(f.IntakePaused ? "paused" : "open")})", "Factory", f.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), ct: ct);
        return await SettingsAsync(ct);
    }

    private async Task<FactoryScope> TeamOwnerAsync(CancellationToken ct) => (await access.CurrentAsync(ct)).Need("team");

    private async Task EnsureFreeNameAsync(long factoryId, string name, long? exceptId, CancellationToken ct)
    {
        if (await factories.StaffNameTakenAsync(factoryId, name.Trim(), exceptId, ct))
        {
            throw new ConflictException("Someone in the team already has this name.", "name_taken");
        }
    }
}

public sealed class StaffRequestValidator : AbstractValidator<StaffRequest>
{
    public StaffRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 80);
        RuleFor(x => x.Role).Must(r => r is not null && Enum.TryParse<FactoryRole>(r, true, out var role) && role != FactoryRole.Owner && Enum.IsDefined(role))
            .WithMessage("Role is Supervisor, Printer, QualityCheck or Packer.");
        RuleFor(x => x.Pin).Matches("^[0-9]{4}$").When(x => !string.IsNullOrEmpty(x.Pin)).WithMessage("The PIN has 4 digits.");
    }
}

public sealed class FactorySettingsRequestValidator : AbstractValidator<FactorySettingsRequest>
{
    public FactorySettingsRequestValidator()
    {
        RuleFor(x => x.CapacityPerDay).InclusiveBetween(1, 100_000);
        RuleFor(x => x.SlaHours).InclusiveBetween(1, 240);
    }
}
