using Microsoft.Extensions.Configuration;

namespace SuperApp.Infrastructure.Persistence.Seed;

/// <summary>Seed settings from .env (<c>SEED__CEO_EMAIL</c>, <c>SEED__CEO_PASSWORD</c>, <c>SEED__CEO_NAME</c>, <c>SEED__DEMODATA</c>).</summary>
public sealed record SeedOptions(string? CeoEmail, string? CeoPassword, string CeoName, bool DemoData)
{
    public static SeedOptions From(IConfiguration configuration)
    {
        var seed = configuration.GetSection("Seed");
        return new SeedOptions(
            NullIfEmpty(seed["Ceo_Email"]),
            NullIfEmpty(seed["Ceo_Password"]),
            NullIfEmpty(seed["Ceo_Name"]) ?? "CEO",
            !bool.TryParse(seed["DemoData"], out var demo) || demo);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
