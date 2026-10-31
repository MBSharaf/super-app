#!/usr/bin/env python3
"""Turns the JMeter results of tools/perf/run-all.sh into report material.

Reads   <results>/<scenario>/results.jtl (+ start/end) and an optional server monitor CSV.
Writes  <out>/summary.json, <out>/charts/*.png and <out>/super-app-performance.xlsx (Dashboard sheet first).

Usage: python3 tools/perf/analyze.py <results dir> <out dir> [server monitor csv]
Needs: matplotlib, openpyxl.
"""
import csv
import json
import os
import re
import statistics
import sys
from collections import Counter, defaultdict

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402
from openpyxl import Workbook  # noqa: E402
from openpyxl.chart import BarChart, LineChart, Reference  # noqa: E402
from openpyxl.styles import Alignment, Font, PatternFill  # noqa: E402

SUB = re.compile(r"-\d+$")
csv.field_size_limit(10**9)


def pct(values, p):
    if not values:
        return None
    v = sorted(values)
    k = max(0, min(len(v) - 1, int(round(p / 100 * len(v) + 0.5)) - 1))
    return v[k]


def stats(rows):
    el = [int(r["elapsed"]) for r in rows]
    if not el:
        return None
    err = sum(1 for r in rows if r["success"] != "true")
    return {
        "count": len(el), "avg": round(statistics.mean(el), 1), "min": min(el), "p50": pct(el, 50), "p90": pct(el, 90),
        "p95": pct(el, 95), "p99": pct(el, 99), "max": max(el), "errors": err, "error_pct": round(100 * err / len(el), 3),
        "avg_kb": round(statistics.mean(int(r["bytes"]) for r in rows) / 1024, 1),
    }


def load_monitor(path):
    if not path or not os.path.exists(path):
        return []
    with open(path) as f:
        return [{k: float(v) for k, v in r.items()} for r in csv.DictReader(f)]


def analyse(name, folder, monitor):
    with open(os.path.join(folder, "results.jtl"), newline="") as f:
        rows = list(csv.DictReader(f))
    start = int(open(os.path.join(folder, "start")).read()) if os.path.exists(os.path.join(folder, "start")) else None
    end = int(open(os.path.join(folder, "end")).read()) if os.path.exists(os.path.join(folder, "end")) else None
    subs = [r for r in rows if SUB.search(r["label"])]
    parents_with_subs = {SUB.sub("", r["label"]) for r in subs}
    tx = [r for r in rows if re.match(r"T\d\d ", r["label"])]
    samplers = [r for r in rows if not SUB.search(r["label"]) and not re.match(r"T\d\d ", r["label"])]
    # HTTP requests actually sent: every sub-request + samplers that have no sub-requests
    requests = subs + [r for r in samplers if r["label"] not in parents_with_subs]
    t0 = min(int(r["timeStamp"]) for r in rows)
    t1 = max(int(r["timeStamp"]) + int(r["elapsed"]) for r in rows)
    secs = max(1.0, (t1 - t0) / 1000)
    by_label = defaultdict(list)
    for r in tx + samplers:
        by_label[r["label"]].append(r)
    codes = Counter(r["responseCode"] for r in requests)
    by_url = defaultdict(list)
    for r in subs:
        by_url[r["URL"].split("?")[0]].append(r)
    slow = sorted(((u, statistics.mean(int(x["elapsed"]) for x in v), statistics.mean(int(x["bytes"]) for x in v), len(v))
                   for u, v in by_url.items()), key=lambda x: -x[1])[:10]
    heavy = sorted(((u, statistics.mean(int(x["bytes"]) for x in v)) for u, v in by_url.items()), key=lambda x: -x[1])[:10]
    page = [r for r in samplers if r["label"].startswith("GET / (index")]
    sub_per_page = round(len([r for r in subs if r["label"].startswith("GET / (index")]) / max(1, len(page)), 1)
    mon = [m for m in monitor if start and end and start <= m["epoch"] <= end]
    # time series (5 s buckets) for charts
    series = defaultdict(lambda: {"req": 0, "pages": 0, "page_el": [], "threads": 0, "err": 0})
    for r in requests:
        b = (int(r["timeStamp"]) - t0) // 5000
        series[b]["req"] += 1
        series[b]["err"] += r["success"] != "true"
        series[b]["threads"] = max(series[b]["threads"], int(r["allThreads"]))
    for r in page:
        b = (int(r["timeStamp"]) - t0) // 5000
        series[b]["pages"] += 1
        series[b]["page_el"].append(int(r["elapsed"]))
    ts = [{"t": b * 5, "rps": s["req"] / 5, "pages_ps": s["pages"] / 5, "threads": s["threads"],
           "page_p95": pct(s["page_el"], 95), "errors": s["err"]} for b, s in sorted(series.items())]
    return {
        "name": name, "duration_s": round(secs, 1), "threads_max": max(int(r["allThreads"]) for r in rows),
        "http_requests": len(requests), "rps": round(len(requests) / secs, 1),
        "page_loads": len(page), "pages_ps": round(len(page) / secs, 2), "sub_requests_per_page": sub_per_page,
        "mb_received": round(sum(int(r["bytes"]) for r in requests) / 1048576, 1),
        "mbit_s": round(sum(int(r["bytes"]) for r in requests) * 8 / 1e6 / secs, 1),
        "request_error_pct": round(100 * sum(1 for r in requests if r["success"] != "true") / max(1, len(requests)), 3),
        "request_stats": stats(requests), "status_codes": dict(codes),
        "labels": {k: stats(v) for k, v in sorted(by_label.items())},
        "slowest_files": [{"url": u.split("/super-app/")[-1], "avg_ms": round(a, 1), "avg_kb": round(b / 1024, 1), "n": n} for u, a, b, n in slow],
        "heaviest_files": [{"url": u.split("/super-app/")[-1], "kb": round(b / 1024, 1)} for u, b in heavy],
        "server": {
            "cpu_avg": round(statistics.mean(m["server_cpu_pct"] for m in mon), 1) if mon else None,
            "cpu_max": max(m["server_cpu_pct"] for m in mon) if mon else None,
            "rss_max_mb": max(m["server_rss_mb"] for m in mon) if mon else None,
            "rss_first_mb": mon[0]["server_rss_mb"] if mon else None,
            "jmeter_cpu_avg": round(statistics.mean(m["jmeter_cpu_pct"] for m in mon), 1) if mon else None,
            "load1_max": max(m["host_load1"] for m in mon) if mon else None,
        },
        "series": ts,
    }


