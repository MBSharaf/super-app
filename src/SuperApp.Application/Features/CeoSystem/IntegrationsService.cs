using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.CeoSystem;

/// <summary>
/// The integrations registry (API §8 "Integrations"): status, mode, usage and last error of every outside service,
/// switching mode / primary provider, and a health check. Secrets stay in the server's <c>.env</c> (never in the database
/// or the API); the registry only shows whether each one is set.
/// </summary>
public interface IIntegrationsService
{
    Task<IReadOnlyList<IntegrationDto>> ListAsync(CancellationToken ct);

    Task<IntegrationDto> GetAsync(string key, CancellationToken ct);

    Task<IntegrationDto> UpdateAsync(string key, UpdateIntegrationRequest request, CancellationToken ct);

    Task<IntegrationDto> TestAsync(string key, CancellationToken ct);
}

public sealed class IntegrationsService(
    IIntegrationRepository integrations,
    ISecretPresence secrets,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock) : IIntegrationsService
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public Task<IReadOnlyList<IntegrationDto>> ListAsync(CancellationToken ct) => integrations.ListAsync(Today, ct);

    public async Task<IntegrationDto> GetAsync(string key, CancellationToken ct) =>
        await integrations.DetailAsync(key, Today, ct) ?? throw new NotFoundException("This integration does not exist.");

    public async Task<IntegrationDto> UpdateAsync(string key, UpdateIntegrationRequest request, CancellationToken ct)
    {
        var integration = await EntityAsync(key, ct);
        var mode = Enum.Parse<IntegrationMode>(request.Mode, true);
        if (mode != IntegrationMode.Simulated && integration.SecretName is not null && !secrets.IsSet(integration.SecretName))
        {
            throw new BusinessRuleException($"Put {integration.SecretName} in the server's .env first.", "secret_missing");
        }
        (integration.Mode, integration.IsEnabled, integration.IsPrimary) = (mode, request.IsEnabled, request.IsPrimary && request.IsEnabled);
        if (request.Config is { ValueKind: System.Text.Json.JsonValueKind.Object } config)
        {
            integration.ConfigJson = config.GetRawText();
        }
        if (integration.IsPrimary)
        {
            await integrations.ClearPrimaryAsync(integration.Category, integration.Id, ct);
        }
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Ceo, "integration.updated", $"{key}: {mode}, {(request.IsEnabled ? "on" : "off")}{(integration.IsPrimary ? ", primary" : "")}",
            "Integration", key, new { request.Mode, request.IsEnabled, request.IsPrimary }, ct);
        return await GetAsync(key, ct);
    }

    /// <summary>Simulated → healthy; a live mode without its secret → down. Real provider pings come with each adapter.</summary>
    public async Task<IntegrationDto> TestAsync(string key, CancellationToken ct)
    {
        var integration = await EntityAsync(key, ct);
        var missing = integration.Mode != IntegrationMode.Simulated && integration.SecretName is not null && !secrets.IsSet(integration.SecretName);
        integration.LastCallAt = clock.UtcNow;
        integration.Status = !integration.IsEnabled ? IntegrationStatus.Unknown : missing ? IntegrationStatus.Down : IntegrationStatus.Healthy;
        if (missing)
        {
            (integration.LastErrorAt, integration.LastError) = (clock.UtcNow, $"{integration.SecretName} is not set on the server.");
        }
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Ceo, "integration.tested", $"{key}: {integration.Status}", "Integration", key, ct: ct);
        return await GetAsync(key, ct);
    }

    private async Task<Integration> EntityAsync(string key, CancellationToken ct) =>
        await integrations.GetAsync(key, ct) ?? throw new NotFoundException("This integration does not exist.");
}

public sealed class UpdateIntegrationRequestValidator : AbstractValidator<UpdateIntegrationRequest>
{
    public UpdateIntegrationRequestValidator()
    {
        RuleFor(x => x.Mode).Must(m => Enum.TryParse<IntegrationMode>(m, true, out _) && !int.TryParse(m, out _)).WithMessage("Mode: Simulated, Test or Live.");
        RuleFor(x => x.Config).Must(c => c is null || c.Value.ValueKind is System.Text.Json.JsonValueKind.Object or System.Text.Json.JsonValueKind.Null)
            .WithMessage("Config must be a JSON object.")
            .Must(c => c is null || c.Value.GetRawText().Length <= 4000).WithMessage("Config is too long.");
    }
}
