// Run click steps on the live site and screenshot after each. Usage: node steps.js <outdir> <startRoute> '<json steps>'
// step: {click:"text"} | {clickRe:"regex"} | {fill:["selector","value"]} | {scroll:px} | {wait:ms} | {shot:"tag", full?:true} | {list:true} | {goto:"#/route"} | {eval:"js"}
const { chromium } = require(require('child_process').execSync('npm root -g').toString().trim() + '/playwright');
const [OUT, START, JSONSTEPS] = process.argv.slice(2), BASE = process.env.BASE || 'https://supermalaab.com/';
const W = +(process.env.VW || 390), H = +(process.env.VH || 844), MOBILE = !process.env.DESKTOP;
(async () => {
  const b = await chromium.launch({ proxy: { server: process.env.HTTPS_PROXY, bypass: '127.0.0.1,localhost' } });
  const ctx = await b.newContext({ viewport: { width: W, height: H }, deviceScaleFactor: MOBILE ? 3 : 2, isMobile: MOBILE, hasTouch: MOBILE, reducedMotion: 'reduce' });
  await ctx.addInitScript(() => { try { localStorage.setItem('superapp.lang', 'ar'); localStorage.setItem('superapp.theme', 'light'); localStorage.setItem('superapp.consent', JSON.stringify({ analytics: false, at: Date.now(), v: 99 })); } catch {} });
  // never let a test booking reach the owner's automation webhooks
  await ctx.route(/ngrok|webhook/i, (r) => r.abort());
  const p = await ctx.newPage();
  p.on('pageerror', (e) => console.log('pageerror', e.message));
  await p.goto(BASE); await p.waitForTimeout(1200);
  await p.evaluate(() => { try { SA.services.db.setSettings({ promoPops: { on: false } }); } catch {} });
  await p.goto(BASE + START); await p.waitForTimeout(1800);
  // hide the floating chatbot button so it does not cover the UI in the reels
  await p.addStyleTag({ content: '.wa-fab { display: none !important; }' }).catch(() => {});
  for (const s of JSON.parse(JSONSTEPS)) {
    try {
      if (s.goto) { await p.goto(BASE + s.goto); await p.waitForTimeout(1600); }
      if (s.click) { await p.getByText(s.click, { exact: false }).first().click(); await p.waitForTimeout(900); }
      if (s.clickRe) { await p.locator('button, a, [role=button]').filter({ hasText: new RegExp(s.clickRe) }).first().click(); await p.waitForTimeout(900); }
      if (s.fill) { await p.fill(s.fill[0], s.fill[1]); await p.waitForTimeout(400); }
      if (s.scroll !== undefined) { await p.evaluate((y) => window.scrollTo(0, y), s.scroll); await p.waitForTimeout(500); }
      if (s.wait) await p.waitForTimeout(s.wait);
      if (s.eval) console.log('eval:', JSON.stringify(await p.evaluate(s.eval)));
      if (s.shot) { await p.screenshot({ path: `${OUT}/${s.shot}.png`, fullPage: !!s.full }); console.log('shot', s.shot, p.url().split('#')[1]); }
      if (s.list) console.log('buttons:', JSON.stringify(await p.$$eval('button, a, [role=button]', (els) => els.filter((e) => e.offsetParent).map((e) => e.textContent.trim().replace(/\s+/g, ' ')).filter(Boolean)).then((a) => a.slice(0, 60))));
    } catch (e) { console.log('step failed', JSON.stringify(s), e.message.split('\n')[0]); }
  }
  await b.close();
})();
