using System.Text.RegularExpressions;

namespace SuperApp.Application.Features.Communities;

/// <summary>A Super App address found in a post: kind (pitch, slot, game, product, design, store, tournament, post, community, news) and id.</summary>
public sealed record SuperAppLink(string Kind, string Id, string Link);

/// <summary>
/// Checks community text (FR-COM-03): no outside links, no phone numbers, no banned words. Super App links ("#/pitches/p-…",
/// or the site's own address) are allowed and become rich cards. Pure, so it is unit-tested on its own.
/// </summary>
public static partial class TextGuard
{
    private static readonly (string Prefix, string Kind)[] Kinds =
    [
        ("pitches/", "pitch"), ("slots/", "slot"), ("players/", "game"), ("shop/product/", "product"), ("product/", "product"), ("design/", "design"),
        ("store/", "store"), ("tournaments/", "tournament"), ("communities/post/", "post"), ("communities/", "community"), ("news/", "news"),
    ];

    /// <summary>The first broken rule as (message, code), or null when the text is fine.</summary>
    public static (string Message, string Code)? Check(string text, IReadOnlyCollection<string> bannedWords, string? siteHost)
    {
        var withoutOurs = OurLinks(siteHost).Replace(text, " ");
        if (OutsideLink().IsMatch(withoutOurs))
        {
            return ("Links to other websites are not allowed. Share Super App links instead.", "outside_link");
        }
        if (Phone().IsMatch(Digits(withoutOurs)))
        {
            return ("Phone numbers are not allowed — there are no private messages in communities.", "phone_number");
        }
        var words = Words().Matches(text.ToLowerInvariant()).Select(m => m.Value).ToHashSet();
        var lower = text.ToLowerInvariant();
        if (bannedWords.Any(b => b.Contains(' ', StringComparison.Ordinal) ? lower.Contains(b.ToLowerInvariant(), StringComparison.Ordinal) : words.Contains(b.ToLowerInvariant())))
        {
            return ("Please keep it friendly — your text has a word that is not allowed here.", "banned_words");
        }
        return null;
    }

    /// <summary>The Super App links in a text, in order, each once (at most 3 cards per post).</summary>
    public static IReadOnlyList<SuperAppLink> Links(string text) =>
        HashLink().Matches(text).Select(m => m.Groups[1].Value.TrimEnd('.', ',', ')', '!', '?'))
            .Select(path => Kinds.FirstOrDefault(k => path.StartsWith(k.Prefix, StringComparison.OrdinalIgnoreCase)) is { Kind: not null } k
                ? new SuperAppLink(k.Kind, path[k.Prefix.Length..].Split('/', '?')[0], "#/" + path) : null)
            .OfType<SuperAppLink>().Where(l => l.Id.Length > 0).DistinctBy(l => l.Link).Take(3).ToList();

    /// <summary>Arabic-Indic digits count as digits; spaces, dots and dashes between digits are ignored.</summary>
    private static string Digits(string text)
    {
        var chars = text.Select(c => c is >= '٠' and <= '٩' ? (char)('0' + (c - '٠')) : c is >= '۰' and <= '۹' ? (char)('0' + (c - '۰')) : c);
        return Separators().Replace(new string(chars.ToArray()), "");
    }

    private static Regex OurLinks(string? siteHost) => string.IsNullOrWhiteSpace(siteHost)
        ? HashLinkOnly()
        : new Regex($@"(https?://)?{Regex.Escape(siteHost)}\S*|#/\S+", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    [GeneratedRegex(@"#/\S+", RegexOptions.IgnoreCase)]
    private static partial Regex HashLinkOnly();

    [GeneratedRegex(@"#/([^\s]+)", RegexOptions.IgnoreCase)]
    private static partial Regex HashLink();

    [GeneratedRegex(@"(https?://|www\.)\S+|\b[a-z0-9-]+\.(com|net|org|eg|io|me|ly|co|info|biz|app|shop|link|gg|tv)\b", RegexOptions.IgnoreCase)]
    private static partial Regex OutsideLink();

    [GeneratedRegex(@"(\+?20|0)1[0125]\d{8}|\d{9,}")]
    private static partial Regex Phone();

    [GeneratedRegex(@"(?<=\d)[\s.\-()]+(?=\d)")]
    private static partial Regex Separators();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Words();
}
