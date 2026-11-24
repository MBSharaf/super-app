using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SuperApp.Infrastructure.Push;

/// <summary>What happened to one push: sent, the token is dead (app removed: forget it), or a passing failure.</summary>
public enum PushResult
{
    Sent,
    DeadToken,
    Failed,
}

/// <summary>
/// Firebase Cloud Messaging (HTTP v1). The service account key is a JSON file on the server only:
/// PUSH__FCM__CREDENTIALSFILE (e.g. /etc/supermalaab-push/fcm.json, a folder the site can read). Without it push is off and nothing is sent.
/// </summary>
public sealed class FcmClient(IHttpClientFactory http, IConfiguration configuration, ILogger<FcmClient> log) : IDisposable
{
    public const string HttpClientName = "fcm";
    private readonly SemaphoreSlim gate = new(1, 1);
    private ServiceAccount? account;
    private string? accessToken;
    private DateTime accessTokenExpires;

    public void Dispose() => gate.Dispose();

    public bool IsOn => File.Exists(configuration["Push:Fcm:CredentialsFile"] ?? "");

    public async Task<PushResult> SendAsync(string token, string title, string? body, string? link, CancellationToken ct)
    {
        var bearer = await AccessTokenAsync(ct);
        var client = http.CreateClient(HttpClientName);
        var message = new
        {
            message = new
            {
                token,
                notification = new { title, body },
                data = new Dictionary<string, string> { ["link"] = link ?? "#/" },
                android = new { priority = "high", notification = new { sound = "default", channel_id = "default" } },
            },
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{account!.ProjectId}/messages:send")
        {
            Content = JsonContent.Create(message),
        };
        request.Headers.Authorization = new("Bearer", bearer);
        using var response = await client.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            return PushResult.Sent;
        }
        var text = await response.Content.ReadAsStringAsync(ct);
        if (response.StatusCode == HttpStatusCode.NotFound || text.Contains("UNREGISTERED") || text.Contains("INVALID_ARGUMENT"))
        {
            return PushResult.DeadToken;
        }
        log.LogWarning("FCM push failed with {Status}: {Body}", (int)response.StatusCode, text.Length > 300 ? text[..300] : text);
        return PushResult.Failed;
    }

    /// <summary>An OAuth token from the service account (signed JWT), kept until 5 minutes before it ends.</summary>
    private async Task<string> AccessTokenAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (accessToken is not null && DateTime.UtcNow < accessTokenExpires)
            {
                return accessToken;
            }
            account ??= ServiceAccount.Load(configuration["Push:Fcm:CredentialsFile"]!);
            var client = http.CreateClient(HttpClientName);
            using var response = await client.PostAsync(account.TokenUri, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = account.SignedJwt(DateTimeOffset.UtcNow),
            }), ct);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            accessToken = doc.RootElement.GetProperty("access_token").GetString();
            accessTokenExpires = DateTime.UtcNow.AddSeconds(doc.RootElement.GetProperty("expires_in").GetInt32() - 300);
            return accessToken!;
        }
        finally
        {
            gate.Release();
        }
    }

    private sealed record ServiceAccount(string ProjectId, string ClientEmail, string PrivateKeyPem, string TokenUri)
    {
        public static ServiceAccount Load(string file)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var root = doc.RootElement;
            return new ServiceAccount(
                root.GetProperty("project_id").GetString()!,
                root.GetProperty("client_email").GetString()!,
                root.GetProperty("private_key").GetString()!,
                root.TryGetProperty("token_uri", out var uri) ? uri.GetString()! : "https://oauth2.googleapis.com/token");
        }

        public string SignedJwt(DateTimeOffset now)
        {
            var header = Base64Url("{\"alg\":\"RS256\",\"typ\":\"JWT\"}");
            var claims = Base64Url(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["iss"] = ClientEmail,
                ["scope"] = "https://www.googleapis.com/auth/firebase.messaging",
                ["aud"] = TokenUri,
                ["iat"] = now.ToUnixTimeSeconds(),
                ["exp"] = now.AddMinutes(55).ToUnixTimeSeconds(),
            }));
            using var rsa = RSA.Create();
            rsa.ImportFromPem(PrivateKeyPem);
            var signature = rsa.SignData(Encoding.ASCII.GetBytes($"{header}.{claims}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return $"{header}.{claims}.{Base64Url(signature)}";
        }

        private static string Base64Url(string text) => Base64Url(Encoding.UTF8.GetBytes(text));

        private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
