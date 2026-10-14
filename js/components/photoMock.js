/**
 * Photo mockups: real photographs of blank garments (Pexels / Unsplash), recoloured to any catalogue colour and with the
 * seller's artwork printed into the fabric (it takes the folds and shadows of the photo).
 *
 * How it works (all in the browser, once per photo):
 *  1. The garment outline was traced by hand (poly, photo pixels); each point is snapped to the strongest edge nearby,
 *     then filled as a soft mask. Skin is removed on the worn hoodie photos.
 *  2. Shading = photo brightness ÷ the garment's white level. Recolour = colour × shading (+ a little sheen on dark colours).
 *  3. Print = artwork × shading, clipped to the garment.
 * The print rectangle of each photo matches the catalogue print area, so seller placements (x, y, w in the 400×440
 * template) land in the same spot on the photo.
 * When the backend exists, the same steps run once on the server and the results are cached on a CDN.
 */
(function (SA) {
  const PX = (id, w = 900) => `https://images.pexels.com/photos/${id}/pexels-photo-${id}.jpeg?auto=compress&w=${w}`;
  const US = (id, w = 900) => `https://images.unsplash.com/${id}?w=${w}&q=82&auto=format`;

  // print: rectangle in photo pixels with the same proportions as the catalogue print area · crop: what is shown
  const BASES = {
    'tee-front': { src: PX(12025472), credit: 'Pexels', snap: 6, print: { x: 345, y: 150, w: 178, h: 320 }, crop: [130, 40, 770, 580],
      poly: [[373, 84], [437, 77], [500, 88], [543, 100], [576, 117], [689, 205], [634, 286], [583, 269], [580, 306], [583, 439], [585, 518], [583, 529], [437, 534], [285, 526], [282, 439], [285, 306], [288, 277], [231, 290], [171, 200], [244, 140], [317, 96]] },
    'tee-back': { src: PX(12039633), credit: 'Pexels', snap: 6, print: { x: 361, y: 140, w: 178, h: 320 }, crop: [140, 40, 780, 580],
      poly: [[399, 70], [447, 64], [497, 70], [552, 86], [604, 110], [683, 200], [634, 269], [594, 246], [591, 372], [588, 525], [588, 534], [450, 536], [312, 532], [309, 372], [314, 239], [270, 277], [204, 210], [258, 128], [300, 100], [348, 78]] },
    'hoodie-front': { src: PX(8217410), credit: 'Pexels', skin: true, holes: [[[345, 300], [380, 258], [430, 248], [490, 256], [530, 292], [545, 370], [540, 450], [522, 520], [492, 580], [470, 606], [442, 598], [402, 560], [370, 510], [350, 450], [340, 380]]], print: { x: 351, y: 760, w: 208, h: 240 }, crop: [0, 200, 900, 1350],
      poly: [[325, 262], [375, 232], [440, 220], [505, 228], [560, 252], [598, 300], [614, 380], [616, 450], [608, 520], [612, 565], [663, 600], [757, 650], [785, 700], [797, 750], [809, 800], [836, 900], [853, 1000], [855, 1100], [846, 1200], [846, 1300], [846, 1350], [88, 1350], [91, 1300], [93, 1200], [102, 1100], [117, 1000], [133, 900], [150, 800], [159, 750], [173, 700], [226, 650], [318, 600], [301, 550], [298, 500], [299, 450], [307, 400], [309, 350], [319, 300]] },
    'hoodie-back': { src: PX(8217415), credit: 'Pexels', skin: true, print: { x: 343, y: 640, w: 225, h: 260 }, crop: [0, 200, 900, 1350],
      poly: [[400, 292], [432, 286], [470, 290], [504, 300], [544, 350], [562, 400], [571, 450], [575, 500], [574, 550], [680, 600], [735, 650], [754, 700], [760, 800], [762, 900], [763, 1000], [763, 1100], [748, 1200], [720, 1240], [650, 1272], [460, 1276], [300, 1272], [226, 1235], [200, 1200], [197, 1100], [194, 1000], [203, 900], [213, 800], [224, 700], [235, 650], [270, 600], [368, 550], [338, 500], [346, 450], [361, 400], [370, 350], [361, 300]] },
    'cap-front': { src: US('photo-1691256676359-20e5c6d4bc92'), credit: 'Unsplash', snap: 3, print: { x: 362, y: 420, w: 182, h: 140 }, crop: [120, 270, 780, 730],
      poly: [[453, 318], [545, 334], [636, 388], [680, 452], [699, 522], [704, 586], [704, 606], [689, 620], [690, 642], [672, 652], [450, 663], [228, 652], [209, 628], [212, 614], [186, 609], [194, 540], [224, 440], [288, 366], [365, 334]] },
  };
  const AREAS = () => ({ tee: SA.ui.garment.area('tee'), hoodie: SA.ui.garment.area('hoodie'), cap: SA.ui.garment.area('cap') });
  const keyFor = (kind, side) => (kind === 'cap' ? 'cap-front' : `${kind === 'hoodie' ? 'hoodie' : 'tee'}-${side === 'back' ? 'back' : 'front'}`);

  const state = {}; // key → { img, W, H, L (Float32), alpha (Uint8), S (Float32), crop, print }
  let loading = null, ready = false;
  const listeners = new Set();

  const loadImg = (src) => new Promise((res, rej) => { const i = new Image(); i.crossOrigin = 'anonymous'; i.onload = () => res(i); i.onerror = rej; i.src = src; });

  /** Mask from the traced outline: snap to edges, fill, feather, remove skin */
  const build = (b, img) => {
    const W = img.naturalWidth, H = img.naturalHeight, N = W * H;
    const cv = document.createElement('canvas'); cv.width = W; cv.height = H;
    const g = cv.getContext('2d', { willReadFrequently: true }); g.drawImage(img, 0, 0);
    const d = g.getImageData(0, 0, W, H).data;
    const L = new Float32Array(N);
    for (let i = 0; i < N; i++) L[i] = 0.299 * d[i * 4] + 0.587 * d[i * 4 + 1] + 0.114 * d[i * 4 + 2];
    // Blurred luminance and gradient for snapping
    const Lb = new Float32Array(N);
    for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++) { let s = 0; for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) s += L[(y + dy) * W + x + dx]; Lb[y * W + x] = s / 9; }
    const grad = (x, y) => { x = Math.round(x); y = Math.round(y); if (x < 2 || y < 2 || x > W - 3 || y > H - 3) return 0; const i = y * W + x; return Math.hypot(Lb[i + 1] - Lb[i - 1], Lb[i + W] - Lb[i - W]); };
    // Densify an outline and snap each point along its normal (±10 px) to the strongest edge, then smooth it
    const snapPoly = (poly) => {
      const pts = [];
      for (let k = 0; k < poly.length; k++) {
        const [x0, y0] = poly[k], [x1, y1] = poly[(k + 1) % poly.length];
        const n = Math.max(1, Math.round(Math.hypot(x1 - x0, y1 - y0) / 4));
        for (let j = 0; j < n; j++) pts.push([x0 + ((x1 - x0) * j) / n, y0 + ((y1 - y0) * j) / n]);
      }
      const snapped = pts.map((p, i) => {
        const a = pts[(i - 2 + pts.length) % pts.length], c = pts[(i + 2) % pts.length];
        let nx = -(c[1] - a[1]), ny = c[0] - a[0]; const len = Math.hypot(nx, ny) || 1; nx /= len; ny /= len;
        if (p[1] >= H - 2 || p[0] <= 1 || p[0] >= W - 2) return p; // edge of the photo
        let best = 0, bt = 0;
        const R = b.snap ?? 10;
        for (let t = -R; t <= R; t++) { const v = grad(p[0] + nx * t, p[1] + ny * t) - Math.abs(t) * 0.35; if (v > best) { best = v; bt = t; } }
        return best > 3 ? [p[0] + nx * bt, p[1] + ny * bt] : p;
      });
      return snapped.map((_, i) => { let sx = 0, sy = 0; for (let k = -3; k <= 3; k++) { const q = snapped[(i + k + snapped.length) % snapped.length]; sx += q[0]; sy += q[1]; } return [sx / 7, sy / 7]; });
    };
    const smooth = snapPoly(b.poly);
    // Fill + feather
    const mc = document.createElement('canvas'); mc.width = W; mc.height = H;
    const mg = mc.getContext('2d', { willReadFrequently: true });
    mg.filter = 'blur(1.2px)'; mg.fillStyle = '#fff'; mg.beginPath(); smooth.forEach(([x, y], i) => (i ? mg.lineTo(x, y) : mg.moveTo(x, y))); mg.closePath(); mg.fill();
    // Openings (the face inside the hood) are cut out, snapped the same way
    mg.globalCompositeOperation = 'destination-out';
    (b.holes || []).forEach((h) => { const hp = snapPoly(h); mg.beginPath(); hp.forEach(([x, y], i) => (i ? mg.lineTo(x, y) : mg.moveTo(x, y))); mg.closePath(); mg.fill(); });
    mg.globalCompositeOperation = 'source-over';
    const md = mg.getImageData(0, 0, W, H).data;
    const alpha = new Uint8ClampedArray(N);
    for (let i = 0; i < N; i++) alpha[i] = md[i * 4 + 3];
    if (b.skin) {
      // Skin, beard, hair and jeans are not fabric
      for (let i = 0; i < N; i++) { if (!alpha[i]) continue; const r = d[i * 4], bl = d[i * 4 + 2]; if ((r - bl > 30 && L[i] < 215) || L[i] < 80) alpha[i] = 0; }
      // Soften the cut
      const a2 = new Uint8ClampedArray(alpha);
      for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++) { const i = y * W + x; if (!a2[i] && !a2[i - 1] && !a2[i + 1] && !a2[i - W] && !a2[i + W]) continue; alpha[i] = (a2[i] * 4 + a2[i - 1] + a2[i + 1] + a2[i - W] + a2[i + W]) / 8; }
    }
    // White level of the fabric (97th percentile) → shading
    const hist = new Uint32Array(256); let cnt = 0;
    for (let i = 0; i < N; i += 3) if (alpha[i] > 200) { hist[Math.min(255, L[i] | 0)]++; cnt++; }
    let acc = 0, white = 245; for (let v = 255; v >= 0; v--) { acc += hist[v]; if (acc > cnt * 0.03) { white = v; break; } }
    const S = new Float32Array(N);
    for (let i = 0; i < N; i++) S[i] = L[i] / white;
    return { img, W, H, d, alpha, S, crop: b.crop, print: b.print, credit: b.credit };
  };

  const load = () => {
    if (loading) return loading;
    loading = Promise.all(Object.entries(BASES).map(async ([k, b]) => { state[k] = build(b, await loadImg(b.src)); }))
      .then(() => { ready = true; listeners.forEach((fn) => { try { fn(); } catch (e) { console.error(e); } }); })
      .catch((e) => { console.warn('Photo mockups unavailable — using drawn mockups', e); loading = null; });
    return loading;
  };

  /* ---------- Rendering ---------- */
  const rgb = (hex) => { let h = String(hex || '#ffffff').replace('#', ''); if (h.length === 3) h = h.split('').map((x) => x + x).join(''); const n = parseInt(h, 16); return [(n >> 16) & 255, (n >> 8) & 255, n & 255]; };
  const recolCache = new Map();
  const MAXW = 760;

  /** Garment photo recoloured to hex, cropped and scaled → canvas */
  const recolor = (key, hex) => {
    const ck = `${key}|${hex}`;
    if (recolCache.has(ck)) return recolCache.get(ck);
    const s = state[key];
    const [cx0, cy0, cx1, cy1] = s.crop;
    const cw = cx1 - cx0, ch = cy1 - cy0;
    const c = document.createElement('canvas'); c.width = cw; c.height = ch;
    const g = c.getContext('2d');
    const out = g.createImageData(cw, ch);
    const [R, G, B] = rgb(hex).map((v) => v / 255);
    const keepWhite = R > 0.93 && G > 0.93 && B > 0.93;
    const o = out.data, d = s.d, W = s.W;
    for (let y = 0; y < ch; y++) for (let x = 0; x < cw; x++) {
      const i = (y + cy0) * W + (x + cx0), j = (y * cw + x) * 4;
      const a = s.alpha[i] / 255;
      let r = d[i * 4], gg = d[i * 4 + 1], b = d[i * 4 + 2];
      if (a > 0 && !keepWhite) {
        const sh = s.S[i], spec = 0.16 * Math.pow(Math.min(1.15, sh), 6);
        const nr = 255 * Math.min(1, R * sh + (1 - R) * spec), ng = 255 * Math.min(1, G * sh + (1 - G) * spec), nb = 255 * Math.min(1, B * sh + (1 - B) * spec);
        r = r * (1 - a) + nr * a; gg = gg * (1 - a) + ng * a; b = b * (1 - a) + nb * a;
      }
      o[j] = r; o[j + 1] = gg; o[j + 2] = b; o[j + 3] = 255;
    }
    g.putImageData(out, 0, 0);
    recolCache.set(ck, c);
    if (recolCache.size > 60) recolCache.delete(recolCache.keys().next().value);
    return c;
  };

  /** Template placement → photo rectangle (photo pixels) */
  const placeRect = (kind, side, place, artRatio = 1) => {
    const s = state[keyFor(kind, side)], a = AREAS()[kind === 'hoodie' ? 'hoodie' : kind === 'cap' ? 'cap' : 'tee'];
    const pr = s.print;
    const w = (place.w / a.w) * pr.w, h = w * artRatio;
    const cx = pr.x + ((place.x - a.x) / a.w) * pr.w, cy = pr.y + ((place.y - a.y) / a.h) * pr.h;
    return { x: cx - w / 2, y: cy - h / 2, w, h };
  };
  /** Photo point → template fraction (for dragging in the studio) */
  const toTemplate = (kind, side, px, py) => {
    const s = state[keyFor(kind, side)], a = AREAS()[kind === 'hoodie' ? 'hoodie' : kind === 'cap' ? 'cap' : 'tee'];
    return { x: a.x + ((px - s.print.x) / s.print.w) * a.w, y: a.y + ((py - s.print.y) / s.print.h) * a.h };
  };

  const artCache = new Map();
  /** Artwork as a loaded image (null until it has loaded; then listeners are told to redraw) */
  const artImg = (src) => {
    if (!src) return null;
    let e = artCache.get(src);
    if (!e) {
      e = { img: new Image(), ok: false };
      e.img.onload = () => { e.ok = true; listeners.forEach((fn) => fn()); };
      e.img.src = src;
      artCache.set(src, e);
      if (artCache.size > 80) artCache.delete(artCache.keys().next().value);
    }
    return e.ok ? e.img : null;
  };

  /** Print the artwork into the fabric of a recoloured canvas (in place) */
  const printOnto = (canvas, key, kind, side, art, place, artRatio) => {
    const img = artImg(art); if (!img || !place) return !art || !place;
    const s = state[key]; const [cx0, cy0] = s.crop;
    const r = placeRect(kind, side, place, artRatio);
    const x0 = Math.max(0, Math.floor(r.x)), y0 = Math.max(0, Math.floor(r.y)), w = Math.ceil(r.w) + 2, h = Math.ceil(r.h) + 2;
    const t = document.createElement('canvas'); t.width = w; t.height = h;
    const tg = t.getContext('2d', { willReadFrequently: true });
    tg.drawImage(img, r.x - x0, r.y - y0, r.w, r.h);
    const id = tg.getImageData(0, 0, w, h), p = id.data;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const X = x0 + x, Y = y0 + y; if (X >= s.W || Y >= s.H) { p[(y * w + x) * 4 + 3] = 0; continue; }
      const i = Y * s.W + X, j = (y * w + x) * 4;
      if (!p[j + 3]) continue;
      const sh = Math.min(1.04, s.S[i] * 1.02);
      p[j] *= sh; p[j + 1] *= sh; p[j + 2] *= sh;
      p[j + 3] *= (s.alpha[i] / 255) * 0.97;
    }
    tg.putImageData(id, 0, 0);
    canvas.getContext('2d').drawImage(t, x0 - cx0, y0 - cy0);
    return true;
  };

  const compCache = new Map();
  /**
   * Full product photo → data URL (JPEG). Returns null while photos or artwork are still loading
   * (callers fall back to the drawn mockup and redraw on onReady).
   */
  const compose = ({ kind = 'tee', side = 'front', hex = '#ffffff', art = '', place = null, artRatio = 1, key: cacheKey = '' }) => {
    if (!ready) { load(); return null; }
    const key = keyFor(kind, side);
    const ck = cacheKey ? `${cacheKey}|${key}|${hex}` : '';
    if (ck && compCache.has(ck)) return compCache.get(ck);
    const base = recolor(key, hex);
    const c = document.createElement('canvas'); c.width = base.width; c.height = base.height;
    c.getContext('2d').drawImage(base, 0, 0);
    if (!printOnto(c, key, kind, side, art, place, artRatio)) return null;
    let out = c;
    if (c.width > MAXW) { out = document.createElement('canvas'); out.width = MAXW; out.height = Math.round((c.height * MAXW) / c.width); out.getContext('2d').drawImage(c, 0, 0, out.width, out.height); }
    const url = out.toDataURL('image/jpeg', 0.88);
    if (ck) { compCache.set(ck, url); if (compCache.size > 120) compCache.delete(compCache.keys().next().value); }
    return url;
  };

  /** Pieces for the live studio editor: blank recoloured photo + geometry in photo pixels */
  const studio = (kind, side, hex) => {
    if (!ready) { load(); return null; }
    const key = keyFor(kind, side), s = state[key];
    const base = recolor(key, hex);
    const ck = `studio|${key}|${hex}`;
    let url = compCache.get(ck);
    if (!url) { url = base.toDataURL('image/jpeg', 0.86); compCache.set(ck, url); }
    return { url, crop: s.crop, print: s.print, credit: s.credit };
  };

  const esc = (v) => SA.core.utils.esc(v);
  /**
   * Live SVG in photo coordinates (studio editor): blank recoloured photo + artwork + print area.
   * data-photo marks it so the editor converts pointer positions with toTemplate().
   */
  const photoSvg = ({ kind, hex, art, place, side = 'front', artRatio = 1, showArea = false, label = '' }) => {
    const ph = studio(kind, side, hex); if (!ph) return null;
    const [x0, y0, x1, y1] = ph.crop, w = x1 - x0, h = y1 - y0;
    const p = [`<image href="${ph.url}" x="${x0}" y="${y0}" width="${w}" height="${h}"/>`];
    if (showArea) p.push(`<rect x="${ph.print.x}" y="${ph.print.y}" width="${ph.print.w}" height="${ph.print.h}" rx="6" fill="none" stroke="#17795A" stroke-width="${Math.max(2, w / 260)}" stroke-dasharray="${w / 70} ${w / 90}" class="g-area"/>`);
    if (art && place) { const r = placeRect(kind, side, place, artRatio); p.push(`<image href="${esc(art)}" x="${r.x}" y="${r.y}" width="${r.w}" height="${r.h}" preserveAspectRatio="xMidYMid meet" class="g-art" opacity=".96"/>`); }
    return `<svg xmlns="http://www.w3.org/2000/svg" class="garment garment-photo g-${kind}" data-photo="1" viewBox="${x0} ${y0} ${w} ${h}" role="img" aria-label="${esc(label || `${kind} ${side}`)}">${p.join('')}</svg>`;
  };
  /**
   * Best available view of a garment as HTML: the printed product photo (<img>), else the live photo SVG,
   * else the drawn mockup while photos load. opts as SA.ui.garment plus cacheKey.
   */
  const view = (opts) => {
    const { kind = 'tee', side = 'front' } = opts;
    if (ready && SA.ui.photoMock.hasSide(kind, side)) {
      if (!opts.showArea) {
        const url = compose({ ...opts, key: opts.cacheKey || '' });
        if (url) return `<img class="garment garment-photo g-${kind}" src="${url}" alt="${esc(opts.label || '')}" draggable="false" />`;
      }
      const svg = photoSvg(opts);
      if (svg) return svg;
    } else load();
    return SA.ui.garment(opts);
  };
  /** Aspect ratio (w/h) of the photo used for a garment side */
  const aspect = (kind, side) => { const b = BASES[keyFor(kind, side)]; return b ? (b.crop[2] - b.crop[0]) / (b.crop[3] - b.crop[1]) : 420 / 466; };

  SA.ui.garmentView = view;
  SA.ui.photoMock = {
    photoSvg, aspect,
    load, compose, studio, placeRect, toTemplate, keyFor, BASES,
    isReady: () => ready,
    hasSide: (kind, side) => !!BASES[keyFor(kind, side)] && (kind !== 'cap' || side !== 'back'),
    onReady(fn) { listeners.add(fn); return () => listeners.delete(fn); },
  };
})(window.SA);
