using FluentValidation;
using SuperApp.Application.Security;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Agents;

/// <summary>Shared rules for the agent card (create and edit).</summary>
internal static class AgentRules
{
    public static bool IsPlatform(string? value) => Enum.TryParse<AgentPlatform>(value, true, out _) && !int.TryParse(value, out _);

    public static bool KnownPermissions(IReadOnlyList<string>? codes) => codes is not null && codes.All(Permissions.Exists);

    /// <summary>Money permissions wait for Phase 5 and are never given to agents from this screen yet.</summary>
    public static bool NoMoney(IReadOnlyList<string>? codes) =>
        codes is null || codes.All(c => Permissions.All.FirstOrDefault(p => p.Code == c)?.Area != "Money");
}

public sealed class CreateAgentRequestValidator : AbstractValidator<CreateAgentRequest>
{
    public CreateAgentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Give the agent a name.").MaximumLength(60);
        RuleFor(x => x.TitleEn).NotEmpty().WithMessage("Write the agent's job title.").MaximumLength(120);
        RuleFor(x => x.TitleAr).NotEmpty().WithMessage("Write the Arabic job title.").MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Model).NotEmpty().WithMessage("Choose the model.").MaximumLength(60);
        RuleFor(x => x.Platform).Must(AgentRules.IsPlatform).WithMessage("Platform: N8n, DirectApi or Mcp.");
        RuleFor(x => x.MoneyLimit).InclusiveBetween(0, 1_000_000);
        RuleFor(x => x.MonthlyBudget).InclusiveBetween(0, 100_000);
        RuleFor(x => x.Permissions).NotNull().Must(AgentRules.KnownPermissions).WithMessage("Unknown permission.")
            .Must(AgentRules.NoMoney).WithMessage("Money permissions come with the money module.");
    }
}

public sealed class UpdateAgentRequestValidator : AbstractValidator<UpdateAgentRequest>
{
    public UpdateAgentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Give the agent a name.").MaximumLength(60);
        RuleFor(x => x.TitleEn).NotEmpty().WithMessage("Write the agent's job title.").MaximumLength(120);
        RuleFor(x => x.TitleAr).NotEmpty().WithMessage("Write the Arabic job title.").MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Model).NotEmpty().WithMessage("Choose the model.").MaximumLength(60);
        RuleFor(x => x.Platform).Must(AgentRules.IsPlatform).WithMessage("Platform: N8n, DirectApi or Mcp.");
        RuleFor(x => x.MoneyLimit).InclusiveBetween(0, 1_000_000);
        RuleFor(x => x.MonthlyBudget).InclusiveBetween(0, 100_000);
    }
}

public sealed class AgentPermissionsRequestValidator : AbstractValidator<AgentPermissionsRequest>
{
    public AgentPermissionsRequestValidator()
    {
        RuleFor(x => x.Permissions).NotNull().Must(AgentRules.KnownPermissions).WithMessage("Unknown permission.")
            .Must(AgentRules.NoMoney).WithMessage("Money permissions come with the money module.");
    }
}

public sealed class AgentTasksRequestValidator : AbstractValidator<AgentTasksRequest>
{
    public AgentTasksRequestValidator()
    {
        RuleFor(x => x.Tasks).NotNull().Must(t => t.Count <= 50).WithMessage("At most 50 tasks.");
        RuleForEach(x => x.Tasks).ChildRules(t =>
        {
            t.RuleFor(x => x.Title).NotEmpty().WithMessage("Each task needs a title.").MaximumLength(120);
            t.RuleFor(x => x.Description).NotNull().MaximumLength(1000);
            t.RuleFor(x => x.Trigger).Must(v => Enum.TryParse<AgentTrigger>(v, true, out _) && !int.TryParse(v, out _)).WithMessage("Trigger: Event, Schedule or Manual.");
            t.RuleFor(x => x.EventType).NotEmpty().When(x => string.Equals(x.Trigger, "Event", StringComparison.OrdinalIgnoreCase)).WithMessage("Which event starts it?").MaximumLength(80);
            t.RuleFor(x => x.Cron).NotEmpty().When(x => string.Equals(x.Trigger, "Schedule", StringComparison.OrdinalIgnoreCase)).WithMessage("When does it run (cron)?").MaximumLength(60);
            t.RuleFor(x => x.ExpectedOutput).MaximumLength(500);
        });
    }
}

public sealed class ReasonRequestValidator : AbstractValidator<ReasonRequest>
{
    public ReasonRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Write the reason.").MaximumLength(300);
    }
}

public sealed class CreateApiKeyRequestValidator : AbstractValidator<CreateApiKeyRequest>
{
    public CreateApiKeyRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name the key (for example \"n8n production\").").MaximumLength(80);
        RuleFor(x => x.RateLimitPerMinute).InclusiveBetween(1, 6000).When(x => x.RateLimitPerMinute is not null);
        RuleFor(x => x.ExpiresAt).Must(d => d > DateTime.UtcNow).When(x => x.ExpiresAt is not null).WithMessage("The expiry date must be in the future.");
    }
}

public sealed class StartRunRequestValidator : AbstractValidator<StartRunRequest>
{
    public StartRunRequestValidator()
    {
        RuleFor(x => x.Summary).MaximumLength(1000);
    }
}

public sealed class FinishRunRequestValidator : AbstractValidator<FinishRunRequest>
{
    public FinishRunRequestValidator()
    {
        RuleFor(x => x.Result).Must(v => Enum.TryParse<AgentRunResult>(v, true, out _) && !int.TryParse(v, out _))
            .WithMessage("Result: Running, Done, WaitingForApproval, Failed or Cancelled.");
        RuleFor(x => x.Summary).MaximumLength(1000);
        RuleFor(x => x.InputTokens).InclusiveBetween(0, 100_000_000);
        RuleFor(x => x.OutputTokens).InclusiveBetween(0, 100_000_000);
        RuleFor(x => x.CostUsd).InclusiveBetween(0, 10_000);
    }
}
