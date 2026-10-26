using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Security;

namespace SuperApp.Application.Features.Money;

/// <summary>
/// The caller's money limits: a person's refund limit is set by the CEO (auth.StaffLimits, none = 0); a call with an agent
/// key uses the agent's money limit (ai.Agents.MoneyLimit). The CEO has no limit. Over the limit → 403, so someone with
/// a higher limit (or the CEO) decides.
/// </summary>
public sealed class MoneyLimits(IMoneyStore store, ICurrentUser currentUser, Agents.ICurrentAgent currentAgent)
{
    public async Task EnsureRefundAsync(decimal amount, CancellationToken ct)
    {
        if (amount <= 0 || (currentAgent.Caller is null && currentUser.IsInRole(SystemRoles.Ceo)))
        {
            return;
        }
        var limit = currentAgent.Caller is { } agent
            ? await store.AgentMoneyLimitAsync(agent.AgentId, ct)
            : await store.RefundLimitAsync(currentUser.UserId ?? throw new UnauthorizedException(), ct);
        if (amount > limit)
        {
            throw new ForbiddenException($"This refund is over your limit of {limit:0.##} EGP. Ask someone with a higher limit or the CEO.", "over_refund_limit");
        }
    }
}
