// Scroll a page like a user and stitch a tall content image, plus the fixed header and tab bar strips.
// Usage: node tall.js <outPrefix> <route> [maxCssHeight] [preSteps json]   (BASE env: live site by default)
const { chromium } = require(require('child_process').execSync('npm root -g').toString().trim() + '/playwright');
const { execFileSync } = require('child_process');
const [PRE, ROUTE, MAXH = '3000', PRESTEPS = '[]'] = process.argv.slice(2), BASE = process.env.BASE || 'https://supermalaab.com/';
const W = 390, H = 844, DPR = 3;
(async () => {
  const b = await chromium.launch({ proxy: { server: process.env.HTTPS_PROXY, bypass: '127.0.0.1,localhost' } });
  const ctx = await b.newContext({ viewport: { width: W, height: H }, deviceScaleFactor: DPR, isMobile: true, hasTouch: true, reducedMotion: 'reduce' });
  await ctx.addInitScript(() => { try { localStorage.setItem('superapp.lang', 'ar'); localStorage.setItem('superapp.theme', 'light'); localStorage.setItem('superapp.consent', JSON.stringify({ analytics: false, at: Date.now(), v: 99 })); } catch {} });
  await ctx.route(/ngrok|webhook/i, (r) => r.abort());
  const p = await ctx.newPage();
  await p.goto(BASE); await p.evaluate(() => { try { SA.services.db.setSettings({ promoPops: { on: false } }); } catch {} });
  await p.goto(BASE + ROUTE); await p.waitForTimeout(2200);
  await p.addStyleTag({ content: '.wa-fab { display: none !important; } html { scroll-behavior: auto !important; }' });
  for (const s of JSON.parse(PRESTEPS)) {
    if (s.clickRe) { await p.locator('button, a, [role=button]').filter({ hasText: new RegExp(s.clickRe) }).first().click(); await p.waitForTimeout(1200); }
    if (s.evalClick) { await p.evaluate((txt) => { const b = [...document.querySelectorAll('button')].find((b) => b.textContent.trim().startsWith(txt)); b.click(); }, s.evalClick); await p.waitForTimeout(900); }
  }
  // fixed bars: the header at the top and the tab bar at the bottom of the viewport
  const bars = await p.evaluate(([w, h]) => {
    let top = 0, bot = 0;
    for (const el of document.querySelectorAll('body *')) {
      const cs = getComputedStyle(el); if (cs.position !== 'fixed' && cs.position !== 'sticky') continue;
      const r = el.getBoundingClientRect(); if (r.width < w * .9 || r.height < 20 || r.height > h / 3) continue;
      if (r.top <= 1) top = Math.max(top, r.bottom); else if (r.bottom >= h - 1) bot = Math.max(bot, h - r.top);
    }
    return { top: Math.round(top), bot: Math.round(bot), docH: document.documentElement.scrollHeight };
  }, [W, H]);
  await p.screenshot({ path: `${PRE}-view.png` });
  const step = H - bars.top - bars.bot, maxH = Math.min(+MAXH, bars.docH), parts = [];
  for (let y = 0, k = 0; y < maxH; y += step, k++) {
    await p.evaluate((yy) => window.scrollTo(0, yy), y); await p.waitForTimeout(700);
    const real = await p.evaluate(() => scrollY);
    const f = `${PRE}-part${k}.png`; await p.screenshot({ path: f }); parts.push([f, real]);
    if (real < y) break;   // reached the bottom
  }
  await b.close();
  console.log(JSON.stringify({ bars, parts: parts.map(([f, y]) => [f.split('/').pop(), y]) }));
  // stitch with python (crop the content band of each part at its real offset)
  execFileSync('python3', ['-c', `
import json,sys
from PIL import Image
bars=${JSON.stringify(bars)}; parts=${JSON.stringify(parts)}; D=${DPR}; H=${H}
top,bot=bars['top'],bars['bot']; band=H-top-bot
last=parts[-1][1]; total=last+band
out=Image.new('RGB',(390*D,total*D),'white')
for f,y in parts:
    im=Image.open(f).crop((0,top*D,390*D,(H-bot)*D)); out.paste(im,(0,y*D))
out.save('${PRE}-tall.png')
v=Image.open('${PRE}-view.png'); v.crop((0,0,390*D,top*D)).save('${PRE}-top.png'); v.crop((0,(H-bot)*D,390*D,H*D)).save('${PRE}-bot.png')
print('tall',out.size,'top',top,'bot',bot)
`], { stdio: 'inherit' });
})();
