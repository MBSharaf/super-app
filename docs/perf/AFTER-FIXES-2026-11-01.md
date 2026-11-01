# Performance after fixes 1, 2 and 6 (2026-11-01)

Follow-up to [`PERFORMANCE-REPORT.md`](PERFORMANCE-REPORT.md) and [`SUMMARY-AND-FIX-PLAN.md`](SUMMARY-AND-FIX-PLAN.md).

## What changed

| Fix | Change | Where |
|---|---|---|
| 1. Compression | Site files under `/super-app` are compressed with Brotli or gzip. API answers are not compressed (BREACH). | `src/SuperApp.Api/Site/SiteCompression.cs` |
| 2. Bundling | At start-up the API joins the 187 scripts and 28 style sheets listed in `index.html`, in the same order, into `bundle/app.<hash>.js` and `bundle/app.<hash>.css`. Both are pre-compressed once (Brotli 659 KB + 56 KB). Relative `url()` addresses in the CSS are rewritten. On by default outside Development (`Site:Bundle`). | `src/SuperApp.Api/Site/SiteBundle.cs`, `SiteHosting.cs` |
| 6. Long cache | Bundle files: `Cache-Control: public, max-age=31536000, immutable`, ETag = hashed name, `304` on revalidation. A new release gets new names. | `SiteHosting.cs` |

* **No source file changed.** GitHub Pages, `tools/serve.ps1` and Development keep serving the files one by one.
* **The design did not change.** `tools/perf/bundle-check.js` opened all 172 pages of the "Every page" map, in EN/light and AR/dark (344 comparisons, staff pages signed in as the demo owner, seller and admin), in the bundled and the unbundled site. It compared full-page screenshots pixel by pixel and the script errors:
  * **0 script errors that only the bundled site has.**
  * Pages with pixel differences: unbundled vs itself **39**, unbundled vs bundled **40**. The extra pages were checked by eye: random demo numbers (e.g. rating 4.2 vs 4.3) and the scroll-reveal animation. Same noise as the A-vs-A run, nothing caused by bundling.
* Tests: 4 new tests in `tests/SuperApp.Tests/Api/PipelineTests.cs` (bundle tags, Brotli/gzip, immutable cache, 304, unknown bundle 404, site compressed but `/api` not, CSS address rewriting). All 21 pipeline tests pass.

## Results: real browser (what a visitor sees)

Same machine, same static server with Brotli, demo mode, median of 3 cold visits; the only difference is bundling. Raw data: `2026-11-01-after/browser-metrics*.json`.

| Network | Page | Requests | Download | First paint | **LCP** | TBT |
|---|---|---|---|---|---|---|
| **Mobile 4G** | Home | 234 → **21** | 1,189 → **838 KB** | 3,792 → **1,528 ms** | 4,892 → **2,656 ms** | 329 → 657 ms |
| | Pitches | 228 → 15 | 1,189 → 838 KB | 3,804 → 1,676 ms | 5,084 → **3,068 ms** | 121 → 614 ms |
| | Shop | 224 → 11 | 1,189 → 838 KB | 3,832 → 1,660 ms | 5,040 → **2,948 ms** | 117 → 519 ms |
| **Slow 4G** | Home | 234 → 21 | 1,189 → 838 KB | 8,528 → **5,644 ms** | 10,052 → **7,072 ms** | 1,510 → 1,900 ms |
| | Pitches | 228 → 15 | | 8,476 → 5,912 ms | 8,860 → 7,460 ms | 715 → 1,594 ms |
| | Shop | 224 → 11 | | 8,468 → 5,836 ms | 8,836 → 7,264 ms | 497 → 1,266 ms |
| Desktop | Home | 236 → 23 | | 712 → 428 ms | 1,432 → 1,292 ms | 42 → 194 ms |

* **4G: the page shows 2.3 s sooner and the main content about 2 s sooner (-45 %).** LCP 2.7 s is now just above Google's "good" line (2.5 s), down from "poor".
* **Slow 4G: about 3 s faster**, still slow (7 s), because 838 KB is a lot for 1.6 Mbps.
* **Trade-off, TBT went up:** the browser now runs 3.3 MB of script as one task instead of 187 small ones, so the screen cannot react to taps for longer while the app starts. **Fix 3 (load staff code only when needed) is the cure.** It cuts the script the browser has to run by about a third.

## Results: JMeter through our own API

The API itself serves the site (`ASPNETCORE_ENVIRONMENT=Production`), same plan and scenarios as the first report. The home page needs the database for its search-engine content, and there is no database here, so the plans open `/super-app/shop` (`-Jhome=shop`). Raw data: `2026-11-01-after/summary.json`, `super-app-performance.xlsx`.

**Same server, before and after bundling** (API with compression only, `Site__Bundle=false`, vs API with the bundle):

| Scenario | | Without bundle | **With bundle** |
|---|---|---|---|
| Load: 100 users, 5 min | Requests per page | 217 | **4** |
| | Page load p95 | 1,705 ms | **46 ms** |
| | Errors | 0 % | 0 % |
| | Server CPU (avg) | 150 % | **54 %** |
| Spike: 300 users in 5 s | Page load p95 | 30,923 ms | **791 ms** |
| | Pages over 3 s | **98.9 %** | **0 %** |

