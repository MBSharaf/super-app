// Shared renderer for reels built on _kit: steps window.render(t) at 30 fps, screenshots each frame,
// encodes with ffmpeg and muxes <reel>/sfx.wav (loudness -14 LUFS) when present.
// Usage: node _kit/render.js <reel-dir> [out.mp4] [--frames=1.3,5 to save stills only]
const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');
const { chromium } = require(require('child_process').execSync('npm root -g').toString().trim() + '/playwright');

const FPS = 30;
const dir = path.resolve(process.argv[2]);
const pos = process.argv.slice(3).filter((a) => !a.startsWith('--'));
const out = path.resolve(dir, pos[0] || path.basename(dir).replace(/^\d+-/, (m) => 'reel-' + m) + '.mp4');
const stills = (process.argv.find((a) => a.startsWith('--frames=')) || '').slice(9);

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1080, height: 1920 } });
  page.on('pageerror', (e) => console.log('page error:', e.message));
  await page.goto('file://' + path.join(dir, 'index.html'));
  await page.evaluate(() => document.fonts.ready);
  await page.evaluate(() => window.ready);
  const dur = await page.evaluate(() => window.DURATION);

  if (stills) {
    for (const s of stills.split(',')) {
      await page.evaluate((t) => window.render(t), +s);
      await page.screenshot({ path: path.join(dir, `still-${s}.png`) });
    }
    await browser.close();
    return;
  }

  const frames = path.join(dir, '.frames');
  fs.rmSync(frames, { recursive: true, force: true });
  fs.mkdirSync(frames);
  const n = Math.round(dur * FPS);
  for (let i = 0; i < n; i++) {
    await page.evaluate((t) => window.render(t), i / FPS);
    await page.screenshot({ path: path.join(frames, `f${String(i).padStart(4, '0')}.jpg`), type: 'jpeg', quality: 95 });
  }
  await browser.close();

  const sfx = path.join(dir, 'sfx.wav');
  const args = ['-y', '-loglevel', 'error', '-framerate', String(FPS), '-i', path.join(frames, 'f%04d.jpg')];
  if (fs.existsSync(sfx)) args.push('-i', sfx, '-af', 'loudnorm=I=-14:TP=-1:LRA=11', '-ar', '48000', '-c:a', 'aac', '-b:a', '192k', '-shortest');
  args.push('-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-crf', '18', '-preset', 'slow', '-movflags', '+faststart', out);
  execFileSync('ffmpeg', args, { stdio: 'inherit' });
  fs.rmSync(frames, { recursive: true, force: true });
  console.log('wrote', out);
})();
