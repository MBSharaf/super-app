using System.Net;
using System.Text.Json;
using Shouldly;

namespace SuperApp.Tests.Api;

/// <summary>Paid things are cancelled through a cancellation request that the team approves (owner's rule, 2026-11-07).</summary>
public static class CancellationKit
{
    /// <summary>The customer asks, the CEO approves; returns the approved request (refunded, status).</summary>
    public static async Task<JsonElement> CancelThroughRequestAsync(this AccountsApiFactory api, ApiClient customer, string kind, string reference)
    {
        var asked = await customer.PostAsync("/api/v1/cancellations", new { kind, reference, reason = "Plans changed" });
        asked.Status.ShouldBe(HttpStatusCode.Created, asked.Body.ToString());
        var code = asked.Body.Data().GetProperty("code").GetString();
        var approved = await (await api.CeoAsync()).PostAsync($"/api/v1/admin/cancellations/{code}/approve", new { note = "OK" });
        approved.Status.ShouldBe(HttpStatusCode.OK, approved.Body.ToString());
        return approved.Body.Data();
    }
}
