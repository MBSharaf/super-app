#!/usr/bin/env node
/**
 * Checks that the bundled site (SiteBundle: one script + one style sheet) looks and behaves exactly like the
 * unbundled one. For every page in the CEO "Every page" map (js/pages/devLinks.js), in English and Arabic, light and
 * dark, it opens both copies in Chromium, compares full-page screenshots pixel by pixel and lists script errors that
 * only one copy has. Staff pages are opened signed in as the demo owner / seller / admin.
 *
 * Usage: node tools/perf/bundle-check.js <unbundled base url> <bundled base url> [out dir]
 *   e.g. node tools/perf/bundle-check.js http://localhost:8081/super-app/ http://localhost:8082/super-app/ /tmp/bundle-check
 * Needs: playwright; python3 with Pillow for the pixel diff.
 */
'use strict';
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');

const [A, B, OUT = '/tmp/bundle-check'] = process.argv.slice(2);
const src = fs.readFileSync(path.join(__dirname, '..', '..', 'js', 'pages', 'devLinks.js'), 'utf8');
const routes = [...new Set([...src.matchAll(/'#(\/[^']*)'/g)].map((m) => m[1]))];
const who = (r) => (/^\/dash/.test(r) ? ['acc-o1', ''] : /^\/seller/.test(r) ? ['acc-s1', 'seller'] : /^\/(admin|ceo)/.test(r) ? ['acc-admin', ''] : null);
const VARIANTS = [{ lang: 'en', theme: 'light' }, { lang: 'ar', theme: 'dark' }];

async function shot(browser, base, route, v, file) {
  const ctx = await browser.newContext({ viewport: { width: 412, height: 915 }, reducedMotion: 'reduce', serviceWorkers: 'block', timezoneId: 'Africa/Cairo' });
  await ctx.addInitScript(({ lang, theme }) => {
    localStorage.setItem('superapp.lang', lang); localStorage.setItem('superapp.theme', theme);
    // Cookie choice already made: the banner appears on a timer, so it would land in some screenshots and not others
    localStorage.setItem('superapp.consent', JSON.stringify({ analytics: false, at: 1793440000000, v: '0.9' }));
    // Same randomness in both copies (time is frozen with page.clock below)
    let seed = 42; Math.random = () => { seed = (seed * 16807) % 2147483647; return (seed - 1) / 2147483646; };
  }, v);
  // Outside images and fonts fail at once in both copies, so their loading time cannot make the screenshots differ
  await ctx.route((url) => !['localhost', '127.0.0.1'].includes(url.hostname), (r) => r.abort());
  const page = await ctx.newPage();
  await page.clock.setFixedTime(new Date('2026-10-31T10:00:00+02:00'));
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
  await page.goto(base, { waitUntil: 'networkidle' });
  const acc = who(route);
  if (acc) await page.evaluate(([id, as]) => SA.services.dash.auth.signIn(id, { as }), acc).catch((e) => errors.push(`sign-in: ${e.message}`));
  await page.evaluate((r) => SA.core.router.go(r), route);
  await page.waitForLoadState('networkidle'); await page.waitForTimeout(700);
  await page.screenshot({ path: file, fullPage: true, animations: 'disabled', caret: 'hide' });
  await ctx.close();
  return errors.filter((e) => !/Failed to load resource|api\/v1|net::ERR/.test(e));
}

(async () => {
  fs.mkdirSync(OUT, { recursive: true });
  const browser = await chromium.launch();
  const rows = [];
  for (const v of VARIANTS) {
    for (const route of routes) {
      const id = `${v.lang}-${v.theme}${route.replace(/[^a-z0-9]+/gi, '_')}`;
      const [fa, fb] = [path.join(OUT, `${id}.a.png`), path.join(OUT, `${id}.b.png`)];
      const ea = await shot(browser, A, route, v, fa);
      const eb = await shot(browser, B, route, v, fb);
      const diff = execFileSync('python3', ['-c', `
from PIL import Image, ImageChops
a,b=Image.open('${fa}').convert('RGB'),Image.open('${fb}').convert('RGB')
if a.size!=b.size: print('size %sx%s vs %sx%s'%(a.size+b.size))
else:
  bb=ImageChops.difference(a,b).getbbox(); print('0' if not bb else 'pixels differ in %s'%(bb,))`]).toString().trim();
      const onlyB = eb.filter((e) => !ea.includes(e));
      const onlyA = ea.filter((e) => !eb.includes(e));
      rows.push({ variant: `${v.lang}/${v.theme}`, route, diff, onlyBundled: onlyB, onlyUnbundled: onlyA });
      if (diff === '0' && !onlyB.length) { fs.unlinkSync(fa); fs.unlinkSync(fb); }
      console.log(`${diff === '0' && !onlyB.length ? 'same ' : 'DIFF '} ${v.lang}/${v.theme} ${route} ${diff === '0' ? '' : diff} ${onlyB.length ? `errors only in bundle: ${onlyB.join(' | ')}` : ''}`);
    }
  }
  await browser.close();
  fs.writeFileSync(path.join(OUT, 'result.json'), JSON.stringify(rows, null, 1));
  const bad = rows.filter((r) => r.diff !== '0' || r.onlyBundled.length);
  console.log(`\n${rows.length - bad.length}/${rows.length} identical (pixels + errors). Details: ${OUT}/result.json`);
  process.exit(bad.length ? 1 : 0);
})();
