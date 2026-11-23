using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SuperApp.Api.Site;

/// <summary>
/// Faster pages on mobile: index.html loads ~200 script files and ~27 style files one by one. The server joins them, in the
/// same order, into one script file (js/bundle-&lt;hash&gt;.js) and one style file (css/bundle-&lt;hash&gt;.css), compressed once
/// at start-up and kept by browsers for a year (the name changes with every release). The site files themselves are not
/// touched: GitHub Pages and local static runs keep loading them one by one, and Site:BundleAssets=false turns this off.
/// Each script that is wrapped in its own function keeps its own try/catch, so one failing file never stops the others.
/// Until the bundles are ready (a few seconds after start), pages are served exactly as before.
/// </summary>
public sealed partial class SiteBundles(ILogger<SiteBundles> logger)
{
    public sealed record Bundle(string ContentType, byte[] Raw, byte[] Brotli, byte[] Gzip);

    private sealed record State(string JsUrl, string CssUrl, HashSet<string> JsFiles, HashSet<string> CssFiles, Dictionary<string, Bundle> Files);

    [GeneratedRegex("""[ \t]*<script src="(js/[^"]+\.js)" defer></script>\r?\n?""")]
    private static partial Regex ScriptTag();

    [GeneratedRegex("""[ \t]*<link rel="stylesheet" href="(css/[^"]+\.css)" />\r?\n?""")]
    private static partial Regex StyleTag();

    private readonly TaskCompletionSource<bool> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private State? state;

    /// <summary>Completes (true) when the bundles are built, false when they are off or failed.</summary>
    public Task<bool> Ready => ready.Task;

    /// <summary>Builds the bundles in the background from the site folder that holds index.html.</summary>
    public void Start(string root, bool enabled)
    {
        if (!enabled)
        {
            ready.TrySetResult(false);
            return;
        }
        _ = Task.Run(() =>
        {
            try
            {
                var built = Build(root);
                state = built;
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Site bundles ready: {Js} scripts, {Css} styles", built.JsFiles.Count, built.CssFiles.Count);
                }
                ready.TrySetResult(true);
            }
            catch (Exception ex)
            {
                // The site never breaks because of this: pages keep loading the files one by one
                logger.LogWarning(ex, "Site bundles skipped");
                ready.TrySetResult(false);
            }
        });
    }

    /// <summary>A built bundle by its address (js/bundle-….js or css/bundle-….css), or null.</summary>
    public Bundle? Find(string path) => state is { } s && s.Files.TryGetValue(path, out var b) ? b : null;

    /// <summary>index.html with the bundled files replaced by the two bundles (unchanged until they are ready).</summary>
    public string Rewrite(string html)
    {
        if (state is not { } s)
        {
            return html;
        }
        html = ReplaceTags(html, ScriptTag(), s.JsFiles, $"<script src=\"{s.JsUrl}\" defer></script>\n");
        return ReplaceTags(html, StyleTag(), s.CssFiles, $"<link rel=\"stylesheet\" href=\"{s.CssUrl}\" />\n");
    }

    private static string ReplaceTags(string html, Regex tag, HashSet<string> bundled, string replacement)
    {
        var first = true;
        return tag.Replace(html, m =>
        {
            if (!bundled.Contains(m.Groups[1].Value))
            {
                return m.Value;
            }
            var indent = m.Value[..(m.Value.Length - m.Value.TrimStart(' ', '\t').Length)];
            var keep = first ? indent + replacement : "";
            first = false;
            return keep;
        });
    }

    private static State Build(string root)
    {
        var index = File.ReadAllText(Path.Combine(root, "index.html"));
        var js = ScriptTag().Matches(index).Select(m => m.Groups[1].Value).Distinct().ToList();
        var css = StyleTag().Matches(index).Select(m => m.Groups[1].Value).Distinct().ToList();
        var jsText = new StringBuilder();
        foreach (var file in js)
        {
            AppendScript(jsText, file, File.ReadAllText(Path.Combine(root, file)));
        }
        var cssText = new StringBuilder();
        foreach (var file in css)
        {
            cssText.Append("/* ").Append(file).Append(" */\n").Append(File.ReadAllText(Path.Combine(root, file))).Append('\n');
        }
        var jsBundle = Pack("text/javascript; charset=utf-8", jsText.ToString());
        var cssBundle = Pack("text/css; charset=utf-8", cssText.ToString());
        var jsUrl = $"js/bundle-{Hash(jsBundle.Raw)}.js";
        var cssUrl = $"css/bundle-{Hash(cssBundle.Raw)}.css";
        return new State(jsUrl, cssUrl, [.. js], [.. css], new Dictionary<string, Bundle> { [jsUrl] = jsBundle, [cssUrl] = cssBundle });
    }

    /// <summary>A file wrapped in its own function gets a try/catch (an error stays in that file, as with separate files);
    /// a file with top-level declarations (namespace.js) is added as it is, so its names stay global.</summary>
    private static void AppendScript(StringBuilder sb, string file, string code)
    {
        sb.Append("/* ").Append(file).Append(" */\n");
        if (IsWrapped(code))
        {
            sb.Append("try {\n").Append(code).Append("\n} catch (e) { console.error('").Append(file).Append("', e); }\n");
            return;
        }
        sb.Append(code).Append("\n;\n");
    }

    public static bool IsWrapped(string code)
    {
        var s = code.TrimStart('﻿');
        while (true)
        {
            s = s.TrimStart();
            if (s.StartsWith("/*", StringComparison.Ordinal) && s.IndexOf("*/", StringComparison.Ordinal) is var end and > 0)
            {
                s = s[(end + 2)..];
            }
            else if (s.StartsWith("//", StringComparison.Ordinal))
            {
                s = s.IndexOf('\n') is var nl and >= 0 ? s[(nl + 1)..] : "";
            }
            else
            {
                break;
            }
        }
        var tail = code.TrimEnd();
        return (s.StartsWith("(function", StringComparison.Ordinal) || s.StartsWith("(()", StringComparison.Ordinal) || s.StartsWith("(async", StringComparison.Ordinal))
            && (tail.EndsWith(");", StringComparison.Ordinal) || tail.EndsWith(")();", StringComparison.Ordinal));
    }

    private static Bundle Pack(string type, string text)
    {
        var raw = Encoding.UTF8.GetBytes(text);
        using var br = new MemoryStream();
        using (var z = new BrotliStream(br, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            z.Write(raw);
        }
        using var gz = new MemoryStream();
        using (var z = new GZipStream(gz, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            z.Write(raw);
        }
        return new Bundle(type, raw, br.ToArray(), gz.ToArray());
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes))[..12].ToLowerInvariant();
}
