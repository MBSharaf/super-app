// Renders every carousel-*.html slide (?s=1..6) to PNG + JPG. Usage: node carousel.js [carousel-fantasy]
const fs = require('fs'), path = require('path');
const { chromium } = require(require('child_process').execSync('npm root -g').toString().trim() + '/playwright');
const only = process.argv[2];
(async () => {
  const b = await chromium.launch();
  const p = await b.newPage({ viewport: { width: 1080, height: 1350 } });
  for (const f of fs.readdirSync(__dirname).filter((f) => /^carousel-.*\.html$/.test(f) && (!only || f.startsWith(only)))) {
    const dir = path.join(__dirname, f.replace('.html', ''));
    fs.mkdirSync(dir, { recursive: true });
    for (let s = 1; s <= 6; s++) {
      await p.goto('file://' + path.join(__dirname, f) + '?s=' + s);
      await p.evaluate(() => document.fonts.ready);
      await p.waitForTimeout(250);
      await p.screenshot({ path: path.join(dir, `slide-${s}.png`) });
      await p.screenshot({ path: path.join(dir, `slide-${s}.jpg`), type: 'jpeg', quality: 92 });
    }
    console.log('wrote', path.basename(dir));
  }
  await b.close();
})();
