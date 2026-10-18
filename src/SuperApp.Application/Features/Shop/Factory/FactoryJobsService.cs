using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Shop.Catalog;
using SuperApp.Application.Features.Shop.Orders;
using SuperApp.Application.Features.Shop.Shipping;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Factory;

/// <summary>
/// The production board (API §4.6, FR-FAC-01..05): new → printing → quality check → packing → ready. A failed check
/// goes back to printing (a reprint at the factory's cost). When every printed item of an order is ready, the courier is
/// booked automatically. Every step is signed with the person's name.
/// </summary>
public interface IFactoryJobsService
{
    Task<FactoryOverviewDto> OverviewAsync(CancellationToken ct);

    Task<PagedResult<FactoryJobCardDto>> ListAsync(FactoryJobsQuery query, CancellationToken ct);

    Task<FactoryJobDto> GetAsync(string code, CancellationToken ct);

    Task<FactoryJobDto> AdvanceAsync(string code, AdvanceRequest request, CancellationToken ct);

    Task<FactoryJobDto> QcAsync(string code, QcRequest request, CancellationToken ct);

    Task<FactoryJobDto> ProblemAsync(string code, ProblemRequest request, CancellationToken ct);

    Task<FactoryJobDto> AssignAsync(string code, AssignRequest request, CancellationToken ct);

    Task<FactoryJobDto> ScanAsync(ScanRequest request, CancellationToken ct);

    Task<ArtImage> ArtAsync(string code, string side, CancellationToken ct);
}

