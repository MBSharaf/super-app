const { chromium } = require(require('child_process').execSync('npm root -g').toString().trim() + '/playwright');
const OUT = process.argv[2], BASE = process.env.BASE || 'https://supermalaab.com/';
const routes = process.argv.slice(3);
(async () => {
  const b = await chromium.launch({ proxy: { server: process.env.HTTPS_PROXY } });
  const ctx = await b.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 3, isMobile: true, hasTouch: true, locale: 'ar-EG' });
  await ctx.addInitScript(() => { try { localStorage.setItem('superapp.lang', 'ar'); localStorage.setItem('superapp.theme', 'light'); localStorage.setItem('superapp.consent', JSON.stringify({ analytics: false, at: Date.now(), v: 99 })); } catch {} });
  const p = await ctx.newPage();
  p.on('pageerror', (e) => console.log('pageerror', e.message));
  await p.goto(BASE + 'index.html'); await p.waitForTimeout(1500);
  await p.evaluate(() => { try { SA.services.db.setSettings({ promoPops: { on: false } }); } catch (e) { console.log(e.message); } });
  for (const r of routes) {
    await p.goto(BASE + 'index.html#' + r); await p.waitForTimeout(1800);
    const name = r.replace(/[^a-z0-9]+/gi, '_') || 'home';
    await p.screenshot({ path: `${OUT}/${name}.png` });
    console.log('shot', name, await p.evaluate(() => document.title));
  }
  await b.close();
})();
