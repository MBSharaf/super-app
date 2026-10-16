/**
 * Real 3D garments: GLB models made from photos of the blank garments with Microsoft TRELLIS (image → 3D), shown with
 * three.js (vendor/three, loaded only when a 3D view opens). The garment takes the chosen colour and the artwork is
 * projected onto the fabric (a decal), so it follows the folds — front and back can have different designs.
 *
 * SA.ui.model3d.has(kind) → a model exists for this garment and the browser has WebGL
 * SA.ui.model3d.mount(el, opts) → same API as SA.ui.garment3d: { update(opts), show(side), destroy() }
 *   opts: { kind, hex, art, artBack, place: { front, back }, artRatio, artRatioBack, label, spin (keeps turning), hint: false,
 *           edit: true → dragging the print moves it (onPlace(side, place)); turning the garment calls onSide(side) }
 * Placements use the same template numbers as the 2D mockups ({ x, y, w } in the 400×440 canvas).
 */
(function (SA) {
  const BASE = 'vendor/three/';
  /** print: the catalogue print area mapped onto the model (model units, height of the garment = 1, y up, front = +z) */
  const MODELS = {
    tee: { url: 'models/tee.glb', print: { front: { x0: -0.16, x1: 0.16, y0: -0.31, y1: 0.27 }, back: { x0: -0.16, x1: 0.16, y0: -0.31, y1: 0.31 } } },
    // hoodie: { url: 'models/hoodie.glb', print: { ... } }  ← add when the hoodie model is generated
  };
  let lib = null;
  const load = () => (lib ||= Promise.all([
    import(new URL(`${BASE}three.module.min.js`, document.baseURI).href),
    import(new URL(`${BASE}addons/GLTFLoader.js`, document.baseURI).href),
    import(new URL(`${BASE}addons/DecalGeometry.js`, document.baseURI).href),
  ]).then(([THREE, { GLTFLoader }, { DecalGeometry }]) => ({ THREE, GLTFLoader, DecalGeometry })));
  const geoCache = {};
  const geometry = (kind) => (geoCache[kind] ||= load().then(({ THREE, GLTFLoader }) => new Promise((res, rej) => {
    new GLTFLoader().load(new URL(MODELS[kind].url, document.baseURI).href, (g) => {
      let geo = null;
      g.scene.traverse((o) => { if (o.isMesh && !geo) geo = o.geometry; });
      if (!geo) return rej(new Error('Empty model'));
      geo.computeBoundingBox();
      const b = geo.boundingBox, c = new THREE.Vector3(), s = new THREE.Vector3();
      b.getCenter(c); b.getSize(s);
      geo.translate(-c.x, -c.y, -c.z);
      geo.scale(1 / s.y, 1 / s.y, 1 / s.y); // height = 1
      geo.computeVertexNormals();
      geo.computeBoundingBox();
      res(geo);
    }, undefined, rej);
  })));

  let webgl = null;
  const hasWebGL = () => {
    if (webgl == null) { try { const c = document.createElement('canvas'); webgl = !!(c.getContext('webgl2') || c.getContext('webgl')); } catch { webgl = false; } }
    return webgl;
  };
  const has = (kind) => !!MODELS[kind] && hasWebGL();

  /** Template placement → model rectangle on one side (centre x/y and width in model units) */
  const toModel = (kind, side, p) => {
    const a = SA.ui.garment.area(kind), r = MODELS[kind].print[side];
    const u = (p.x - a.x) / a.w, v = (p.y - a.y) / a.h;
    const x = r.x0 + u * (r.x1 - r.x0);
    return { x: side === 'back' ? -x : x, y: r.y1 - v * (r.y1 - r.y0), w: (p.w / a.w) * (r.x1 - r.x0) };
  };
  const toTemplate = (kind, side, x, y, w) => {
    const a = SA.ui.garment.area(kind), r = MODELS[kind].print[side];
    const mx = side === 'back' ? -x : x;
    return { x: a.x + ((mx - r.x0) / (r.x1 - r.x0)) * a.w, y: a.y + ((r.y1 - y) / (r.y1 - r.y0)) * a.h, w };
  };

  const mount = (el, opts) => {
    let o = { ...opts };
    let dead = false, flat = null, raf = 0, three = null, renderer, scene, camera, pivot, body, mat, geo;
    const decals = { front: null, back: null };
    let angle = 0, target = 0, drag = null, idleT = 0, last = performance.now();
    const reduced = () => !SA.core.motion?.enabled?.() || matchMedia('(prefers-reduced-motion: reduce)').matches;
    el.classList.add('g3d', 'm3d');
    el.setAttribute('tabindex', '0');
    el.setAttribute('role', 'img');
    el.setAttribute('aria-label', `${o.label || 'Product'} — 3D view. Drag ${o.edit ? 'the garment to turn it, or the print to move it' : 'or use the arrow keys to turn it'}.`);
    el.innerHTML = `<div class="m3d-stage"><div class="m3d-loading xs muted">${SA.ui.icon('sparkle', 14)} <span>Loading 3D…</span></div></div><div class="g3d-shadow"></div>
      ${o.hint === false ? '' : `<div class="g3d-hint xs">${SA.ui.icon('swap', 13)} <span>${o.edit ? 'Drag to turn · drag the print to move it' : 'Drag to turn'}</span></div>`}`;
    const stage = el.querySelector('.m3d-stage');

    const texCache = new Map();
    const texture = (src) => {
      if (!src) return Promise.resolve(null);
      if (texCache.has(src)) return texCache.get(src);
      const p = new Promise((res) => {
        const img = new Image(); img.crossOrigin = 'anonymous';
        img.onload = () => { const t = new three.THREE.Texture(img); t.colorSpace = three.THREE.SRGBColorSpace; t.anisotropy = 4; t.needsUpdate = true; res(t); };
        img.onerror = () => res(null);
        img.src = src;
      });
      texCache.set(src, p);
      return p;
    };
    /** Keep only the decal triangles that face the print side (the projection box also reaches the fabric behind) */
    const facing = (g, dir) => {
      const pos = g.getAttribute('position'), nor = g.getAttribute('normal'), uv = g.getAttribute('uv');
      const keep = { p: [], n: [], u: [] };
      for (let i = 0; i < pos.count; i += 3) {
        if ((nor.getZ(i) + nor.getZ(i + 1) + nor.getZ(i + 2)) * dir < 0.45) continue;
        for (let k = i; k < i + 3; k++) { keep.p.push(pos.getX(k), pos.getY(k), pos.getZ(k)); keep.n.push(nor.getX(k), nor.getY(k), nor.getZ(k)); keep.u.push(uv.getX(k), uv.getY(k)); }
      }
      const { THREE } = three, out = new THREE.BufferGeometry();
      out.setAttribute('position', new THREE.Float32BufferAttribute(keep.p, 3));
      out.setAttribute('normal', new THREE.Float32BufferAttribute(keep.n, 3));
      out.setAttribute('uv', new THREE.Float32BufferAttribute(keep.u, 2));
      g.dispose();
      return out;
    };
    const sideArt = (side) => (side === 'back' ? o.artBack || o.art : o.art);
    const sideRatio = (side) => (side === 'back' && o.artBack ? o.artRatioBack || 1 : o.artRatio || 1);
    let decalJob = 0;
    /** Rebuild the print on one side (or both) */
    const buildDecals = async (only) => {
      if (!three) return;
      const job = ++decalJob;
      const { THREE, DecalGeometry } = three;
      for (const side of only ? [only] : ['front', 'back']) {
        const p = o.place?.[side], src = sideArt(side);
        const tex = p && src ? await texture(src) : null;
        if (job !== decalJob && !only) return;
        if (decals[side]) { pivot.remove(decals[side]); decals[side].geometry.dispose(); decals[side].material.dispose(); decals[side] = null; }
        if (!tex) continue;
        const m = toModel(o.kind, side, p), h = m.w * sideRatio(side);
        const keep = pivot.rotation.y; pivot.rotation.y = 0; pivot.updateMatrixWorld(true);
        // Find the fabric surface under the print centre
        const ray = new THREE.Raycaster(new THREE.Vector3(m.x, m.y, side === 'back' ? -3 : 3), new THREE.Vector3(0, 0, side === 'back' ? 1 : -1));
        const hit = ray.intersectObject(body, false)[0];
        const pos = hit ? hit.point : new THREE.Vector3(m.x, m.y, 0);
        const dg = facing(new DecalGeometry(body, pos, new THREE.Euler(0, side === 'back' ? Math.PI : 0, 0), new THREE.Vector3(m.w, h, 0.3)), side === 'back' ? -1 : 1);
        pivot.rotation.y = keep; pivot.updateMatrixWorld(true);
        const dm = new THREE.MeshStandardMaterial({ map: tex, transparent: true, roughness: 0.95, metalness: 0, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -4 });
        decals[side] = new THREE.Mesh(dg, dm);
        decals[side].userData.side = side;
        pivot.add(decals[side]);
      }
    };
    const colour = () => {
      if (!mat) return;
      const c = new three.THREE.Color(o.hex || '#ffffff');
      // Very dark fabric still shows its folds; very light fabric doesn't burn out
      const l = SA.ui.garment.lum(o.hex || '#ffffff');
      if (l < 0.12) c.offsetHSL(0, 0, 0.05);
      if (l > 0.9) c.multiplyScalar(0.94);
      mat.color.copy(c);
    };
    const size = () => {
      if (!renderer) return;
      const w = stage.clientWidth || 300, h = stage.clientHeight || w;
      renderer.setSize(w, h, false);
      camera.aspect = w / h; camera.updateProjectionMatrix();
    };

    const start = async () => {
      try {
        three = await load();
        geo = await geometry(o.kind);
      } catch (err) { console.warn('3D model', err); if (!dead) { el.classList.remove('m3d'); flat = SA.ui.garment3d.mount(el, { ...o, flat: true }); } return; }
      if (dead) return;
      const { THREE } = three;
      renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, powerPreference: 'low-power' });
      renderer.setPixelRatio(Math.min(2, window.devicePixelRatio || 1));
      renderer.outputColorSpace = THREE.SRGBColorSpace;
      renderer.toneMapping = THREE.ACESFilmicToneMapping; renderer.toneMappingExposure = 1.05;
      stage.innerHTML = ''; stage.appendChild(renderer.domElement);
      scene = new THREE.Scene();
      camera = new THREE.PerspectiveCamera(28, 1, 0.1, 20);
      camera.position.set(0, 0.02, 2.35);
      scene.add(new THREE.HemisphereLight(0xffffff, 0x8a8f88, 1.6));
      const key = new THREE.DirectionalLight(0xffffff, 1.9); key.position.set(1.2, 1.6, 2.2); scene.add(key);
      const rim = new THREE.DirectionalLight(0xffffff, 0.9); rim.position.set(-1.6, 0.6, -2); scene.add(rim);
      const fill = new THREE.DirectionalLight(0xffffff, 0.5); fill.position.set(-1.5, -0.4, 1.4); scene.add(fill);
      pivot = new THREE.Group(); scene.add(pivot);
      mat = new THREE.MeshStandardMaterial({ roughness: 0.93, metalness: 0, side: THREE.DoubleSide });
      body = new THREE.Mesh(geo, mat); pivot.add(body);
      colour();
      size();
      await buildDecals();
      loop(performance.now());
    };

    const apply = () => {
      if (!pivot) return;
      const t = performance.now() / 1000;
      const sway = !drag && !o.edit && !reduced() ? Math.sin(t * 0.8) * 0.16 * Math.min(1, idleT) : 0;
      pivot.rotation.y = angle + sway;
      pivot.position.y = !reduced() && !o.edit ? Math.sin(t * 1.3) * 0.012 : 0;
      const n = (((angle % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI));
      el.classList.toggle('is-back', n > Math.PI / 2 && n < (3 * Math.PI) / 2);
    };
    const loop = (now) => {
      if (dead) return;
      const dt = Math.min(0.05, (now - last) / 1000); last = now;
      // spin: turns slowly on its own (pop-up ads); otherwise it eases to the front or back
      if (!drag && o.spin && !reduced()) target += dt * 0.9;
      if (!drag) { angle += (target - angle) * Math.min(1, dt * 7); idleT = Math.min(1, idleT + dt * 0.4); }
      apply();
      renderer.render(scene, camera);
      raf = requestAnimationFrame(loop);
    };
    const sideNow = () => { const n = (((target % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI)); return n > Math.PI / 2 && n < (3 * Math.PI) / 2 ? 'back' : 'front'; };

    // Pointer: drag the print (studio) or turn the garment
    const ndc = (e) => { const r = renderer.domElement.getBoundingClientRect(); return new three.THREE.Vector2(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1); };
    const pick = (e, objs) => { const rc = new three.THREE.Raycaster(); rc.setFromCamera(ndc(e), camera); return rc.intersectObjects(objs.filter(Boolean), false)[0]; };
    const down = (e) => {
      if (!renderer) return;
      el.querySelector('.g3d-hint')?.classList.add('is-gone');
      idleT = 0;
      const side = sideNow();
      const onPrint = o.edit && decals[side] && pick(e, [decals[side]]);
      drag = onPrint ? { print: side } : { x: e.clientX, a: angle };
      el.classList.add('is-drag');
      try { el.setPointerCapture?.(e.pointerId); } catch { /* synthetic or lost pointer */ }
    };
    let moveRaf = 0;
    const move = (e) => {
      if (!drag) return;
      if (drag.print) {
        if (moveRaf) return;
        moveRaf = requestAnimationFrame(() => {
          moveRaf = 0;
          if (!drag?.print) return;
          const hit = pick(e, [body]); if (!hit) return;
          const local = pivot.worldToLocal(hit.point.clone());
          const side = drag.print, cur = o.place[side];
          const p = SA.ui.garment.clampPlace(o.kind, toTemplate(o.kind, side, local.x, local.y, cur.w), sideRatio(side));
          o.place = { ...o.place, [side]: p };
          buildDecals(side);
          o.onPlace?.(side, p, { live: true });
        });
        return;
      }
      angle = drag.a + (e.clientX - drag.x) * 0.0105; target = angle;
    };
    const up = () => {
      if (!drag) return;
      const was = drag; drag = null; el.classList.remove('is-drag');
      if (was.print) { o.onPlace?.(was.print, o.place[was.print], { live: false }); return; }
      target = Math.round(angle / Math.PI) * Math.PI;
      o.onSide?.(sideNow());
    };
    const key = (e) => { if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') { e.preventDefault(); target += e.key === 'ArrowRight' ? Math.PI : -Math.PI; idleT = 0; o.onSide?.(sideNow()); } };
    el.addEventListener('pointerdown', down);
    el.addEventListener('pointermove', move);
    el.addEventListener('pointerup', up);
    el.addEventListener('pointercancel', up);
    el.addEventListener('keydown', key);
    const ro = new ResizeObserver(size); ro.observe(stage);
    start();

    const api = {
      update(next) {
        if (flat) return flat.update(next);
        const prev = o; o = { ...o, ...next };
        if (next.hex !== undefined) colour();
        const artChanged = next.art !== undefined && next.art !== prev.art || next.artBack !== undefined && next.artBack !== prev.artBack || next.artRatio !== prev.artRatio && next.artRatio !== undefined || next.artRatioBack !== undefined && next.artRatioBack !== prev.artRatioBack;
        if (next.place || artChanged) buildDecals(artChanged ? null : next.only);
      },
      show(side) { if (flat) return flat.show(side); const base = Math.round(target / (2 * Math.PI)) * 2 * Math.PI; target = base + (side === 'back' ? Math.PI : 0); idleT = 0; },
      destroy() {
        flat?.destroy(); dead = true; cancelAnimationFrame(raf); ro.disconnect();
        ['pointerdown', 'pointermove', 'pointerup', 'pointercancel', 'keydown'].forEach((t) => el.removeEventListener(t, { pointerdown: down, pointermove: move, pointerup: up, pointercancel: up, keydown: key }[t]));
        Object.values(decals).forEach((d) => { if (d) { d.geometry.dispose(); d.material.dispose(); } });
        mat?.dispose(); renderer?.dispose();
      },
    };
    const watch = setInterval(() => { if (!el.isConnected) { api.destroy(); clearInterval(watch); } }, 2000);
    return api;
  };

  SA.ui.model3d = { has, mount, load, MODELS };
})(window.SA);
