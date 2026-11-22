// Screenshots _app/covers.html?r=<id> to each reel's cover.png (1080×1920) and cover-grid.jpg (the centre 1080×1350).
// Usage: node marketing/reels/_app/covers.js [08 09 …]   (default: all eight)
const fs = require('fs'), path = require('path');
const { chromium } = require(require('child_process').execSync('npm root -g').toString().trim() + '/playwright');
const ROOT = path.resolve(__dirname, '..');
const IDS = process.argv.slice(2).length ? process.argv.slice(2) : ['08', '09', '10', '11', '12', '13', '14', '15'];
(async () => {
  const b = await chromium.launch();
  const p = await b.newPage({ viewport: { width: 1080, height: 1920 } });
  for (const id of IDS) {
    const dir = fs.readdirSync(ROOT).find((d) => d.startsWith(id + '-'));
    await p.goto('file://' + path.join(__dirname, 'covers.html') + '?r=' + id);
    await p.evaluate(() => window.ready);
    await p.waitForTimeout(200);
    await p.screenshot({ path: path.join(ROOT, dir, 'cover.png') });
    await p.screenshot({ path: path.join(ROOT, dir, 'cover-grid.jpg'), type: 'jpeg', quality: 92, clip: { x: 0, y: 285, width: 1080, height: 1350 } });
    console.log('cover', dir);
  }
  await b.close();
})();
