/* App-promo reel engine. Everything is a pure function of t, so _kit/render.js can step it frame by frame.
   AppReel({ dur, vo: offset of voiceover, hook, lines, scenes, phone, taps, scroll, pops, end, custom }) — see a reel's index.html. */
function bez(p1x, p1y, p2x, p2y) { const cx = 3 * p1x, bx = 3 * (p2x - p1x) - cx, ax = 1 - cx - bx, cy = 3 * p1y, by = 3 * (p2y - p1y) - cy, ay = 1 - cy - by; const sx = (t) => ((ax * t + bx) * t + cx) * t, sy = (t) => ((ay * t + by) * t + cy) * t; return (x) => { if (x <= 0) return 0; if (x >= 1) return 1; let t = x; for (let i = 0; i < 8; i++) { const d = (3 * ax * t + 2 * bx) * t + cx, e = sx(t) - x; if (Math.abs(e) < 1e-5 || !d) break; t -= e / d; } return sy(Math.min(1, Math.max(0, t))); }; }
const ease = bez(.2, .8, .2, 1), inout = bez(.65, 0, .35, 1), spring = bez(.34, 1.56, .64, 1);
const clamp = (v) => Math.min(1, Math.max(0, v)), prog = (t, a, d) => clamp((t - a) / d), lerp = (a, b, p) => a + (b - a) * p;
const $ = (s) => document.querySelector(s), $$ = (s) => [...document.querySelectorAll(s)];
const K = 528 / 390;   // site CSS px → phone screen px

const LOGO = (size = 150, bg = '#fff', fg = '#0D1411') => `<svg width="${size}" height="${size}" viewBox="0 0 34 34"><rect width="34" height="34" rx="11" fill="${bg}"/><svg x="8" y="8" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="${fg}" stroke-width="2.6" stroke-linecap="round"><path d="M17 7.2C15.9 6 14.3 5.4 12.4 5.4c-2.7 0-4.5 1.4-4.5 3.4 0 4.6 9.2 2.6 9.2 7 0 2-1.9 3.4-4.8 3.4-2.1 0-3.9-.7-5.3-2"/></svg><circle cx="24" cy="10" r="4" fill="#FF5B14"/></svg>`;
const ICON_WEB = '<svg width="44" height="44" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3a14 14 0 0 1 0 18M12 3a14 14 0 0 0 0 18"/></svg>';
const ICON_PHONE = '<svg width="44" height="44" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><rect x="6" y="2.5" width="12" height="19" rx="2.5"/><path d="M11 18.5h2"/></svg>';

function endCard(line) {
  return `${LOGO(170)}<div class="wm">SUPER MALAAB</div><div class="ar">سوبر ملعب</div>
    <div class="url">supermalaab.com</div>
    <div class="plat"><span>${ICON_WEB}موقع</span><span>${ICON_PHONE}تطبيق موبايل</span></div>
    ${line ? `<div class="line">${line}</div>` : ''}`;
}

// keyframe track: [[t, {x, y, s, r}], ...] → interpolated with inout easing between keys
function track(keys, t) {
  let a = keys[0], b = keys[0];
  for (let i = 0; i < keys.length; i++) { if (keys[i][0] <= t) { a = keys[i]; b = keys[i + 1] || keys[i]; } }
  const d = b[0] - a[0], p = d > 0 ? inout(clamp((t - a[0]) / Math.min(d, b[1].d || .6))) : 0;
  const o = {}; for (const k of ['x', 'y', 's', 'r']) o[k] = lerp(a[1][k] ?? (k === 's' ? 1 : 0), b[1][k] ?? (k === 's' ? 1 : 0), p);
  return o;
}

