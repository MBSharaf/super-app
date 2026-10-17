using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace SuperApp.Tests.Api;

/// <summary>Small helper around HttpClient that sends the CSRF header and reads the unified response.</summary>
public sealed class ApiClient(HttpClient http)
{
    public HttpClient Http => http;

    public async Task RefreshCsrfAsync()
    {
        var body = await ReadAsync(await http.GetAsync("/api/v1/auth/csrf"));
        http.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        http.DefaultRequestHeaders.Add("X-CSRF-TOKEN", body.GetProperty("data").GetProperty("token").GetString());
    }

    public async Task<(HttpStatusCode Status, JsonElement Body)> PostAsync(string path, object? payload = null)
    {
        var response = await http.PostAsJsonAsync(path, payload ?? new { });
        return (response.StatusCode, await ReadAsync(response));
    }

    public async Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(HttpMethod method, string path, object? payload = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = payload is null ? null : JsonContent.Create(payload) };
        var response = await http.SendAsync(request);
        return (response.StatusCode, await ReadAsync(response));
    }

    public async Task<(HttpStatusCode Status, JsonElement Body)> GetAsync(string path)
    {
        var response = await http.GetAsync(path);
        return (response.StatusCode, await ReadAsync(response));
    }

    /// <summary>Signs in and refreshes the CSRF token (it is tied to the signed-in person).</summary>
    public async Task<JsonElement> SignInAsync(string login, string password)
    {
        var (status, body) = await PostAsync("/api/v1/auth/sign-in", new { login, password });
        status.ShouldBe(HttpStatusCode.OK, body.ToString());
        await RefreshCsrfAsync();
        return body;
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(text) ? default : JsonDocument.Parse(text).RootElement.Clone();
    }
}

/// <summary>Reads values out of the unified response in tests.</summary>
public static class ResponseExtensions
{
    public static string? Code(this JsonElement body) =>
        body.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object && errors.TryGetProperty("code", out var code)
            ? code[0].GetString() : null;

    public static bool HasFieldError(this JsonElement body, string field) =>
        body.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object && errors.TryGetProperty(field, out _);

    public static JsonElement Data(this JsonElement body) => body.GetProperty("data");
}
