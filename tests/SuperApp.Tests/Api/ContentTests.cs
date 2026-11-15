using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Content;

namespace SuperApp.Tests.Api;

/// <summary>Home banners and announcement, pop-ups, legal documents, cookie consent, help center and SEO files (API §6.6, §7.2).</summary>
[Trait("Category", "Database")]
public sealed class ContentTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private readonly PartnerKit kit = new(api);

    [Fact]
    public async Task Banners_and_the_announcement_bar_are_managed_by_admins()
    {
        var guest = await api.ClientAsync();
        var home = (await guest.GetAsync("/api/v1/content/home")).Body.Data();
        home.GetProperty("banners").GetArrayLength().ShouldBeLessThanOrEqualTo(3);
        home.GetProperty("announcement").ValueKind.ShouldBe(JsonValueKind.Null);

        var ceo = await api.CeoAsync();
        var banner = new { title = "Weekend tournament", badge = "New", ctaText = "Enter", link = "#/tournaments", imageKey = "stadiumNight", isActive = true, sortOrder = -1, showFrom = (string?)null, showUntil = (string?)null };
        (await ceo.PostAsync("/api/v1/admin/banners", banner with { link = "https://example.com" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        var created = await ceo.PostAsync("/api/v1/admin/banners", banner);
        created.Status.ShouldBe(HttpStatusCode.Created, created.Body.ToString());
        (await guest.GetAsync("/api/v1/content/home")).Body.Data().GetProperty("banners")[0].GetProperty("title").GetString().ShouldBe("Weekend tournament");

        var on = new { on = true, text = "Pitch bookings are 10% off this weekend", link = "#/pitches", cta = "Book", tone = "success", until = (string?)null };
        (await ceo.SendAsync(HttpMethod.Put, "/api/v1/admin/announcement", on with { tone = "loud" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await ceo.SendAsync(HttpMethod.Put, "/api/v1/admin/announcement", on)).Status.ShouldBe(HttpStatusCode.OK);
        (await guest.GetAsync("/api/v1/content/home")).Body.Data().GetProperty("announcement").GetProperty("text").GetString().ShouldBe(on.text);

        var (customer, _) = await kit.PersonAsync(partner: false);
        (await customer.PostAsync("/api/v1/admin/banners", banner)).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await ceo.SendAsync(HttpMethod.Delete, $"/api/v1/admin/banners/{created.Body.Data().GetProperty("id").GetInt64()}")).Status.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Popups_count_views_and_clicks_and_follow_their_timing()
    {
        var guest = await api.ClientAsync();
        var popups = (await guest.GetAsync("/api/v1/content/popups")).Body.Data();
        popups.GetProperty("timing").GetProperty("on").GetBoolean().ShouldBeTrue();
        var first = popups.GetProperty("items")[0];
        var id = first.GetProperty("id").GetInt64();
        (await guest.PostAsync($"/api/v1/content/popups/{id}/event", new { kind = "shown" })).Status.ShouldBe(HttpStatusCode.OK);
        (await guest.PostAsync($"/api/v1/content/popups/{id}/event", new { kind = "clicked" })).Status.ShouldBe(HttpStatusCode.OK);
        (await guest.PostAsync($"/api/v1/content/popups/{id}/event", new { kind = "liked" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await guest.PostAsync("/api/v1/content/popups/999999/event", new { kind = "shown" })).Status.ShouldBe(HttpStatusCode.NotFound);
        var counts = await api.DbAsync(db => db.Set<PopupAd>().Where(p => p.Id == id).Select(p => new { p.ShownCount, p.ClickedCount }).SingleAsync());
        (counts.ShownCount, counts.ClickedCount).ShouldBe((first.GetProperty("shownCount").GetInt32() + 1, first.GetProperty("clickedCount").GetInt32() + 1));

        var ceo = await api.CeoAsync();
        var popup = new { titleEn = "Print your own kit", titleAr = "اطبع طقمك", textEn = "Design a shirt in the studio.", textAr = "صمم تيشيرت في الاستوديو.", ctaEn = "Open studio",
            ctaAr = "افتح الاستوديو", link = "#/studio", badge = "New", color = "#0B6E4F", shape = "tee", icons = (string[]?)null, hideFromSellers = true, isActive = true, sortOrder = 0 };
        (await ceo.PostAsync("/api/v1/admin/popups", popup with { shape = "pyramid" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await ceo.PostAsync("/api/v1/admin/popups", popup)).Body.Data().GetProperty("shape").GetString().ShouldBe("tee");
        (await ceo.SendAsync(HttpMethod.Put, "/api/v1/admin/popups/timing", new { on = false, first = 6, interval = 10, stay = 7, max = 6 })).Status.ShouldBe(HttpStatusCode.OK);
        (await guest.GetAsync("/api/v1/content/popups")).Body.Data().GetProperty("items").GetArrayLength().ShouldBe(0);
        (await ceo.SendAsync(HttpMethod.Put, "/api/v1/admin/popups/timing", new { on = true, first = 6, interval = 2, stay = 7, max = 6 })).Status.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Legal_versions_are_published_accepted_and_reviewed_by_the_lawyer()
    {
        var guest = await api.ClientAsync();
        var docs = (await guest.GetAsync("/api/v1/legal")).Body.Data();
        docs.GetArrayLength().ShouldBe(7);
        docs.EnumerateArray().ShouldAllBe(d => d.GetProperty("isDraft").GetBoolean() && d.GetProperty("bodyEn").ValueKind == JsonValueKind.Null);
        (await guest.GetAsync("/api/v1/legal/privacy")).Body.Data().GetProperty("bodyEn").GetString().ShouldNotBeNullOrEmpty();
        (await guest.PostAsync("/api/v1/legal/privacy/accept", new { context = "site" })).Status.ShouldBe(HttpStatusCode.Unauthorized);

        var (customer, userId) = await kit.PersonAsync(partner: false);
        (await customer.PostAsync("/api/v1/legal/privacy/accept", new { context = "nowhere" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await customer.PostAsync("/api/v1/legal/privacy/accept", new { context = "site" })).Status.ShouldBe(HttpStatusCode.OK);
        (await api.DbAsync(db => db.Set<LegalAcceptance>().CountAsync(a => a.UserId == userId && a.Context == "site"))).ShouldBe(1);

        var ceo = await api.CeoAsync();
        var draft = await ceo.PostAsync("/api/v1/admin/legal/privacy/versions", new { summaryEn = "We now keep chat logs for 90 days.", summaryAr = "نحتفظ بالمحادثات ٩٠ يومًا.", bodyEn = "Privacy v2", bodyAr = "الخصوصية ٢" });
        draft.Status.ShouldBe(HttpStatusCode.Created, draft.Body.ToString());
        var version = draft.Body.Data().GetProperty("version").GetInt32();
        version.ShouldBe(2);
        (await guest.GetAsync("/api/v1/legal/privacy")).Body.Data().GetProperty("version").GetInt32().ShouldBe(1);
        (await ceo.PostAsync($"/api/v1/admin/legal/privacy/versions/{version}/publish")).Status.ShouldBe(HttpStatusCode.OK);
        (await ceo.PostAsync($"/api/v1/admin/legal/privacy/versions/{version}/publish")).Body.Code().ShouldBe("already_published");
        (await ceo.PostAsync($"/api/v1/admin/legal/privacy/versions/{version}/lawyer-reviewed", new { note = "Reviewed by counsel" })).Status.ShouldBe(HttpStatusCode.OK);
        var current = (await guest.GetAsync("/api/v1/legal/privacy")).Body.Data();
        (current.GetProperty("version").GetInt32(), current.GetProperty("isDraft").GetBoolean(), current.GetProperty("bodyEn").GetString()).ShouldBe((2, false, "Privacy v2"));
        (await customer.GetAsync("/api/v1/admin/legal/privacy/versions")).Status.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Help_articles_are_searched_and_voted_once_and_cookie_choices_are_stored()
    {
        var guest = await api.ClientAsync();
        var topics = (await guest.GetAsync("/api/v1/help/topics")).Body.Data();
        topics.GetArrayLength().ShouldBe(12);
        var all = (await guest.GetAsync("/api/v1/help/articles")).Body.Data();
        all.GetArrayLength().ShouldBe(20);
        var word = all[0].GetProperty("questionEn").GetString()!.Split(' ').OrderByDescending(w => w.Length).First();
        (await guest.GetAsync($"/api/v1/help/articles?q={Uri.EscapeDataString(word)}")).Body.Data().GetArrayLength().ShouldBeGreaterThan(0);

        var id = all[0].GetProperty("id").GetInt64();
        (await guest.PostAsync($"/api/v1/help/articles/{id}/vote", new { helpful = true, visitorId = (string?)null })).Body.Code().ShouldBe("visitor");
        (await guest.PostAsync($"/api/v1/help/articles/{id}/vote", new { helpful = true, visitorId = "v-123" })).Body.Data().GetProperty("helpfulCount").GetInt32().ShouldBe(1);
        var changed = (await guest.PostAsync($"/api/v1/help/articles/{id}/vote", new { helpful = false, visitorId = "v-123" })).Body.Data();
        (changed.GetProperty("helpfulCount").GetInt32(), changed.GetProperty("notHelpfulCount").GetInt32()).ShouldBe((0, 1));

        var ceo = await api.CeoAsync();
        var article = new { topicId = topics[0].GetProperty("id").GetInt32(), questionEn = "How do I change my team name?", questionAr = "أغير اسم فريقي إزاي؟",
            answerEn = "Open Fantasy and tap the name.", answerAr = "افتح الفانتازي واضغط على الاسم.", sortOrder = 9, isPublished = true };
        (await ceo.PostAsync("/api/v1/admin/help", article)).Status.ShouldBe(HttpStatusCode.Created);
        (await ceo.PostAsync("/api/v1/admin/help", article with { topicId = 9999 })).Body.Code().ShouldBe("topic");

        (await guest.PostAsync("/api/v1/consents/cookies", new { visitorId = "v-123", analytics = false })).Status.ShouldBe(HttpStatusCode.OK);
        (await guest.PostAsync("/api/v1/consents/cookies", new { visitorId = "", analytics = false })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await api.DbAsync(db => db.Set<CookieConsent>().AnyAsync(c => c.VisitorId == "v-123" && !c.Analytics))).ShouldBeTrue();
    }

    [Fact]
    public async Task The_sitemap_and_robots_file_come_from_live_data()
    {
        var guest = await api.ClientAsync();
        var sitemap = await guest.Http.GetStringAsync("/api/v1/seo/sitemap.xml");
        sitemap.ShouldContain("<urlset");
        sitemap.ShouldContain("#/communities/");
        var robots = await guest.Http.GetStringAsync("/api/v1/seo/robots.txt");
        robots.ShouldContain("Disallow: /api/");
        robots.ShouldContain("sitemap.xml");
    }
}
