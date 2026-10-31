# Performance tests (Apache JMeter)

Load tests for the Super App web front end and its public API. Latest results and findings are in
[`docs/perf/PERFORMANCE-REPORT.md`](../../docs/perf/PERFORMANCE-REPORT.md).

| File | What it is |
|---|---|
| `super-app-web.jmx` | JMeter plan for the website: new visitor (empty cache, full page with all 216 css/js/font files, 6 parallel downloads like a browser), deep link, 3D model, SEO files; returning visitor (reload, every file revalidated, 304s) |
| `super-app-api.jmx` | JMeter plan for the public API (`/api/v1/pitches`, pitch details, availability, reviews, free slots, news, fantasy, lookups, `/health`) |
| `serve-static.js` | Production-like static server (works like GitHub Pages: `/super-app/`, gzip, ETag, `max-age=600`, 404.html for clean URLs) |
| `run-all.sh` | Runs every scenario (smoke, baseline, load, stress steps, spike, soak) and builds a JMeter HTML dashboard for each |
| `analyze.py` | Turns the results into `summary.json`, charts and an Excel workbook (Dashboard sheet first) |
| `browser-metrics.js` | Real Chromium page loads (FCP, LCP, TBT, load time, bytes) on desktop, 4G and slow 4G |

## Install JMeter

Needs Java 8+ (Java 17/21 recommended). Download JMeter 5.6.3 from https://jmeter.apache.org/download_jmeter.cgi and unzip it.
On Windows run `bin\jmeter.bat`; on Linux/macOS `bin/jmeter`. Open a `.jmx` in the GUI to view or edit it; run real tests from the
command line (`-n`), never from the GUI.

## Website test

```bash
node tools/perf/serve-static.js 8080                       # terminal 1 (or test the real site, below)
JMETER_HOME=/path/to/apache-jmeter-5.6.3 tools/perf/run-all.sh /tmp/perf-results    # terminal 2, ~25 min
python3 tools/perf/analyze.py /tmp/perf-results docs/perf/<date>
node tools/perf/browser-metrics.js http://localhost:8080/super-app/ docs/perf/<date>/browser-metrics.json
```

One scenario only: `ONLY=03-load-100 tools/perf/run-all.sh`. One custom run:

```bash
jmeter -n -t tools/perf/super-app-web.jmx -Jusers=50 -Jusers_returning=10 -Jrampup=60 -Jduration=300 \
  -l out.jtl -e -o out-dashboard
```

| Property | Default | Meaning |
|---|---|---|
| `host` / `port` / `protocol` | `localhost` / `8080` / `http` | Target. Real demo site: `-Jhost=mbsharaf.github.io -Jport=443 -Jprotocol=https` |
| `base` | `/super-app/` | Folder of the site |
| `users` | 10 | New visitors (empty cache every visit) |
| `users_returning` | 0 | Returning visitors (warm cache, reload) |
| `rampup` | 30 | Seconds to start all users |
| `duration` | 120 | Seconds the test runs |
| `loops` | -1 | Visits per user (-1 = until `duration`) |
| `think_min` / `think_range` | 2000 / 3000 | Pause between pages: `think_min` + random(0..`think_range`) ms |
| `sla_page_ms` | 3000 | Full page load above this fails the sample |
| `sla_reload_ms` | 1500 | Warm reload above this fails the sample |

**Testing GitHub Pages directly:** keep it small (for example 10–20 users for 2 minutes). GitHub Pages is a shared service with a soft
limit of 100 GB/month and may throttle or block heavy traffic; a full load test belongs on our own hosting.

## API test

The API allows 300 read requests per minute per caller (`RateLimits:Read`). All JMeter users share one IP, so raise it for the test
or every user gets HTTP 429:

```powershell
$env:RateLimits__Read = 1000000
dotnet run --project src/SuperApp.Api --launch-profile https
jmeter -n -t tools/perf/super-app-api.jmx -Jusers=50 -Jrampup=30 -Jduration=300 -l api.jtl -e -o api-dashboard
```

Use a database with the demo seed (pitches must exist, or the pitch-details steps are skipped). The plan only reads; it writes nothing.
Properties: `host` (localhost), `port` (7254), `protocol` (https), `users`, `rampup`, `duration`, `think_min` (500), `think_range` (1500),
`sla_api_ms` (500).
