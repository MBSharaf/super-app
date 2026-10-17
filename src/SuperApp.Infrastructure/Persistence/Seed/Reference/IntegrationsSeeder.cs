using System.Text.Json;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Seed.Reference;

/// <summary>
/// Seed 6: the integrations registry (INTEGRATIONS document). Everything starts Simulated; companies that need a
/// contract start switched off; API-FOOTBALL has its free daily quota of 100. Secrets are never stored — only the
/// name of the environment variable that holds them.
/// </summary>
public sealed class IntegrationsSeeder(AppDbContext db) : ISeeder
{
    private sealed record IntegrationRow(string Key, IntegrationCategory Category, string Name, bool Enabled, bool Primary,
        int? Quota, string? Secret, JsonElement? Config);

    public int Order => 6;

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        var rows = SeedFile.Read<List<IntegrationRow>>("Reference/integrations.json").Select(i => new Integration
        {
            Key = i.Key,
            Category = i.Category,
            Name = i.Name,
            Mode = IntegrationMode.Simulated,
            IsEnabled = i.Enabled,
            IsPrimary = i.Primary,
            DailyQuota = i.Quota,
            SecretName = i.Secret,
            ConfigJson = i.Config?.GetRawText(),
            Status = IntegrationStatus.Unknown,
        });
        var added = await db.Set<Integration>().AddMissingAsync(rows, i => i.Key, ct);
        await db.SaveChangesAsync(ct);
        return added;
    }
}
