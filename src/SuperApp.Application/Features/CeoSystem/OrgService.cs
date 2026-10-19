using System.Text.Json;
using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.CeoSystem;

/// <summary>The org chart (API §8 "Org chart"): the pyramid of departments and positions held by people or AI agents.</summary>
public interface IOrgService
{
    Task<OrgChartDto> ChartAsync(CancellationToken ct);

    Task<PositionDto> CreateAsync(PositionRequest request, CancellationToken ct);

    Task<PositionDto> UpdateAsync(long id, PositionRequest request, CancellationToken ct);

    Task DeleteAsync(long id, CancellationToken ct);
}

public sealed class OrgService(IOrgRepository org, IUnitOfWork unitOfWork, IAuditLogger audit) : IOrgService
{
    public Task<OrgChartDto> ChartAsync(CancellationToken ct) => org.ChartAsync(ct);

    public async Task<PositionDto> CreateAsync(PositionRequest request, CancellationToken ct)
    {
        await CheckAsync(null, request, ct);
        var position = new OrgPosition();
        Apply(position, request);
        org.Add(position);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Ceo, "org.position_created", $"Position {position.TitleEn} added", "OrgPosition", position.Id.ToString(), request, ct);
        return await DtoAsync(position.Id, ct);
    }

    public async Task<PositionDto> UpdateAsync(long id, PositionRequest request, CancellationToken ct)
    {
        var position = await org.PositionAsync(id, ct) ?? throw new NotFoundException("This position does not exist.");
        await CheckAsync(id, request, ct);
        Apply(position, request);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Ceo, "org.position_updated", $"Position {position.TitleEn} edited", "OrgPosition", id.ToString(), request, ct);
        return await DtoAsync(id, ct);
    }

    /// <summary>409 while other positions report to it; agents sitting there lose the position.</summary>
    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        var position = await org.PositionAsync(id, ct) ?? throw new NotFoundException("This position does not exist.");
        if (await org.HasReportsAsync(id, ct))
        {
            throw new ConflictException("Other positions report to this one. Move them first.", "has_reports");
        }
        await org.DetachAgentsAsync(id, ct);
        org.Remove(position);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Ceo, "org.position_deleted", $"Position {position.TitleEn} removed", "OrgPosition", id.ToString(), ct: ct);
    }

    private async Task CheckAsync(long? id, PositionRequest request, CancellationToken ct)
    {
        if (request.DepartmentId is { } d && !await org.DepartmentExistsAsync(d, ct))
        {
            throw new BadRequestException("This department does not exist.", "departmentId");
        }
        if (request.ReportsToId is { } parent && (await org.PositionAsync(parent, ct) is null || (id is { } self && await org.WouldLoopAsync(self, parent, ct))))
        {
            throw new BadRequestException("Choose a position above this one to report to.", "reportsToId");
        }
        if (request.HolderUserId is { } holder && !await org.PersonExistsAsync(holder, ct))
        {
            throw new BadRequestException("This account does not exist.", "holderUserId");
        }
    }

    private static void Apply(OrgPosition position, PositionRequest request)
    {
        position.DepartmentId = request.DepartmentId;
        position.ReportsToId = request.ReportsToId;
        position.TitleEn = request.TitleEn.Trim();
        position.TitleAr = request.TitleAr.Trim();
        position.HolderUserId = request.HolderUserId;
        position.DutiesJson = JsonSerializer.Serialize(request.Duties?.Select(x => x.Trim()).Where(x => x.Length > 0).ToList() ?? []);
        position.SortOrder = request.SortOrder;
    }

    private async Task<PositionDto> DtoAsync(long id, CancellationToken ct) => (await org.ChartAsync(ct)).Positions.Single(p => p.Id == id);
}

public sealed class PositionRequestValidator : AbstractValidator<PositionRequest>
{
    public PositionRequestValidator()
    {
        RuleFor(x => x.TitleEn).NotEmpty().WithMessage("Write the position's title.").MaximumLength(120);
        RuleFor(x => x.TitleAr).NotEmpty().WithMessage("Write the Arabic title.").MaximumLength(120);
        RuleFor(x => x.Duties).Must(d => d is null || (d.Count <= 20 && d.All(x => x.Length <= 200))).WithMessage("At most 20 duties of 200 characters.");
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 10_000);
    }
}
