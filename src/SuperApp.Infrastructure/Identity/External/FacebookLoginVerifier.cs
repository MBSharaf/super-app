using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperApp.Application.Abstractions.Identity;
using SuperApp.Application.Common.Options;

namespace SuperApp.Infrastructure.Identity.External;

/// <summary>
/// Checks a Facebook access token (from "Continue with Facebook" on the site) with Facebook itself: the token must be
/// valid and issued for our app, then the name and email are read with it (signed with the app secret proof).
/// Facebook only shares an email the person has confirmed with Facebook.
/// </summary>
public sealed class FacebookLoginVerifier(
    IHttpClientFactory http,
    IOptions<ExternalLoginOptions> options,
    ILogger<FacebookLoginVerifier> log) : IExternalLoginVerifier
{
    public const string HttpClientName = "facebook-login";
    private const string Graph = "https://graph.facebook.com/v21.0";

    public string Provider => ExternalProviders.Facebook;

    public async Task<ExternalProfile?> VerifyAsync(string token, CancellationToken ct)
    {
        var app = options.Value.Facebook;
        var client = http.CreateClient(HttpClientName);
        try
        {
            var userId = await TokenUserAsync(client, token, app.AppId!, app.AppSecret!, ct);
            if (userId is null)
            {
                return null;
            }
            var proof = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(app.AppSecret!), Encoding.UTF8.GetBytes(token)));
            var url = $"{Graph}/me?fields=id,name,email&access_token={Uri.EscapeDataString(token)}&appsecret_proof={proof}";
            using var me = JsonDocument.Parse(await client.GetStringAsync(url, ct));
            var root = me.RootElement;
            if (root.GetProperty("id").GetString() != userId)
            {
                return null;
            }
            var email = root.TryGetProperty("email", out var e) ? e.GetString() ?? "" : "";
            var name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            return new ExternalProfile(Provider, userId, email, EmailVerified: email.Length > 0, name);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            log.LogWarning(ex, "A Facebook sign-in token could not be checked");
            return null;
        }
    }

    /// <summary>The Facebook user id the token belongs to, when the token is valid and was issued for our app.</summary>
    private static async Task<string?> TokenUserAsync(HttpClient client, string token, string appId, string secret, CancellationToken ct)
    {
        var url = $"{Graph}/debug_token?input_token={Uri.EscapeDataString(token)}&access_token={Uri.EscapeDataString($"{appId}|{secret}")}";
        using var doc = JsonDocument.Parse(await client.GetStringAsync(url, ct));
        var data = doc.RootElement.GetProperty("data");
        var valid = data.TryGetProperty("is_valid", out var v) && v.ValueKind == JsonValueKind.True;
        var ours = data.TryGetProperty("app_id", out var a) && a.GetString() == appId;
        return valid && ours && data.TryGetProperty("user_id", out var u) ? u.GetString() : null;
    }
}
