using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Content;
using SuperApp.Application.Features.Pitches;

namespace SuperApp.Api.Site;

/// <summary>What a page shows search engines before the app's JavaScript runs.</summary>
public sealed record SeoPage(string Title, string Description, string Path, string Body, object? JsonLd = null);

/// <summary>
/// Search-engine HTML for the main public pages (home, pitches, a pitch). The server writes it into
/// index.html — title, description, canonical, share tags, JSON-LD and readable content inside &lt;main id="app"&gt; — and
/// the app replaces that content as soon as it loads, so visitors see the site exactly as before.
/// </summary>
public sealed class SeoPages(IPitchesService pitches, IConfiguration configuration)
{
    private const string Brand = "Super Malaab";
    private const string BrandAr = "سوبر ملعب";

    /// <summary>Public pages without live data: title, description and a short readable text, in Arabic and English, so a
    /// search for the Arabic name (سوبر ملعب) or the English one finds the right page.</summary>
    private static readonly Dictionary<string, (string Title, string Description, string Body)> Static = new(StringComparer.OrdinalIgnoreCase)
    {
        ["players"] = ("دور على لاعيبة لماتشك | Find players — Super Malaab سوبر ملعب",
            "سوبر ملعب: لاقي لاعيبة ناقصين لماتشك أو انضم لماتش قريب منك. Find players for your football match in Egypt on Super Malaab.",
            "<h1>Find players for your match — دور على لاعيبة</h1><p>Post a game with the places you still need, or join a match near you.</p>"),
        ["matches"] = ("مواعيد ونتايج الماتشات | Matches — Super Malaab سوبر ملعب",
            "مواعيد ماتشات الدوري المصري والدوري الإنجليزي ونتايجها على سوبر ملعب. Fixtures and results of the Egyptian and English leagues.",
            "<h1>Matches — الماتشات</h1><p>Fixtures, live scores and results from the Egyptian Premier League and the English Premier League.</p>"),
        ["fantasy"] = ("فانتازي الدوري الإنجليزي | Fantasy — Super Malaab سوبر ملعب",
            "العب فانتازي الدوري الإنجليزي على سوبر ملعب: اختار فريقك، اعمل دوري مع صحابك واكسب جوايز. Play Premier League fantasy football.",
            "<h1>Premier League fantasy — فانتازي الدوري الإنجليزي</h1><p>Pick your squad, make a league with friends and win prizes.</p>"),
        ["shop"] = ("متجر أدوات رياضية | Shop — Super Malaab سوبر ملعب",
            "اشتري أدوات وملابس كورة وبادل من متاجر مصرية على سوبر ملعب، والدفع عند الاستلام. Sports gear from Egyptian stores.",
            "<h1>Sports shop — متجر سوبر ملعب</h1><p>Football and padel gear from Egyptian stores, delivered to your door.</p>"),
        ["community"] = ("مجتمع لاعيبة الكورة | Community — Super Malaab سوبر ملعب",
            "انضم لمجتمعات لاعيبة الكورة والبادل في مصر على سوبر ملعب. Join football and padel communities in Egypt.",
            "<h1>Community — المجتمع</h1><p>Talk football, share your games and meet players near you.</p>"),
        ["tournaments"] = ("بطولات كورة | Tournaments — Super Malaab سوبر ملعب",
            "بطولات كرة القدم في الملاعب القريبة منك على سوبر ملعب. Football tournaments at pitches near you in Egypt.",
            "<h1>Tournaments — البطولات</h1><p>Enter your team in football tournaments at pitches near you.</p>"),
        ["help"] = ("مركز المساعدة | Help — Super Malaab سوبر ملعب",
            "إجابات عن الحجز والدفع والعربون والإلغاء على سوبر ملعب. Answers about booking, payment, deposits and cancellation.",
            "<h1>Help center — مركز المساعدة</h1><p>Booking, payments, deposits, cancellations and your account.</p>"),
        ["legal"] = ("الشروط والخصوصية | Legal — Super Malaab سوبر ملعب",
            "شروط الاستخدام وسياسة الخصوصية والاسترجاع في سوبر ملعب. Terms, privacy and refund policies of Super Malaab.",
            "<h1>Legal — الشروط والسياسات</h1><p>Terms of use, privacy policy, refunds and cookies.</p>"),
    };

