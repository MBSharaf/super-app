using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Webhooks;

public sealed record WebhookDto(int Id, string Name, string Url, IReadOnlyList<string> Events, bool IsEnabled, int FailureCount, DateTime CreatedAt);

/// <summary>The secret signs every delivery; it is shown once, here.</summary>
public sealed record WebhookCreatedDto(WebhookDto Webhook, string Secret);

/// <summary>POST /ceo/webhooks — <c>Events</c>: names from <see cref="WebhookEvents.All"/>, or "*" for every event.</summary>
public sealed record WebhookRequest(string Name, string Url, IReadOnlyList<string> Events);

public sealed record WebhookUpdateRequest(IReadOnlyList<string> Events, bool IsEnabled);

public sealed record WebhookDeliveryDto(long Id, string EventType, Guid EventId, int Attempt, int? StatusCode, int? DurationMs, string? Error, DateTime At);

/// <summary>The answer of a test delivery.</summary>
public sealed record WebhookTestDto(bool Ok, int? StatusCode, int DurationMs, string? Error);

public interface IWebhookStore
{
    Task<IReadOnlyList<WebhookDto>> ListAsync(CancellationToken ct);

    Task<WebhookSubscription?> FindAsync(int id, CancellationToken ct);

    void Add(WebhookSubscription subscription);

    void Remove(WebhookSubscription subscription);

    Task<IReadOnlyList<WebhookDeliveryDto>> DeliveriesAsync(int id, CancellationToken ct);
}

/// <summary>Signs and sends one delivery; the secret is kept encrypted with the server's keys.</summary>
public interface IWebhookSender
{
    /// <summary>A new signing secret: the random salt kept in the database and the secret shown once.</summary>
    (byte[] Salt, string Secret) NewSecret();

    Task<WebhookTestDto> SendAsync(WebhookSubscription subscription, Guid eventId, string type, DateTime occurredAt, string payloadJson, CancellationToken ct);
}

/// <summary>
/// Webhooks for outside systems such as n8n (CEO only): add a URL with the events it wants, get its signing secret once,
/// turn it on or off, test it, and see its deliveries.
/// </summary>
public interface IWebhooksService
{
    Task<IReadOnlyList<WebhookDto>> ListAsync(CancellationToken ct);

    Task<WebhookCreatedDto> CreateAsync(WebhookRequest request, CancellationToken ct);

    Task<WebhookDto> UpdateAsync(int id, WebhookUpdateRequest request, CancellationToken ct);

    Task DeleteAsync(int id, CancellationToken ct);

    Task<WebhookTestDto> TestAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<WebhookDeliveryDto>> DeliveriesAsync(int id, CancellationToken ct);
}

public sealed class WebhooksService(
    IWebhookStore store,
    IWebhookSender sender,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock,
    IValidator<WebhookRequest> rules,
    IValidator<WebhookUpdateRequest> updateRules) : IWebhooksService
{
    public Task<IReadOnlyList<WebhookDto>> ListAsync(CancellationToken ct) => store.ListAsync(ct);

    public async Task<WebhookCreatedDto> CreateAsync(WebhookRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var (salt, secret) = sender.NewSecret();
        var sub = new WebhookSubscription { Name = request.Name.Trim(), Url = request.Url.Trim(), Events = System.Text.Json.JsonSerializer.Serialize(request.Events), SecretHash = salt, IsEnabled = true };
        store.Add(sub);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Ceo, "webhook.created", $"Webhook {sub.Name} → {sub.Url}", "Webhook", sub.Id.ToString(), ct: ct);
        return new WebhookCreatedDto((await store.ListAsync(ct)).Single(w => w.Id == sub.Id), secret);
    }

    public async Task<WebhookDto> UpdateAsync(int id, WebhookUpdateRequest request, CancellationToken ct)
    {
        await updateRules.ValidateAndThrowAsync(request, ct);
        var sub = await store.FindAsync(id, ct) ?? throw new NotFoundException("Webhook not found.");
        sub.Events = System.Text.Json.JsonSerializer.Serialize(request.Events);
        sub.IsEnabled = request.IsEnabled;
        if (request.IsEnabled)
        {
            sub.FailureCount = 0;
        }
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Ceo, "webhook.updated", $"Webhook {sub.Name} {(sub.IsEnabled ? "on" : "off")}", "Webhook", id.ToString(), ct: ct);
        return (await store.ListAsync(ct)).Single(w => w.Id == id);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var sub = await store.FindAsync(id, ct) ?? throw new NotFoundException("Webhook not found.");
        store.Remove(sub);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Ceo, "webhook.deleted", $"Webhook {sub.Name} deleted", "Webhook", id.ToString(), ct: ct);
    }

    public async Task<WebhookTestDto> TestAsync(int id, CancellationToken ct)
    {
        var sub = await store.FindAsync(id, ct) ?? throw new NotFoundException("Webhook not found.");
        var payload = System.Text.Json.JsonSerializer.Serialize(new { message = "Test from Super App — your webhook works." });
        return await sender.SendAsync(sub, Guid.NewGuid(), "webhook.test", clock.UtcNow, payload, ct);
    }

    public Task<IReadOnlyList<WebhookDeliveryDto>> DeliveriesAsync(int id, CancellationToken ct) => store.DeliveriesAsync(id, ct);
}

public sealed class WebhookRequestValidator : AbstractValidator<WebhookRequest>
{
    public WebhookRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Url).NotEmpty().MaximumLength(500)
            .Must(u => u.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || u.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase))
            .Must(u => Uri.TryCreate(u, UriKind.Absolute, out _))
            .WithMessage("Enter the full webhook URL: https://… (or http://localhost… on this computer).");
        RuleFor(x => x.Events).NotEmpty().Must(e => e.All(v => v == "*" || WebhookEvents.All.Contains(v)))
            .WithMessage($"Events: * or {string.Join(", ", WebhookEvents.All)}.");
    }
}

public sealed class WebhookUpdateRequestValidator : AbstractValidator<WebhookUpdateRequest>
{
    public WebhookUpdateRequestValidator()
    {
        RuleFor(x => x.Events).NotEmpty().Must(e => e.All(v => v == "*" || WebhookEvents.All.Contains(v)))
            .WithMessage($"Events: * or {string.Join(", ", WebhookEvents.All)}.");
    }
}
