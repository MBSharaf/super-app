using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Fantasy;

/// <summary>
/// The Fantasy Pass (FR-FAN-08): 30 days of prize leagues for one league (50 EGP) or both (80 EGP), prices from the
/// setting <c>fantasy.passPrice</c>, seller-level discount (Pro 50%, Elite free). Paid straight away until the payment
/// gateway arrives (Phase 5: the payment and its receipt PDF are linked through <c>FantasyPass.PaymentId</c>).
/// </summary>
public interface IFantasyPassService
{
    Task<PassStatusDto> StatusAsync(CancellationToken ct);

    Task<PassStatusDto> BuyAsync(BuyPassRequest request, string? idempotencyKey, CancellationToken ct);
}

public sealed class FantasyPassService(
    IFantasyPassRepository passes,
    IUnitOfWork unitOfWork,
    IFeatureFlags flags,
    INotifier notifier,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<BuyPassRequest> rules) : IFantasyPassService
{
    public const int PassDays = 30;
    private static readonly TimeSpan RepeatWindow = TimeSpan.FromMinutes(10);

    private Guid UserId => currentUser.UserId ?? throw new UnauthorizedException();

    public async Task<PassStatusDto> StatusAsync(CancellationToken ct)
    {
        var competitions = await passes.CompetitionsAsync(ct);
        var active = await passes.ActiveAsync(UserId, clock.UtcNow, ct);
        var covers = competitions.ToDictionary(c => c.Key.ToLowerInvariant(),
            c => active.Where(p => p.CompetitionId is null || p.CompetitionId == c.Id).Select(p => (DateTime?)p.EndsAt).DefaultIfEmpty(null).Max());
        var list = active.Select(p => new ActivePassDto(PlanOf(p.CompetitionId, competitions), p.StartsAt, p.EndsAt)).ToList();
        return new PassStatusDto(list, covers, await PlansAsync(competitions, ct));
    }

    public async Task<PassStatusDto> BuyAsync(BuyPassRequest request, string? idempotencyKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new BadRequestException("Send an Idempotency-Key header with the purchase.", "idempotency_key");
        }
        await rules.ValidateAndThrowAsync(request, ct);
        await flags.EnsureOnAsync(KillSwitches.Fantasy, ct);
        var competitions = await passes.CompetitionsAsync(ct);
        var plan = request.Plan.ToLowerInvariant();
        var competitionId = CompetitionOf(plan, competitions);
        if (await passes.RecentAsync(UserId, competitionId, clock.UtcNow - RepeatWindow, ct) is null)
        {
            var price = (await PlansAsync(competitions, ct)).Single(p => p.Plan == plan).Price;
            var pass = await NewPassAsync(competitionId, price, ct);
            passes.Add(pass);
            await unitOfWork.SaveChangesAsync(ct);
            await notifier.NotifyUserAsync(UserId, new NotificationMessage(NotificationCategory.Fantasy, "Fantasy Pass active",
                $"Prize leagues open until {pass.EndsAt:d MMM yyyy}.", "#/fantasy/leagues", NotificationTone.Success), ct);
            await audit.LogAsync(AuditSystem.Site, "fantasy.pass.bought", $"Fantasy Pass ({plan}) for {price:0.##} EGP", "FantasyPass", pass.Id.ToString(), ct: ct);
        }
        return await StatusAsync(ct);
    }

    /// <summary>A renewal starts when the current pass of the same plan ends, so no days are lost.</summary>
    private async Task<FantasyPass> NewPassAsync(int? competitionId, decimal price, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var current = (await passes.ActiveAsync(UserId, now, ct)).Where(p => p.CompetitionId == competitionId).Select(p => p.EndsAt).DefaultIfEmpty(now).Max();
        var starts = current > now ? current : now;
        return new FantasyPass { UserId = UserId, CompetitionId = competitionId, Price = price, StartsAt = starts, EndsAt = starts.AddDays(PassDays) };
    }

    private async Task<IReadOnlyList<PassPlanDto>> PlansAsync(IReadOnlyList<(int Id, string Key)> competitions, CancellationToken ct)
    {
        var (one, both) = await passes.PricesAsync(ct);
        var discount = Math.Clamp(await passes.SellerDiscountAsync(UserId, ct), 0, 1);
        PassPlanDto Plan(string key, decimal list) => new(key, list, Math.Round(list * (1 - discount), 2), discount);
        return [.. competitions.Select(c => Plan(c.Key.ToLowerInvariant(), one)), Plan("both", both)];
    }

    /// <summary>null for "both", else the league's id; anything else → 400 unknown_plan.</summary>
    private static int? CompetitionOf(string plan, IReadOnlyList<(int Id, string Key)> competitions)
    {
        if (plan == "both")
        {
            return null;
        }
        var match = competitions.FirstOrDefault(c => c.Key.Equals(plan, StringComparison.OrdinalIgnoreCase));
        return match.Id > 0 ? match.Id : throw new BadRequestException("Choose egy, epl or both.", "unknown_plan");
    }

    private static string PlanOf(int? competitionId, IReadOnlyList<(int Id, string Key)> competitions) =>
        competitionId is null ? "both" : competitions.First(c => c.Id == competitionId).Key.ToLowerInvariant();
}

public sealed class BuyPassRequestValidator : AbstractValidator<BuyPassRequest>
{
    public BuyPassRequestValidator()
    {
        RuleFor(x => x.Plan).NotEmpty().WithMessage("Choose a plan.").MaximumLength(10);
    }
}
