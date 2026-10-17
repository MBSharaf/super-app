using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Bookings;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Services;

/// <summary>Reads number settings stored as {"value": 0.03} (or a bare number) in ops.Settings.</summary>
public sealed class SettingsReader(AppDbContext db) : ISettingsReader
{
    public async Task<decimal> DecimalAsync(string key, decimal fallback, CancellationToken ct)
    {
        var json = await db.Set<Setting>().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync(ct);
        if (json is null)
        {
            return fallback;
        }
        var element = JsonDocument.Parse(json).RootElement;
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("value", out var inner))
        {
            element = inner;
        }
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) => d,
            _ => fallback,
        };
    }
}
