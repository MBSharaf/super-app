using System.Text.Json;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Seed.Reference;

/// <summary>
/// Seed 5: platform settings (commissions, VAT, fees, holds, money limits, package maximum discount, points,
/// delivery, pop-up timing) and the feature flags (9 kill switches + API groups), all switched on.
/// Existing values are never overwritten — the CEO's changes are kept.
/// </summary>
public sealed class SettingsSeeder(AppDbContext db) : ISeeder
{
    private sealed record SettingRow(string Key, string Group, JsonElement Value);
    private sealed record FlagRow(string Key, FeatureFlagKind Kind);

    public int Order => 5;

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        var settings = SeedFile.Read<List<SettingRow>>("Reference/settings.json")
            .Select(s => new Setting { Key = s.Key, Group = s.Group, Value = ToStoredJson(s.Value) });
        var added = await db.Set<Setting>().AddMissingAsync(settings, s => s.Key, ct);

        var flags = SeedFile.Read<List<FlagRow>>("Reference/feature-flags.json")
            .Select(f => new FeatureFlag { Key = f.Key, Kind = f.Kind, IsEnabled = true });
        added += await db.Set<FeatureFlag>().AddMissingAsync(flags, f => f.Key, ct);
        await db.SaveChangesAsync(ct);
        return added;
    }

    /// <summary>
    /// SQL Server's ISJSON (the CHECK on Value) accepts only objects and arrays, so a single number, text or
    /// true/false is stored as <c>{"value": …}</c>. The settings service reads both shapes.
    /// </summary>
    internal static string ToStoredJson(JsonElement value) =>
        value.ValueKind is JsonValueKind.Object or JsonValueKind.Array ? value.GetRawText() : $"{{\"value\":{value.GetRawText()}}}";
}
