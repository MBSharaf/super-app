using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SuperApp.Api.Site;

/// <summary>One bundle file kept in memory with its Brotli and gzip copies (made once, at start-up).</summary>
public sealed record BundleFile(string Name, string ContentType, byte[] Raw, byte[] Gzip, byte[] Brotli);

/// <summary>
/// Joins the site's ~190 scripts and ~30 style sheets into one script and one style sheet, in the order index.html
/// lists them (performance report docs/perf: 217 requests per first visit become 3). The files on disk do not change:
/// GitHub Pages and tools/serve.ps1 keep using them one by one, and the page looks exactly the same.
/// <list type="bullet">
/// <item>Scripts: every file is a plain script (no modules) that adds to <c>window.SA</c>, so joining them in order
/// runs the same code in the same order as the separate deferred tags.</item>
/// <item>Style sheets: relative <c>url(...)</c> addresses are rewritten to full site addresses, because the bundle
/// lives in another folder than the original file.</item>
/// <item>File names carry a hash of their content, so they can be cached for a year: a new release gets new names.</item>
/// </list>
/// </summary>
public sealed partial class SiteBundle
{
    public const string Folder = "bundle";

    public required string IndexHtml { get; init; }
    public required IReadOnlyDictionary<string, BundleFile> Files { get; init; }
    public int ScriptCount { get; init; }
    public int StyleCount { get; init; }

    [GeneratedRegex("""^[ \t]*<script src="(?<src>[^"]+)" defer></script>[ \t]*\r?\n""", RegexOptions.Multiline)]
    private static partial Regex ScriptTag();

    [GeneratedRegex("""^[ \t]*<link rel="stylesheet" href="(?<src>[^"]+)" />[ \t]*\r?\n""", RegexOptions.Multiline)]
    private static partial Regex StyleTag();

    [GeneratedRegex("""url\(\s*(?<q>['"]?)(?<url>[^'")]+)\k<q>\s*\)""")]
    private static partial Regex CssUrl();

    /// <summary>Builds the bundle from the site folder (the folder that holds index.html).</summary>
    public static SiteBundle Build(string root, string basePath)
    {
        var index = File.ReadAllText(Path.Combine(root, "index.html"));
        var scripts = Local(ScriptTag().Matches(index));
        var styles = Local(StyleTag().Matches(index));
        var js = Make("app", ".js", "text/javascript; charset=utf-8", JoinScripts(root, scripts));
        var css = Make("app", ".css", "text/css; charset=utf-8", JoinStyles(root, styles, basePath));
        var html = Replace(index, StyleTag(), styles, $"""<link rel="stylesheet" href="{Folder}/{css.Name}" />""");
        html = Replace(html, ScriptTag(), scripts, $"""<script src="{Folder}/{js.Name}" defer></script>""");
        return new SiteBundle
        {
            IndexHtml = html,
            Files = new Dictionary<string, BundleFile>(StringComparer.Ordinal) { [js.Name] = js, [css.Name] = css },
            ScriptCount = scripts.Count,
            StyleCount = styles.Count,
        };
    }

    /// <summary>Site files only: addresses on other hosts (or protocol-relative) are left as they are.</summary>
    private static List<string> Local(MatchCollection tags) =>
        tags.Select(m => m.Groups["src"].Value).Where(src => !src.Contains(':', StringComparison.Ordinal) && !src.StartsWith("//", StringComparison.Ordinal)).ToList();

    private static string JoinScripts(string root, IEnumerable<string> files)
    {
        var sb = new StringBuilder();
        foreach (var file in files)
        {
            // ";" + new line: a file that ends without one (or in a // comment) cannot run into the next
            sb.Append("/* ").Append(file).Append(" */\n").Append(Read(root, file)).Append("\n;\n");
        }
        return sb.ToString();
    }

    private static string JoinStyles(string root, IEnumerable<string> files, string basePath)
    {
        var sb = new StringBuilder();
        foreach (var file in files)
        {
            var folder = Path.GetDirectoryName(file)!.Replace('\\', '/');
            sb.Append("/* ").Append(file).Append(" */\n").Append(RewriteUrls(Read(root, file), folder, basePath)).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>url(images/x.png) in vendor/leaflet/leaflet.css → url(/super-app/vendor/leaflet/images/x.png).</summary>
    public static string RewriteUrls(string css, string folder, string basePath) => CssUrl().Replace(css, m =>
    {
        var url = m.Groups["url"].Value.Trim();
        if (url.StartsWith('/') || url.StartsWith('#') || url.Contains(':', StringComparison.Ordinal))
        {
            return m.Value;   // data:, https:, absolute and fragment addresses already work from anywhere
        }
        var full = new Uri(new Uri($"http://site{basePath}/{folder}/"), url).AbsolutePath;
        return $"url(\"{full}\")";
    });

    /// <summary>Reads a site file; the address must stay inside the site folder.</summary>
    private static string Read(string root, string relative)
    {
        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(Path.GetFullPath(root), StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"index.html points outside the site: {relative}");
        }
        return File.ReadAllText(path);
    }

    /// <summary>Takes out every bundled tag; the bundle tag goes where the first one was.</summary>
    private static string Replace(string html, Regex tag, List<string> bundled, string bundleTag)
    {
        var first = true;
        return tag.Replace(html, m =>
        {
            if (!bundled.Contains(m.Groups["src"].Value))
            {
                return m.Value;
            }
            var indent = m.Value[..(m.Value.Length - m.Value.TrimStart().Length)];
            var line = first ? $"{indent}{bundleTag}\n" : "";
            first = false;
            return line;
        });
    }

    private static BundleFile Make(string stem, string ext, string type, string text)
    {
        var raw = Encoding.UTF8.GetBytes(text);
        var hash = Convert.ToHexStringLower(SHA256.HashData(raw))[..12];
        return new BundleFile($"{stem}.{hash}{ext}", type, raw, Compress(raw, s => new GZipStream(s, CompressionLevel.SmallestSize)),
            Compress(raw, s => new BrotliStream(s, CompressionLevel.SmallestSize)));
    }

    private static byte[] Compress(byte[] raw, Func<Stream, Stream> wrap)
    {
        using var output = new MemoryStream();
        using (var zip = wrap(output))
        {
            zip.Write(raw);
        }
        return output.ToArray();
    }
}
