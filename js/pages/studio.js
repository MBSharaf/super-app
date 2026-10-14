/**
 * Site: design studio for print-on-demand sellers — upload artwork, place it on the product (front/back),
 * choose colours, write the listing, set the price with live profit, then save a draft or send it for review.
 * Route: #/sell/studio (new) and #/sell/studio/:id (edit).
 */
(function (SA) {
  const { esc, money } = SA.core.utils;
  const { icon } = SA.ui;
  const c = SA.ui.c;
  const S = SA.services;
  const G = () => SA.ui.garment;

  const MAX_FILE = 8 * 1024 * 1024;      // 8 MB upload
  const KEEP_PX = 1000;                   // stored preview size (the full file goes to the print partner with the backend)
  const PRINT_CM = { tee: 30, hoodie: 30, cap: 11 }; // physical width of the print area

  /** Read, check and downscale an uploaded image; returns { src, w, h } with the original pixel size */
  const readArt = (file) => new Promise((resolve, reject) => {
    if (!/^image\/(png|jpeg|webp|svg\+xml)$/.test(file.type)) return reject(new Error('Upload a PNG, JPG, WebP or SVG file.'));
    if (file.size > MAX_FILE) return reject(new Error('The file is over 8 MB. Export a smaller version.'));
    const fr = new FileReader();
    fr.onerror = () => reject(new Error('We couldn’t read that file.'));
    fr.onload = () => {
      const img = new Image();
      img.onerror = () => reject(new Error('That image looks damaged. Try exporting it again.'));
      img.onload = () => {
        const w = img.naturalWidth || 1000, h = img.naturalHeight || 1000;
        if (file.type === 'image/svg+xml') return resolve({ src: fr.result, w: 4000, h: Math.round(4000 * (h / w)), vector: true });
        const k = Math.min(1, KEEP_PX / Math.max(w, h));
        const cv = document.createElement('canvas');
        cv.width = Math.round(w * k); cv.height = Math.round(h * k);
        cv.getContext('2d').drawImage(img, 0, 0, cv.width, cv.height);
        let src = cv.toDataURL('image/webp', 0.9);
        if (!src.startsWith('data:image/webp')) src = cv.toDataURL('image/png');
        resolve({ src, w, h });
      };
      img.src = fr.result;
    };
    fr.readAsDataURL(file);
  });

  /** Effective print resolution for a placement (dots per inch) */
  const dpi = (artW, place, kind) => {
    const a = G().area(kind);
    const cm = PRINT_CM[kind] * (place.w / a.w);
    return Math.round(artW / (cm / 2.54));
  };

  const notSeller = () => `${c.pageHead({ crumbs: [['Sell on Super App', '#/sell'], ['Design studio']], title: 'Design studio' })}
    <div class="container"><div class="panel">${c.empty({ icon: 'shirt', title: 'Turn on seller mode first', text: 'The studio is for sellers. It takes a minute to open your store.', action: c.cta('Start selling', { href: '#/sell' }) })}</div></div>`;

  SA.pages.studio = {
    title: 'Design studio — Super App',
    render(ctx) {
      const s = S.sellers.current();
      if (!s) return notSeller();
      if (s.mode === 'store') return `${c.pageHead({ crumbs: [['Sell on Super App', '#/sell'], ['Design studio']], title: 'Design studio' })}<div class="container"><div class="panel">${c.empty({ icon: 'shirt', title: 'Print on demand is off for your store', text: 'Your store sells your own products. Switch your selling mode to “Both” to add designs too.', action: '<button class="btn btn-primary" data-enable-pod>Turn on print on demand</button>' })}</div></div>`;
      const d = ctx.params.id ? S.pod.get(ctx.params.id) : null;
      if (ctx.params.id && (!d || d.sellerId !== s.id)) return `${c.pageHead({ crumbs: [['Sell on Super App', '#/sell'], ['Design studio']], title: 'Design not found' })}<div class="container"><div class="panel">${c.empty({ icon: 'image', title: 'We couldn’t find that design', text: 'It may have been deleted.', action: c.cta('New design', { href: '#/sell/studio' }) })}</div></div>`;
      if (!d) {
        const lim = S.sellers.canAddDesign(s);
        if (!lim.ok) return `${c.pageHead({ crumbs: [['Sell on Super App', '#/sell'], ['Design studio']], title: 'Design limit reached' })}<div class="container"><div class="panel">${c.empty({ icon: 'lock', title: `You have ${lim.used} of ${lim.limit} active designs`, text: 'Pause or delete a design, or level up to add more.', action: c.cta('See my designs', { href: '#/sell' }) })}</div></div>`;
      }
      const items = S.pod.catalog();
      const lvl = S.sellers.levelOf(s);
      const cats = S.pod.categories();
      return `${c.pageHead({ crumbs: [['Sell on Super App', '#/sell'], [d ? 'Edit design' : 'New design']], title: d ? esc(d.title || 'Edit design') : 'New design',
        text: d?.status === 'rejected' ? `Rejected: ${esc(d.reason)} — fix it and send it again.` : d?.status === 'live' ? 'Editing a live design takes it out of the shop until it’s approved again.' : `Level ${esc(lvl.name)}${lvl.baseDiscount ? ` · ${money(lvl.baseDiscount)} off every base price` : ''}` })}
      <div class="container studio">
        <section class="studio-stage panel">
          <div class="studio-tabs" role="tablist" aria-label="Side" data-sides></div>
          <button type="button" class="btn btn-outline btn-sm studio-3d-btn" data-3d>${icon('sparkle', 14)} 3D preview</button>
          <div class="studio-canvas" data-canvas></div>
          <div class="studio-tools" data-tools hidden>
            <label class="studio-size"><span class="xs muted">Size</span><input type="range" min="8" max="100" value="60" data-size aria-label="Design size" /></label>
            <button class="btn btn-ghost btn-sm" type="button" data-center>${icon('target', 14)} Centre</button>
            <button class="btn btn-ghost btn-sm" type="button" data-replace>${icon('image', 14)} Replace</button>
          </div>
          <p class="studio-dpi xs" data-dpi></p>
          <div class="studio-colors" data-preview-colors></div>
        </section>
        <form class="studio-form stack" style="--gap:16px" id="studio-form" novalidate>
          <section class="panel panel-pad">
            <h2 class="h4">1. Product</h2>
            <div class="studio-products">${items.map((x) => `<label class="studio-product"><input type="radio" name="catalogId" value="${x.id}" ${(d?.catalogId || 'pod-tee') === x.id ? 'checked' : ''} />
              <span class="sp-media" data-sp="${x.id}">${SA.ui.garmentView({ kind: x.kind, hex: x.colors[0].hex, label: x.name })}</span><b class="small">${esc(x.name)}</b><span class="xs muted">from ${money(S.pod.baseFor(x.id, { sellerId: s.id }))}</span></label>`).join('')}</div>
          </section>
          <section class="panel panel-pad">
            <h2 class="h4">2. Artwork</h2>
            <label class="studio-drop" data-drop><input type="file" accept="image/png,image/jpeg,image/webp,image/svg+xml" data-file hidden />
              ${icon('image', 26)}<b>Upload your design</b><span class="xs muted">PNG with a transparent background works best · up to 8 MB · 3000 px wide for sharp prints</span></label>
            <div class="studio-print-on" data-print-on hidden><span class="field-label">Print on</span><div class="chips" role="group" aria-label="Print on"></div></div>
          </section>
          <section class="panel panel-pad">
            <h2 class="h4">3. Colours</h2>
            <p class="xs muted">Customers choose from the colours you tick.</p>
            <div class="studio-swatches" data-swatches></div>
          </section>
          <section class="panel panel-pad">
            <h2 class="h4">4. Listing</h2>
            <div class="form-grid" style="margin-top:12px">
              <div class="field span-2"><label for="st-title">Title</label><input class="input" id="st-title" name="title" maxlength="60" value="${esc(d?.title || '')}" placeholder="e.g. Cairo Nights FC" /></div>
              <div class="field span-2"><label for="st-desc">Description</label><textarea class="textarea" id="st-desc" name="desc" maxlength="500" placeholder="Who is it for? What’s the idea behind it?">${esc(d?.desc || '')}</textarea></div>
              <div class="field"><label for="st-cat">Category</label><select class="select" id="st-cat" name="category"><option value="">Choose…</option>${cats.map((x) => `<option value="${x.id}" ${d?.category === x.id ? 'selected' : ''}>${esc(x.name)}</option>`).join('')}</select></div>
              <div class="field"><label for="st-tags">Search tags <span class="muted">(comma separated)</span></label><input class="input" id="st-tags" name="tags" maxlength="120" value="${esc((d?.tags || []).join(', '))}" placeholder="football, cairo, gift" /></div>
            </div>
          </section>
          <section class="panel panel-pad">
            <h2 class="h4">5. Price</h2>
            <div class="studio-price">
              <div class="field"><label for="st-price">Your price (EGP)</label><input class="input num" id="st-price" name="price" type="number" inputmode="numeric" value="${d?.price || ''}" /><span class="field-hint" data-range></span></div>
              <div class="sell-split" data-split></div>
            </div>
          </section>
          <p class="field-error" data-err></p>
          <div class="studio-actions">
            <button class="btn btn-outline btn-lg" type="button" data-draft>${icon('file', 16)} Save draft</button>
            <button class="btn btn-blaze btn-lg" type="submit">${icon('arrowUpRight', 16)} ${lvl.review === 'instant' ? 'Publish now' : 'Send for review'}</button>
          </div>
          <p class="xs muted">${lvl.review === 'instant' ? 'Elite sellers publish straight away; we spot-check later.' : lvl.review === 'priority' ? 'Pro sellers are reviewed first — usually within a few hours.' : 'We review every design, usually within 24 hours.'} By sending it you confirm you own the rights to this artwork.</p>
        </form>
      </div>`;
    },

    mount(el, ctx) {
      el.querySelector('[data-enable-pod]')?.addEventListener('click', () => { const s = S.sellers.current(); S.sellers.update(s.id, { mode: 'both' }); SA.app.rerender(); });
      const form = el.querySelector('#studio-form');
      if (!form) return;
      const seller = S.sellers.current();
      const existing = ctx.params.id ? S.pod.get(ctx.params.id) : null;
      // Working state
      const st = {
        id: existing?.id || null,
        art: existing?.src || '', artW: existing?.artW || (existing ? 3000 : 0), artH: existing?.artH || (existing ? 3000 : 0),
        place: { front: existing?.placement?.front || null, back: existing?.placement?.back || null },
        side: existing?.placement?.front ? 'front' : existing?.placement?.back ? 'back' : 'front',
        colors: existing?.colors?.slice() || [], preview: existing?.colors?.[0] || '',
      };
      const item = () => S.pod.catalogItem(form.querySelector('[name="catalogId"]:checked').value);
      const ratio = () => (st.artW ? st.artH / st.artW : 1);
      const defPlace = (kind) => { const a = G().area(kind); return G().clampPlace(kind, { x: a.x + a.w / 2, y: a.y + a.h * 0.42, w: a.w * 0.8 }, ratio()); };
      const sides = () => ['front', 'back'].filter((x) => st.place[x]);
      const draft = () => {
        const f = SA.core.utils.formData(form);
        return { id: st.id, catalogId: f.catalogId, title: f.title, desc: f.desc, category: f.category, tags: String(f.tags || '').split(',').map((t) => t.trim()).filter(Boolean),
          price: +f.price, colors: st.colors, art: st.art, artW: st.artW, artH: st.artH, placement: { front: st.place.front, back: st.place.back } };
      };

      const $ = (q) => el.querySelector(q);
      const renderSides = () => {
        const x = item();
        $('[data-sides]').innerHTML = x.areas.map((a) => `<button type="button" class="studio-tab" role="tab" aria-selected="${st.side === a}" data-side="${a}">${a === 'front' ? 'Front' : 'Back'}${st.place[a] ? ` ${icon('check', 12, 2.6)}` : ''}</button>`).join('');
        const po = $('[data-print-on]');
        po.hidden = !st.art || x.areas.length < 2;
        po.querySelector('.chips').innerHTML = [['front', 'Front'], ['back', `Back +${money(x.backExtra)}`], ['both', 'Both sides']].map(([v, l]) => {
          const on = v === 'both' ? !!(st.place.front && st.place.back) : v === 'front' ? !!st.place.front && !st.place.back : !!st.place.back && !st.place.front;
          return `<button type="button" class="chip" aria-pressed="${on}" data-print="${v}">${l}</button>`;
        }).join('');
      };
      const renderCanvas = () => {
        const x = item();
        const col = x.colors.find((k) => k.name === st.preview) || x.colors.find((k) => st.colors.includes(k.name)) || x.colors[0];
        const p = st.place[st.side];
        const gopts = { kind: x.kind, hex: col.hex, art: st.art, place: p, side: st.side, artRatio: ratio(), showArea: true, label: `${x.name} ${st.side} preview in ${col.name}` };
        $('[data-canvas]').innerHTML = (SA.ui.photoMock?.photoSvg(gopts) || G()(gopts))
          + (st.art ? '' : `<button type="button" class="studio-canvas-cta" data-upload>${icon('plus', 18)} Add your design</button>`)
          + (st.art && !p ? `<button type="button" class="studio-canvas-cta" data-add-side>${icon('plus', 18)} Print on the ${st.side} too</button>` : '');
        $('[data-tools]').hidden = !(st.art && p);
        if (p) $('[data-size]').value = Math.round((p.w / G().area(x.kind).w) * 100);
        const dp = $('[data-dpi]');
        if (st.art && p) {
          const v = dpi(st.artW, p, x.kind);
          dp.innerHTML = v >= 150 ? `${icon('check', 12, 2.6)} Print quality: sharp (${v} dpi)` : v >= 90 ? `${icon('alert', 12)} Print quality: OK (${v} dpi). A bigger file will look sharper.` : `${icon('alert', 12)} Too small for this size (${v} dpi) — it will look blurry. Upload a bigger file or make it smaller.`;
          dp.className = `studio-dpi xs ${v >= 150 ? 'text-turf' : v >= 90 ? '' : 'text-danger'}`;
        } else dp.textContent = '';
        $('[data-preview-colors]').innerHTML = x.colors.filter((k) => !st.colors.length || st.colors.includes(k.name)).map((k) => `<button type="button" class="pc-sw sw-btn ${k.name === col.name ? 'is-on' : ''}" style="--c:${k.hex}" data-preview="${esc(k.name)}" aria-label="Preview in ${esc(k.name)}" title="${esc(k.name)}"></button>`).join('');
        bindDrag();
      };
      const renderSwatches = () => {
        const x = item();
        st.colors = st.colors.filter((n) => x.colors.some((k) => k.name === n));
        if (!st.colors.length) st.colors = [x.colors[0].name];
        $('[data-swatches]').innerHTML = x.colors.map((k) => `<label class="studio-sw"><input type="checkbox" value="${esc(k.name)}" ${st.colors.includes(k.name) ? 'checked' : ''} /><span class="pc-sw" style="--c:${k.hex}"></span><span class="small">${esc(k.name)}</span></label>`).join('');
      };
      const renderPrice = () => {
        const x = item();
        const back = !!st.place.back;
        const base = S.pod.baseFor(x.id, { back, sellerId: seller.id });
        const inp = form.price;
        if (!inp.value) inp.value = Math.max(x.minPrice, base + Math.round((x.base * 0.6) / 10) * 10);
        const p = +inp.value || 0;
        const ok = p >= x.minPrice && p <= x.maxPrice && p > base;
        $('[data-range]').textContent = `Between ${money(x.minPrice)} and ${money(x.maxPrice)}`;
        $('[data-range]').className = `field-hint ${ok ? '' : 'text-danger'}`;
        $('[data-split]').innerHTML = `<span style="flex:${base}"><b class="num">${money(base)}</b><small>Base${back ? ' (incl. back print)' : ''}</small></span><span class="is-you" style="flex:${Math.max(1, p - base)}"><b class="num">${money(Math.max(0, p - base))}</b><small>You earn per item</small></span>`;
      };
      const all = () => { renderSides(); renderSwatches(); renderCanvas(); renderPrice(); };
      // The first render floats in; after that edits redraw in place (no replayed animation), side changes flip
      const canvasEl = $('[data-canvas]');
      // When the product photos finish loading, swap the drawn previews for the real photos (keeps the edits)
      const offPhotos = SA.ui.photoMock?.onReady(() => {
        if (!canvasEl.isConnected) return offPhotos?.();
        if (canvasEl.querySelector('svg[data-photo]')) return; // already on photos
        el.querySelectorAll('[data-sp]').forEach((n) => { const c = S.pod.catalogItem(n.dataset.sp); n.innerHTML = SA.ui.garmentView({ kind: c.kind, hex: c.colors[0].hex, label: c.name }); });
        renderCanvas();
      });
      setTimeout(() => canvasEl.classList.add('is-entered'), 750);
      const flip = () => { canvasEl.classList.remove('is-flip'); void canvasEl.offsetWidth; canvasEl.classList.add('is-flip'); setTimeout(() => canvasEl.classList.remove('is-flip'), 650); };
      /** Full 3D preview in a dialog: drag to turn front/back, in the colour being previewed */
      const preview3d = () => {
        const x = item();
        const col = x.colors.find((k) => k.name === st.preview) || x.colors.find((k) => st.colors.includes(k.name)) || x.colors[0];
        SA.ui.overlay.open({
          wide: true, title: '3D preview', subtitle: 'Drag to turn it. This is how customers see it on the product page.',
          body: `<div class="g3d-modal" data-g3d-modal></div><div class="studio-colors" data-g3d-colors>${x.colors.filter((k) => st.colors.includes(k.name)).map((k) => `<button type="button" class="pc-sw sw-btn ${k.name === col.name ? 'is-on' : ''}" style="--c:${k.hex}" data-hex="${k.hex}" aria-label="${esc(k.name)}" title="${esc(k.name)}"></button>`).join('')}</div>`,
          onMount(m) {
            const v = SA.ui.garment3d.mount(m.querySelector('[data-g3d-modal]'), { kind: x.kind, hex: col.hex, art: st.art, place: { front: st.place.front, back: st.place.back }, artRatio: ratio(), label: draft().title || x.name });
            m.querySelector('[data-g3d-colors]').addEventListener('click', (e) => {
              const b = e.target.closest('[data-hex]'); if (!b) return;
              m.querySelectorAll('[data-hex]').forEach((n) => n.classList.toggle('is-on', n === b));
              v.update({ hex: b.dataset.hex });
            });
          },
        });
      };

      // Drag the artwork inside the print area
      const bindDrag = () => {
        const svg = $('[data-canvas] svg'), img = svg?.querySelector('.g-art');
        if (!img) return;
        const kind = item().kind;
        // Real-photo canvas works in photo pixels; the drawn one in the 400×440 template
        const photo = svg.dataset.photo === '1';
        const toCanvas = (e) => { const pt = svg.createSVGPoint(); pt.x = e.clientX; pt.y = e.clientY; const q = pt.matrixTransform(svg.getScreenCTM().inverse()); return photo ? SA.ui.photoMock.toTemplate(kind, st.side, q.x, q.y) : { x: q.x / G().W, y: q.y / G().H }; };
        const placeArt = (p) => {
          if (photo) { const r = SA.ui.photoMock.placeRect(kind, st.side, p, ratio()); img.setAttribute('x', r.x); img.setAttribute('y', r.y); return; }
          const w = p.w * G().W, h = w * ratio();
          img.setAttribute('x', p.x * G().W - w / 2); img.setAttribute('y', p.y * G().H - h / 2);
        };
        let start = null;
        img.style.cursor = 'grab'; img.style.touchAction = 'none';
        img.addEventListener('pointerdown', (e) => { e.preventDefault(); try { img.setPointerCapture(e.pointerId); } catch { /* synthetic or lost pointer */ } const q = toCanvas(e); const p = st.place[st.side]; start = { dx: p.x - q.x, dy: p.y - q.y }; img.style.cursor = 'grabbing'; });
        img.addEventListener('pointermove', (e) => {
          if (!start) return;
          const q = toCanvas(e);
          const p = G().clampPlace(kind, { ...st.place[st.side], x: q.x + start.dx, y: q.y + start.dy }, ratio());
          st.place[st.side] = p;
          placeArt(p);
        });
        const end = () => { if (start) { start = null; img.style.cursor = 'grab'; } };
        img.addEventListener('pointerup', end); img.addEventListener('pointercancel', end);
        // Arrow keys nudge the design (keyboard users)
        svg.setAttribute('tabindex', '0');
        svg.addEventListener('keydown', (e) => {
          const k = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] }[e.key]; if (!k) return;
          e.preventDefault();
          const p = st.place[st.side];
          st.place[st.side] = G().clampPlace(kind, { ...p, x: p.x + k[0] * 0.01, y: p.y + k[1] * 0.01 }, ratio());
          renderCanvas(); $('[data-canvas] svg')?.focus();
        });
      };

      const upload = async (file) => {
        if (!file) return;
        try {
          const a = await readArt(file);
          Object.assign(st, { art: a.src, artW: a.w, artH: a.h });
          const kind = item().kind;
          if (!st.place.front && !st.place.back) st.place.front = defPlace(kind);
          ['front', 'back'].forEach((sd) => { if (st.place[sd]) st.place[sd] = G().clampPlace(kind, st.place[sd], ratio()); });
          all();
        } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
      };
      const fileInput = $('[data-file]');
      fileInput.addEventListener('change', () => upload(fileInput.files[0]));
      const drop = $('[data-drop]');
      ['dragenter', 'dragover'].forEach((t) => drop.addEventListener(t, (e) => { e.preventDefault(); drop.classList.add('is-over'); }));
      ['dragleave', 'drop'].forEach((t) => drop.addEventListener(t, (e) => { e.preventDefault(); drop.classList.remove('is-over'); }));
      drop.addEventListener('drop', (e) => upload(e.dataTransfer.files[0]));

      el.addEventListener('click', (e) => {
        if (e.target.closest('[data-upload],[data-replace]')) return fileInput.click();
        const sd = e.target.closest('[data-side]'); if (sd) { if (sd.dataset.side !== st.side) { st.side = sd.dataset.side; flip(); } renderSides(); renderCanvas(); return; }
        if (e.target.closest('[data-3d]')) return preview3d();
        if (e.target.closest('[data-add-side]')) { st.place[st.side] = defPlace(item().kind); all(); return; }
        const pr = e.target.closest('[data-print]');
        if (pr) {
          const kind = item().kind, v = pr.dataset.print;
          const src = st.place.front || st.place.back || defPlace(kind);
          st.place = { front: v !== 'back' ? st.place.front || src : null, back: v !== 'front' ? st.place.back || src : null };
          st.side = v === 'back' ? 'back' : 'front';
          all(); return;
        }
        if (e.target.closest('[data-center]')) { const a = G().area(item().kind); st.place[st.side] = G().clampPlace(item().kind, { ...st.place[st.side], x: a.x + a.w / 2 }, ratio()); renderCanvas(); return; }
        const pv = e.target.closest('[data-preview]'); if (pv) { st.preview = pv.dataset.preview; renderCanvas(); }
      });
      $('[data-size]').addEventListener('input', (e) => {
        const kind = item().kind, a = G().area(kind);
        st.place[st.side] = G().clampPlace(kind, { ...st.place[st.side], w: (a.w * +e.target.value) / 100 }, ratio());
        renderCanvas();
      });
      form.addEventListener('change', (e) => {
        if (e.target.name === 'catalogId') {
          const x = item();
          if (!x.areas.includes('back')) st.place.back = null;
          if (!st.place.front && !st.place.back && st.art) st.place.front = defPlace(x.kind);
          ['front', 'back'].forEach((sd) => { if (st.place[sd]) st.place[sd] = G().clampPlace(x.kind, st.place[sd], ratio()); });
          if (!x.areas.includes(st.side)) st.side = 'front';
          form.price.value = '';
          all();
        }
        if (e.target.closest('[data-swatches]')) { st.colors = [...el.querySelectorAll('[data-swatches] input:checked')].map((i) => i.value); if (!st.colors.includes(st.preview)) st.preview = st.colors[0] || ''; renderCanvas(); }
      });
      form.price.addEventListener('input', renderPrice);

      const save = (submit) => {
        const err = $('[data-err]'); err.textContent = '';
        try {
          if (submit && !st.art) throw new Error('Upload your design first.');
          if (submit && !(st.place.front || st.place.back)) throw new Error('Place your design on the front or the back.');
          const d = S.pod.saveDraft(seller.id, draft());
          st.id = d.id;
          if (!submit) { SA.ui.overlay.toast('Draft saved'); history.replaceState(null, '', `#/sell/studio/${d.id}`); return; }
          const out = S.pod.submit(d.id);
          SA.core.motion?.confetti?.({ count: 70 });
          SA.ui.overlay.toast(out.status === 'live' ? `“${out.title}” is live in the shop` : `“${out.title}” is in review — we’ll notify you`, { type: 'success' });
          SA.core.router.go(SA.services.dash.me()?.role === 'seller' ? '/dash/seller/designs' : '/sell');
        } catch (e2) { err.textContent = e2.message; SA.ui.overlay.toast(e2.message, { type: 'error' }); }
      };
      el.querySelector('[data-draft]').addEventListener('click', () => save(false));
      form.addEventListener('submit', (e) => { e.preventDefault(); save(true); });
      all();
    },
  };
})(window.SA);
