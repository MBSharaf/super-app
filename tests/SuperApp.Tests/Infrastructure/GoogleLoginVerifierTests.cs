using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Options;
using SuperApp.Infrastructure.Identity.External;

namespace SuperApp.Tests.Infrastructure;

/// <summary>The Google ID token check: signature, issuer, audience and expiry (no network: Google's keys are faked).</summary>
public sealed class GoogleLoginVerifierTests : IDisposable
{
    private const string ClientId = "our-client.apps.googleusercontent.com";
    private static readonly DateTime Now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);
    private readonly RSA google = RSA.Create(2048);
    private readonly RSA stranger = RSA.Create(2048);
    private readonly GoogleKeyCache cache = new();

    public void Dispose()
    {
        google.Dispose();
        stranger.Dispose();
        cache.Dispose();
    }

    private GoogleLoginVerifier Verifier()
    {
        var p = google.ExportParameters(false);
        var jwks = JsonSerializer.Serialize(new { keys = new[] { new { kty = "RSA", kid = "k1", alg = "RS256", n = Base64Url.EncodeToString(p.Modulus), e = Base64Url.EncodeToString(p.Exponent) } } });
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(new Answer(jwks)));
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);
        var options = Options.Create(new ExternalLoginOptions { Google = { ClientId = ClientId } });
        return new GoogleLoginVerifier(factory, cache, clock, options, NullLogger<GoogleLoginVerifier>.Instance);
    }

    private static string Token(RSA key, object claims, string kid = "k1")
    {
        static string Part(object o) => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(o));
        var head = Part(new { alg = "RS256", kid, typ = "JWT" });
        var body = Part(claims);
        var sig = key.SignData(Encoding.ASCII.GetBytes($"{head}.{body}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{head}.{body}.{Base64Url.EncodeToString(sig)}";
    }

    private static object Claims(string aud = ClientId, string iss = "https://accounts.google.com", int expiresInMinutes = 30) => new
    {
        iss, aud, sub = "1100220033", email = "player@gmail.com", email_verified = true, name = "Gmail Player",
        exp = new DateTimeOffset(Now.AddMinutes(expiresInMinutes)).ToUnixTimeSeconds(),
    };

    [Fact]
    public async Task A_genuine_token_gives_the_person()
    {
        var profile = await Verifier().VerifyAsync(Token(google, Claims()), default);

        profile.ShouldNotBeNull();
        profile.ProviderKey.ShouldBe("1100220033");
        profile.Email.ShouldBe("player@gmail.com");
        profile.EmailVerified.ShouldBeTrue();
        profile.FullName.ShouldBe("Gmail Player");
    }

    [Fact]
    public async Task A_token_signed_by_anyone_else_is_refused() =>
        (await Verifier().VerifyAsync(Token(stranger, Claims()), default)).ShouldBeNull();

    [Fact]
    public async Task A_token_for_another_app_is_refused() =>
        (await Verifier().VerifyAsync(Token(google, Claims(aud: "someone-else.apps.googleusercontent.com")), default)).ShouldBeNull();

    [Fact]
    public async Task A_token_from_another_issuer_is_refused() =>
        (await Verifier().VerifyAsync(Token(google, Claims(iss: "https://evil.example")), default)).ShouldBeNull();

    [Fact]
    public async Task An_expired_token_is_refused() =>
        (await Verifier().VerifyAsync(Token(google, Claims(expiresInMinutes: -10)), default)).ShouldBeNull();

    [Fact]
    public async Task A_changed_payload_breaks_the_signature()
    {
        var parts = Token(google, Claims()).Split('.');
        var forged = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new { iss = "https://accounts.google.com", aud = ClientId, sub = "1", email = "ceo@superapp.test", email_verified = true, exp = 9999999999 }));
        (await Verifier().VerifyAsync($"{parts[0]}.{forged}.{parts[2]}", default)).ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    [InlineData("a.b.c")]
    public async Task Garbage_is_refused(string token) => (await Verifier().VerifyAsync(token, default)).ShouldBeNull();

    private sealed class Answer(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }
}
