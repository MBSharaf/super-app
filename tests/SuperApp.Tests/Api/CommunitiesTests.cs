using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Communities;
using SuperApp.Domain.Entities.Notifications;
using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Tests.Api;

/// <summary>Communities: audience, join, posts with rules, comments, reactions, polls, reports and moderation (API §6.3, §7.2).</summary>
[Trait("Category", "Database")]
public sealed class CommunitiesTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private readonly PartnerKit kit = new(api);

    private async Task<ApiClient> MemberAsync(string slug = "fantasy")
    {
        var (client, _) = await kit.PersonAsync(partner: false);
        (await client.PostAsync($"/api/v1/communities/{slug}/join")).Status.ShouldBe(HttpStatusCode.OK);
        return client;
    }

    [Fact]
    public async Task Public_communities_are_read_by_anyone_and_sellers_only_ones_need_the_role()
    {
        var guest = await api.ClientAsync();
        var list = (await guest.GetAsync("/api/v1/communities")).Body.Data();
        list.GetArrayLength().ShouldBeGreaterThanOrEqualTo(5);   // 5 seeded (another test may add one)
        list.EnumerateArray().Single(c => c.GetProperty("slug").GetString() == "creators").GetProperty("canJoin").GetBoolean().ShouldBeFalse();
        (await guest.GetAsync("/api/v1/communities/fantasy/posts")).Status.ShouldBe(HttpStatusCode.OK);
        (await guest.GetAsync("/api/v1/communities/creators")).Body.Code().ShouldBe("audience");
        (await guest.PostAsync("/api/v1/communities/fantasy/join")).Status.ShouldBe(HttpStatusCode.Unauthorized);

        var (customer, _) = await kit.PersonAsync(partner: false);
        (await customer.PostAsync("/api/v1/communities/creators/join")).Body.Code().ShouldBe("audience");
        (await customer.PostAsync("/api/v1/communities/pitches/posts", new { text = "Anyone up for 5-a-side?", pollOptions = (string[]?)null })).Body.Code().ShouldBe("not_member");
        var joined = (await customer.PostAsync("/api/v1/communities/pitches/join")).Body.Data();
        (joined.GetProperty("joined").GetBoolean(), joined.GetProperty("members").GetInt32()).ShouldBe((true, 1));
        (await customer.SendAsync(HttpMethod.Delete, "/api/v1/communities/pitches/join")).Status.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Posts_follow_the_rules_and_comments_reactions_and_polls_work()
    {
        var author = await MemberAsync();
        foreach (var (text, code) in new[] { ("Book at https://other.example", "outside_link"), ("Call 01012345678", "phone_number"), ("What a scam", "banned_words") })
        {
            (await author.PostAsync("/api/v1/communities/fantasy/posts", new { text, pollOptions = (string[]?)null })).Body.Code().ShouldBe(code);
        }
        (await author.PostAsync("/api/v1/communities/fantasy/posts", new { text = "Poll", pollOptions = new[] { "Only one" } })).Status.ShouldBe(HttpStatusCode.BadRequest);
        var created = await author.PostAsync("/api/v1/communities/fantasy/posts", new { text = "Captain this week? Also see #/communities/players", pollOptions = new[] { "Salah", "Haaland" } });
        created.Status.ShouldBe(HttpStatusCode.Created, created.Body.ToString());
        var post = created.Body.Data();
        var id = post.GetProperty("id").GetInt64();
        post.GetProperty("cards")[0].GetProperty("kind").GetString().ShouldBe("community");
        post.GetProperty("isMine").GetBoolean().ShouldBeTrue();
        post.GetProperty("author").GetProperty("name").GetString().ShouldBe("Test P.");

        var reader = await MemberAsync();
        var comment = await reader.PostAsync($"/api/v1/posts/{id}/comments", new { text = "Salah every week" });
        comment.Status.ShouldBe(HttpStatusCode.Created);
        var commentId = comment.Body.Data().GetProperty("id").GetInt64();
        (await author.PostAsync($"/api/v1/posts/{id}/comments/{commentId}/replies", new { text = "Agreed!" })).Body.Data().GetProperty("parentId").GetInt64().ShouldBe(commentId);
        (await reader.GetAsync($"/api/v1/posts/{id}/comments")).Body.Data().GetArrayLength().ShouldBe(2);

        (await reader.SendAsync(HttpMethod.Put, $"/api/v1/posts/{id}/reaction", new { type = "Fire" })).Body.Data().GetProperty("myReaction").GetString().ShouldBe("Fire");
        (await reader.SendAsync(HttpMethod.Put, $"/api/v1/posts/{id}/reaction", new { type = "Goal" })).Body.Data().GetProperty("reactionsCount").GetInt32().ShouldBe(1);
        (await reader.SendAsync(HttpMethod.Delete, $"/api/v1/posts/{id}/reaction")).Body.Data().GetProperty("reactionsCount").GetInt32().ShouldBe(0);

        var option = post.GetProperty("poll").GetProperty("options")[0].GetProperty("id").GetInt64();
        (await reader.PostAsync($"/api/v1/posts/{id}/vote", new { optionId = option })).Body.Data().GetProperty("poll").GetProperty("myVote").GetInt64().ShouldBe(option);
        (await reader.PostAsync($"/api/v1/posts/{id}/vote", new { optionId = option })).Body.Code().ShouldBe("already_voted");

        (await reader.SendAsync(HttpMethod.Put, $"/api/v1/posts/{id}", new { text = "Not mine" })).Status.ShouldBe(HttpStatusCode.NotFound);
        (await author.SendAsync(HttpMethod.Put, $"/api/v1/posts/{id}", new { text = "Captain this week? (edited)" })).Status.ShouldBe(HttpStatusCode.OK);
        (await api.DbAsync(db => db.Set<Notification>().CountAsync(n => n.Title == "New comment on your post"))).ShouldBeGreaterThanOrEqualTo(1);
        (await author.GetAsync("/api/v1/link-preview?url=https://evil.example")).Body.Code().ShouldBe("outside_link");
        (await author.GetAsync("/api/v1/link-preview?url=%23/communities/fantasy")).Body.Data().GetProperty("title").GetString().ShouldNotBeNullOrEmpty();
        (await author.GetAsync("/api/v1/communities/feed")).Body.Data().GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Reports_are_moderated_and_muted_members_can_read_but_not_post()
    {
        var author = await MemberAsync("players");
        var post = (await author.PostAsync("/api/v1/communities/players/posts", new { text = "Selling my boots cheap", pollOptions = (string[]?)null })).Body.Data();
        var id = post.GetProperty("id").GetInt64();
        var reporter = await MemberAsync("players");
        (await reporter.PostAsync($"/api/v1/posts/{id}/report", new { reason = "Spam", detail = "Selling" })).Status.ShouldBe(HttpStatusCode.OK);
        (await reporter.PostAsync($"/api/v1/posts/{id}/report", new { reason = "Spam", detail = "Selling" })).Body.Code().ShouldBe("already_reported");

        var ceo = await api.CeoAsync();
        var reports = (await ceo.GetAsync("/api/v1/admin/community/reports")).Body.Data();
        var reportId = reports.EnumerateArray().Single(r => r.GetProperty("postId").GetInt64() == id).GetProperty("id").GetInt64();
        (await reporter.PostAsync($"/api/v1/admin/community/reports/{reportId}/remove")).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await ceo.PostAsync($"/api/v1/admin/community/reports/{reportId}/mute-author", new { days = 3, reason = "Selling is not allowed here." })).Status.ShouldBe(HttpStatusCode.OK);
        (await ceo.PostAsync($"/api/v1/admin/community/reports/{reportId}/keep")).Body.Code().ShouldBe("already_handled");

        (await reporter.GetAsync($"/api/v1/posts/{id}")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await author.GetAsync("/api/v1/communities/players/posts")).Status.ShouldBe(HttpStatusCode.OK);
        (await author.PostAsync("/api/v1/communities/players/posts", new { text = "Back again", pollOptions = (string[]?)null })).Body.Code().ShouldBe("muted");
        (await ceo.GetAsync("/api/v1/admin/mutes")).Body.Data().GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Admins_run_communities_post_as_super_app_and_slow_mode_waits()
    {
        var ceo = await api.CeoAsync();
        var body = new { slug = "derby-talk", name = "Derby talk", about = "Before and after the derby.", audience = "Everyone", rules = new[] { "Respect both clubs" }, slowModeSeconds = 600, color = "#C8102E", icon = "ball" };
        (await ceo.PostAsync("/api/v1/admin/communities", body with { slug = "Derby Talk!" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await ceo.PostAsync("/api/v1/admin/communities", body)).Status.ShouldBe(HttpStatusCode.Created);
        (await ceo.PostAsync("/api/v1/admin/communities", body)).Body.Code().ShouldBe("slug_taken");

        var announcement = (await ceo.PostAsync("/api/v1/admin/communities/derby-talk/posts", new { text = "Welcome! Keep it friendly.", pollOptions = (string[]?)null, isPinned = true, isAnnouncement = true })).Body.Data();
        (announcement.GetProperty("author").GetProperty("name").GetString(), announcement.GetProperty("isPinned").GetBoolean()).ShouldBe(("Super App Admin", true));

        var member = await MemberAsync("derby-talk");
        (await member.PostAsync("/api/v1/communities/derby-talk/posts", new { text = "Ahly by two", pollOptions = (string[]?)null })).Status.ShouldBe(HttpStatusCode.Created);
        var second = await member.PostAsync("/api/v1/communities/derby-talk/posts", new { text = "Or three", pollOptions = (string[]?)null });
        ((int)second.Status, second.Body.Code()).ShouldBe((429, "slow_mode"));
        var posts = (await member.GetAsync("/api/v1/communities/derby-talk/posts")).Body.Data();
        posts[0].GetProperty("isAnnouncement").GetBoolean().ShouldBeTrue();
        var reply = await ceo.PostAsync($"/api/v1/admin/posts/{posts[1].GetProperty("id").GetInt64()}/reply", new { text = "Bold prediction!" });
        reply.Body.Data().GetProperty("asSuperAppAdmin").GetBoolean().ShouldBeTrue();

        (await ceo.PostAsync("/api/v1/admin/banned-words", new { word = "Cheat", language = "en" })).Status.ShouldBe(HttpStatusCode.Created);
        (await ceo.PostAsync("/api/v1/admin/banned-words", new { word = "cheat", language = "en" })).Body.Code().ShouldBe("word_exists");
        await api.DbAsync(db => db.Set<FeatureFlag>().Where(f => f.Key == "kill.community").ExecuteUpdateAsync(u => u.SetProperty(f => f.IsEnabled, false)));
        try
        {
            (await member.PostAsync($"/api/v1/posts/{posts[1].GetProperty("id").GetInt64()}/comments", new { text = "hi" })).Status.ShouldBe((HttpStatusCode)423);
        }
        finally
        {
            await api.DbAsync(db => db.Set<FeatureFlag>().Where(f => f.Key == "kill.community").ExecuteUpdateAsync(u => u.SetProperty(f => f.IsEnabled, true)));
        }
        (await api.DbAsync(db => db.Set<Community>().CountAsync(c => c.Slug == "derby-talk"))).ShouldBe(1);
    }
}
