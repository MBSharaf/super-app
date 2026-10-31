#!/usr/bin/env node
/**
 * Real-browser page-load metrics (Chromium via Playwright) to go with the JMeter load tests:
 * JMeter downloads the files but does not run JavaScript, so this measures what a visitor sees.
 * For each network profile and page it opens a fresh browser context (empty cache) 3 times and keeps the median of:
 * TTFB, First Contentful Paint, DOMContentLoaded, load, Largest Contentful Paint, Total Blocking Time (long tasks),
 * request count, transferred bytes and JS heap. A 4th visit in the same context measures a warm-cache reload.
 *
 * Usage: node tools/perf/browser-metrics.js [baseUrl=http://localhost:8080/super-app/] [out=browser-metrics.json]
 */
'use strict';
const { chromium } = require('playwright');
const fs = require('fs');

const BASE = process.argv[2] || 'http://localhost:8080/super-app/';
const OUT = process.argv[3] || 'browser-metrics.json';
const RUNS = 3;
// Lighthouse-style profiles. Throughput in bytes/s.
const PROFILES = [
  { name: 'Desktop (no throttling)', net: null, cpu: 1 },
  { name: 'Mobile 4G (9 Mbps, 85 ms RTT, 2x CPU)', net: { latency: 85, down: 9e6 / 8, up: 1.5e6 / 8 }, cpu: 2 },
  { name: 'Slow 4G (1.6 Mbps, 150 ms RTT, 4x CPU)', net: { latency: 150, down: 1.6e6 / 8, up: 0.75e6 / 8 }, cpu: 4 },
];
const PAGES = [
  { name: 'Home', path: '' },
  { name: 'Pitches (deep link)', path: 'pitches' },
  { name: 'Shop (deep link)', path: 'shop' },
];

const collect = () => new Promise((resolve) => {
  const nav = performance.getEntriesByType('navigation')[0];
  const paint = performance.getEntriesByName('first-contentful-paint')[0];
  let lcp = 0; let tbt = 0;
  new PerformanceObserver((l) => l.getEntries().forEach((e) => { lcp = Math.max(lcp, e.startTime); })).observe({ type: 'largest-contentful-paint', buffered: true });
  new PerformanceObserver((l) => l.getEntries().forEach((e) => { tbt += Math.max(0, e.duration - 50); })).observe({ type: 'longtask', buffered: true });
  setTimeout(() => {
    const res = performance.getEntriesByType('resource');
    resolve({
      ttfb: nav.responseStart, fcp: paint ? paint.startTime : null, dcl: nav.domContentLoadedEventEnd, load: nav.loadEventEnd, lcp, tbt,
      requests: res.length + 1, transferKB: (res.reduce((s, r) => s + (r.transferSize || 0), nav.transferSize || 0)) / 1024,
      decodedKB: (res.reduce((s, r) => s + (r.decodedBodySize || 0), nav.decodedBodySize || 0)) / 1024,
      heapMB: performance.memory ? performance.memory.usedJSHeapSize / 1048576 : null,
      redirectedFrom404: sessionStorage.getItem('superapp.redirect') !== null || document.referrer.includes('/super-app/'),
    });
  }, 1500);
});

async function visit(context, profile, url) {
  const page = await context.newPage();
  const cdp = await context.newCDPSession(page);
  if (profile.net) await cdp.send('Network.emulateNetworkConditions', { offline: false, latency: profile.net.latency, downloadThroughput: profile.net.down, uploadThroughput: profile.net.up });
  await cdp.send('Emulation.setCPUThrottlingRate', { rate: profile.cpu });
  const t0 = Date.now();
  await page.goto(url, { waitUntil: 'load', timeout: 120000 });
  // A deep link first gets 404.html, whose script opens the app: wait until the real app has loaded.
  for (let i = 0; i < 60; i++) {
    const ready = await page.evaluate(() => document.readyState === 'complete' && !!document.querySelector('script[src*="app.js"]')).catch(() => false);
    if (ready) break;
    await page.waitForTimeout(250);
  }
  await page.waitForLoadState('networkidle', { timeout: 120000 }).catch(() => {});
  const m = await page.evaluate(collect);
  m.wallMs = Date.now() - t0; // includes the 404.html -> index.html hop for deep links
  m.finalUrl = page.url();
  await page.close();
  return m;
}

const median = (xs) => { const v = xs.filter((x) => x != null).sort((a, b) => a - b); return v.length ? v[Math.floor(v.length / 2)] : null; };

(async () => {
  const browser = await chromium.launch();
  const results = [];
  for (const profile of PROFILES) {
    for (const pg of PAGES) {
      const cold = []; let warm = null;
      for (let i = 0; i < RUNS; i++) {
        const context = await browser.newContext({ viewport: { width: 412, height: 915 }, serviceWorkers: 'block' });
        cold.push(await visit(context, profile, BASE + pg.path));
        if (i === RUNS - 1) warm = await visit(context, profile, BASE + pg.path);
        await context.close();
      }
      const row = { profile: profile.name, page: pg.name, runs: RUNS, warm };
      for (const k of Object.keys(cold[0])) if (typeof cold[0][k] === 'number') row[k] = median(cold.map((c) => c[k]));
      results.push(row);
      console.log(`${profile.name} | ${pg.name} | load ${Math.round(row.load)} ms | LCP ${Math.round(row.lcp)} ms | TBT ${Math.round(row.tbt)} ms | ${row.requests} req | ${Math.round(row.transferKB)} KB | warm load ${Math.round(warm.load)} ms`);
    }
  }
  await browser.close();
  fs.writeFileSync(OUT, JSON.stringify({ base: BASE, when: new Date().toISOString(), results }, null, 2));
})();
