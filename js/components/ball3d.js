/**
 * 3D soccer ball that jumps from section to section as you scroll (home page only).
 *
 * Rendering: a real truncated icosahedron mapped onto a sphere and ray-shaded per pixel on a
 * small canvas (no WebGL or library). Each pixel's surface normal is rotated into ball space and
 * classified by its nearest panel centre: 12 pentagons (black), 20 hexagons (white), with seams
 * where two panels meet. Lighting is fixed in screen space, so it is precomputed once.
 *
 * Motion: landing spots are elements on the page. Scroll position maps to a continuous progress
 * value P (spot index + fraction). The ball follows P with a spring, flies a parabolic arc between
 * spots, spins about an axis perpendicular to its travel, squashes on impact and kicks up dust.
 */
(function (SA) {
  const PHI = (1 + Math.sqrt(5)) / 2;
  const norm = (v) => { const l = Math.hypot(v[0], v[1], v[2]); return [v[0] / l, v[1] / l, v[2] / l]; };

  // 12 icosahedron vertices → pentagon centres; 20 dodecahedron vertices → hexagon centres
  const ip = 1 / PHI;
  const PENT = [], HEX = [];
  for (const s1 of [1, -1]) for (const s2 of [1, -1]) {
    // Icosahedron vertices: cyclic permutations of (0, ±1, ±φ)
    PENT.push(norm([0, s1, s2 * PHI]), norm([s1, s2 * PHI, 0]), norm([s2 * PHI, 0, s1]));
    // Its face centres (dual dodecahedron): cyclic permutations of (0, ±φ, ±1/φ)
    HEX.push(norm([0, s1 * PHI, s2 * ip]), norm([s1 * PHI, s2 * ip, 0]), norm([s2 * ip, 0, s1 * PHI]));
  }
  for (const x of [1, -1]) for (const y of [1, -1]) for (const z of [1, -1]) HEX.push(norm([x, y, z]));
  const CENTERS = [...PENT, ...HEX];            // 12 pentagons + 20 hexagons
  const CX = new Float32Array(CENTERS.map((c) => c[0]));
  const CY = new Float32Array(CENTERS.map((c) => c[1]));
  const CZ = new Float32Array(CENTERS.map((c) => c[2]));

  // ---------- 3x3 rotation helpers (row-major) ----------
  const mul = (a, b) => {
    const r = new Array(9);
    for (let i = 0; i < 3; i++) for (let j = 0; j < 3; j++) r[i * 3 + j] = a[i * 3] * b[j] + a[i * 3 + 1] * b[3 + j] + a[i * 3 + 2] * b[6 + j];
    return r;
  };
  const axisAngle = ([x, y, z], t) => {
    const c = Math.cos(t), s = Math.sin(t), k = 1 - c;
    return [c + x * x * k, x * y * k - z * s, x * z * k + y * s, y * x * k + z * s, c + y * y * k, y * z * k - x * s, z * x * k - y * s, z * y * k + x * s, c + z * z * k];
  };
  const orthonormalize = (m) => {
    let a = norm([m[0], m[3], m[6]]);
    let b = [m[1], m[4], m[7]];
    const d = a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
    b = norm([b[0] - d * a[0], b[1] - d * a[1], b[2] - d * a[2]]);
    const c = [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
    return [a[0], b[0], c[0], a[1], b[1], c[1], a[2], b[2], c[2]];
  };

  // ---------- Ball renderer ----------
  const createRenderer = (canvas) => {
    const ctx = canvas.getContext('2d');
    let D = 0, img, nx, ny, nz, light, spec, alpha, idx;

    const resize = (size) => {
      D = size; canvas.width = canvas.height = D;
      img = ctx.createImageData(D, D);
      const n = D * D;
      nx = new Float32Array(n); ny = new Float32Array(n); nz = new Float32Array(n);
      light = new Float32Array(n); spec = new Float32Array(n); alpha = new Float32Array(n);
      const list = [];
      const L = norm([-0.5, 0.62, 0.62]);            // key light: upper-left, in front
      const H = norm([L[0], L[1], L[2] + 1]);          // half vector with viewer (0,0,1)
      const F = norm([0.7, -0.3, 0.4]);                // cool fill from lower right
      for (let py = 0; py < D; py++) for (let px = 0; px < D; px++) {
        const i = py * D + px;
        const x = ((px + 0.5) / D) * 2 - 1, y = 1 - ((py + 0.5) / D) * 2;
        const r2 = x * x + y * y;
        const edge = (1 - Math.sqrt(r2)) * D * 0.5;  // distance to silhouette in pixels
        if (edge <= -0.5) continue;
        const z = Math.sqrt(Math.max(0, 1 - r2));
        nx[i] = x; ny[i] = y; nz[i] = z;
        const diff = Math.max(0, x * L[0] + y * L[1] + z * L[2]);
        const fill = Math.max(0, x * F[0] + y * F[1] + z * F[2]) * 0.18;
        const rim = 0.55 + 0.45 * Math.pow(z, 0.6);              // limb darkening
        const ao = 0.82 + 0.18 * Math.min(1, (y + 1));            // a touch darker underneath
        light[i] = (0.28 + 0.78 * diff + fill) * rim * ao;
        spec[i] = Math.pow(Math.max(0, x * H[0] + y * H[1] + z * H[2]), 70) * 0.9 + Math.pow(Math.max(0, x * H[0] + y * H[1] + z * H[2]), 12) * 0.08;
        alpha[i] = Math.min(1, edge + 0.5);
        list.push(i);
      }
      idx = Uint32Array.from(list);
    };

    const draw = (R) => {
      const data = img.data;
      data.fill(0);
      // R^T n: rotate screen normal into ball space
      const r0 = R[0], r1 = R[1], r2 = R[2], r3 = R[3], r4 = R[4], r5 = R[5], r6 = R[6], r7 = R[7], r8 = R[8];
      for (let k = 0; k < idx.length; k++) {
        const i = idx[k];
        const x = nx[i], y = ny[i], z = nz[i];
        const bx = r0 * x + r3 * y + r6 * z, by = r1 * x + r4 * y + r7 * z, bz = r2 * x + r5 * y + r8 * z;
        let d1 = -2, d2 = -2, c1 = 0;
        for (let c = 0; c < 32; c++) {
          const d = bx * CX[c] + by * CY[c] + bz * CZ[c];
          if (d > d1) { d2 = d1; d1 = d; c1 = c; } else if (d > d2) d2 = d;
        }
        const pent = c1 < 12;
        const gap = d1 - d2;                                     // ~0 on a seam
        const seam = gap < 0.011 ? 1 - gap / 0.011 : 0;
        const puff = 0.9 + 0.1 * Math.min(1, gap * 9);           // panels bulge slightly
        const tint = pent ? [28, 32, 36] : [246, 247, 243];
        const l = light[i] * puff;
        const s = spec[i] * (pent ? 0.55 : 1) * (1 - seam * 0.8) * 255;
        const dark = 1 - seam * (pent ? 0.2 : 0.62);
        const o = i * 4;
        data[o] = tint[0] * l * dark + s;
        data[o + 1] = tint[1] * l * dark + s;
        data[o + 2] = tint[2] * l * dark + s;
        data[o + 3] = alpha[i] * 255;
      }
      ctx.putImageData(img, 0, 0);
    };
    return { resize, draw, size: () => D };
  };

  // ---------- Scroll choreography ----------
  let lastUnmount = 0, saved = null;

  /**
   * mount(view, spots) — spots: [{ sel, x: 0..1 across the element, at: 'top' | 'center' }]
   * Returns a cleanup function.
   */
  const mount = (view, spotsConfig) => {
    if (!SA.core.motion.enabled()) return () => {};
    const resumed = Date.now() - lastUnmount < 1500 && saved;

    const wrap = document.createElement('div');
    wrap.className = 'ball3d';
    wrap.setAttribute('aria-hidden', 'true');
    wrap.innerHTML = '<div class="ball3d-shadow"></div><div class="ball3d-body"><canvas></canvas><span class="ball3d-gloss"></span></div>';
    document.body.appendChild(wrap);
    const body = wrap.querySelector('.ball3d-body');
    const shadow = wrap.querySelector('.ball3d-shadow');
    const canvas = wrap.querySelector('canvas');
    const renderer = createRenderer(canvas);

    let radius = 0;
    const setSize = () => {
      radius = innerWidth < 640 ? 30 : innerWidth < 1100 ? 38 : 46;
      const dpr = Math.min(2, devicePixelRatio || 1);
      renderer.resize(Math.round(radius * 2 * dpr));
      Object.assign(body.style, { width: `${radius * 2}px`, height: `${radius * 2}px` });
      canvas.style.width = canvas.style.height = `${radius * 2}px`;
      dirty = true;
    };

    let R = resumed ? saved.R : orthonormalize(mul(axisAngle(norm([1, 0.4, 0.2]), 0.9), axisAngle([0, 1, 0], 0.5)));
    let P = resumed ? saved.P : 0;
    let vP = 0, dirty = true, raf = 0, last = performance.now(), frame = 0;
    let kick = { h: 0, v: 0 };                 // extra hop from a click
    let intro = resumed ? 1 : 0;               // 0 → 1 drop-in on first load
    let landedAt = Math.round(P);
    setSize();

    // Resolve landing spots to page geometry every frame (layout can shift as images load)
    const spotEls = spotsConfig.map((s) => ({ ...s, el: view.querySelector(s.sel) })).filter((s) => s.el);
    const geometry = () => spotEls.map((s) => {
      const r = s.el.getBoundingClientRect();
      const x = r.left + r.width * s.x;
      const contactY = s.at === 'center' ? r.top + r.height * (s.y ?? 0.5) : r.top;   // viewport
      return { x, docY: contactY + scrollY, el: s.el };
    });

    const impact = (g) => {
      const ring = document.createElement('div');
      ring.className = 'ball3d-impact';
      ring.style.left = `${g.x}px`; ring.style.top = `${g.docY - scrollY}px`;
      for (let i = 0; i < 9; i++) {
        const p = document.createElement('i');
        const a = Math.PI + (i / 8) * Math.PI;           // spray upward
        p.style.setProperty('--tx', `${Math.cos(a) * (26 + Math.random() * 30)}px`);
        p.style.setProperty('--ty', `${Math.sin(a) * (14 + Math.random() * 26)}px`);
        ring.appendChild(p);
      }
      document.body.appendChild(ring);
      setTimeout(() => ring.remove(), 700);
      g.el.classList.remove('ball3d-hit'); void g.el.offsetWidth; g.el.classList.add('ball3d-hit');
      setTimeout(() => g.el.classList.remove('ball3d-hit'), 650);
    };

    const tick = (now) => {
      raf = requestAnimationFrame(tick);
      const dt = Math.min(0.05, (now - last) / 1000); last = now; frame++;
      const G = geometry();
      if (G.length < 2) return;
      const vh = innerHeight, rest = vh * (innerWidth < 640 ? 0.62 : 0.58);

      // Scroll → target progress: spot i is "active" when its contact point sits at `rest`
      const S = G.map((g) => g.docY - rest);
      const s = scrollY;
      let target;
      if (s <= S[0]) target = 0;
      else if (s >= S[S.length - 1]) target = G.length - 1;
      else {
        let i = 0; while (i < S.length - 2 && s > S[i + 1]) i++;
        // Rest on spot i while its section scrolls up a little, then make one decisive jump,
        // and sit on spot i+1 for the rest of the segment.
        const len = Math.max(1, S[i + 1] - S[i]);
        const u = s - S[i];
        // Take off while spot i is still on screen; land as spot i+1 reaches the resting line.
        // On screen the ball then stays between (rest - jumpStart) and (rest + endDwell), whatever the gap.
        const jumpStart = Math.min(len * 0.25, vh * 0.18);
        const endDwell = Math.min(len * 0.15, vh * 0.1);
        const jumpLen = Math.max(40, len - jumpStart - endDwell);
        const t = Math.min(1, Math.max(0, (u - jumpStart) / jumpLen));
        target = i + t;
      }

      // Spring toward target (critically damped feel)
      const k = 1 - Math.exp(-dt * 7.5);
      const prevP = P;
      P += (target - P) * k;
      vP = (P - prevP) / Math.max(dt, 1e-3);

      // Position along the arc between spots
      const i = Math.min(G.length - 2, Math.floor(P)), t = P - i;
      const a = G[i], b = G[i + 1];
      const dy = Math.abs(b.docY - a.docY);
      const arc = Math.min(vh * 0.4, 110 + dy * 0.22);
      const ease = t; // linear t + parabola = natural projectile path
      const cx = a.x + (b.x - a.x) * ease;
      const contactDoc = a.docY + (b.docY - a.docY) * ease;
      const lift = arc * 4 * t * (1 - t);

      // Idle juggle when resting on a spot
      const resting = Math.abs(target - P) < 0.003 && Math.abs(vP) < 0.02;
      const juggle = resting ? Math.abs(Math.sin(now / 420)) * radius * 0.35 : 0;

      // Intro: drop from above onto the first spot with a bounce
      let drop = 0;
      if (intro < 1) {
        intro = Math.min(1, intro + dt / 1.3);
        let e = intro; const n1 = 7.5625, d1 = 2.75;
        const bounce = e < 1 / d1 ? n1 * e * e : e < 2 / d1 ? n1 * (e -= 1.5 / d1) * e + 0.75 : e < 2.5 / d1 ? n1 * (e -= 2.25 / d1) * e + 0.9375 : n1 * (e -= 2.625 / d1) * e + 0.984375;
        drop = (1 - bounce) * (contactDoc - scrollY + radius * 2);
      }

      // Click kick: simple ballistic hop
      if (kick.h > 0 || kick.v > 0) { kick.v -= 2400 * dt; kick.h = Math.max(0, kick.h + kick.v * dt); if (kick.h === 0) kick.v = 0; }

      const height = lift + juggle + drop + kick.h;                       // px above the surface
      const contactY = contactDoc - scrollY;
      const cy = contactY - radius - height;

      // Squash on contact, stretch in fast flight
      const nearContact = Math.min(t, 1 - t);
      const impactAmt = (lift < radius * 0.6 ? Math.max(0, 1 - nearContact / 0.06) : 0) * Math.min(1, Math.abs(vP) * 1.6);
      const jugSquash = resting && juggle < radius * 0.05 ? 0.08 : 0;
      const sq = Math.max(impactAmt * 0.2, jugSquash);
      const stretch = Math.min(0.12, Math.abs(vP) * 0.08) * (lift > radius ? 1 : 0);
      const sx = 1 + sq - stretch * 0.5, sy = 1 - sq + stretch;

      body.style.transform = `translate3d(${cx - radius}px, ${cy + radius * (1 - sy)}px, 0) scale(${sx}, ${sy})`;
      const shadowScale = Math.max(0.25, 1 - height / 320);
      shadow.style.transform = `translate3d(${cx - radius * 1.1}px, ${contactY - radius * 0.22}px, 0) scale(${shadowScale})`;
      shadow.style.opacity = String(Math.max(0.08, 0.55 * shadowScale));

      // Landing detection → dust and a bump on the element that was hit
      const nearest = Math.round(P);
      if (nearest !== landedAt && Math.abs(P - nearest) < 0.04 && Math.abs(vP) > 0.15) { landedAt = nearest; impact(G[nearest]); }
      if (Math.abs(P - nearest) > 0.3) landedAt = -1;

      // Spin: axis perpendicular to screen-space travel, angle from distance travelled
      const dxs = (b.x - a.x) * (P - prevP), dys = -((b.docY - a.docY) * (P - prevP)) + (kick.v * dt * 0.2);
      const travel = Math.hypot(dxs, dys);
      let spin = travel / radius;
      let axis = travel > 1e-4 ? norm([-dys, dxs * 0.35, -dxs]) : [0, 1, 0];
      if (resting && kick.h === 0) { spin = dt * 0.6; axis = [0.15, 1, 0.1]; }  // slow idle turn
      if (kick.h > 0) { spin += dt * 9; axis = norm([1, 0.3, -0.6]); }
      if (spin > 1e-4) { R = mul(axisAngle(norm(axis), spin), R); dirty = true; }
      if (frame % 120 === 0) R = orthonormalize(R);
      if (dirty) { renderer.draw(R); dirty = false; }
    };
    raf = requestAnimationFrame(tick);

    // Click the ball to kick it
    body.addEventListener('click', () => { if (kick.h === 0) { kick.v = 900; kick.h = 0.1; } });
    const onResize = SA.core.utils.debounce(setSize, 150);
    window.addEventListener('resize', onResize);

    return () => {
      cancelAnimationFrame(raf);
      window.removeEventListener('resize', onResize);
      saved = { R, P }; lastUnmount = Date.now();
      wrap.remove();
    };
  };

  SA.ui.ball3d = { mount };
})(window.SA);
