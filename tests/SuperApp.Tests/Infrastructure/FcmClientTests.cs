using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using SuperApp.Infrastructure.Push;

namespace SuperApp.Tests.Infrastructure;

/// <summary>Firebase push (no network): the service-account JWT is signed with its key, and dead tokens are recognised.</summary>
public sealed class FcmClientTests : IDisposable
{
    private readonly RSA key = RSA.Create(2048);
    private readonly string file = Path.Combine(Path.GetTempPath(), $"fcm-test-{Guid.NewGuid():N}.json");
    private readonly List<HttpRequestMessage> requests = [];
    private readonly List<string> bodies = [];

    public FcmClientTests() => File.WriteAllText(file, JsonSerializer.Serialize(new
    {
        project_id = "demo-project", client_email = "bot@demo-project.iam.gserviceaccount.com",
        private_key = key.ExportPkcs8PrivateKeyPem(), token_uri = "https://oauth2.example/token",
    }));

    public void Dispose()
    {
        key.Dispose();
        File.Delete(file);
    }

    private FcmClient Client(HttpStatusCode sendStatus, string sendBody = "{}", string? credentials = null)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(new Fake(this, sendStatus, sendBody)));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Push:Fcm:CredentialsFile"] = credentials ?? file }).Build();
        return new FcmClient(factory, config, NullLogger<FcmClient>.Instance);
    }

    [Fact]
    public async Task A_push_is_sent_with_a_token_signed_by_the_service_account()
    {
        using var fcm = Client(HttpStatusCode.OK);
        fcm.IsOn.ShouldBeTrue();
        (await fcm.SendAsync("device-1", "Booked", "Pitch A at 9 PM", "#/bookings", default)).ShouldBe(PushResult.Sent);
        (await fcm.SendAsync("device-2", "Booked", null, null, default)).ShouldBe(PushResult.Sent);

        requests.Count(r => r.RequestUri!.Host == "oauth2.example").ShouldBe(1);   // the access token is reused
        var assertion = bodies[0].Split('&').Single(p => p.StartsWith("assertion=", StringComparison.Ordinal))["assertion=".Length..];
        var parts = assertion.Split('.');
        key.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), Convert.FromBase64String(Pad(parts[2])), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).ShouldBeTrue();
        var claims = JsonDocument.Parse(Convert.FromBase64String(Pad(parts[1]))).RootElement;
        claims.GetProperty("iss").GetString().ShouldBe("bot@demo-project.iam.gserviceaccount.com");
        claims.GetProperty("scope").GetString().ShouldBe("https://www.googleapis.com/auth/firebase.messaging");

        var send = requests.First(r => r.RequestUri!.Host == "fcm.googleapis.com");
        send.RequestUri!.AbsolutePath.ShouldBe("/v1/projects/demo-project/messages:send");
        send.Headers.Authorization!.Parameter.ShouldBe("access-1");
        bodies[1].ShouldContain("\"token\":\"device-1\"");
        bodies[1].ShouldContain("\"link\":\"#/bookings\"");
    }

    [Fact]
    public async Task An_unregistered_phone_is_reported_dead_and_other_errors_are_not()
    {
        using (var gone = Client(HttpStatusCode.NotFound, "{\"error\":{\"status\":\"NOT_FOUND\",\"details\":[{\"errorCode\":\"UNREGISTERED\"}]}}"))
        {
            (await gone.SendAsync("old", "t", null, null, default)).ShouldBe(PushResult.DeadToken);
        }
        using var busy = Client(HttpStatusCode.ServiceUnavailable, "{\"error\":{\"status\":\"UNAVAILABLE\"}}");
        (await busy.SendAsync("ok", "t", null, null, default)).ShouldBe(PushResult.Failed);
    }

    [Fact]
    public void Without_a_credentials_file_push_is_off()
    {
        using var fcm = Client(HttpStatusCode.OK, credentials: "/nowhere/fcm.json");
        fcm.IsOn.ShouldBeFalse();
    }

    private static string Pad(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        return s.PadRight(s.Length + ((4 - (s.Length % 4)) % 4), '=');
    }

    private sealed class Fake(FcmClientTests test, HttpStatusCode sendStatus, string sendBody) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            test.requests.Add(request);
            test.bodies.Add(request.Content is null ? "" : Uri.UnescapeDataString(await request.Content.ReadAsStringAsync(cancellationToken)));
            return request.RequestUri!.Host == "oauth2.example"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"access-1\",\"expires_in\":3600}") }
                : new HttpResponseMessage(sendStatus) { Content = new StringContent(sendBody) };
        }
    }
}