function AppReel(cfg) {
  const { dur, vo = .15, hook, lines, scenes, phone, taps = [], scroll = {}, pops = [], end, custom } = cfg;
  $('#end').innerHTML = endCard(end.line);
  const sb = document.createElement('div'); sb.className = 'sb';
  sb.innerHTML = '<span>9:41</span><span><svg width="30" height="18" viewBox="0 0 30 18"><rect x="0" y="10" width="5" height="8" rx="1" fill="#0D1411"/><rect x="7" y="6" width="5" height="12" rx="1" fill="#0D1411"/><rect x="14" y="3" width="5" height="15" rx="1" fill="#0D1411"/><rect x="21" y="0" width="5" height="18" rx="1" fill="#0D1411"/></svg> <svg width="44" height="20" viewBox="0 0 44 20"><rect x="1" y="1" width="38" height="18" rx="5" fill="none" stroke="#0D1411" stroke-width="2"/><rect x="4" y="4" width="28" height="12" rx="2.5" fill="#0D1411"/><rect x="40.5" y="7" width="2.5" height="6" rx="1" fill="#0D1411"/></svg></span>';
  $('#phone .scr').prepend(sb);
  window.ready = Promise.all($$('img').map((im) => im.decode().catch(() => {})));
  let lineKey = -2;
  window.render = function (t) {
    // background: circular wipe into each scene's colour
    let i = 0; scenes.forEach((s, k) => { if (s.t <= t) i = k; });
    const cur = scenes[i], prev = scenes[Math.max(0, i - 1)], w = ease(prog(t, cur.t, .4));
    $('#bg').style.background = i > 0 && w < 1 ? prev.bg : cur.bg;
    $('#bg2').style.background = cur.bg; $('#bg2').style.clipPath = i > 0 && w < 1 ? `circle(${w * 1500}px at 50% 55%)` : 'circle(0 at 50% 50%)';
    document.body.style.color = cur.fg || '#fff';

    // hook card, then the phone flies in
    const hk = hook && t < hook.until + .3;
    if (hook) {
      $('#hook').style.display = hk ? 'flex' : 'none';
      const hp = spring(prog(t, .05, .45)), out = ease(prog(t, hook.until - .1, .35));
      $$('#hook > *').forEach((el, k) => { const p = spring(prog(t, .05 + k * .18, .45)); el.style.opacity = clamp(p * 3) * (1 - out); el.style.transform = `translateY(${-out * 200}px) scale(${lerp(1.5, 1, p)})`; });
    }

    // phone position, scale and tilt
    const ph = track(phone, t), arrive = hook ? spring(prog(t, hook.until - .15, .6)) : 1;
    $('#phone').style.transform = `translate(${ph.x}px, ${ph.y + (1 - arrive) * 1400}px) scale(${ph.s}) rotate(${ph.r}deg)`;
    $('#phone').style.opacity = (hook && t < hook.until - .2) || t >= end.at + .4 ? 0 : 1;

    // which screen is on the phone (a short slide when it changes)
    let si = 0; cur.screen; const shown = scenes.map((s) => s.screen);
    let sIdx = 0; scenes.forEach((s, k) => { if (s.t <= t) sIdx = k; });
    const sNow = scenes[sIdx].screen, sPrev = sIdx > 0 ? scenes[sIdx - 1].screen : sNow, sw = ease(prog(t, scenes[sIdx].t, .35));
    $$('#phone .scr > [data-s]').forEach((el) => {
      const id = el.dataset.s;
      if (id === sNow) { el.style.display = 'block'; el.style.zIndex = 2; el.style.transform = sPrev !== sNow ? `translateX(${(1 - sw) * -528}px)` : 'none'; }
      else if (id === sPrev && sw < 1 && sPrev !== sNow) { el.style.display = 'block'; el.style.zIndex = 1; el.style.transform = `translateX(${sw * 160}px)`; }
      else el.style.display = 'none';
    });
    // tall pages scroll: scroll[id] = [[t0, t1, y0, y1], ...] in site CSS px
    for (const [id, segs] of Object.entries(scroll)) {
      let y = segs[0][2]; for (const [a, b, y0, y1] of segs) { if (t >= a) y = lerp(y0, y1, inout(prog(t, a, b - a))); }
      const mid = document.querySelector(`[data-s="${id}"] .mid`); if (mid) mid.style.transform = `translateY(${-y * K}px)`;
    }

    // touch dot: [[t, x, y]] in site CSS px; appears .3 s before, presses at t, fades after
    let tp = null; for (const tt of taps) if (t >= tt[0] - .35 && t < tt[0] + .35) tp = tt;
    const tap = $('#tap');
    if (tp) { const k = t - tp[0]; tap.style.display = 'block'; tap.style.left = `${tp[1] * K}px`; tap.style.top = `${tp[2] * K + 56}px`;
      tap.style.opacity = k < 0 ? clamp((k + .35) / .15) : 1 - clamp(k / .35); tap.style.transform = `scale(${k < 0 ? 1 : lerp(.8, 1.25, clamp(k / .35))})`; }
    else tap.style.display = 'none';

    // headline synced to the voiceover (also the caption)
    let L = -1; lines.forEach(([a, b], k) => { if (t >= a + vo && t < b + vo) L = k; });
    if (t < (hook ? hook.until : 0) || t >= end.at) L = -1;
    if (L !== lineKey) { $('#title').innerHTML = L >= 0 ? `<div class="t">${lines[L][2]}</div>${lines[L][3] ? `<div class="s">${lines[L][3]}</div>` : ''}` : ''; lineKey = L; }
    if (L >= 0) { const p = spring(prog(t, lines[L][0] + vo, .35)); $('#title').style.opacity = clamp(p * 3); $('#title').style.transform = `translateY(${(1 - p) * 40}px)`; }

    // pops: [{id, t0, t1, from: 'up' | 'scale'}]
    for (const pp of pops) { const el = document.getElementById(pp.id); const p = spring(prog(t, pp.t0, .4)), o = ease(prog(t, pp.t1 - .2, .25));
      el.style.display = t >= pp.t0 && t < pp.t1 + .05 ? '' : 'none'; el.style.opacity = clamp(p * 3) * (1 - o); el.style.transform = `${pp.base || ''} scale(${lerp(pp.from || 1.6, 1, p)})`; }

    if (custom) custom(t);

    // end card
    const e = ease(prog(t, end.at, .35));
    $('#end').style.display = t >= end.at ? 'flex' : 'none'; $('#end').style.opacity = e;
    $$('#end > *').forEach((el, k) => { const p = spring(prog(t, end.at + .1 + k * .12, .45)); el.style.opacity = clamp(p * 3); el.style.transform = `translateY(${(1 - p) * 60}px)`; });
  };
  window.DURATION = dur;
  window.render(0);
}
