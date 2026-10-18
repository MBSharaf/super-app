using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Shop.Orders;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Factory;

/// <summary>
/// Admins over all factories (FR-FAC-09): late jobs and problems, reassign to another factory, cancel, send for reprint after a
/// complaint (optional deduction from the factory), resolve problems, pause or suspend a factory.
/// </summary>
public interface IAdminFactoryService
{
    Task<IReadOnlyList<AdminFactoryDto>> FactoriesAsync(CancellationToken ct);

    Task<PagedResult<FactoryJobCardDto>> JobsAsync(AdminJobsQuery query, CancellationToken ct);

    Task ReassignAsync(string code, ReassignRequest request, CancellationToken ct);

    Task CancelAsync(string code, ResolveRequest request, CancellationToken ct);

    Task ReprintAsync(string code, ReprintRequest request, CancellationToken ct);

    Task ResolveProblemAsync(long id, ResolveRequest request, CancellationToken ct);

    /// <summary>status = Active, PausedByAdmin or Suspended.</summary>
    Task SetStatusAsync(long factoryId, string status, CancellationToken ct);
}

public sealed class AdminFactoryService(
    IFactoryAdminRepository admin,
    IFactoryRepository factories,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IClock clock) : IAdminFactoryService
{
    public Task<IReadOnlyList<AdminFactoryDto>> FactoriesAsync(CancellationToken ct) => admin.FactoriesAsync(clock.UtcNow, ct);

    public Task<PagedResult<FactoryJobCardDto>> JobsAsync(AdminJobsQuery query, CancellationToken ct) =>
        admin.JobsAsync(query with { Page = Math.Max(1, query.Page), PageSize = query.PageSize is < 1 or > 200 ? 50 : query.PageSize }, clock.UtcNow, ct);

    public async Task ReassignAsync(string code, ReassignRequest request, CancellationToken ct)
    {
        var job = await OpenJobAsync(code, ct);
        var target = await factories.FactoryAsync(request.FactoryId, ct);
        if (target is not { Status: FactoryStatus.Active } || target.Id == job.FactoryId)
        {
            throw new BusinessRuleException("Choose another active factory.", "bad_factory");
        }
        job.Events.Add(new JobEvent { FromStage = (byte)job.Stage, ToStage = (byte)JobStage.New, Note = $"Moved to {target.Name} by Super App", At = clock.UtcNow });
        (job.FactoryId, job.Stage, job.AssignedStaffId, job.DueAt) = (target.Id, JobStage.New, null, clock.UtcNow.AddHours(target.SlaHours));
        job.OrderItem.Status = OrderItemStatus.Received;
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyUserAsync(target.OwnerUserId, new NotificationMessage(NotificationCategory.Orders, "A job was moved to you", job.Code, "#/factory"), ct);
        await audit.LogAsync(AuditSystem.Admin, "job.reassigned", $"Moved {job.Code} to {target.Name}", "Job", job.Code, ct: ct);
    }

    public async Task CancelAsync(string code, ResolveRequest request, CancellationToken ct)
    {
        var reason = Reason(request.Resolution);
        var job = await OpenJobAsync(code, ct);
        job.Events.Add(new JobEvent { FromStage = (byte)job.Stage, ToStage = (byte)JobStage.Cancelled, Note = $"Cancelled by Super App: {reason}", At = clock.UtcNow });
        job.Stage = JobStage.Cancelled;
        job.OrderItem.Status = OrderItemStatus.Cancelled;
        var order = await admin.OrderOfJobAsync(job, ct);
        OrderProgress.Update(order);
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyUserAsync(order.CustomerId, new NotificationMessage(NotificationCategory.Orders, $"An item of {order.Code} was cancelled",
            $"{job.OrderItem.Title}: {reason}. You won’t pay for it.", $"#/order/{order.Code}", NotificationTone.Warning), ct);
        await audit.LogAsync(AuditSystem.Admin, "job.cancelled", $"Cancelled {job.Code}: {reason}", "Job", job.Code, ct: ct);
    }

    public async Task ReprintAsync(string code, ReprintRequest request, CancellationToken ct)
    {
        var reason = Reason(request.Reason);
        if (request.Deduction is < 0 or > 100_000)
        {
            throw new BadRequestException("The deduction is between 0 and 100,000 EGP.", "bad_deduction");
        }
        var job = await admin.JobAsync(code, ct) ?? throw new NotFoundException("Job not found.");
        if (job.Stage == JobStage.Cancelled)
        {
            throw new BusinessRuleException("A cancelled job can’t be reprinted.", "wrong_stage");
        }
        job.Events.Add(new JobEvent { FromStage = (byte)job.Stage, ToStage = (byte)JobStage.New, Note = $"Reprint after a complaint: {reason}", At = clock.UtcNow });
        (job.Stage, job.ReprintCount, job.DeductionAmount, job.DueAt) = (JobStage.New, (byte)(job.ReprintCount + 1), job.DeductionAmount + request.Deduction, clock.UtcNow.AddHours(48));
        job.OrderItem.Status = OrderItemStatus.Printing;
        await unitOfWork.SaveChangesAsync(ct);
        var owner = (await factories.FactoryAsync(job.FactoryId, ct))!.OwnerUserId;
        await notifier.NotifyUserAsync(owner, new NotificationMessage(NotificationCategory.Orders, $"Reprint {job.Code}",
            reason + (request.Deduction > 0 ? $" · {request.Deduction:0} EGP deducted" : ""), "#/factory", NotificationTone.Warning), ct);
        await audit.LogAsync(AuditSystem.Admin, "job.reprint", $"Reprint of {job.Code}: {reason}, deduction {request.Deduction:0}", "Job", job.Code, ct: ct);
    }

    public async Task ResolveProblemAsync(long id, ResolveRequest request, CancellationToken ct)
    {
        var resolution = Reason(request.Resolution);
        var problem = await admin.ProblemAsync(id, ct) ?? throw new NotFoundException("Problem not found.");
        if (problem.Status == JobProblemStatus.Resolved)
        {
            throw new BusinessRuleException("This problem is already resolved.", "wrong_status");
        }
        (problem.Status, problem.Resolution, problem.ResolvedById) = (JobProblemStatus.Resolved, resolution, currentUser.UserId);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task SetStatusAsync(long factoryId, string status, CancellationToken ct)
    {
        if (!Enum.TryParse<FactoryStatus>(status, true, out var to) || !Enum.IsDefined(to))
        {
            throw new BadRequestException("Status is Active, PausedByAdmin or Suspended.", "bad_status");
        }
        var factory = await factories.FactoryAsync(factoryId, ct) ?? throw new NotFoundException("Factory not found.");
        factory.Status = to;
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyUserAsync(factory.OwnerUserId, new NotificationMessage(NotificationCategory.System, $"Your factory is now {to}", null, "#/factory",
            to == FactoryStatus.Active ? NotificationTone.Success : NotificationTone.Warning), ct);
        await audit.LogAsync(AuditSystem.Admin, "factory.status", $"{factory.Name} → {to}", "Factory", factoryId.ToString(System.Globalization.CultureInfo.InvariantCulture), ct: ct);
    }

    private async Task<Job> OpenJobAsync(string code, CancellationToken ct)
    {
        var job = await admin.JobAsync(code, ct) ?? throw new NotFoundException("Job not found.");
        return job.Stage < JobStage.Ready ? job : throw new BusinessRuleException("This job is already packed or shipped.", "wrong_stage");
    }

    private static string Reason(string? text) =>
        string.IsNullOrWhiteSpace(text) || text.Trim().Length is < 3 or > 300
            ? throw new FluentValidation.ValidationException([new FluentValidation.Results.ValidationFailure("reason", "Give a reason (3–300 characters).")])
            : text.Trim();
}
