using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Domain.Entities.Support;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Cancellations;

/// <summary>
/// "Every action notifies everyone concerned" for cancellation requests: the customer and the support team are told,
/// and each step is an activity line that becomes the n8n webhook (cancellation.requested — the AI agent's trigger —,
/// cancellation.approved, cancellation.rejected).
/// </summary>
public sealed class CancellationNotices(INotifier notifier, IAuditLogger audit, ICustomerContacts contacts)
{
    public async Task RequestedAsync(CancellationRequest r, CancellationSubject subject, CancellationToken ct)
    {
        var hours = CancellationsService.ReplyHours;
        await notifier.NotifyUserAsync(r.CustomerId, new NotificationMessage(NotificationCategory.Support, $"Cancellation request {r.Code} sent",
            $"{r.Label} · we’ll reply within {hours} hours. It stays active until then.", subject.Link, NotificationTone.Info), ct);
        await notifier.NotifyStaffAsync("support.read", new NotificationMessage(NotificationCategory.Support, $"Cancellation request {r.Code}",
            $"{r.Kind}: {r.Label} · paid {r.PaidOnline:0} EGP · reply within {hours} hours", $"#/admin/cancellations?id={r.Code}", NotificationTone.Warning), ct);
        await audit.LogAsync(AuditSystem.Site, "cancellation.requested", $"Cancellation request {r.Code}: {r.Kind} {r.Reference}", "CancellationRequest", r.Code,
            await DetailsAsync(r, subject.Link, subject.Policy, ct), ct);
    }

    public async Task DecidedAsync(CancellationRequest r, CancellationToken ct)
    {
        var approved = r.Status == CancellationStatus.Approved;
        var body = approved
            ? $"{r.Label} is cancelled.{(r.Refunded > 0 ? $" {r.Refunded:0} EGP is back in your wallet." : "")}{(r.DecisionNote is { } n ? $" {n}" : "")}"
            : $"{r.DecisionNote} {r.Label} stays active.";
        await notifier.NotifyUserAsync(r.CustomerId, new NotificationMessage(NotificationCategory.Support,
            approved ? $"Cancellation {r.Code} approved" : $"Cancellation {r.Code} not approved", body, null, approved ? NotificationTone.Success : NotificationTone.Warning), ct);
        await audit.LogAsync(AuditSystem.Admin, approved ? "cancellation.approved" : "cancellation.rejected",
            $"Cancellation request {r.Code} {(approved ? "approved" : "refused")}", "CancellationRequest", r.Code, await DetailsAsync(r, null, null, ct), ct);
    }

    /// <summary>What the activity line (and the webhook for n8n) carries: the request and the customer's contact.</summary>
    private async Task<object> DetailsAsync(CancellationRequest r, string? link, string? policy, CancellationToken ct)
    {
        var customer = await contacts.GetAsync(r.CustomerId, ct);
        return new
        {
            RequestId = r.Code, Status = r.Status.ToString(), Type = r.Kind.ToString(), ReferenceId = r.Reference, Item = r.Label, Link = link,
            PaidOnline = r.PaidOnline, RefundIfApproved = r.RefundIfApproved, r.Refunded, Currency = "EGP", CancelPolicy = policy, r.Reason,
            ReplyBy = r.DueAt, ReplyWithinHours = CancellationsService.ReplyHours, Channel = "chatbot", Decision = r.DecisionNote,
            AdminLink = $"#/admin/cancellations?id={r.Code}",
            Customer = new { Id = r.CustomerId, customer.Name, customer.Email, customer.Phone },
        };
    }
}
