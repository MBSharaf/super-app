/* Shared timing kit for collage reels. A reel page has #hook (.hw children), #step, #cap, #end (.ew children), #brand,
   and calls Reel({ T: { steps: [..], end }, caps: [[title, note], ..], scene(t) }). Everything is a pure function of t,
   so _kit/render.js can step it frame by frame. */
function bezier(p1x, p1y, p2x, p2y) { const cx = 3 * p1x, bx = 3 * (p2x - p1x) - cx, ax = 1 - cx - bx, cy = 3 * p1y, by = 3 * (p2y - p1y) - cy, ay = 1 - cy - by; const sx = (t) => ((ax * t + bx) * t + cx) * t, sy = (t) => ((ay * t + by) * t + cy) * t; return (x) => { if (x <= 0) return 0; if (x >= 1) return 1; let t = x; for (let i = 0; i < 8; i++) { const d = (3 * ax * t + 2 * bx) * t + cx, e = sx(t) - x; if (Math.abs(e) < 1e-5 || !d) break; t -= e / d; } return sy(Math.min(1, Math.max(0, t))); }; }
const ease = bezier(.2, .8, .2, 1), spring = bezier(.34, 1.56, .64, 1);
const clamp = (v) => Math.min(1, Math.max(0, v)), prog = (t, a, d) => clamp((t - a) / d), lerp = (a, b, p) => a + (b - a) * p;
// stop-motion jitter: changes 12 times a second
const jit = (seed, t, amp) => { const k = Math.floor(t * 12) * 97 + seed * 131; const r = Math.sin(k * 12.9898) * 43758.5453; return ((r - Math.floor(r)) - .5) * 2 * amp; };
const $ = (s) => document.querySelector(s), $$ = (s) => [...document.querySelectorAll(s)];
// show/hide without losing the element's own inline display (flex etc.)
function show(el, o, tf) { if (el.dataset.d === undefined) el.dataset.d = el.style.display; el.style.opacity = o; el.style.display = o > 0.001 ? el.dataset.d : 'none'; if (tf !== undefined) el.style.transform = tf; }
// pop in a group of collage pieces: spring scale-down from 1.6 with a fixed tilt per piece
function popIn(els, t, at, tilts, seed = 0, gap = .14) { els.forEach((el, i) => { const p = spring(prog(t, at + i * gap, .32)); el.style.opacity = clamp(p * 4); el.style.transform = `translate(${jit(i + seed, t, 3)}px,${jit(i + seed + 9, t, 3)}px) rotate(${tilts[i % tilts.length]}deg) scale(${lerp(1.6, 1, p)})`; }); }

const MARK = '<svg width="120" height="120" viewBox="0 0 34 34"><rect width="34" height="34" rx="11" fill="var(--mark)"/><svg x="8" y="8" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2.6" stroke-linecap="round"><path d="M17 7.2C15.9 6 14.3 5.4 12.4 5.4c-2.7 0-4.5 1.4-4.5 3.4 0 4.6 9.2 2.6 9.2 7 0 2-1.9 3.4-4.8 3.4-2.1 0-3.9-.7-5.3-2"/></svg><circle cx="24" cy="10" r="4" fill="#FF5B14"/></svg>';

function Reel({ dur = 15, T, caps, scene }) {
  const S = T.steps, n = S.length;
  const brand = $('#brand'); if (brand && !brand.innerHTML.trim()) brand.innerHTML = `${MARK}<div class="wm">SUPER APP</div><div class="ln">قريب · كل الكورة في مكان واحد</div>`;
  window.ready = Promise.all($$('img').map((im) => im.decode().catch(() => {})));
  let capKey = -1;
  window.render = function (t) {
    const hookEnd = S[0];
    const hookOut = prog(t, hookEnd - .25, .3);
    show($('#hook'), t < hookEnd + .1 ? 1 - hookOut : 0, `translateX(${-ease(hookOut) * 1100}px)`);
    popIn($$('.hw'), t, .05, [-2, 1.5, 1, -1], 0, .16);

    let k = -1; for (let i = 0; i < n; i++) if (t >= S[i] && t < (S[i + 1] ?? T.end)) k = i;
    show($('#step'), k >= 0 ? 1 : 0); if (k >= 0) $('#step').textContent = `0${k + 1} / 0${n}`;
    if (k !== capKey) { $('#cap').innerHTML = k >= 0 ? `<span class="block b-paper cw" style="font-size:${caps[k][2] || 96}px">${caps[k][0]}</span><span class="note cw" style="font-size:44px">${caps[k][1]}</span>` : ''; capKey = k; }
    if (k >= 0) $$('.cw').forEach((el, i) => { const p = spring(prog(t, S[k] + .15 + i * .14, .32)); el.style.opacity = clamp(p * 4); el.style.transform = `rotate(${[-1.5, 1.2][i] + jit(i + 3, t, .6)}deg) scale(${lerp(1.5, 1, p)})`; });

    scene(t, k);

    const endOn = t >= T.end;
    show($('#end'), endOn ? 1 : 0);
    popIn($$('.ew'), t, T.end + .05, [-2, 1.5, 1, -1], 20);
    const br = ease(prog(t, T.end + .5, .4));
    show(brand, endOn ? br : 0, `translateY(${(1 - br) * 40}px)`);
  };
  window.DURATION = dur;
  window.render(0);
}

// voiceover subtitles: lines = [[start, end, html], ...]; the line on screen pops in and stays until its end
function subtitles(el, lines, t) {
  const k = lines.findIndex(([a, b]) => t >= a && t < b);
  if (el.dataset.k !== String(k)) { el.innerHTML = k >= 0 ? `<span>${lines[k][2]}</span>` : ''; el.dataset.k = String(k); }
  const s = el.firstElementChild; if (!s) return;
  const p = spring(prog(t, lines[k][0], .25)); s.style.opacity = clamp(p * 3); s.style.transform = `scale(${lerp(.85, 1, p)})`;
}
