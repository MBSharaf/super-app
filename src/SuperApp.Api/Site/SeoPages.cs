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
/// Search-engine HTML for the main public pages (home, pitches, a pitch, news, an article). The server writes it into
/// index.html — title, description, canonical, share tags, JSON-LD and readable content inside &lt;main id="app"&gt; — and
/// the app replaces that content as soon as it loads, so visitors see the site exactly as before.
/// </summary>
public sealed class SeoPages(IPitchesService pitches, INewsService news)
{
    private const string Brand = "Super App";

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
                ["news"] => await NewsListAsync(ct),
                ["news", var id] when long.TryParse(id, out var n) => await ArticleAsync(n, ct),
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
        var latest = await news.NewsAsync(new NewsQuery(PageSize: 5), admin: false, ct);
        var body = new StringBuilder("<h1>Super App — book football pitches, find players and play fantasy in Egypt</h1>")
            .Append("<p>Book 5, 7 and 11-a-side pitches near you in Cairo, Giza and Alexandria, find players for your match and play fantasy football.</p>")
            .Append("<h2>Top pitches</h2>").Append(PitchList(top.Items))
            .Append("<h2>Latest news</h2>").Append(NewsList(latest.Items));
        return new SeoPage("Super App — Book, play, compete", "Book football pitches near you, find players for your match and play Egyptian Premier League fantasy — in one app.", "", body.ToString(),
            new Dictionary<string, object> { ["@context"] = "https://schema.org", ["@type"] = "WebSite", ["name"] = Brand });
    }

    private async Task<SeoPage> PitchesAsync(CancellationToken ct)
    {
        var list = await pitches.SearchAsync(new PitchSearchQuery(PageSize: 50), ct);
        var body = "<h1>Book a football pitch in Egypt</h1><p>Live availability, prices and reviews for football pitches near you. Pick a time, pay, and you're on.</p>"
            + PitchList(list.Items);
        return new SeoPage("Book a football pitch — Super App", $"Compare {list.Total} football pitches in Egypt by price, rating and area, and book an hour online.", "pitches", body);
    }

    private async Task<SeoPage> PitchAsync(string slug, CancellationToken ct)
    {
        var d = await pitches.GetAsync(slug, ct);
        var c = d.Card;
        var price = $"{c.PricePerHour.ToString("0", CultureInfo.InvariantCulture)} EGP";
        var about = d.DescriptionEn ?? $"{c.Format}-a-side {c.Surface} pitch in {c.AreaEn}.";
        var body = $"<h1>{E(c.Name)}</h1><p>{E(c.Format.ToString(CultureInfo.InvariantCulture))}-a-side · {E(c.AreaEn)}, {E(c.GovernorateEn)} · {price} per hour · rated {c.Rating.ToString("0.0", CultureInfo.InvariantCulture)} from {c.ReviewsCount} reviews</p>"
            + $"<p>{E(d.Address)}</p><p>{E(about)}</p><h2>Facilities</h2><ul>{string.Concat(c.Facilities.Select(f => $"<li>{E(f)}</li>"))}</ul>"
            + $"<p><a href=\"/super-app/pitches\">More pitches</a></p>";
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
        return new SeoPage($"{c.Name} — {c.AreaEn} | Book on Super App", $"{c.Name} in {c.AreaEn}: {c.Format}-a-side, {price} an hour, rated {c.Rating:0.0}. See live hours and book online.", $"pitches/p-{slug}", body, ld);
    }

    private async Task<SeoPage> NewsListAsync(CancellationToken ct)
    {
        var list = await news.NewsAsync(new NewsQuery(PageSize: 30), admin: false, ct);
        return new SeoPage("Football news — Super App", "The latest Egyptian and English football news, transfers and match previews.", "news",
            "<h1>Football news</h1>" + NewsList(list.Items));
    }

    private async Task<SeoPage> ArticleAsync(long id, CancellationToken ct)
    {
        var a = await news.PostAsync(id, admin: false, ct);
        var body = $"<article><h1>{E(a.Title)}</h1><p>{E(a.Excerpt)}</p>{string.Concat((a.Body ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(p => $"<p>{E(p)}</p>"))}</article>";
        var ld = new Dictionary<string, object> { ["@context"] = "https://schema.org", ["@type"] = "NewsArticle", ["headline"] = a.Title, ["description"] = a.Excerpt,
            ["datePublished"] = a.PublishedAt.ToString("o", CultureInfo.InvariantCulture), ["publisher"] = new Dictionary<string, object> { ["@type"] = "Organization", ["name"] = Brand } };
        return new SeoPage($"{a.Title} — Super App", a.Excerpt, $"news/{id}", body, ld);
    }

    private static string PitchList(IEnumerable<PitchCardDto> items) =>
        "<ul>" + string.Concat(items.Select(p => $"<li><a href=\"/super-app/pitches/p-{E(p.Slug)}\">{E(p.Name)}</a> — {E(p.AreaEn)}, {p.PricePerHour.ToString("0", CultureInfo.InvariantCulture)} EGP / hour</li>")) + "</ul>";

    private static string NewsList(IEnumerable<NewsDto> items) =>
        "<ul>" + string.Concat(items.Select(n => $"<li><a href=\"/super-app/news/{n.Id}\">{E(n.Title)}</a> — {E(n.Excerpt)}</li>")) + "</ul>";

    private static string E(string? text) => WebUtility.HtmlEncode(text ?? "");

    /// <summary>Writes the page into index.html: title, description, canonical, share tags, JSON-LD and the content.</summary>
    public static string Render(string indexHtml, SeoPage page, string origin)
    {
        var url = $"{origin}/super-app/{page.Path}";
        string A(string s) => WebUtility.HtmlEncode(s);
        var html = indexHtml
            .Replace("<title>Super App — Book, play, compete</title>", $"<title>{A(page.Title)}</title>", StringComparison.Ordinal)
            .Replace("https://mbsharaf.github.io/super-app/\" />", $"{A(url)}\" />", StringComparison.Ordinal);
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
