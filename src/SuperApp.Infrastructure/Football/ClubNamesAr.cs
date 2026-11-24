using System.Text.Json;
using System.Text.RegularExpressions;

namespace SuperApp.Infrastructure.Football;

/// <summary>
/// Arabic names of clubs as the site shows them (copied from js/data/clubs-ar.js by tools/gen-club-names-ar.mjs):
/// exact name first, then without a "FC"/"SC"… suffix or prefix. Unknown names give null.
/// </summary>
public static partial class ClubNamesAr
{
    private static readonly Lazy<Dictionary<string, string>> Map = new(() =>
    {
        using var stream = typeof(ClubNamesAr).Assembly.GetManifestResourceStream("SuperApp.Infrastructure.Football.ClubNamesAr.json");
        return stream is null ? [] : JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
    });

    public static string? Of(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }
        return Map.Value.TryGetValue(name, out var ar) || Map.Value.TryGetValue(Suffix().Replace(name, string.Empty).Trim(), out ar) ? ar : null;
    }

    [GeneratedRegex(@"\s+(FC|SC|CF|AFC|AC|SAD|FK|Club)$|^(FC|AFC|SC|AC)\s+")]
    private static partial Regex Suffix();
}
