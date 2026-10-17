using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Services;

/// <summary>Reads the CEO's switches from ops.FeatureFlags. A missing switch counts as on.</summary>
public sealed class FeatureFlags(AppDbContext db) : IFeatureFlags
{
    public async Task<bool> IsOnAsync(string key, CancellationToken ct) =>
        await db.Set<FeatureFlag>().Where(f => f.Key == key).Select(f => (bool?)f.IsEnabled).FirstOrDefaultAsync(ct) ?? true;

    public async Task EnsureOnAsync(string key, CancellationToken ct)
    {
        if (!await IsOnAsync(key, ct))
        {
            throw new FeatureDisabledException();
        }
    }
}
