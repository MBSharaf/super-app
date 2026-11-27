using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace SuperApp.Tests.Api;

[Trait("Category", "Database")]
public sealed class AccountDeletionTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private readonly PartnerKit kit = new(api);

    [Fact]
    public async Task A_player_deletes_their_account_and_can_no_longer_sign_in()
    {
        var (player, id) = await kit.PersonAsync(partner: false);
        var email = await api.DbAsync(db => db.Users.Where(u => u.Id == id).Select(u => u.Email!).SingleAsync());

        (await player.PostAsync("/api/v1/me/delete", new { confirm = "yes", password = "Football2026" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        var wrong = await player.PostAsync("/api/v1/me/delete", new { confirm = "DELETE", password = "Wrong2026" });
        wrong.Status.ShouldBe(HttpStatusCode.BadRequest);
        wrong.Body.Code().ShouldBe("wrong_password");
        (await player.PostAsync("/api/v1/me/delete", new { confirm = "DELETE", password = "Football2026" })).Status.ShouldBe(HttpStatusCode.NoContent);

        (await player.GetAsync("/api/v1/me")).Status.ShouldBe(HttpStatusCode.Unauthorized);
        var user = await api.DbAsync(db => db.Users.SingleAsync(u => u.Id == id));
        user.FullName.ShouldBe("Deleted user");
        user.Email.ShouldNotBe(email);
        user.PhoneNumber.ShouldBeNull();
        user.PasswordHash.ShouldBeNull();
        var again = await api.ClientAsync();
        (await again.PostAsync("/api/v1/auth/sign-in", new { login = email, password = "Football2026" })).Status.ShouldNotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Business_and_team_accounts_are_closed_through_support_not_here()
    {
        var (partner, _) = await kit.PersonAsync(partner: true);
        var r = await partner.PostAsync("/api/v1/me/delete", new { confirm = "DELETE", password = "Football2026" });
        r.Status.ShouldBe(HttpStatusCode.Conflict);
        r.Body.Code().ShouldBe("business_account");
        var ceo = await api.CeoAsync();
        (await ceo.PostAsync("/api/v1/me/delete", new { confirm = "DELETE", password = "x" })).Body.Code().ShouldBe("staff_account");
    }
}
