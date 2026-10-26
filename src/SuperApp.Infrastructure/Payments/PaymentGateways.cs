using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SuperApp.Application.Features.Payments;

namespace SuperApp.Infrastructure.Payments;

/// <summary>Used while no gateway keys are in .env: every payment is paid at once, nothing leaves the server.</summary>
public sealed class SimulatedGateway : IPaymentGateway
{
    public string Provider => "simulated";

    public bool IsSimulated => true;

    public Task<GatewaySession> StartAsync(GatewayStart request, CancellationToken ct) =>
        Task.FromResult(new GatewaySession(request.ReturnPath, request.MerchantRef));

    public GatewayEvent? VerifyWebhook(IReadOnlyDictionary<string, string> query, string body) => null;

    public Task<string> RefundAsync(string transactionId, decimal amount, CancellationToken ct) => Task.FromResult($"sim-refund-{transactionId}");
}

/// <summary>
/// Paymob (accept.paymob.com), Intention API + Unified Checkout. Keys from .env: <c>PAYMENTS__PAYMOB__SECRETKEY</c>,
/// <c>__PUBLICKEY</c>, <c>__HMACSECRET</c> and <c>__INTEGRATIONIDS</c> (comma list: card, wallet…). Callback URL to set in the
/// Paymob dashboard: <c>{Payments:PublicBaseUrl}/api/v1/webhooks/payments/paymob</c> ("Transaction processed callback").
/// </summary>
public sealed class PaymobGateway(HttpClient http, IConfiguration configuration) : IPaymentGateway
{
    public const string BaseAddress = "https://accept.paymob.com/";

    /// <summary>The transaction fields Paymob signs, in its order (HMAC-SHA512 of their values joined).</summary>
    private static readonly string[] SignedFields =
    [
        "amount_cents", "created_at", "currency", "error_occured", "has_parent_transaction", "id", "integration_id", "is_3d_secure",
        "is_auth", "is_capture", "is_refunded", "is_standalone_payment", "is_voided", "order.id", "owner", "pending",
        "source_data.pan", "source_data.sub_type", "source_data.type", "success",
    ];

    public string Provider => "paymob";

    public bool IsSimulated => false;

    private string Setting(string key) => configuration[$"Payments:Paymob:{key}"] ?? string.Empty;

    public async Task<GatewaySession> StartAsync(GatewayStart request, CancellationToken ct)
    {
        var site = (configuration["Payments:PublicBaseUrl"] ?? "https://localhost:7254").TrimEnd('/');
        var cents = (long)Math.Round(request.Amount * 100);
        var names = (request.PayerName.Trim() + " -").Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var body = new
        {
            amount = cents, currency = "EGP",
            payment_methods = Setting("IntegrationIds").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToArray(),
            items = new[] { new { name = request.Description.Length > 50 ? request.Description[..50] : request.Description, amount = cents, description = request.Description, quantity = 1 } },
            billing_data = new { first_name = names[0], last_name = names[1].Trim(), email = request.PayerEmail, phone_number = string.IsNullOrEmpty(request.PayerPhone) ? "NA" : request.PayerPhone,
                apartment = "NA", street = "NA", building = "NA", floor = "NA", city = "NA", country = "EG" },
            special_reference = request.MerchantRef,
            notification_url = $"{site}/api/v1/webhooks/payments/paymob",
            redirection_url = $"{site}/super-app/{request.ReturnPath}",
        };
        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/intention/") { Content = JsonContent.Create(body) };
        message.Headers.TryAddWithoutValidation("Authorization", $"Token {Setting("SecretKey")}");
        using var response = await http.SendAsync(message, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Paymob refused the payment ({(int)response.StatusCode}): {(json.Length > 300 ? json[..300] : json)}");
        }
        using var doc = JsonDocument.Parse(json);
        var secret = doc.RootElement.GetProperty("client_secret").GetString();
        var order = doc.RootElement.TryGetProperty("intention_order_id", out var o) ? o.ToString() : request.MerchantRef;
        return new GatewaySession($"{BaseAddress}unifiedcheckout/?publicKey={Uri.EscapeDataString(Setting("PublicKey"))}&clientSecret={Uri.EscapeDataString(secret!)}", order);
    }

    /// <summary>A body that is not Paymob's JSON is treated like a bad signature (null → 400), never a server error.</summary>
    public GatewayEvent? VerifyWebhook(IReadOnlyDictionary<string, string> query, string body)
    {
        try
        {
            return Verify(query, body);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private GatewayEvent? Verify(IReadOnlyDictionary<string, string> query, string body)
    {
        if (!query.TryGetValue("hmac", out var hmac) || string.IsNullOrEmpty(Setting("HmacSecret")))
        {
            return null;
        }
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("obj", out var obj) || doc.RootElement.TryGetProperty("type", out var type) && type.GetString() != "TRANSACTION")
        {
            return null;
        }
        var signed = string.Concat(SignedFields.Select(f => Value(obj, f)));
        var expected = Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(Setting("HmacSecret")), Encoding.UTF8.GetBytes(signed))).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(hmac.ToLowerInvariant())))
        {
            return null;
        }
        var order = obj.GetProperty("order");
        var merchantRef = order.TryGetProperty("merchant_order_id", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString()! : string.Empty;
        return new GatewayEvent(merchantRef, Value(obj, "order.id"), Value(obj, "id"), Value(obj, "success") == "true", Value(obj, "pending") == "true",
            obj.GetProperty("amount_cents").GetInt64() / 100m, body);
    }

    public async Task<string> RefundAsync(string transactionId, decimal amount, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/acceptance/void_refund/refund")
        {
            Content = JsonContent.Create(new { transaction_id = transactionId, amount_cents = (long)Math.Round(amount * 100) }),
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"Token {Setting("SecretKey")}");
        using var response = await http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Paymob refused the refund ({(int)response.StatusCode}).");
        }
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("id", out var id) ? id.ToString() : transactionId;
    }

    /// <summary>A field as Paymob signs it: booleans lower-case, null as empty, nested fields by dot path.</summary>
    internal static string Value(JsonElement obj, string path)
    {
        var e = obj;
        foreach (var part in path.Split('.'))
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(part, out e))
            {
                return string.Empty;
            }
        }
        return e.ValueKind switch
        {
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            JsonValueKind.String => e.GetString() ?? string.Empty,
            _ => e.GetRawText(),
        };
    }
}
