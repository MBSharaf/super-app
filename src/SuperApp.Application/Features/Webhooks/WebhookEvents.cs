namespace SuperApp.Application.Features.Webhooks;

/// <summary>
/// Events sent to outside systems (n8n). Each one comes from an activity-log line, so it is saved together with that line
/// (ops.OutboxEvents) and delivered by the webhook dispatcher with retries.
/// </summary>
public static class WebhookEvents
{
    public const string BookingCreated = "booking.created";
    public const string BookingCancelled = "booking.cancelled";
    public const string ApplicationSubmitted = "application.submitted";
    public const string PitchSubmitted = "pitch.submitted";
    public const string ReviewCreated = "review.created";
    public const string TicketCreated = "support.ticket.created";
    public const string CheckInAccepted = "checkin.accepted";
    public const string OrderCreated = "order.created";

    public static readonly IReadOnlyList<string> All =
        [BookingCreated, BookingCancelled, ApplicationSubmitted, PitchSubmitted, ReviewCreated, TicketCreated, CheckInAccepted, OrderCreated];

    /// <summary>Activity-log action → webhook event (and who acted, when it matters).</summary>
    public static (string Type, string? By)? FromAudit(string action) => action switch
    {
        "booking.created" => (BookingCreated, null),
        "booking.cancelled" => (BookingCancelled, "customer"),
        "booking.cancelled_by_venue" => (BookingCancelled, "venue"),
        "application.submitted" or "application.resent" => (ApplicationSubmitted, null),
        "pitch.created" or "pitch.resubmitted" => (PitchSubmitted, null),
        "review.created" => (ReviewCreated, null),
        "ticket.opened" => (TicketCreated, null),
        "checkin.accepted" => (CheckInAccepted, null),
        "order.created" => (OrderCreated, null),
        _ => null,
    };
}