**All scenarios with the bundle** (compare with section 4 of the first report):

| Scenario | Users | Page loads/s | Page p50 | **Page p95** | Page p99 | Errors | First report p95 |
|---|---|---|---|---|---|---|---|
| Baseline | 10 | 1.1 | 35 ms | 45 ms | 53 ms | 0 % | 188 ms |
| Load | 100 | 10.3 | 32 ms | **46 ms** | 58 ms | 0 % | 127 ms |
| Stress | 5 | 45.6 | 45 ms | 69 ms | 85 ms | 0 % | 265 ms |
| Stress | 25 | 50.1 | 200 ms | 349 ms | 423 ms | 0 % | 1,213 ms |
| Stress | 50 | 46.3 | 424 ms | **848 ms** | 1,094 ms | 0 % | 3,144 ms ❌ |
| Stress | 100 | 46.9 | 766 ms | **1,459 ms** | 2,046 ms | 0.04 % | 5,444 ms ❌ |
| **Spike** | **300** | 32.2 | 63 ms | **791 ms** | 1,437 ms | **0 %** | 19,988 ms ❌ |
| Soak, 10 min | 50 | 5.6 | 29 ms | 40 ms | 49 ms | 0 % | 98 ms |

* **Every scenario now passes the 3-second limit.** The stress test only fails at 100 back-to-back users, by 1 sample in 2,500.
* **Capacity almost doubled:** 50 full page loads/s vs 27, about 350 active visitors with normal pauses on this 4-core test machine (JMeter on the same machine).
* **Returning visitor reload** (after the ETag fix): p95 **10 ms**, 20 KB, vs 89 ms and 81 KB. Real browsers do not even ask again for a year, because of `immutable`.
* Soak: no slowdown and no memory growth over 10 minutes (376 → 391 MB, the .NET process; the bundle itself is about 5 MB of it).
* The "first report" column was measured on a lightweight GitHub Pages-like Node server, not the API. On the API without the bundle the numbers were much worse (table above), so **for our own hosting the bundle is not optional**.

## Checklist update

- [x] Fix 1: compression (site only, never `/api`)
- [x] Fix 2: bundling (our hosting). GitHub Pages still serves files one by one; it is only the demo.
- [x] Fix 6: long cache + ETag for bundle files
- [ ] **Fix 3: staff code split**: next step, also brings TBT back down
- [ ] Fix 5: app shell (instant first paint)
- [ ] Fix 7: smaller 3D model
- [ ] Fix 8: CDN (Cloudflare) at launch

## For the owner

* Nothing to run: no migration, no new setting. In Production the bundle is on by default. To switch it off: `Site__Bundle=false`.
* In Development the files are served one by one as before, so changes show at once.
* After a release the server builds the new bundle when it starts (about 2 s), and the new names make every browser fetch it.

## Crowd test: 1,000 / 2,000 / 10,000 visitors arriving within 10 seconds

Same setup (API with the bundle, `/super-app/shop`, 80 % new and 20 % returning visitors, 2–5 s pauses, 3 minutes).
JMeter and the API share the same 4-core test machine.

| Visitors | Real failures | First page, median | First page, p95 | First pages over 3 s | Last visitor got the page after | Server CPU | Valid? |
|---|---|---|---|---|---|---|---|
| **1,000** | **0** | 0.9 s | 15.6 s | 29 % | 50 s | 151 % avg | ✅ |
| **2,000** | **0** | 1.0 s | 12.8 s | 26 % | 58 s | 154 % avg | ✅ |
| **10,000** | 0.75 %, all inside JMeter | 5.7 s | 17.8 s | 69 % | 300 s | **43 % avg** | ❌ the test machine could not generate it |

* **1,000 and 2,000:** nothing failed. Half the crowd had the page in about 1 s; the rest queued, because on this machine the server peaks at **about 190 full page loads per second** (each first visit = 0.84–1.1 MB). After the first wave, 2,000 visitors who keep browsing need more than that, so about a quarter of their pages stay over 3 s.
* **10,000:** JMeter ran out of memory (8 GB heap, 443 `OutOfMemoryError`) and needed 300 s instead of 10 s to start the users; at most 6,691 were active at once. The API sat at 43 % CPU, waiting for traffic. **This run measures JMeter, not the site.** A real 10,000 test needs several load machines (JMeter distributed mode or a cloud load service) against a server like the production one.

### What 10,000 at once means for production (estimate, not measured)

* **Bandwidth is the first wall:** 10,000 first visits × 0.84 MB = **8.4 GB**. A VPS with a 1 Gbit/s port needs at least **about 67 s** to send that, whatever the CPU. With 190 page loads/s per 4 cores the CPU alone needs about 53 s.
* **With a CDN in front (fix 8, Cloudflare):** the bundle files are cached at the edge for a year (`immutable`), so the server only sends the 3 KB page and the API answers. The static part of a 10,000-visitor burst then costs the server almost nothing.
* **Not covered yet:** the API and the database under a crowd (sign-in, pitch search, booking). Run `tools/perf/super-app-api.jmx` on a machine with SQL Server and the demo data.