def chart_series(s, out):
    fig, ax = plt.subplots(figsize=(9, 3.6))
    t = [p["t"] for p in s["series"]]
    ax.plot(t, [p["page_p95"] or 0 for p in s["series"]], color="#FF5B14", label="Full page load p95 (ms)")
    ax.set_xlabel("seconds"); ax.set_ylabel("ms"); ax.grid(alpha=.3)
    ax2 = ax.twinx()
    ax2.plot(t, [p["threads"] for p in s["series"]], color="#0D1411", ls="--", label="Active users")
    ax2.plot(t, [p["rps"] for p in s["series"]], color="#2E7D5B", alpha=.8, label="HTTP requests/s")
    ax2.set_ylabel("users  |  req/s")
    h1, l1 = ax.get_legend_handles_labels(); h2, l2 = ax2.get_legend_handles_labels()
    ax.legend(h1 + h2, l1 + l2, loc="upper left", fontsize=8)
    ax.set_title(f"{s['name']}: response time, users and throughput over time", fontsize=10)
    fig.tight_layout(); fig.savefig(out, dpi=110); plt.close(fig)


def chart_stress(stress, out):
    c = [s["threads_max"] for s in stress]
    fig, ax = plt.subplots(figsize=(9, 3.6))
    ax.plot(c, [s["labels"]["T01 Home page - full load"]["p95"] for s in stress], "o-", color="#FF5B14", label="Full page load p95 (ms)")
    ax.plot(c, [s["labels"]["T01 Home page - full load"]["p50"] for s in stress], "o--", color="#FF9A6B", label="Full page load median (ms)")
    ax.set_xlabel("concurrent users (no think time)"); ax.set_ylabel("ms"); ax.grid(alpha=.3)
    ax2 = ax.twinx()
    ax2.plot(c, [s["rps"] for s in stress], "s-", color="#2E7D5B", label="HTTP requests/s")
    ax2.plot(c, [s["server"]["cpu_avg"] or 0 for s in stress], "^-", color="#555", label="Server CPU % (1 core = 100)")
    ax2.set_ylabel("req/s  |  CPU %")
    h1, l1 = ax.get_legend_handles_labels(); h2, l2 = ax2.get_legend_handles_labels()
    ax.legend(h1 + h2, l1 + l2, loc="upper left", fontsize=8)
    ax.set_title("Stress: capacity curve", fontsize=10)
    fig.tight_layout(); fig.savefig(out, dpi=110); plt.close(fig)


HEAD = PatternFill("solid", fgColor="0D1411")


