// Renders _kit/covers.html (?r=01..07) into each reel folder as cover.png (1080×1920).
// Usage: node _kit/covers.js [01]
const fs = require('fs'), path = require('path');
const { chromium } = require(require('child_process').execSync('npm root -g').toString().trim() + '/playwright');
const reels = path.resolve(__dirname, '..');
const only = process.argv[2];
(async () => {
  const b = await chromium.launch();
  const p = await b.newPage({ viewport: { width: 1080, height: 1920 } });
  for (const dir of fs.readdirSync(reels).filter((d) => /^\d\d-/.test(d) && (!only || d.startsWith(only)))) {
    const r = dir.slice(0, 2);
    await p.goto('file://' + path.join(__dirname, 'covers.html') + '?r=' + r);
    await p.evaluate(() => document.fonts.ready); await p.evaluate(() => window.ready); await p.waitForTimeout(200);
    await p.screenshot({ path: path.join(reels, dir, 'cover.png') });
    console.log('cover', dir);
  }
  await b.close();
})();
