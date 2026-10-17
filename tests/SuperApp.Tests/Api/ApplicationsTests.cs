using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Geo;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Api;

/// <summary>Applications (API §2.3), the approvals queue (§7.2) and private files, with their edge cases.</summary>
[Trait("Category", "Database")]
public sealed class ApplicationsTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    /// <summary>A real 1×1 PNG and a tiny PDF; "fake" is a program renamed to .jpg.</summary>
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF");
    private static readonly byte[] Fake = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

    private static readonly (string, string, byte[])[] IdCard = [("idFront", "front.png", Png), ("idBack", "back.png", Png)];

    private async Task<(ApiClient Client, string Email)> CustomerAsync()
    {
        var client = await api.ClientAsync();
        var email = $"applicant.{Guid.NewGuid():N}"[..24] + "@test.example";
        var (status, body) = await client.PostAsync("/api/v1/auth/sign-up", new { fullName = "Karim Adel", email, phone = "+2012" + Random.Shared.Next(10_000_000, 99_999_999), password = "Football2026", acceptTerms = true });
        status.ShouldBe(HttpStatusCode.Created, body.ToString());
        await client.RefreshCsrfAsync();
        return (client, email);
    }

    private Task<int> AreaAsync() => api.DbAsync(db => db.Set<Area>().Where(a => a.NameEn == "Maadi").Select(a => a.Id).SingleAsync());

    private async Task<object> PitchAnswersAsync(int count = 2, bool terms = true, int? area = null) => new
    {
        ownerName = "Karim Adel Mostafa", phone = "01012345678", nationalId = "29801011234567", pitchName = "Maadi Night Arena",
        areaId = area ?? await AreaAsync(), address = "Road 9, near the metro", lat = 29.96m, lng = 31.27m, count,
        formats = new[] { 5, 7 }, price = 600, payoutMethod = "InstaPay", payoutDetail = "karim@instapay", acceptTerms = terms,
    };

    private static object SellerAnswers(string? handle = null) => new
    {
        fullName = "Salma Nabil", phone = "01112345678", nationalId = "30001011234567", storeName = "Nile Lines Studio", handle,
        mode = "pod", bio = "Designs from Cairo", payoutMethod = "Bank", payoutDetail = "CIB 0042 1234", acceptTerms = true,
    };

    private static async Task<string> ApplyPitchAsync(ApiClient client, object answers)
    {
        var (status, body) = await client.PostFormAsync("/api/v1/applications/pitch-owner", answers, [.. IdCard, ("photos", "pitch.png", Png)]);
        status.ShouldBe(HttpStatusCode.Created, body.ToString());
        return body.Data().GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task A_customer_applies_once_with_the_id_masked_and_sees_it_in_my_applications()
    {
        var (client, _) = await CustomerAsync();
        var code = await ApplyPitchAsync(client, await PitchAnswersAsync());

        var mine = (await client.GetAsync("/api/v1/applications/mine")).Body.Data()[0];
        mine.GetProperty("code").GetString().ShouldBe(code);
        mine.GetProperty("status").GetString().ShouldBe("Pending");
        mine.GetProperty("answers").GetProperty("nationalId").GetString().ShouldBe("••••••••••4567");
        mine.GetProperty("files").GetArrayLength().ShouldBe(3);

        var again = await client.PostFormAsync("/api/v1/applications/pitch-owner", await PitchAnswersAsync(), [.. IdCard, ("photos", "p.png", Png)]);
        again.Status.ShouldBe(HttpStatusCode.Conflict);
        again.Body.Code().ShouldBe("application_open");
    }

    [Fact]
    public async Task An_application_checks_files_area_and_agreement()
    {
        var (client, _) = await CustomerAsync();
        var noBack = await client.PostFormAsync("/api/v1/applications/pitch-owner", await PitchAnswersAsync(), ("idFront", "f.png", Png), ("photos", "p.png", Png));
        noBack.Status.ShouldBe(HttpStatusCode.BadRequest);
        noBack.Body.HasFieldError("idBack").ShouldBeTrue();

        var fake = await client.PostFormAsync("/api/v1/applications/pitch-owner", await PitchAnswersAsync(), ("idFront", "virus.jpg", Fake), ("idBack", "b.png", Png), ("photos", "p.png", Png));
        fake.Status.ShouldBe(HttpStatusCode.BadRequest);
        fake.Body.HasFieldError("idFront").ShouldBeTrue();

        var pdfPhoto = await client.PostFormAsync("/api/v1/applications/pitch-owner", await PitchAnswersAsync(), [.. IdCard, ("photos", "p.pdf", Pdf)]);
        pdfPhoto.Body.HasFieldError("photos").ShouldBeTrue();

        (await client.PostFormAsync("/api/v1/applications/pitch-owner", await PitchAnswersAsync(area: 999_999), [.. IdCard, ("photos", "p.png", Png)])).Body.HasFieldError("areaId").ShouldBeTrue();
        (await client.PostFormAsync("/api/v1/applications/pitch-owner", await PitchAnswersAsync(terms: false), [.. IdCard, ("photos", "p.png", Png)])).Body.HasFieldError("acceptTerms").ShouldBeTrue();
        (await client.PostFormAsync("/api/v1/applications/pitch-owner", new { ownerName = "x" }, [.. IdCard])).Status.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Approving_a_pitch_owner_creates_the_partner_and_pitches_and_tells_the_applicant()
    {
        var (client, email) = await CustomerAsync();
        var code = await ApplyPitchAsync(client, await PitchAnswersAsync(count: 2));
        var ceo = await api.CeoAsync();

        var approved = await ceo.PostAsync($"/api/v1/admin/approvals/applications/{code}/approve");
        approved.Status.ShouldBe(HttpStatusCode.OK, approved.Body.ToString());
        (await ceo.PostAsync($"/api/v1/admin/approvals/applications/{code}/approve")).Body.Code().ShouldBe("already_decided");

        var me = (await client.GetAsync("/api/v1/me")).Body.Data();
        me.GetProperty("dashboards").EnumerateArray().Select(d => d.GetString()).ShouldContain("partner");
        api.Emails.Sent.ShouldContain(m => m.To == email && m.Subject.Contains("مبروك", StringComparison.Ordinal));

        var pitches = await api.DbAsync(db => db.Set<Pitch>().Where(p => p.Venue.Partner.User.Email == email).ToListAsync());
        pitches.Count.ShouldBe(2);
        pitches.ShouldAllBe(p => p.Status == PitchStatus.Live && p.PricePerHour == 600);
        pitches.Select(p => (int)p.Format).ShouldBe([5, 7], ignoreOrder: true);
    }

    [Fact]
    public async Task A_rejected_application_needs_a_reason_and_can_be_fixed_and_sent_again()
    {
        var (client, _) = await CustomerAsync();
        var code = await ApplyPitchAsync(client, await PitchAnswersAsync());
        var ceo = await api.CeoAsync();

        (await ceo.PostAsync($"/api/v1/admin/approvals/applications/{code}/reject", new { reason = "" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await ceo.PostAsync($"/api/v1/admin/approvals/applications/{code}/reject", new { reason = "The ID photo is not clear." })).Status.ShouldBe(HttpStatusCode.OK);
        var mine = (await client.GetAsync("/api/v1/applications/mine")).Body.Data()[0];
        mine.GetProperty("rejectReason").GetString().ShouldBe("The ID photo is not clear.");

        var resent = await client.SendFormAsync(HttpMethod.Put, $"/api/v1/applications/{code}", await PitchAnswersAsync(), ("idFront", "clear.png", Png));
        resent.Status.ShouldBe(HttpStatusCode.OK, resent.Body.ToString());
        resent.Body.Data().GetProperty("status").GetString().ShouldBe("Pending");
        (await client.SendFormAsync(HttpMethod.Put, $"/api/v1/applications/{code}", await PitchAnswersAsync())).Body.Code().ShouldBe("not_rejected");
        (await ceo.PostAsync($"/api/v1/admin/approvals/applications/{code}/approve")).Status.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Approving_a_seller_creates_the_store_with_its_link_and_a_taken_link_is_refused()
    {
        var (client, _) = await CustomerAsync();
        var handle = "studio-" + Guid.NewGuid().ToString("N")[..6];
        var (status, body) = await client.PostFormAsync("/api/v1/applications/seller", SellerAnswers(handle), IdCard);
        status.ShouldBe(HttpStatusCode.Created, body.ToString());
        var ceo = await api.CeoAsync();
        (await ceo.PostAsync($"/api/v1/admin/approvals/applications/{body.Data().GetProperty("code").GetString()}/approve")).Status.ShouldBe(HttpStatusCode.OK);

        var store = await api.DbAsync(db => db.Set<Store>().SingleAsync(s => s.Handle == handle));
        store.Type.ShouldBe(StoreType.Seller);
        (await api.DbAsync(db => db.Set<Seller>().AnyAsync(s => s.StoreId == store.Id))).ShouldBeTrue();

        var (other, _) = await CustomerAsync();
        var taken = await other.PostFormAsync("/api/v1/applications/seller", SellerAnswers(handle), IdCard);
        taken.Body.HasFieldError("handle").ShouldBeTrue();
    }

    [Fact]
    public async Task Id_photos_are_private_to_the_applicant_and_the_id_checkers()
    {
        var (client, _) = await CustomerAsync();
        await ApplyPitchAsync(client, await PitchAnswersAsync());
        var file = (await client.GetAsync("/api/v1/applications/mine")).Body.Data()[0].GetProperty("files")[0].GetProperty("url").GetString()!;

        (await client.Http.GetAsync(file)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var (stranger, _) = await CustomerAsync();
        (await stranger.Http.GetAsync(file)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await (await api.ClientAsync()).Http.GetAsync(file)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await (await api.CeoAsync()).Http.GetAsync(file)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_ceo_kill_switch_pauses_applications()
    {
        var (client, _) = await CustomerAsync();
        await SetSwitchAsync(false);
        try
        {
            var paused = await client.PostFormAsync("/api/v1/applications/seller", SellerAnswers(), IdCard);
            paused.Status.ShouldBe(HttpStatusCode.Locked);
            paused.Body.Code().ShouldBe("feature_disabled");
        }
        finally
        {
            await SetSwitchAsync(true);
        }
    }

    [Fact]
    public async Task Approvers_need_2fa_and_only_see_the_types_they_may_decide()
    {
        var (applicant, _) = await CustomerAsync();
        var code = await ApplyPitchAsync(applicant, await PitchAnswersAsync());
        var ceo = await api.CeoAsync();
        var email = $"sellers.{Guid.NewGuid():N}"[..20] + "@superapp.test";
        var created = (await ceo.PostAsync("/api/v1/ceo/admins", new { fullName = "Seller Checker", email, roles = new[] { "Support" }, permissions = new[] { "approvals.sellers" } })).Body.Data();
        var link = api.Emails.LinkTo(email, "auth/invite")!;
        var token = System.Web.HttpUtility.ParseQueryString(link.Fragment[(link.Fragment.IndexOf('?', StringComparison.Ordinal) + 1)..])["t"];
        await (await api.ClientAsync()).PostAsync("/api/v1/auth/accept-invite", new { userId = created.GetProperty("id").GetGuid(), token, password = "Checker2026" });

        var admin = await api.ClientAsync();
        await admin.SignInAsync(email, "Checker2026");
        var no2fa = await admin.GetAsync("/api/v1/admin/approvals");
        no2fa.Status.ShouldBe(HttpStatusCode.Forbidden);
        no2fa.Body.Code().ShouldBe("2fa_required");

        await admin.EnableTwoFactorAsync();
        (await admin.GetAsync("/api/v1/admin/approvals")).Status.ShouldBe(HttpStatusCode.OK);
        (await admin.GetAsync($"/api/v1/admin/approvals/applications/{code}")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await admin.PostAsync($"/api/v1/admin/approvals/applications/{code}/approve")).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    private Task<int> SetSwitchAsync(bool on) => api.DbAsync(async db =>
    {
        var flag = await db.Set<FeatureFlag>().SingleAsync(f => f.Key == "kill.applications");
        flag.IsEnabled = on;
        return await db.SaveChangesAsync();
    });
}
