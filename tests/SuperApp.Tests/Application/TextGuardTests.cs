using Shouldly;
using SuperApp.Application.Features.Communities;

namespace SuperApp.Tests.Application;

/// <summary>Community text rules: outside links, phone numbers (also in Arabic digits), banned words, and Super App link cards.</summary>
public sealed class TextGuardTests
{
    private static readonly string[] Banned = ["scam", "نصاب"];

    [Theory]
    [InlineData("Great game last night at Maadi!", null)]
    [InlineData("Book here https://example.com/pitch", "outside_link")]
    [InlineData("see www.otherapp.net for details", "outside_link")]
    [InlineData("visit cheapkits.com now", "outside_link")]
    [InlineData("call me 01012345678", "phone_number")]
    [InlineData("whatsapp +20 101 234 5678", "phone_number")]
    [InlineData("رقمي ٠١٠١٢٣٤٥٦٧٨", "phone_number")]
    [InlineData("this seller is a SCAM", "banned_words")]
    [InlineData("ده نصاب", "banned_words")]
    [InlineData("Scampi pasta after the match", null)]
    [InlineData("Join us #/pitches/p-maadi-arena tonight at 9", null)]
    [InlineData("Our page https://superapp.example/super-app/#/communities/fantasy", null)]
    public void Text_rules(string text, string? code) =>
        TextGuard.Check(text, Banned, "superapp.example")?.Code.ShouldBe(code);

    [Fact]
    public void Super_app_links_become_cards_once_and_at_most_three()
    {
        var links = TextGuard.Links("Play #/pitches/p-maadi then #/tournaments/12, shirt #/shop/product/44 and #/news/7 — again #/pitches/p-maadi");
        links.Select(l => (l.Kind, l.Id)).ShouldBe([("pitch", "p-maadi"), ("tournament", "12"), ("product", "44")]);
    }
}