def sheet_table(ws, headers, rows, start_row=1):
    for i, h in enumerate(headers, 1):
        c = ws.cell(row=start_row, column=i, value=h)
        c.font = Font(bold=True, color="FFFFFF"); c.fill = HEAD; c.alignment = Alignment(wrap_text=True)
    for r, row in enumerate(rows, start_row + 1):
        for i, v in enumerate(row, 1):
            ws.cell(row=r, column=i, value=v)
    for i in range(1, len(headers) + 1):
        ws.column_dimensions[chr(64 + i) if i <= 26 else "A"].width = 16
    ws.column_dimensions["A"].width = 44


def workbook(all_s, out):
    wb = Workbook()
    ws = wb.active; ws.title = "Dashboard"
    ws["A1"] = "Super App - web performance (JMeter)"; ws["A1"].font = Font(bold=True, size=14)
    ws["A2"] = "Simulated test against the production-like static server; see docs/perf/PERFORMANCE-REPORT.md"
    hdr = ["Scenario", "Max users", "Duration s", "HTTP requests", "Req/s", "Page loads/s", "Page p50 ms", "Page p95 ms",
           "Page p99 ms", "Request p95 ms", "Error %", "Mbit/s", "Server CPU avg %", "Server RSS max MB"]
    rows = []
    for s in all_s:
        p = s["labels"].get("T01 Home page - full load") or s["labels"].get("T05 Home page - reload (warm cache)") or {}
        rows.append([s["name"], s["threads_max"], s["duration_s"], s["http_requests"], s["rps"], s["pages_ps"], p.get("p50"), p.get("p95"),
                     p.get("p99"), s["request_stats"]["p95"], s["request_error_pct"], s["mbit_s"], s["server"]["cpu_avg"], s["server"]["rss_max_mb"]])
    sheet_table(ws, hdr, rows, start_row=4)
    n = len(rows)
    bar = BarChart(); bar.title = "Full page load p95 (ms) by scenario"; bar.height = 8; bar.width = 22
    bar.add_data(Reference(ws, min_col=8, min_row=4, max_row=4 + n), titles_from_data=True)
    bar.set_categories(Reference(ws, min_col=1, min_row=5, max_row=4 + n))
    ws.add_chart(bar, f"A{7 + n}")
    line = BarChart(); line.title = "HTTP requests per second by scenario"; line.height = 8; line.width = 22
    line.add_data(Reference(ws, min_col=5, min_row=4, max_row=4 + n), titles_from_data=True)
    line.set_categories(Reference(ws, min_col=1, min_row=5, max_row=4 + n))
    ws.add_chart(line, f"A{25 + n}")
    for s in all_s:
        w = wb.create_sheet(s["name"][:31])
        sheet_table(w, ["Label", "Count", "Avg ms", "Min", "p50", "p90", "p95", "p99", "Max", "Errors", "Error %", "Avg KB"],
                    [[k, v["count"], v["avg"], v["min"], v["p50"], v["p90"], v["p95"], v["p99"], v["max"], v["errors"], v["error_pct"], v["avg_kb"]]
                     for k, v in s["labels"].items()])
        r0 = len(s["labels"]) + 4
        sheet_table(w, ["Time s", "Active users", "Req/s", "Page loads/s", "Page p95 ms", "Errors"],
                    [[p["t"], p["threads"], p["rps"], p["pages_ps"], p["page_p95"], p["errors"]] for p in s["series"]], start_row=r0)
        lc = LineChart(); lc.title = "Requests/s and active users"; lc.height = 7; lc.width = 20
        lc.add_data(Reference(w, min_col=2, max_col=3, min_row=r0, max_row=r0 + len(s["series"])), titles_from_data=True)
        lc.set_categories(Reference(w, min_col=1, min_row=r0 + 1, max_row=r0 + len(s["series"])))
        w.add_chart(lc, "N2")
    wb.save(out)


def main():
    results, out = sys.argv[1], sys.argv[2]
    monitor = load_monitor(sys.argv[3] if len(sys.argv) > 3 else None)
    os.makedirs(os.path.join(out, "charts"), exist_ok=True)
    all_s = []
    for name in sorted(os.listdir(results)):
        folder = os.path.join(results, name)
        if os.path.exists(os.path.join(folder, "results.jtl")):
            print("analysing", name)
            s = analyse(name, folder, monitor)
            all_s.append(s)
            if not name.startswith("04-stress") and not name.startswith("01"):
                chart_series(s, os.path.join(out, "charts", f"{name}.png"))
    stress = [s for s in all_s if s["name"].startswith("04-stress")]
    if stress:
        chart_stress(stress, os.path.join(out, "charts", "04-stress-curve.png"))
    with open(os.path.join(out, "summary.json"), "w") as f:
        json.dump([{k: v for k, v in s.items() if k != "series"} for s in all_s], f, indent=1)
    workbook(all_s, os.path.join(out, "super-app-performance.xlsx"))
    print("written to", out)


if __name__ == "__main__":
    main()
