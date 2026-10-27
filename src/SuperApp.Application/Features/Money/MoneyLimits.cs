using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Security;

namespace SuperApp.Application.Features.Money;

public enum StaffLimitKind { Refund, Credit, Payout }

/// <summary>
/// The caller's money limits: a person's refund / credit / payout limit is set by the CEO (auth.StaffLimits, none = 0); a call with an agent
/// key uses the agent's money limit (ai.Agents.MoneyLimit). The CEO has no limit. Over the limit → 403, so someone with
/// a higher limit (or the CEO) decides.
/// </summary>
public sealed class MoneyLimits(IMoneyStore store, ICurrentUser currentUser, Agents.ICurrentAgent currentAgent)
{
    public Task EnsureRefundAsync(decimal amount, CancellationToken ct) => EnsureAsync(amount, StaffLimitKind.Refund, "refund", "over_refund_limit", ct);

    /// <summary>A wallet credit to a customer (the credit limit).</summary>
    public Task EnsureCreditAsync(decimal amount, CancellationToken ct) => EnsureAsync(amount, StaffLimitKind.Credit, "credit", "over_credit_limit", ct);

    /// <summary>A balance adjustment, either way (the payout limit).</summary>
    public Task EnsureAdjustAsync(decimal amount, CancellationToken ct) => EnsureAsync(Math.Abs(amount), StaffLimitKind.Payout, "adjustment", "over_adjust_limit", ct);

    private async Task EnsureAsync(decimal amount, StaffLimitKind kind, string what, string code, CancellationToken ct)
    {
        if (amount <= 0 || (currentAgent.Caller is null && currentUser.IsInRole(SystemRoles.Ceo)))
        {
            return;
        }
        var limit = currentAgent.Caller is { } agent
            ? await store.AgentMoneyLimitAsync(agent.AgentId, ct)
            : await store.StaffLimitAsync(currentUser.UserId ?? throw new UnauthorizedException(), kind, ct);
        if (amount > limit)
        {
            throw new ForbiddenException($"This {what} is over your limit of {limit:0.##} EGP. Ask someone with a higher limit or the CEO.", code);
        }
    }
}
