# Performance: summary and fix plan

Short version of [`PERFORMANCE-REPORT.md`](PERFORMANCE-REPORT.md) (JMeter, 2026-10-31) plus the steps to fix what it found.
Rule: none of these fixes may change how the site looks (CLAUDE.md rule 9). They change how files are delivered, not the files' content.

## 1. Summary

| Area | Result | Status |
|---|---|---|
| 100 visitors at the same time (5 min) | Page load p95 **127 ms**, 0 errors in about 1 million requests | ✅ |
| 50 visitors for 10 min (soak) | p95 98 ms, flat; server memory 117 MB at start and end (no leak) | ✅ |
| Stress (no pause between pages) | 3-second limit broken at **50 users** (p95 3.1 s); about 27 full page loads/s maximum | ⚠️ |
| Spike: 300 visitors in 5 s | p95 **20 s**, 77 % of page loads over 3 s; server did not crash and recovered by itself | ❌ |
| Real phone, good 4G | Largest content shows after **about 4.9 s** (Google "good" is ≤ 2.5 s) | ❌ |
| Real phone, slow 4G | **About 10.5 s**; blank screen until all scripts have run | ❌ |

**Root cause: the front end, not the server.** Every first visit downloads **217 files, 1.33 MB gzipped (3.8 MB raw)**:

| What | Share | Problem |
|---|---|---|
| 187 separate JS files + 28 CSS files | 217 requests per page | Each file is a separate request; a burst of users becomes tens of thousands of requests |
| Staff-only pages (dashboards, admin, CEO, factory) | ≥ 867 KB raw, plus their services and texts | Every player downloads code only staff use |
| Arabic texts (18 `i18n-ar*.js`) | 589 KB raw / 179 KB gzipped (14 %) | Loaded even in English mode |
| Nothing drawn until all JS runs | First paint = DOMContentLoaded | Blank screen for 4–10 s on mobile |
| Static files served from the API (`src/SuperApp.Api/Site/SiteHosting.cs`) | **No compression**, `no-cache` on every file | When the site moves to our own hosting, visitors would download **3.8 MB instead of 1.3 MB**, and every file is revalidated on every visit |

## 2. Fix plan (in order of impact)

### Fix 1: compression on our own hosting (backend, small, do first)
`src/SuperApp.Api` has no response compression, so the site served by the API goes out uncompressed.

```csharp
// ApiServiceExtensions: register
services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;                       // static, non-secret files only — see note
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
    o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["image/svg+xml", "model/gltf-binary"]);
});
services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
// PipelineExtensions: before app.UseSuperAppSite()
app.UseResponseCompression();
```
Note: `EnableForHttps` with responses that mix secrets and attacker-controlled text can enable BREACH. Limit compression to the site files (`/super-app/**`, not `/api/**` responses that carry tokens), e.g. with `app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/super-app"), b => b.UseResponseCompression())`.
**Gain:** 3.8 MB → 1.3 MB per first visit on our hosting (GitHub Pages already compresses).

### Fix 2: bundle and minify (biggest gain)
All scripts are plain files that share the `SA` namespace (no `import`), so joining them **in the same order as `index.html`** is safe.
1. Add `tools/build/bundle.mjs` (Node + esbuild):
   - read the `<script src>` and `<link rel=stylesheet>` lists from `index.html` in order;
   - join and minify into `dist/js/app.<hash>.js` and `dist/css/app.<hash>.css` (`esbuild --minify`, no renaming of globals);
   - copy `index.html` to `dist/` with the ~215 tags replaced by 1–3 tags; copy `vendor/`, `icons/`, `models/`, `404.html`, `manifest.json`, `sw.js`, `offline.html`, SEO files.
2. Add `.github/workflows/pages.yml`: on push to `main`, run the bundler and publish `dist/` with `actions/deploy-pages` (Pages source = GitHub Actions). The repository keeps the source files unchanged; local development still uses the unbundled `index.html`.
3. `SiteHosting.cs` serves `dist/` when it exists, otherwise the source (so the backend keeps working in development).

**Gain:** 217 → about 10 requests; about 30–40 % less to download after minification; a burst of 300 users becomes about 20 times lighter.