    public async Task<SeoPage?> BuildAsync(string path, CancellationToken ct)
    {
        var parts = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        try
        {
            return parts switch
            {
                [] => await HomeAsync(ct),
                ["pitches"] => await PitchesAsync(ct),
                ["pitches", var id] when id.StartsWith("p-", StringComparison.Ordinal) => await PitchAsync(id[2..], ct),
                [var key] when Static.TryGetValue(key, out var s) => new SeoPage(s.Title, s.Description, key.ToLowerInvariant(), s.Body),
                _ => null,
            };
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    private async Task<SeoPage> HomeAsync(CancellationToken ct)
    {
        var top = await pitches.SearchAsync(new PitchSearchQuery(PageSize: 8), ct);
        var body = new StringBuilder("<h1>سوبر ملعب Super Malaab — احجز ملعب كورة أو بادل في مصر</h1>")
            .Append("<p>سوبر ملعب هو تطبيق الكورة في مصر: احجز ملاعب خماسي وسباعي وبادل قريبة منك في القاهرة والجيزة والإسكندرية، لاقي لاعيبة لماتشك، والعب فانتازي الدوري الإنجليزي.</p>")
            .Append("<p>Super Malaab is Egypt's football app: book 5, 7 and 11-a-side and padel pitches near you, find players for your match and play Premier League fantasy.</p>")
            .Append("<h2>Top pitches — أشهر الملاعب</h2>").Append(PitchList(top.Items));
        return new SeoPage("سوبر ملعب Super Malaab — احجز ملعب كورة وبادل في مصر",
            "سوبر ملعب: احجز ملاعب الكورة والبادل، لاقي لاعيبة لماتشك والعب فانتازي. Super Malaab: book football and padel pitches in Egypt, find players and play Premier League fantasy.",
            "", body.ToString(), BrandGraph());
    }

    /// <summary>Who we are, for search engines and AI assistants: both names, the logo and the official social pages
    /// (Site:Social:Facebook / Instagram / TikTok / YouTube / X, set on the server once the pages exist).</summary>
    private Dictionary<string, object> BrandGraph()
    {
        var url = (configuration["Site:PublicUrl"] ?? "https://supermalaab.com/").TrimEnd('/') + "/";
        var social = configuration.GetSection("Site:Social").GetChildren().Select(c => c.Value?.Trim())
            .Where(v => v is { Length: > 0 } && v.StartsWith("https://", StringComparison.Ordinal)).ToArray();
        string[] names = [BrandAr, "Supermalaab", "SuperMalaab", "سوبر ملاعب"];
        var org = new Dictionary<string, object>
        {
            ["@type"] = "Organization", ["@id"] = url + "#org", ["name"] = Brand, ["alternateName"] = names, ["url"] = url,
            ["logo"] = url + "icons/icon-512.png", ["areaServed"] = "EG",
            ["description"] = "سوبر ملعب — حجز ملاعب كورة وبادل في مصر. Super Malaab — football and padel pitch booking in Egypt.",
        };
        if (social.Length > 0)
        {
            org["sameAs"] = social;
        }
        var site = new Dictionary<string, object>
        {
            ["@type"] = "WebSite", ["@id"] = url + "#site", ["name"] = Brand, ["alternateName"] = names, ["url"] = url,
            ["inLanguage"] = new[] { "ar-EG", "en" }, ["publisher"] = new Dictionary<string, object> { ["@id"] = url + "#org" },
        };
        return new Dictionary<string, object> { ["@context"] = "https://schema.org", ["@graph"] = new object[] { org, site } };
    }

    private async Task<SeoPage> PitchesAsync(CancellationToken ct)
    {
        var list = await pitches.SearchAsync(new PitchSearchQuery(PageSize: 50), ct);
        var body = "<h1>Book a football or padel pitch in Egypt — احجز ملعب</h1><p>Live availability, prices and reviews for football pitches near you. Pick a time, pay, and you're on.</p>"
            + PitchList(list.Items);
        return new SeoPage("احجز ملعب كورة أو بادل | Book a pitch — Super Malaab سوبر ملعب",
            $"قارن ملاعب الكورة والبادل في مصر بالسعر والتقييم والمنطقة واحجز أونلاين على سوبر ملعب. Compare {list.Total} pitches in Egypt and book an hour online.", "pitches", body);
    }

    private async Task<SeoPage> PitchAsync(string slug, CancellationToken ct)
    {
        var d = await pitches.GetAsync(slug, ct);
        var c = d.Card;
        var price = $"{c.PricePerHour.ToString("0", CultureInfo.InvariantCulture)} EGP";
        var about = d.DescriptionEn ?? $"{c.Format}-a-side {c.Surface} pitch in {c.AreaEn}.";
        var body = $"<h1>{E(c.Name)}</h1><p>{E(c.Format.ToString(CultureInfo.InvariantCulture))}-a-side · {E(c.AreaEn)}, {E(c.GovernorateEn)} · {price} per hour · rated {c.Rating.ToString("0.0", CultureInfo.InvariantCulture)} from {c.ReviewsCount} reviews</p>"
            + $"<p>{E(d.Address)}</p><p>{E(about)}</p><h2>Facilities</h2><ul>{string.Concat(c.Facilities.Select(f => $"<li>{E(f)}</li>"))}</ul>"
            + $"<p><a href=\"pitches\">More pitches</a></p>";
        var ld = new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org", ["@type"] = "SportsActivityLocation", ["name"] = c.Name, ["description"] = about, ["priceRange"] = price,
            ["address"] = new Dictionary<string, object> { ["@type"] = "PostalAddress", ["streetAddress"] = d.Address, ["addressLocality"] = c.AreaEn, ["addressRegion"] = c.GovernorateEn, ["addressCountry"] = "EG" },
            ["geo"] = new Dictionary<string, object> { ["@type"] = "GeoCoordinates", ["latitude"] = c.Lat, ["longitude"] = c.Lng },
        };
        if (c.ReviewsCount > 0)
        {
            ld["aggregateRating"] = new Dictionary<string, object> { ["@type"] = "AggregateRating", ["ratingValue"] = c.Rating, ["reviewCount"] = c.ReviewsCount };
        }
        return new SeoPage($"{c.Name} — {c.AreaEn} | Book on Super Malaab", $"{c.Name} in {c.AreaEn}: {c.Format}-a-side, {price} an hour, rated {c.Rating:0.0}. See live hours and book online.", $"pitches/p-{slug}", body, ld);
    }

    private static string PitchList(IEnumerable<PitchCardDto> items) =>
        "<ul>" + string.Concat(items.Select(p => $"<li><a href=\"pitches/p-{E(p.Slug)}\">{E(p.Name)}</a> — {E(p.AreaEn)}, {p.PricePerHour.ToString("0", CultureInfo.InvariantCulture)} EGP / hour</li>")) + "</ul>";

    private static string E(string? text) => WebUtility.HtmlEncode(text ?? "");

    /// <summary>Writes the page into index.html: title, description, canonical, share tags, JSON-LD and the content.
    /// <paramref name="siteUrl"/> is where the site lives, without a trailing slash (https://supermalaab.com or …/super-app).</summary>
    public static string Render(string indexHtml, SeoPage page, string siteUrl)
    {
        var url = $"{siteUrl}/{page.Path}";
        string A(string s) => WebUtility.HtmlEncode(s);
        var html = indexHtml
            .Replace("<title>Super Malaab — Book, play, compete</title>", $"<title>{A(page.Title)}</title>", StringComparison.Ordinal)
            .Replace("https://supermalaab.com/\" />", $"{A(url)}\" />", StringComparison.Ordinal);
        html = ReplaceMeta(html, "name=\"description\"", page.Description);
        html = ReplaceMeta(html, "property=\"og:title\"", page.Title);
        html = ReplaceMeta(html, "property=\"og:description\"", page.Description);
        html = ReplaceMeta(html, "name=\"twitter:title\"", page.Title);
        html = ReplaceMeta(html, "name=\"twitter:description\"", page.Description);
        if (page.JsonLd is not null)
        {
            var json = JsonSerializer.Serialize(page.JsonLd).Replace("</", "<\\/", StringComparison.Ordinal);
            html = html.Replace("</head>", $"<script type=\"application/ld+json\">{json}</script>\n</head>", StringComparison.Ordinal);
        }
        return html.Replace("<main id=\"app\" class=\"app\" tabindex=\"-1\" aria-live=\"polite\"></main>",
            $"<main id=\"app\" class=\"app\" tabindex=\"-1\" aria-live=\"polite\"><div class=\"container seo-prerender\">{page.Body}</div></main>", StringComparison.Ordinal);
    }

    private static string ReplaceMeta(string html, string key, string content)
    {
        var start = html.IndexOf($"<meta {key} content=\"", StringComparison.Ordinal);
        if (start < 0)
        {
            return html;
        }
        var valueStart = start + $"<meta {key} content=\"".Length;
        var end = html.IndexOf('"', valueStart);
        return html[..valueStart] + WebUtility.HtmlEncode(content) + html[end..];
    }
}
