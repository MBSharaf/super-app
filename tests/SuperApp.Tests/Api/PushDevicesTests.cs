using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Notifications;

namespace SuperApp.Tests.Api;

/// <summary>Phones registered for push: a token belongs to whoever signed in last on that phone; only its owner can remove it.</summary>
[Trait("Category", "Database")]
public sealed class PushDevicesTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private readonly PartnerKit kit = new(api);

    private Task<Guid?> OwnerAsync(string token) =>
        api.DbAsync(db => db.Set<PushDevice>().Where(d => d.Token == token).Select(d => (Guid?)d.UserId).SingleOrDefaultAsync());

    [Fact]
    public async Task A_phone_token_moves_to_the_last_person_and_only_they_can_remove_it()
    {
        var token = $"fcm-{Guid.NewGuid():N}:APA91b_test-token.1";
        var (first, firstId) = await kit.PersonAsync(partner: false);
        var (second, secondId) = await kit.PersonAsync(partner: false);

        (await first.PostAsync("/api/v1/me/push-devices", new { token, platform = "android" })).Status.ShouldBe(HttpStatusCode.OK);
        (await first.PostAsync("/api/v1/me/push-devices", new { token, platform = "android" })).Status.ShouldBe(HttpStatusCode.OK);
        (await OwnerAsync(token)).ShouldBe(firstId);

        (await second.PostAsync("/api/v1/me/push-devices", new { token, platform = "android" })).Status.ShouldBe(HttpStatusCode.OK);
        (await OwnerAsync(token)).ShouldBe(secondId);

        (await first.PostAsync("/api/v1/me/push-devices/remove", new { token })).Status.ShouldBe(HttpStatusCode.OK);
        (await OwnerAsync(token)).ShouldBe(secondId);
        (await second.PostAsync("/api/v1/me/push-devices/remove", new { token })).Status.ShouldBe(HttpStatusCode.OK);
        (await OwnerAsync(token)).ShouldBeNull();
    }

    [Fact]
    public async Task Bad_tokens_and_platforms_are_refused_and_guests_cannot_register()
    {
        var (person, _) = await kit.PersonAsync(partner: false);
        (await person.PostAsync("/api/v1/me/push-devices", new { token = "bad token <script>", platform = "android" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await person.PostAsync("/api/v1/me/push-devices", new { token = "abc", platform = "windows" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await person.PostAsync("/api/v1/me/push-devices", new { token = new string('a', 600), platform = "android" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        var guest = await api.ClientAsync();
        (await guest.PostAsync("/api/v1/me/push-devices", new { token = "abc", platform = "android" })).Status.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
