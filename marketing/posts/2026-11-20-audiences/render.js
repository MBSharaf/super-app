// Renders each post-*.html to a 1080×1350 PNG (and a JPG for upload). Usage: node render.js [post-01-players]
const fs = require('fs'), path = require('path');
const { chromium } = require(require('child_process').execSync('npm root -g').toString().trim() + '/playwright');
const only = process.argv[2];
(async () => {
  const b = await chromium.launch();
  const p = await b.newPage({ viewport: { width: 1080, height: 1350 } });
  for (const f of fs.readdirSync(__dirname).filter((f) => /^post-.*\.html$/.test(f) && (!only || f.startsWith(only)))) {
    await p.goto('file://' + path.join(__dirname, f));
    await p.evaluate(() => document.fonts.ready);
    await p.waitForTimeout(300);
    const base = path.join(__dirname, f.replace('.html', ''));
    await p.screenshot({ path: base + '.png' });
    await p.screenshot({ path: base + '.jpg', type: 'jpeg', quality: 92 });
    console.log('wrote', path.basename(base) + '.png/.jpg');
  }
  await b.close();
})();