### Fix 3: load staff code only when needed (code splitting)
1. In the bundler, split by folder into: `app` (core, components, public pages and services), `staff` (`js/pages/dash`, `admin`, `ceo`, `factory`, seller/studio pages, `adminService`, `ceoService`, `dashService`, `partnerService`, `sellerService`, `podService`, …), and the matching Arabic text files.
2. In `js/app.js`, staff routes are registered with a small stub: on first open it loads `staff.<hash>.js` (one `<script>` tag, then a Promise), then calls the real page. `app.js` already wraps `router.add` (line 26), so the stub goes there.
3. Prefetch `staff.js` after sign-in for partner/seller/admin accounts, so staff never wait.

**Gain:** public pages (home, pitches, shop, fantasy) download about 35–40 % less.

### Fix 4: Arabic texts only in Arabic mode
Bundle `i18n-ar*.js` as `ar.<hash>.js` (and `ar-staff` for the staff texts). Before the app starts, `index.html` checks the saved language and loads `ar.js` only if it is Arabic; switching language loads it, then re-renders.
**Gain:** 179 KB gzipped less (14 %) for English-mode visitors.

### Fix 5: show something at once (app shell)
Put a static copy of the header and an empty page skeleton (same classes, same tokens) in `index.html`'s `<body>`; the app replaces it on first render. Inline the few CSS rules it needs in `<head>`.
**Gain:** first paint under 1 s on 4G instead of about 3.8 s. The final look is unchanged.

### Fix 6: long cache for bundles
After fix 2 the bundle names carry a content hash, so they never change:
* `SiteHosting.cs`: `Cache-Control: public, max-age=31536000, immutable` for hashed files; keep `no-cache` for `index.html`, `sw.js`, `manifest.json`.
* `sw.js`: bump `VERSION` on release (already planned there).

**Gain:** a returning visitor makes about 1 request instead of 217 revalidations.

### Fix 7: heavy single files
* `models/tee.glb` (556 KB): `npx @gltf-transform/cli optimize tee.glb tee.glb --compress meshopt` (about 70–90 % smaller; the 3D viewer must load the meshopt decoder).
* `vendor/fonts/archivo-latin.woff2` (59 KB): subset to used characters with `pyftsubset`.

### Fix 8: hosting for launch
GitHub Pages' soft limit (100 GB/month) is only about 75,000 first visits at today's size (about 250,000 after fixes 2–4). For launch, put a CDN (Cloudflare or Azure Front Door) in front of the API server, with edge caching close to Egypt.

## 3. Checklist and targets

- [x] Fix 1: compression on the API's site files (+ test that `/api/**` is not compressed): done 2026-11-01, see [`AFTER-FIXES-2026-11-01.md`](AFTER-FIXES-2026-11-01.md)
- [x] Fix 2: bundling, done 2026-11-01 inside the API (`SiteBundle`, no Node build step); GitHub Pages workflow not done (demo only); site looks identical (compare screenshots of every page in `devLinks` GROUPS, EN + AR, light + dark)
- [ ] Fix 3: staff code split; every staff route still opens directly (deep link) and from the menu
- [ ] Fix 4: Arabic texts on demand; language switch still works without reload problems
- [ ] Fix 5: app shell
- [x] Fix 6: long cache for hashed files (+ ETag/304), done 2026-11-01
- [ ] Fix 7: smaller 3D model and font
- [ ] Re-run `tools/perf/run-all.sh` + `tools/perf/browser-metrics.js`, update the report

| Metric | Now | Target after fixes |
|---|---|---|
| Requests on first visit | 217 | ≤ 15 |
| Download on first visit (public page) | 1.33 MB | ≤ 500 KB |
| LCP, good 4G | 4.9 s | ≤ 2.5 s |
| LCP, slow 4G | 10.5 s | ≤ 5 s |
| First paint, good 4G | 3.8 s | ≤ 1 s |
| Spike 300 users, p95 (same test machine) | 20 s | ≤ 3 s |
| Returning visit, requests | 217 (304s) | ≤ 3 |

The targets are estimates; the re-run gives the real numbers.
