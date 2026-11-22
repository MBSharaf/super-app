using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperApp.Application.Abstractions.Identity;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Options;

namespace SuperApp.Infrastructure.Identity.External;

/// <summary>
/// Checks a Google ID token (from "Sign in with Google" on the site): RS256 signature against Google's published
/// keys, issuer, audience = our client id, and expiry. Google's keys are cached for a few hours.
/// </summary>
public sealed class GoogleLoginVerifier(
    IHttpClientFactory http,
    GoogleKeyCache keys,
    IClock clock,
    IOptions<ExternalLoginOptions> options,
    ILogger<GoogleLoginVerifier> log) : IExternalLoginVerifier
{
    public const string HttpClientName = "google-login";
    private static readonly string[] Issuers = ["accounts.google.com", "https://accounts.google.com"];
    private static readonly TimeSpan Skew = TimeSpan.FromMinutes(5);

    public string Provider => ExternalProviders.Google;

    public async Task<ExternalProfile?> VerifyAsync(string token, CancellationToken ct)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }
        try
        {
            using var header = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[0]));
            var kid = header.RootElement.TryGetProperty("kid", out var k) ? k.GetString() : null;
            if (header.RootElement.GetProperty("alg").GetString() != "RS256" || kid is null)
            {
                return null;
            }
            var key = await keys.GetAsync(kid, http.CreateClient(HttpClientName), ct);
            if (key is null || !SignatureIsValid(key.Value, parts))
            {
                return null;
            }
            using var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
            return ReadProfile(payload.RootElement);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or KeyNotFoundException or InvalidOperationException or HttpRequestException or CryptographicException)
        {
            log.LogWarning(ex, "A Google sign-in token could not be checked");
            return null;
        }
    }

    private static bool SignatureIsValid(RSAParameters key, string[] parts)
    {
        using var rsa = RSA.Create(key);
        var signed = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        return rsa.VerifyData(signed, Base64Url.DecodeFromChars(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    private ExternalProfile? ReadProfile(JsonElement claims)
    {
        var now = new DateTimeOffset(clock.UtcNow, TimeSpan.Zero).ToUnixTimeSeconds();
        var issuerOk = Issuers.Contains(claims.GetProperty("iss").GetString());
        var audienceOk = claims.GetProperty("aud").GetString() == options.Value.Google.ClientId;
        var fresh = claims.GetProperty("exp").GetInt64() + (long)Skew.TotalSeconds > now;
        var sub = claims.GetProperty("sub").GetString();
        if (!issuerOk || !audienceOk || !fresh || string.IsNullOrEmpty(sub))
        {
            return null;
        }
        var email = claims.TryGetProperty("email", out var e) ? e.GetString() ?? "" : "";
        var verified = claims.TryGetProperty("email_verified", out var v) && (v.ValueKind == JsonValueKind.True || v.GetString() == "true");
        var name = claims.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        return new ExternalProfile(Provider, sub, email, verified, name);
    }
}

/// <summary>Google's public signing keys (https://www.googleapis.com/oauth2/v3/certs), fetched again every 6 hours or for an unknown key id.</summary>
public sealed class GoogleKeyCache : IDisposable
{
    public const string CertsUrl = "https://www.googleapis.com/oauth2/v3/certs";
    private readonly SemaphoreSlim gate = new(1, 1);
    private Dictionary<string, RSAParameters> keys = [];
    private DateTime fetchedAt = DateTime.MinValue;

    public void Dispose() => gate.Dispose();

    public async Task<RSAParameters?> GetAsync(string kid, HttpClient client, CancellationToken ct)
    {
        if (keys.TryGetValue(kid, out var key) && DateTime.UtcNow - fetchedAt < TimeSpan.FromHours(6))
        {
            return key;
        }
        await gate.WaitAsync(ct);
        try
        {
            // A key id we don't know may be a new Google key: fetch at most once a minute
            if (DateTime.UtcNow - fetchedAt > TimeSpan.FromMinutes(1))
            {
                keys = await FetchAsync(client, ct);
                fetchedAt = DateTime.UtcNow;
            }
            return keys.TryGetValue(kid, out key) ? key : null;
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<Dictionary<string, RSAParameters>> FetchAsync(HttpClient client, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(await client.GetStringAsync(CertsUrl, ct));
        var result = new Dictionary<string, RSAParameters>();
        foreach (var jwk in doc.RootElement.GetProperty("keys").EnumerateArray())
        {
            if (jwk.GetProperty("kty").GetString() != "RSA")
            {
                continue;
            }
            result[jwk.GetProperty("kid").GetString()!] = new RSAParameters
            {
                Modulus = Base64Url.DecodeFromChars(jwk.GetProperty("n").GetString()),
                Exponent = Base64Url.DecodeFromChars(jwk.GetProperty("e").GetString()),
            };
        }
        return result;
    }
}
