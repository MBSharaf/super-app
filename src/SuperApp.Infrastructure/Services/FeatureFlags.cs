using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Services;

/// <summary>
/// Reads the CEO's switches from ops.FeatureFlags. A missing switch counts as on. Print on demand also needs the setting
/// <c>Features:PodEnabled</c> (off while the store sells own products only), so <c>kill.pod</c> reads as off without it.
/// </summary>
public sealed class FeatureFlags(AppDbContext db, IConfiguration configuration) : IFeatureFlags
{
    public async Task<bool> IsOnAsync(string key, CancellationToken ct)
    {
        if (key == KillSwitches.Pod && !configuration.GetValue("Features:PodEnabled", false))
        {
            return false;
        }
        return await db.Set<FeatureFlag>().Where(f => f.Key == key).Select(f => (bool?)f.IsEnabled).FirstOrDefaultAsync(ct) ?? true;
    }

    public async Task EnsureOnAsync(string key, CancellationToken ct)
    {
        if (!await IsOnAsync(key, ct))
        {
            throw key == KillSwitches.Pod ? new FeatureDisabledException("Print on demand is paused for now. Sellers can sell their own products.", "pod_paused") : new FeatureDisabledException();
        }
    }
}