public sealed class FactoryJobsService(
    IFactoryAccess access,
    IFactoryRepository factories,
    IArtworkStore artwork,
    ShipmentBooker courier,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IClock clock,
    IValidator<ProblemRequest> problemRules) : IFactoryJobsService
{
    /// <summary>The step a job takes from each stage and the action that may take it.</summary>
    private static readonly Dictionary<JobStage, (JobStage To, string Action)> Next = new()
    {
        [JobStage.New] = (JobStage.Printing, "print"),
        [JobStage.Printing] = (JobStage.QualityCheck, "print"),
        [JobStage.QualityCheck] = (JobStage.Packing, "qc"),
        [JobStage.Packing] = (JobStage.Ready, "pack"),
    };

    public async Task<FactoryOverviewDto> OverviewAsync(CancellationToken ct) => await factories.OverviewAsync((await access.CurrentAsync(ct)).FactoryId, clock.UtcNow, ct);

    public async Task<PagedResult<FactoryJobCardDto>> ListAsync(FactoryJobsQuery query, CancellationToken ct)
    {
        var me = await access.CurrentAsync(ct);
        if (query.Page < 1 || query.PageSize is < 1 or > 200)
        {
            throw new BadRequestException("Page from 1, page size 1–200.", "bad_page");
        }
        return await factories.JobsAsync(me.FactoryId, query, me.StaffId, clock.UtcNow, ct);
    }

    public async Task<FactoryJobDto> GetAsync(string code, CancellationToken ct) =>
        await factories.JobDtoAsync((await access.CurrentAsync(ct)).FactoryId, code, clock.UtcNow, ct) ?? throw new NotFoundException("Job not found.");

    public async Task<FactoryJobDto> AdvanceAsync(string code, AdvanceRequest request, CancellationToken ct)
    {
        var me = await access.CurrentAsync(ct);
        var job = await factories.JobAsync(me.FactoryId, code, ct) ?? throw new NotFoundException("Job not found.");
        if (!Enum.TryParse<JobStage>(request.ToStage, true, out var to) || !Next.TryGetValue(job.Stage, out var step) || step.To != to)
        {
            throw new ConflictException($"This job is at {job.Stage}; that step isn’t possible now.", "wrong_stage");
        }
        me.Need(step.Action);
        Move(job, to, me, request.Note);
        if (to == JobStage.Printing && job.AssignedStaffId is null && me.Role == FactoryRole.Printer)
        {
            job.AssignedStaffId = me.StaffId;
        }
        await unitOfWork.SaveChangesAsync(ct);
        if (to == JobStage.Ready)
        {
            await ShipWhenAllReadyAsync(me, job, ct);
        }
        return await GetAsync(code, ct);
    }

    public async Task<FactoryJobDto> QcAsync(string code, QcRequest request, CancellationToken ct)
    {
        var me = (await access.CurrentAsync(ct)).Need("qc");
        var job = await factories.JobAsync(me.FactoryId, code, ct) ?? throw new NotFoundException("Job not found.");
        if (job.Stage != JobStage.QualityCheck)
        {
            throw new ConflictException("This job is not waiting for the quality check.", "wrong_stage");
        }
        if (!request.Pass && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new FluentValidation.ValidationException([new FluentValidation.Results.ValidationFailure("reason", "Say what is wrong with the print.")]);
        }
        Move(job, request.Pass ? JobStage.Packing : JobStage.Printing, me, request.Pass ? "Passed quality check" : $"QC failed: {request.Reason!.Trim()}");
        job.ReprintCount += request.Pass ? (byte)0 : (byte)1;
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(code, ct);
    }

    public async Task<FactoryJobDto> ProblemAsync(string code, ProblemRequest request, CancellationToken ct)
    {
        await problemRules.ValidateAndThrowAsync(request, ct);
        var me = await access.CurrentAsync(ct);
        var job = await factories.JobAsync(me.FactoryId, code, ct) ?? throw new NotFoundException("Job not found.");
        factories.AddProblem(new JobProblem { JobId = job.Id, ReportedByStaffId = me.StaffId, Kind = Enum.Parse<JobProblemKind>(request.Kind, true), Detail = request.Detail.Trim() });
        await unitOfWork.SaveChangesAsync(ct);
        var message = new NotificationMessage(NotificationCategory.Orders, $"Factory problem on {job.Code}", $"{request.Kind}: {request.Detail.Trim()} · {me.Name}", "#/factory", NotificationTone.Warning);
        await notifier.NotifyUserAsync(me.OwnerUserId, message, ct);
        await notifier.NotifyStaffAsync("factories.manage", message with { Link = "#/admin/factories" }, ct);
        return await GetAsync(code, ct);
    }

    public async Task<FactoryJobDto> AssignAsync(string code, AssignRequest request, CancellationToken ct)
    {
        var me = (await access.CurrentAsync(ct)).Need("assign");
        var job = await factories.JobAsync(me.FactoryId, code, ct) ?? throw new NotFoundException("Job not found.");
        if (request.StaffId is { } staffId && await factories.StaffAsync(me.FactoryId, staffId, ct) is not { IsActive: true })
        {
            throw new NotFoundException("This person is not in the team.");
        }
        job.AssignedStaffId = request.StaffId;
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(code, ct);
    }

    /// <summary>A scanner or camera reads the ticket's QR (the job code, or a link that ends with it).</summary>
    public Task<FactoryJobDto> ScanAsync(ScanRequest request, CancellationToken ct)
    {
        var text = (request.Code ?? "").Trim();
        var code = text.Contains('/', StringComparison.Ordinal) ? text[(text.LastIndexOf('/') + 1)..] : text;
        return string.IsNullOrEmpty(code) || code.Length > 16 ? throw new NotFoundException("Job not found.") : GetAsync(code.ToUpperInvariant(), ct);
    }

    public async Task<ArtImage> ArtAsync(string code, string side, CancellationToken ct)
    {
        var me = await access.CurrentAsync(ct);
        var fileId = await factories.ArtFileAsync(me.FactoryId, code, side == "back" ? 1 : 0, ct) ?? throw new NotFoundException("No art on this side.");
        return await artwork.OriginalAsync(fileId, ct) ?? throw new NotFoundException("No art on this side.");
    }

    private void Move(Job job, JobStage to, FactoryScope me, string? note)
    {
        job.Events.Add(new JobEvent { FromStage = (byte)job.Stage, ToStage = (byte)to, StaffId = me.StaffId, Note = Note(note, me), At = clock.UtcNow });
        job.Stage = to;
        job.OrderItem.Status = to switch
        {
            JobStage.Printing or JobStage.QualityCheck or JobStage.Packing => OrderItemStatus.Printing,
            JobStage.Ready => OrderItemStatus.Packed,
            _ => job.OrderItem.Status,
        };
        OrderProgress.Update(job.OrderItem.Order);
    }

    private static string? Note(string? note, FactoryScope me)
    {
        var text = me.StaffId is null ? $"{me.Name} (owner)" + (string.IsNullOrWhiteSpace(note) ? "" : $": {note.Trim()}") : note?.Trim();
        return text is { Length: > 300 } ? text[..300] : text;
    }

    /// <summary>FR-FAC-05: when all printed items of the order are packed, one parcel is booked with the courier.</summary>
    private async Task ShipWhenAllReadyAsync(FactoryScope me, Job job, CancellationToken ct)
    {
        var order = job.OrderItem.Order;
        var jobs = (await factories.OrderJobsAsync(order.Id, ct)).Where(j => j.Stage != JobStage.Cancelled).ToList();
        if (jobs.Any(j => j.Stage < JobStage.Ready) || await factories.HasFactoryShipmentAsync(order.Id, ct))
        {
            return;
        }
        var items = order.Items.Where(i => i.Kind == OrderItemKind.PrintedDesign && i.Status != OrderItemStatus.Cancelled).ToList();
        var factory = await factories.FactoryAsync(me.FactoryId, ct);
        var name = job.IsSample ? me.FactoryName : await factories.CustomerNameAsync(order.CustomerId, ct);
        await courier.BookAsync(order, null, me.FactoryId, items.Sum(i => i.UnitPrice * i.Qty), items.Sum(i => i.Qty), name, (me.FactoryName, factory?.Address ?? ""), ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}

public sealed class ProblemRequestValidator : AbstractValidator<ProblemRequest>
{
    public ProblemRequestValidator()
    {
        RuleFor(x => x.Kind).IsEnumName(typeof(JobProblemKind), caseSensitive: false).WithMessage("OutOfBlanks, MachineDown, BadArtwork, Damaged or Other.");
        RuleFor(x => x.Detail).NotEmpty().Length(3, 400);
    }
}
