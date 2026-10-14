/**
 * Garment mockups — studio-style product renders drawn as SVG: tee, hoodie and cap in any colour, with the
 * artwork printed on the front or back. Fabric folds, lighting, cotton texture, rib knit and stitching are layered
 * on top of the print so it reads as ink on fabric. Placement is relative to the 400×440 canvas:
 * { x: centre 0–1, y: centre 0–1, w: width 0–1 } — the same numbers go to the print partner.
 *
 * SA.ui.garment3d mounts an interactive viewer (drag to turn, front/back faces, idle float, moving light).
 */
(function (SA) {
  const { esc } = SA.core.utils;
  const W = 400, H = 440;

  /* ---------- Colour helpers ---------- */
  const rgb = (hex) => { let h = String(hex || '#ffffff').replace('#', ''); if (h.length === 3) h = h.split('').map((x) => x + x).join(''); const n = parseInt(h.padEnd(6, '0'), 16); return [(n >> 16) & 255, (n >> 8) & 255, n & 255]; };
  const lum = (hex) => { const [r, g, b] = rgb(hex); return (0.299 * r + 0.587 * g + 0.114 * b) / 255; };
  const mix = (hex, to, t) => { const a = rgb(hex), b = rgb(to); return `rgb(${a.map((v, i) => Math.round(v + (b[i] - v) * t)).join(',')})`; };

  /* ---------- Shapes (front / back) ---------- */
  const TEE = {
    body: 'M158 34C140 40 118 46 96 56L36 90Q29 94 31 101L58 166Q61 174 69 171L107 156C110 200 108 260 106 300C105 340 104 380 106 412Q200 425 294 412C296 380 295 340 294 300C292 260 290 200 293 156L331 171Q339 174 342 166L369 101Q371 94 364 90L304 56C282 46 260 40 242 34C236 63 164 63 158 34Z',
    bodyBack: 'M158 34C140 40 118 46 96 56L36 90Q29 94 31 101L58 166Q61 174 69 171L107 156C110 200 108 260 106 300C105 340 104 380 106 412Q200 425 294 412C296 380 295 340 294 300C292 260 290 200 293 156L331 171Q339 174 342 166L369 101Q371 94 364 90L304 56C282 46 260 40 242 34C232 46 168 46 158 34Z',
    inner: 'M158 34C172 24 228 24 242 34C236 63 164 63 158 34Z',
    collar: { front: 'M156 33C164 66 236 66 244 33L236 31C230 56 170 56 164 31Z', back: 'M156 33C166 47 234 47 244 33L238 31C230 41 170 41 162 31Z' },
    seams: 'M96 56C104 92 110 124 107 156M304 56C296 92 290 124 293 156',
    stitch: 'M66 160L104 145M334 160L296 145M110 400Q200 412 290 400',
    darkFolds: ['M108 160C130 200 140 250 128 300L112 300C116 250 112 200 104 160Z', 'M292 160C270 200 260 250 272 300L288 300C284 250 288 200 296 160Z', 'M140 330C170 342 230 342 260 330L262 340C230 354 170 354 138 340Z', 'M70 108C82 120 92 136 98 152L90 154C82 138 72 124 60 112Z', 'M330 108C318 120 308 136 302 152L310 154C318 138 328 124 340 112Z', 'M150 384C175 392 225 392 250 384L252 392C226 402 174 402 148 392Z'],
    lightFolds: ['M175 80C215 85 230 140 222 210C200 230 160 220 150 180C140 120 150 82 175 80Z', 'M226 180C236 240 234 310 242 382L252 382C246 310 248 240 238 180Z', 'M48 98C58 120 66 140 72 160L64 162C58 142 50 122 42 102Z'],
  };
  const HOODIE = {
    body: 'M150 46C128 52 108 58 92 70C72 120 56 200 44 300L40 350Q41 362 52 362L82 360Q90 359 90 350L96 300C100 250 104 210 110 180C108 250 106 330 106 392L106 414Q200 427 294 414L294 392C294 330 292 250 290 180C296 210 300 250 304 300L310 350Q310 359 318 360L348 362Q359 362 360 350L356 300C344 200 328 120 308 70C292 58 272 52 250 46C240 56 160 56 150 46Z',
    hoodFront: 'M150 50C140 12 170 -4 200 -4C230 -4 260 12 250 50C242 80 222 98 200 100C178 98 158 80 150 50Z',
    hoodInner: 'M166 46C163 20 181 8 200 8C219 8 237 20 234 46C228 70 214 84 200 86C186 84 172 70 166 46Z',
    hoodBack: 'M150 46C132 36 142 -4 200 -4C258 -4 268 36 250 46C262 92 240 132 200 138C160 132 138 92 150 46Z',
    seams: 'M92 70C100 100 106 140 110 180M308 70C300 100 294 140 290 180',
    cuffs: ['M41 336L44 300Q66 304 96 300L90 336Q66 340 41 336Z', 'M359 336L356 300Q334 304 304 300L310 336Q334 340 359 336Z'],
    waist: 'M106 392Q200 404 294 392L294 414Q200 427 106 414Z',
    pocket: 'M132 300L268 300Q282 330 290 384L110 384Q118 330 132 300Z',
    pocketStitch: 'M136 306L264 306Q276 332 283 378L117 378Q124 332 136 306Z',
    darkFolds: ['M110 182C130 220 136 260 126 300L112 300C114 260 112 220 104 182Z', 'M290 182C270 220 264 260 274 300L288 300C286 260 288 220 296 182Z', 'M58 230C70 240 82 252 92 266L86 270C76 258 64 248 52 240Z', 'M342 230C330 240 318 252 308 266L314 270C324 258 336 248 348 240Z', 'M66 290C74 296 84 300 94 302L92 310C82 308 72 302 64 296Z', 'M334 290C326 296 316 300 306 302L308 310C318 308 328 302 336 296Z', 'M118 380C140 386 160 388 200 388L200 384C160 384 140 382 118 376Z'],
    lightFolds: ['M178 100C220 104 236 150 226 220C204 236 166 228 156 190C146 140 156 102 178 100Z', 'M60 120C64 170 58 220 52 280L60 280C66 220 72 170 70 120Z', 'M340 120C336 170 342 220 348 280L340 280C334 220 328 170 330 120Z'],
  };
  const CAP = {
    dome: 'M72 262C64 170 120 104 200 100C280 104 336 170 328 262Z',
    panels: 'M200 100C186 150 182 210 186 262M200 100C214 150 218 210 214 262M200 100C150 120 122 180 118 260M200 100C250 120 278 180 282 260',
    brim: 'M56 258C120 236 280 236 344 258C362 300 252 334 200 336C148 334 38 300 56 258Z',
    brimUnder: 'M60 270C110 252 290 252 340 270C346 290 300 312 200 318C100 312 54 290 60 270Z',
    brimStitch: 'M70 266C130 248 270 248 330 266M80 278C140 262 260 262 320 278',
    band: 'M72 262C140 250 260 250 328 262L330 272C260 260 140 260 70 272Z',
    darkFolds: ['M72 262C70 200 92 150 130 120L138 128C104 158 86 206 86 262Z', 'M328 262C330 200 308 150 270 120L262 128C296 158 314 206 314 262Z'],
    lightFolds: ['M170 112C210 108 236 130 236 170C214 190 172 186 160 160C152 136 156 116 170 112Z'],
  };

  /** Print area per kind (canvas fractions) */
  const AREAS = { tee: { x: 0.31, y: 0.2, w: 0.38, h: 0.62 }, hoodie: { x: 0.3, y: 0.24, w: 0.4, h: 0.42 }, cap: { x: 0.3, y: 0.3, w: 0.4, h: 0.28 } };

  let uid = 0;
  /**
   * kind: tee | hoodie | cap · hex: garment colour · art: image URL · place: {x,y,w} · side: front | back
   * artRatio: height/width of the artwork · showArea: dashed print area (studio) · shadow: ground shadow · bg: background colour
   */
  const garment = ({ kind = 'tee', hex = '#F7F7F5', art = '', place = null, side = 'front', artRatio = 1, showArea = false, label = '', shadow = true, bg = '' } = {}) => {
    const id = `g${++uid}`;
    const L = lum(hex);
    const light = L > 0.6;
    const dark = L < 0.22;
    const edge = light ? 'rgba(0,0,0,.16)' : 'rgba(0,0,0,.35)';
    const stitch = light ? 'rgba(0,0,0,.28)' : dark ? 'rgba(255,255,255,.2)' : 'rgba(0,0,0,.3)';
    const deep = mix(hex, '#000', light ? 0.32 : 0.45);
    const rib = mix(hex, '#000', 0.08);
    const shadeK = dark ? 1.25 : light ? 0.75 : 1;
    const S = kind === 'hoodie' ? HOODIE : kind === 'cap' ? CAP : TEE;
    const back = side === 'back';
    const bodyPath = kind === 'cap' ? CAP.dome : kind === 'tee' && back ? TEE.bodyBack : S.body;
    const a = AREAS[kind] || AREAS.tee;

    const defs = `
      <clipPath id="${id}c"><path d="${bodyPath}"/>${kind === 'cap' ? `<path d="${CAP.brim}"/>` : ''}</clipPath>
      <linearGradient id="${id}h" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#000" stop-opacity="${0.34 * shadeK}"/><stop offset=".2" stop-color="#000" stop-opacity="0"/><stop offset=".5" stop-color="#fff" stop-opacity="${light ? 0.05 : dark ? 0.03 : 0.06}"/><stop offset=".8" stop-color="#000" stop-opacity="0"/><stop offset="1" stop-color="#000" stop-opacity="${0.38 * shadeK}"/></linearGradient>
      <linearGradient id="${id}v" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff" stop-opacity="${light ? 0.12 : dark ? 0.05 : 0.08}"/><stop offset=".55" stop-color="#fff" stop-opacity="0"/><stop offset="1" stop-color="#000" stop-opacity="${0.2 * shadeK}"/></linearGradient>
      <radialGradient id="${id}r" cx=".36" cy=".22" r=".55"><stop offset="0" stop-color="#fff" stop-opacity="${light ? 0.22 : dark ? 0.1 : 0.16}"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></radialGradient>
      <filter id="${id}b" x="-20%" y="-20%" width="140%" height="140%"><feGaussianBlur stdDeviation="7"/></filter>
      <filter id="${id}s" x="-30%" y="-200%" width="160%" height="500%"><feGaussianBlur stdDeviation="9"/></filter>
      <filter id="${id}n"><feTurbulence type="fractalNoise" baseFrequency=".9" numOctaves="2" seed="4"/><feColorMatrix type="saturate" values="0"/></filter>
      <filter id="${id}d" x="-5%" y="-5%" width="110%" height="110%"><feTurbulence type="fractalNoise" baseFrequency=".035" numOctaves="1" seed="7" result="t"/><feDisplacementMap in="SourceGraphic" in2="t" scale="3.5" xChannelSelector="R" yChannelSelector="G"/></filter>
      <pattern id="${id}k" width="3.2" height="12" patternUnits="userSpaceOnUse"><rect width="1.3" height="12" fill="#000" opacity="${dark ? 0.35 : 0.14}"/><rect x="1.6" width="1" height="12" fill="#fff" opacity="${dark ? 0.06 : 0.1}"/></pattern>`;

    const p = [];
    if (bg) p.push(`<rect x="-10" y="-10" width="${W + 20}" height="${H + 20}" fill="${bg}"/>`);
    if (shadow) p.push(`<ellipse cx="200" cy="${kind === 'cap' ? 334 : 428}" rx="${kind === 'cap' ? 150 : 140}" ry="10" fill="#000" opacity=".28" filter="url(#${id}s)"/>`);

    // Behind the body: hood on the back (drawn later), inside of the neck on the front
    if (kind === 'tee' && !back) p.push(`<path d="${TEE.inner}" fill="${deep}"/><path d="${TEE.inner}" fill="url(#${id}k)" opacity=".5"/>`);
    if (kind === 'hoodie' && !back) p.push(`<path d="${HOODIE.hoodFront}" fill="${hex}"/><path d="${HOODIE.hoodFront}" fill="url(#${id}h)"/>`);

    // Body
    p.push(`<path d="${bodyPath}" fill="${hex}"/>`);
    if (kind === 'cap') p.push(`<path d="${CAP.brimUnder}" fill="${deep}"/><path d="${CAP.brim}" fill="${hex}"/><path d="${CAP.brim}" fill="url(#${id}v)"/>`);

    // Print (follows the fabric slightly), clipped to the garment
    if (art && place) {
      const w = place.w * W, h = w * artRatio;
      p.push(`<g clip-path="url(#${id}c)"><image href="${esc(art)}" x="${place.x * W - w / 2}" y="${place.y * H - h / 2}" width="${w}" height="${h}" preserveAspectRatio="xMidYMid meet" filter="url(#${id}d)" opacity=".97" class="g-art"/></g>`);
    }

    // Fabric light: folds, gradients, texture — on top of the print so the ink sits in the fabric
    p.push(`<g clip-path="url(#${id}c)">
      <g filter="url(#${id}b)">${S.darkFolds.map((d) => `<path d="${d}" fill="#000" opacity="${0.2 * shadeK}"/>`).join('')}${S.lightFolds.map((d) => `<path d="${d}" fill="#fff" opacity="${light ? 0.22 : dark ? 0.07 : 0.12}"/>`).join('')}</g>
      <rect x="0" y="-10" width="${W}" height="${H + 20}" fill="url(#${id}h)"/><rect x="0" y="-10" width="${W}" height="${H + 20}" fill="url(#${id}v)"/><rect x="0" y="-10" width="${W}" height="${H + 20}" fill="url(#${id}r)"/>
      <rect x="0" y="-10" width="${W}" height="${H + 20}" filter="url(#${id}n)" opacity="${dark ? 0.12 : 0.07}" style="mix-blend-mode:multiply"/></g>`);

    // Construction details
    if (kind === 'tee') {
      p.push(`<path d="${TEE.collar[back ? 'back' : 'front']}" fill="${rib}"/><path d="${TEE.collar[back ? 'back' : 'front']}" fill="url(#${id}k)"/>`);
      p.push(`<path d="${TEE.seams}" fill="none" stroke="${edge}" stroke-width="1.6"/><path d="${TEE.stitch}" fill="none" stroke="${stitch}" stroke-width="1.2" stroke-dasharray="3 3"/>`);
    }
    if (kind === 'hoodie') {
      HOODIE.cuffs.forEach((d) => p.push(`<path d="${d}" fill="${rib}"/><path d="${d}" fill="url(#${id}k)"/>`));
      p.push(`<path d="${HOODIE.waist}" fill="${rib}"/><path d="${HOODIE.waist}" fill="url(#${id}k)"/>`);
      p.push(`<path d="${HOODIE.seams}" fill="none" stroke="${edge}" stroke-width="1.6"/>`);
      if (!back) {
        p.push(`<path d="${HOODIE.pocket}" fill="${hex}"/><path d="${HOODIE.pocket}" fill="url(#${id}v)"/><path d="${HOODIE.pocket}" fill="none" stroke="${edge}" stroke-width="1.6"/><path d="${HOODIE.pocketStitch}" fill="none" stroke="${stitch}" stroke-width="1.1" stroke-dasharray="3 3"/>`);
        p.push(`<path d="M132 300Q122 330 116 384" stroke="#000" stroke-opacity=".25" stroke-width="5" fill="none" filter="url(#${id}b)"/><path d="M268 300Q278 330 284 384" stroke="#000" stroke-opacity=".25" stroke-width="5" fill="none" filter="url(#${id}b)"/>`);
        p.push(`<path d="${HOODIE.hoodInner}" fill="${deep}"/><path d="${HOODIE.hoodInner}" fill="#000" opacity=".25"/>`);
        p.push(`<path d="${HOODIE.hoodFront}" fill="none" stroke="${edge}" stroke-width="1.6"/>`);
        const cord = light ? '#d9d9d4' : '#f2f2ee';
        p.push(`<path d="M186 84C184 104 186 124 182 150" stroke="${cord}" stroke-width="4" stroke-linecap="round" fill="none"/><path d="M214 84C216 104 214 124 218 150" stroke="${cord}" stroke-width="4" stroke-linecap="round" fill="none"/>
          <rect x="178" y="146" width="8" height="14" rx="3" fill="#2b2b2b"/><rect x="214" y="146" width="8" height="14" rx="3" fill="#2b2b2b"/><circle cx="186" cy="84" r="3.5" fill="${deep}" stroke="${stitch}"/><circle cx="214" cy="84" r="3.5" fill="${deep}" stroke="${stitch}"/>`);
      } else {
        p.push(`<path d="${HOODIE.hoodBack}" fill="#000" opacity=".3" filter="url(#${id}b)" transform="translate(0 8)"/><path d="${HOODIE.hoodBack}" fill="${hex}"/><path d="${HOODIE.hoodBack}" fill="url(#${id}h)"/><path d="${HOODIE.hoodBack}" fill="url(#${id}r)"/>
          <path d="M200 -2C200 40 200 90 200 136" stroke="${edge}" stroke-width="1.6" fill="none"/><path d="${HOODIE.hoodBack}" fill="none" stroke="${edge}" stroke-width="1.6"/>`);
      }
    }
    if (kind === 'cap') {
      p.push(`<path d="${CAP.panels}" fill="none" stroke="${edge}" stroke-width="1.6"/><path d="${CAP.band}" fill="#000" opacity=".1"/>`);
      p.push(`<path d="${CAP.brim}" fill="none" stroke="${edge}" stroke-width="1.4"/><path d="${CAP.brimStitch}" fill="none" stroke="${stitch}" stroke-width="1.1" stroke-dasharray="3 3"/>`);
      [[150, 152], [250, 152]].forEach(([x, y]) => p.push(`<circle cx="${x}" cy="${y}" r="3.2" fill="${deep}" stroke="${stitch}"/>`));
      p.push(`<circle cx="200" cy="101" r="9" fill="${hex}"/><circle cx="200" cy="101" r="9" fill="url(#${id}r)"/><circle cx="200" cy="101" r="9" fill="none" stroke="${edge}"/>`);
    }
    // Outline
    p.push(`<path d="${bodyPath}" fill="none" stroke="${edge}" stroke-width="1.4"/>`);
    if (showArea) p.push(`<rect x="${a.x * W}" y="${a.y * H}" width="${a.w * W}" height="${a.h * H}" rx="6" fill="none" stroke="${light ? '#17795A' : '#7FE0B7'}" stroke-width="2" stroke-dasharray="7 6" class="g-area"/>`);

    return `<svg xmlns="http://www.w3.org/2000/svg" class="garment g-${kind}" viewBox="-10 -14 ${W + 20} ${H + 26}" role="img" aria-label="${esc(label || `${kind} ${side}`)}"><defs>${defs}</defs>${p.join('')}</svg>`;
  };

  /** Print area of a garment in canvas units (0–1) — used to keep artwork inside it */
  const area = (kind) => AREAS[kind] || AREAS.tee;
  /** Keep a placement inside the print area */
  const clampPlace = (kind, p, artRatio = 1) => {
    const a = area(kind);
    const maxW = Math.min(a.w, (a.h * H) / (artRatio * W));
    const w = Math.max(0.08, Math.min(maxW, p.w));
    const hw = w / 2, hh = (w * artRatio * W) / H / 2;
    return { w: +w.toFixed(4), x: +Math.min(a.x + a.w - hw, Math.max(a.x + hw, p.x)).toFixed(4), y: +Math.min(a.y + a.h - hh, Math.max(a.y + hh, p.y)).toFixed(4) };
  };

  SA.ui.garment = Object.assign(garment, { area, clampPlace, W, H, lum, mix });

  /* ---------- Interactive 3D viewer ---------- */
  /**
   * mount(el, { kind, hex, art, place: {front, back}, artRatio, label }) → { update(opts), show(side), destroy() }
   * Drag (mouse or touch) to turn it; it settles on the front or back. Idle: slow float and sway.
   * Arrow keys turn it too. Respects reduced motion.
   */
  const garment3d = {
    mount(el, opts) {
      let o = { ...opts };
      let angle = 0, target = 0, drag = null, raf = 0, idleT = 0, last = performance.now();
      const reduced = () => !SA.core.motion?.enabled?.() || matchMedia('(prefers-reduced-motion: reduce)').matches;
      el.classList.add('g3d');
      el.setAttribute('tabindex', '0');
      el.setAttribute('role', 'img');
      const draw = () => {
        const hasBack = !!o.place?.back || o.kind !== 'cap';
        // Real product photos when they are loaded (SA.ui.garmentView), drawn mockups until then
        const V = SA.ui.garmentView || garment;
        const pm = SA.ui.photoMock;
        const back = hasBack && (!pm?.isReady() || pm.hasSide(o.kind, 'back'));
        const ar = pm?.isReady() ? pm.aspect(o.kind, 'front') : 420 / 466;
        el.innerHTML = `<div class="g3d-stage"><div class="g3d-obj" style="aspect-ratio:${ar}">
            <div class="g3d-face g3d-front">${V({ kind: o.kind, hex: o.hex, art: o.art, place: o.place?.front, side: 'front', artRatio: o.artRatio, label: `${o.label || ''} front`, shadow: false, cacheKey: o.cacheKey ? `${o.cacheKey}|f` : '' })}</div>
            <div class="g3d-face g3d-back">${back ? V({ kind: o.kind, hex: o.hex, art: o.art, place: o.place?.back, side: 'back', artRatio: o.artRatio, label: `${o.label || ''} back`, shadow: false, cacheKey: o.cacheKey ? `${o.cacheKey}|b` : '' }) : ''}</div>
            <div class="g3d-sheen"></div></div></div>
          <div class="g3d-shadow"></div>
          <div class="g3d-hint xs">${SA.ui.icon('swap', 13)} <span>Drag to turn</span></div>`;
        el.setAttribute('aria-label', `${o.label || 'Product'} — 3D view. Drag or use the arrow keys to turn it.`);
      };
      const obj = () => el.querySelector('.g3d-obj');
      const apply = () => {
        const ob = obj(); if (!ob) return;
        const t = performance.now() / 1000;
        const sway = !drag && !reduced() ? Math.sin(t * 0.8) * 9 * Math.min(1, idleT) : 0;
        const bob = !reduced() ? Math.sin(t * 1.3) * 6 : 0;
        const a = angle + sway;
        ob.style.transform = `translateY(${bob}px) rotateY(${a}deg)`;
        const n = ((a % 360) + 360) % 360;
        el.style.setProperty('--sheen', `${50 + Math.sin((n * Math.PI) / 180) * 60}%`);
        el.style.setProperty('--shadow-s', `${0.85 + Math.abs(Math.cos((n * Math.PI) / 180)) * 0.15 - bob / 120}`);
        el.classList.toggle('is-back', n > 90 && n < 270);
      };
      const loop = (now) => {
        const dt = Math.min(0.05, (now - last) / 1000); last = now;
        if (!drag) { angle += (target - angle) * Math.min(1, dt * 7); idleT = Math.min(1, idleT + dt * 0.4); }
        apply();
        raf = requestAnimationFrame(loop);
      };
      const down = (e) => { drag = { x: e.clientX, a: angle, moved: 0 }; idleT = 0; el.classList.add('is-drag'); try { el.setPointerCapture?.(e.pointerId); } catch { /* synthetic or lost pointer */ } el.querySelector('.g3d-hint')?.classList.add('is-gone'); };
      const move = (e) => { if (!drag) return; const dx = e.clientX - drag.x; drag.moved = Math.abs(dx); angle = drag.a + dx * 0.6; target = angle; };
      const up = () => { if (!drag) return; drag = null; el.classList.remove('is-drag'); target = Math.round(angle / 180) * 180; };
      const key = (e) => { if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') { e.preventDefault(); target += e.key === 'ArrowRight' ? 180 : -180; idleT = 0; } };
      el.addEventListener('pointerdown', down);
      el.addEventListener('pointermove', move);
      el.addEventListener('pointerup', up);
      el.addEventListener('pointercancel', up);
      el.addEventListener('keydown', key);
      draw();
      raf = requestAnimationFrame(loop);
      // Redraw once the product photos (or the artwork) finish loading
      const offReady = SA.ui.photoMock?.onReady(() => { if (el.isConnected) { draw(); apply(); } });
      const api = {
        update(next) { o = { ...o, ...next }; draw(); apply(); },
        show(side) { const base = Math.round(target / 360) * 360; target = base + (side === 'back' ? 180 : 0); idleT = 0; },
        destroy() { cancelAnimationFrame(raf); offReady?.(); el.removeEventListener('pointerdown', down); el.removeEventListener('pointermove', move); el.removeEventListener('pointerup', up); el.removeEventListener('pointercancel', up); el.removeEventListener('keydown', key); },
      };
      // Stop when the element leaves the page (route change)
      const watch = setInterval(() => { if (!el.isConnected) { api.destroy(); clearInterval(watch); } }, 2000);
      return api;
    },
  };
  SA.ui.garment3d = garment3d;
})(window.SA);
