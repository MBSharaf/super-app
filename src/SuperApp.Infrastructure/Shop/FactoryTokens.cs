using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using SuperApp.Application.Features.Shop.Factory;

namespace SuperApp.Infrastructure.Shop;

/// <summary>Device tokens (32 random bytes, stored as SHA-256) and staff sessions protected with the server's data-protection keys.</summary>
public sealed class FactoryTokens(IDataProtectionProvider protection) : IFactoryTokens
{
    private readonly IDataProtector protector = protection.CreateProtector("SuperApp.Factory.Session");

    public (string Token, byte[] Hash) NewDeviceToken()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (token, Hash(token));
    }

    public byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    public string CreateSession(long staffId, long deviceId, DateTime expiresAt) =>
        protector.Protect(string.Join('|', staffId.ToString(CultureInfo.InvariantCulture), deviceId.ToString(CultureInfo.InvariantCulture), expiresAt.Ticks.ToString(CultureInfo.InvariantCulture)));

    public (long StaffId, long DeviceId)? ReadSession(string token)
    {
        try
        {
            var parts = protector.Unprotect(token).Split('|');
            return parts.Length == 3 && long.TryParse(parts[0], CultureInfo.InvariantCulture, out var staff) && long.TryParse(parts[1], CultureInfo.InvariantCulture, out var device)
                && long.TryParse(parts[2], CultureInfo.InvariantCulture, out var ticks) && ticks > DateTime.UtcNow.Ticks
                ? (staff, device) : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}

/// <summary>The factory headers of the current request: X-Factory-Device and X-Factory-Session.</summary>
public sealed class FactoryHeaders(IHttpContextAccessor http) : IFactoryHeaders
{
    public string? DeviceToken => Header("X-Factory-Device");

    public string? SessionToken => Header("X-Factory-Session");

    private string? Header(string name) => http.HttpContext?.Request.Headers.TryGetValue(name, out var v) == true && !string.IsNullOrWhiteSpace(v) ? v.ToString() : null;
}

/// <summary>PIN hashing for the Application layer.</summary>
public sealed class PinHasherService : IPinHasher
{
    public byte[] Hash(string pin) => PinHasher.Hash(pin);

    public bool Verify(string pin, byte[] hash) => PinHasher.Verify(pin, hash);
}
