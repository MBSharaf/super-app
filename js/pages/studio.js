/**
 * Site: design studio for print-on-demand sellers — upload a front and/or back design, place it on the 3D garment (or on
 * the photo mockup while a garment has no 3D model), read the product details and size chart, choose colours, write the
 * listing, set the price with live profit (back print and level discount counted, 0 profit allowed), then save or send.
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
  const PRINT_CM = { tee: 30, hoodie: 30 }; // physical width of the print area

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
    const cm = (PRINT_CM[kind] || 30) * (place.w / a.w);
    return Math.round(artW / (cm / 2.54));
  };

  const notSeller = () => `${c.pageHead({ crumbs: [['Sell on Super App', '#/sell'], ['Design studio']], title: 'Design studio' })}
    <div class="container"><div class="panel">${c.empty({ icon: 'shirt', title: 'Turn on seller mode first', text: 'The studio is for sellers. It takes a minute to open your store.', action: c.cta('Start selling', { href: '#/sell' }) })}</div></div>`;

  SA.ui.readArt = readArt; // shared with the bulk upload (#/sell/bulk)

  /** Product details and the size chart (cm) — shown in the studio and on the product page */
  const sizeChart = (x) => `<div class="table-wrap"><table class="table size-chart"><thead><tr><th>Size</th>${x.sizeChart.cols.map((k) => `<th>${esc(k)}</th>`).join('')}</tr></thead>
    <tbody>${x.sizes.map((sz) => `<tr><th scope="row" translate="no">${esc(sz)}</th>${(x.sizeChart.rows[sz] || []).map((v) => `<td class="num">${v}</td>`).join('')}</tr>`).join('')}</tbody></table></div>
    <p class="xs muted" style="margin-top:6px">Measured flat in cm: chest is armpit to armpit, length is shoulder to hem. ±1–2 cm.</p>`;
  const productInfo = (x) => `${(x.details || []).length ? `<dl class="kv studio-details">${x.details.map(([k, v]) => `<div><dt>${esc(k)}</dt><dd>${esc(v)}</dd></div>`).join('')}</dl>` : `<p class="small soft">${esc(x.desc || '')}</p>`}
    ${x.sizeChart ? `<details class="studio-chart"><summary>${icon('ruler', 14)} Size chart (cm)</summary>${sizeChart(x)}</details>` : ''}`;
  SA.ui.sizeChart = sizeChart;

  SA.pages.studio = {
    title: 'Design studio — Super App',
    render(ctx) {
      const s = S.sellers.current();
      if (!s) return notSeller();
      if (s.mode === 'store') return `${c.pageHead({ crumbs: [['Sell on Super App', '#/sell'], ['Design studio']], title: 'Design studio' })}<div class="container"><div class="panel">${c.empty({ icon: 'shirt', title: 'Print on demand is off for your store', text: 'Your store sells your own products. Switch your selling mode to “Both” to add designs too.', action: '<button class="btn btn-primary" data-enable-pod>Turn on print on demand</button>' })}</div></div>`;
      const d = ctx.params.id ? S.pod.get(ctx.params.id) : null;
      if (ctx.params.id && (!d || d.sellerId !== s.id || d.deleted)) return `${c.pageHead({ crumbs: [['Sell on Super App', '#/sell'], ['Design studio']], title: 'Design not found' })}<div class="container"><div class="panel">${c.empty({ icon: 'image', title: 'We couldn’t find that design', text: 'It may have been deleted.', action: c.cta('New design', { href: '#/sell/studio' }) })}</div></div>`;
      if (!d) {
        const lim = S.sellers.canAddDesign(s);
        if (!lim.ok) return `${c.pageHead({ crumbs: [['Sell on Super App', '#/sell'], ['Design studio']], title: 'Design limit reached' })}<div class="container"><div class="panel">${c.empty({ icon: 'lock', title: `You have ${lim.used} of ${lim.limit} active designs`, text: 'Pause or delete a design, or level up to add more.', action: c.cta('See my designs', { href: '#/sell' }) })}</div></div>`;
      }
      const items = S.pod.catalog();
      const lvl = S.sellers.levelOf(s);
      const cats = S.pod.categories();
      const cur = items.find((x) => x.id === d?.catalogId) || items[0];
      return `${c.pageHead({ crumbs: [['Sell on Super App', '#/sell'], [d ? 'Edit design' : 'New design']], title: d ? esc(d.title || 'Edit design') : 'New design',
        text: d?.status === 'rejected' ? `Rejected: ${esc(d.reason)} — fix it and send it again.` : d?.status === 'live' ? 'Editing a live design takes it out of the shop until it’s approved again.' : `Level ${esc(lvl.name)}${lvl.baseDiscount ? ` · ${money(lvl.baseDiscount)} off every base price` : ''}`,
        actions: `<a class="btn btn-ghost btn-sm" href="#/seller">${icon('grid', 15)} Dashboard</a>` })}
      <div class="container studio">
        <section class="studio-stage panel">
          <div class="studio-tabs" role="tablist" aria-label="Side" data-sides></div>
          <div class="studio-view">
            <div class="studio-canvas" data-canvas></div>
            <div class="studio-cta-layer" data-cta></div>
          </div>
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
            <div class="studio-products">${items.map((x) => `<label class="studio-product"><input type="radio" name="catalogId" value="${x.id}" ${cur.id === x.id ? 'checked' : ''} />
              <span class="sp-media" data-sp="${x.id}">${SA.ui.garmentView({ kind: x.kind, hex: x.colors[0].hex, label: x.name })}</span><b class="small">${esc(x.name)}</b><span class="xs muted">from ${money(S.pod.baseFor(x.id, { sellerId: s.id }))}</span></label>`).join('')}</div>
            <div class="studio-info" data-product-info>${productInfo(cur)}</div>
          </section>
          <section class="panel panel-pad">
            <h2 class="h4">2. Artwork</h2>
            <p class="xs muted">PNG with a transparent background works best · up to 8 MB · 3000 px wide for sharp prints. The front and the back can have different designs.</p>
            <input type="file" accept="image/png,image/jpeg,image/webp,image/svg+xml" data-file hidden />
            <div class="studio-arts" data-arts></div>
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
              <div class="field span-2"><label for="st-desc">Description</label><textarea class="textarea" id="st-desc" name="desc" maxlength="500" placeholder="Who is it for? What’s the idea behind it?">${esc(d?.desc || '')}</textarea><span class="field-hint">The fabric, fit, print and size chart are added to the product page for you.</span></div>
              <div class="field"><label for="st-cat">Category</label><select class="select" id="st-cat" name="category"><option value="">Choose…</option>${cats.map((x) => `<option value="${x.id}" ${d?.category === x.id ? 'selected' : ''}>${esc(x.name)}</option>`).join('')}</select></div>
              <div class="field"><label for="st-tags">Search tags <span class="muted">(comma separated)</span></label><input class="input" id="st-tags" name="tags" maxlength="120" value="${esc((d?.tags || []).join(', '))}" placeholder="football, cairo, gift" /></div>
            </div>
          </section>
          <section class="panel panel-pad">
            <h2 class="h4">5. Price</h2>
            <div class="studio-price">
              <div class="field"><label for="st-price">Your price (EGP)</label><input class="input num" id="st-price" name="price" type="number" inputmode="numeric" value="${d?.price || ''}" /><span class="field-hint" data-range></span></div>
              <div class="studio-breakdown" data-breakdown></div>
              <div class="sell-split" data-split></div>
            </div>
          </section>
          <p class="field-error" data-err></p>
          <div class="studio-actions">
            <button class="btn btn-outline btn-lg" type="button" data-draft>${icon('file', 16)} Save draft</button>
            <button class="btn btn-blaze btn-lg" type="submit">${icon('arrowUpRight', 16)} ${lvl.review === 'instant' ? 'Publish now' : 'Send for review'}</button>
          </div>
          <p class="xs muted">${lvl.review === 'instant' ? 'Elite sellers publish straight away; we spot-check later.' : lvl.review === 'priority' ? 'Pro sellers are reviewed first — usually within a few hours.' : 'We review every design, usually within 24 hours.'} It shows in your store and in the shop once it’s approved. By sending it you confirm you own the rights to this artwork.</p>
        </form>
      </div>`;
    },

    mount(el, ctx) {
      el.querySelector('[data-enable-pod]')?.addEventListener('click', () => { const s = S.sellers.current(); S.sellers.update(s.id, { mode: 'both' }); SA.app.rerender(); });
      const form = el.querySelector('#studio-form');
      if (!form) return;
      const seller = S.sellers.current();
      const existing = ctx.params.id ? S.pod.get(ctx.params.id) : null;
      // Working state: one artwork per side (the back may repeat the front file)
      const artOf = (side) => {
        if (!existing?.placement?.[side]) return null;
        if (side === 'back' && existing.artBack) return { src: existing.srcBack, w: existing.artBackW || 3000, h: existing.artBackH || 3000 };
        return { src: existing.src, w: existing.artW || 3000, h: existing.artH || 3000 };
      };
      const st = {
        id: existing?.id || null,
        arts: { front: artOf('front'), back: artOf('back') },
        place: { front: existing?.placement?.front || null, back: existing?.placement?.back || null },
        side: existing?.placement?.front || !existing?.placement?.back ? 'front' : 'back',
        colors: existing?.colors?.slice() || [], preview: existing?.colors?.[0] || '',
        target: 'front', lastBase: null, viewer: null, viewerKind: '',
      };
      const $ = (q) => el.querySelector(q);
      const item = () => S.pod.catalogItem(form.querySelector('[name="catalogId"]:checked').value);
      const ratio = (side = st.side) => (st.arts[side] ? st.arts[side].h / st.arts[side].w : 1);
      const defPlace = (kind, side) => { const a = G().area(kind); return G().clampPlace(kind, { x: a.x + a.w / 2, y: a.y + a.h * (side === 'back' ? 0.36 : 0.42), w: a.w * 0.8 }, ratio(side)); };
      const placeFor = (side) => (st.arts[side] ? st.place[side] : null);
      const places = () => ({ front: placeFor('front'), back: placeFor('back') });
      const colour = () => { const x = item(); return x.colors.find((k) => k.name === st.preview) || x.colors.find((k) => st.colors.includes(k.name)) || x.colors[0]; };
      const is3d = () => !!SA.ui.model3d?.has(item().kind);
      const draft = () => {
        const f = SA.core.utils.formData(form);
        const fr = st.arts.front, bk = st.arts.back, main = fr || bk;
        const ownBack = !!(fr && bk && bk.src !== fr.src);
        return { id: st.id, catalogId: f.catalogId, title: f.title, desc: f.desc, category: f.category, tags: String(f.tags || '').split(',').map((t) => t.trim()).filter(Boolean),
          price: +f.price, colors: st.colors, art: main?.src || '', artW: main?.w || 0, artH: main?.h || 0,
          artBack: ownBack ? bk.src : '', artBackW: ownBack ? bk.w : 0, artBackH: ownBack ? bk.h : 0, placement: places() };
      };

      /* ---------- Stage: the 3D garment (or the photo mockup), Front / Back turn it ---------- */
      const renderSides = () => {
        $('[data-sides]').innerHTML = item().areas.map((a) => `<button type="button" class="studio-tab" role="tab" aria-selected="${st.side === a}" data-side="${a}">${a === 'front' ? 'Front' : 'Back'}${st.arts[a] ? ` ${icon('check', 12, 2.6)}` : ''}</button>`).join('');
      };
      const viewerOpts = () => {
        const fr = st.arts.front, bk = st.arts.back;
        return { kind: item().kind, hex: colour().hex, art: (fr || bk)?.src || '', artBack: bk && fr && bk.src !== fr.src ? bk.src : '', artRatio: ratio(fr ? 'front' : 'back'), artRatioBack: ratio('back'), place: places() };
      };
      const renderCanvas = () => {
        const x = item(), col = colour(), cv = $('[data-canvas]');
        if (is3d()) {
          cv.classList.add('is-3d');
          if (!st.viewer || st.viewerKind !== x.kind || !cv.querySelector('.m3d')) {
            st.viewer?.destroy?.();
            cv.innerHTML = '<div data-m3d></div>';
            st.viewer = SA.ui.model3d.mount(cv.querySelector('[data-m3d]'), { ...viewerOpts(), label: `${x.name} preview`, edit: true,
              onPlace: (side, np, { live }) => { st.place[side] = np; if (!live) renderTools(); },
              onSide: (side) => { if (side !== st.side && item().areas.includes(side)) { st.side = side; renderSides(); renderCta(); renderTools(); } } });
            st.viewerKind = x.kind;
            st.viewer.show(st.side);
          } else st.viewer.update(viewerOpts());
        } else {
          st.viewer?.destroy?.(); st.viewer = null; st.viewerKind = '';
          cv.classList.remove('is-3d');
          const gopts = { kind: x.kind, hex: col.hex, art: st.arts[st.side]?.src || '', place: placeFor(st.side), side: st.side, artRatio: ratio(), showArea: true, label: `${x.name} ${st.side} preview in ${col.name}` };
          cv.innerHTML = SA.ui.photoMock?.photoSvg(gopts) || G()(gopts);
          bindDrag();
        }
        renderCta(); renderTools();
        $('[data-preview-colors]').innerHTML = x.colors.filter((k) => !st.colors.length || st.colors.includes(k.name)).map((k) => `<button type="button" class="pc-sw sw-btn ${k.name === col.name ? 'is-on' : ''}" style="--c:${k.hex}" data-preview="${esc(k.name)}" aria-label="Preview in ${esc(k.name)}" title="${esc(k.name)}"></button>`).join('');
      };
      const renderCta = () => {
        const side = st.side, other = side === 'front' ? 'back' : 'front';
        $('[data-cta]').innerHTML = st.arts[side] ? '' : `<div class="studio-canvas-ctas"><button type="button" class="studio-canvas-cta" data-upload="${side}">${icon('plus', 18)} ${side === 'front' ? 'Add a front design' : 'Add a back design'}</button>
          ${st.arts[other] ? `<button type="button" class="btn btn-sm ss-glass" data-same="${side}">${icon('copy', 14)} ${side === 'front' ? 'Use the back design here' : 'Use the front design here'}</button>` : ''}</div>`;
      };
      const renderTools = () => {
        const x = item(), p = placeFor(st.side);
        $('[data-tools]').hidden = !p;
        if (p) $('[data-size]').value = Math.round((p.w / G().area(x.kind).w) * 100);
        const dp = $('[data-dpi]');
        if (p) {
          const v = dpi(st.arts[st.side].w, p, x.kind);
          dp.innerHTML = `<b>${st.side === 'front' ? 'Front' : 'Back'}</b> · ${v >= 150 ? `${icon('check', 12, 2.6)} Print quality: sharp (${v} dpi)` : v >= 90 ? `${icon('alert', 12)} Print quality: OK (${v} dpi). A bigger file will look sharper.` : `${icon('alert', 12)} Too small for this size (${v} dpi) — it will look blurry. Upload a bigger file or make it smaller.`}`;
          dp.className = `studio-dpi xs ${v >= 150 ? 'text-turf' : v >= 90 ? '' : 'text-danger'}`;
        } else dp.textContent = '';
      };

      /* ---------- Artwork: a slot per side with the uploaded image ---------- */
      const renderArts = () => {
        const x = item();
        $('[data-arts]').innerHTML = x.areas.map((side) => {
          const a = st.arts[side], other = side === 'front' ? 'back' : 'front';
          const same = side === 'back' && a && st.arts.front && a.src === st.arts.front.src;
          return `<div class="studio-art ${a ? 'has-art' : ''} ${st.side === side ? 'is-current' : ''}" data-slot="${side}">
            <div class="sa-thumb" data-upload="${side}" role="button" tabindex="0" aria-label="${a ? (side === 'front' ? 'Replace the front design' : 'Replace the back design') : (side === 'front' ? 'Upload the front design' : 'Upload the back design')}">${a ? `<img src="${esc(a.src)}" alt="" />` : icon('image', 24)}</div>
            <div class="sa-body"><b class="small">${side === 'front' ? 'Front design' : 'Back design'}</b>
              <span class="xs muted">${a ? `${a.w}×${a.h} px${same ? ' · same as the front' : ''}` : side === 'back' ? `Optional · back print +${money(x.backExtra)}` : 'Upload or drop an image here'}</span>
              <div class="sa-actions">
                <button type="button" class="btn btn-outline btn-sm" data-upload="${side}">${icon(a ? 'image' : 'plus', 14)} ${a ? 'Replace' : 'Upload'}</button>
                ${!a && st.arts[other] ? `<button type="button" class="btn btn-ghost btn-sm" data-same="${side}">${icon('copy', 14)} ${side === 'back' ? 'Same as the front' : 'Same as the back'}</button>` : ''}
                ${a ? `<button type="button" class="btn btn-ghost btn-sm" data-show="${side}">${icon('eye', 14)} Show</button><button type="button" class="btn btn-ghost btn-sm text-danger" data-remove="${side}">${icon('trash', 14)} Remove</button>` : ''}
              </div></div></div>`;
        }).join('');
      };
      const renderSwatches = () => {
        const x = item();
        st.colors = st.colors.filter((n) => x.colors.some((k) => k.name === n));
        if (!st.colors.length) st.colors = [x.colors[0].name];
        $('[data-swatches]').innerHTML = x.colors.map((k) => `<label class="studio-sw"><input type="checkbox" value="${esc(k.name)}" ${st.colors.includes(k.name) ? 'checked' : ''} /><span class="pc-sw" style="--c:${k.hex}"></span><span class="small">${esc(k.name)}</span></label>`).join('');
      };

      /* ---------- Price: base = product + back print − level discount; the price may equal it (0 profit) ---------- */
      const baseNow = () => S.pod.baseFor(item().id, { back: !!placeFor('back'), sellerId: seller.id });
      const renderPrice = () => {
        const x = item(), back = !!placeFor('back'), base = baseNow();
        const lv = S.sellers.levelOf(seller), disc = lv.baseDiscount || 0;
        const inp = form.price;
        // Adding or removing the back print moves the price by the same amount, so the profit per item stays the same
        if (inp.value && st.lastBase != null && st.lastBase !== base) inp.value = Math.min(x.maxPrice, Math.max(base, (+inp.value || 0) + (base - st.lastBase)));
        if (!inp.value) inp.value = Math.min(x.maxPrice, base + Math.round((x.base * 0.6) / 10) * 10);
        st.lastBase = base;
        const p = +inp.value || 0, profit = p - base;
        const ok = p >= base && p <= x.maxPrice;
        $('[data-range]').textContent = p < base ? `At least ${money(base)} — the base price of this product.` : p > x.maxPrice ? `At most ${money(x.maxPrice)}.` : profit === 0 ? 'You earn nothing per item — that’s fine if it’s for you.' : `From ${money(base)} (no profit) to ${money(x.maxPrice)}`;
        $('[data-range]').className = `field-hint ${ok ? '' : 'text-danger'}`;
        $('[data-breakdown]').innerHTML = `<dl class="kv studio-bd">
          <div><dt>${esc(x.name)}${placeFor('front') ? ' + front print' : ''}</dt><dd class="num">${money(x.base)}</dd></div>
          ${back ? `<div><dt>Back print</dt><dd class="num">+${money(x.backExtra)}</dd></div>` : ''}
          ${disc ? `<div><dt>Level discount (${esc(lv.name)})</dt><dd class="num">−${money(disc)}</dd></div>` : ''}
          <div class="is-total"><dt>Base price</dt><dd class="num">${money(base)}</dd></div></dl>`;
        $('[data-split]').innerHTML = `<span style="flex:${base}"><b class="num">${money(base)}</b><small>Base${back ? ' (front + back)' : ''}</small></span><span class="is-you" style="flex:${Math.max(1, profit)}"><b class="num">${money(Math.max(0, profit))}</b><small>You earn per item</small></span>`;
      };
      const all = () => { renderSides(); renderArts(); renderSwatches(); renderCanvas(); renderPrice(); };

      // When the product photos finish loading, swap the drawn previews for the real photos (keeps the edits)
      const canvasEl = $('[data-canvas]');
      const offPhotos = SA.ui.photoMock?.onReady(() => {
        if (!canvasEl.isConnected) return offPhotos?.();
        el.querySelectorAll('[data-sp]').forEach((n) => { const k = S.pod.catalogItem(n.dataset.sp); n.innerHTML = SA.ui.garmentView({ kind: k.kind, hex: k.colors[0].hex, label: k.name }); });
        if (!is3d() && !canvasEl.querySelector('svg[data-photo]')) renderCanvas();
      });
      setTimeout(() => canvasEl.classList.add('is-entered'), 750);
      const flip = () => { canvasEl.classList.remove('is-flip'); void canvasEl.offsetWidth; canvasEl.classList.add('is-flip'); setTimeout(() => canvasEl.classList.remove('is-flip'), 650); };
      const showSide = (side) => {
        if (side === st.side) return;
        st.side = side;
        if (st.viewer) { st.viewer.show(side); renderSides(); renderArts(); renderCta(); renderTools(); return; }
        flip(); renderSides(); renderArts(); renderCanvas();
      };

      // Drag the artwork inside the print area (photo mockup; the 3D view has its own drag)
      const bindDrag = () => {
        const svg = $('[data-canvas] svg'), img = svg?.querySelector('.g-art');
        if (!img) return;
        const kind = item().kind;
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

      const setArt = (side, a) => {
        const kind = item().kind;
        st.arts[side] = a;
        st.place[side] = st.place[side] ? G().clampPlace(kind, st.place[side], ratio(side)) : defPlace(kind, side);
        if (st.side !== side) { st.side = side; st.viewer?.show(side); }
        all();
      };
      const upload = async (file, side = st.target) => {
        if (!file) return;
        try { setArt(side, await readArt(file)); SA.ui.overlay.toast(side === 'back' ? 'Back design added' : 'Front design added'); } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
      };
      const fileInput = $('[data-file]');
      fileInput.addEventListener('change', () => { upload(fileInput.files[0]); fileInput.value = ''; });
      const pickFor = (side) => { st.target = side; fileInput.click(); };
      // Drop an image on a slot, or on the preview (goes to the side being shown)
      const dropOn = (zone, sideOf) => {
        ['dragenter', 'dragover'].forEach((t) => zone.addEventListener(t, (e) => { e.preventDefault(); (e.target.closest?.('[data-slot]') || zone).classList.add('is-over'); }));
        ['dragleave', 'drop'].forEach((t) => zone.addEventListener(t, (e) => { e.preventDefault(); zone.querySelectorAll('.is-over').forEach((n) => n.classList.remove('is-over')); zone.classList.remove('is-over'); }));
        zone.addEventListener('drop', (e) => { const f = e.dataTransfer?.files?.[0]; if (f) upload(f, sideOf(e)); });
      };
      dropOn($('[data-arts]'), (e) => e.target.closest('[data-slot]')?.dataset.slot || 'front');
      dropOn($('.studio-stage'), () => st.side);

      el.addEventListener('click', (e) => {
        const up = e.target.closest('[data-upload]'); if (up) return pickFor(up.dataset.upload);
        if (e.target.closest('[data-replace]')) return pickFor(st.side);
        const same = e.target.closest('[data-same]');
        if (same) { const side = same.dataset.same, other = side === 'front' ? 'back' : 'front'; if (st.arts[other]) setArt(side, { ...st.arts[other] }); return; }
        const rm = e.target.closest('[data-remove]');
        if (rm) { const side = rm.dataset.remove; st.arts[side] = null; st.place[side] = null; all(); SA.ui.overlay.toast(side === 'back' ? 'Back design removed' : 'Front design removed'); return; }
        const sh = e.target.closest('[data-show]'); if (sh) { showSide(sh.dataset.show); $('.studio-stage').scrollIntoView({ behavior: 'smooth', block: 'nearest' }); return; }
        const sd = e.target.closest('[data-side]'); if (sd) return showSide(sd.dataset.side);
        if (e.target.closest('[data-center]')) { const a = G().area(item().kind); st.place[st.side] = G().clampPlace(item().kind, { ...st.place[st.side], x: a.x + a.w / 2 }, ratio()); renderCanvas(); return; }
        const pv = e.target.closest('[data-preview]'); if (pv) { st.preview = pv.dataset.preview; renderCanvas(); }
      });
      el.addEventListener('keydown', (e) => { const t = e.target.closest?.('.sa-thumb[data-upload]'); if (t && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); pickFor(t.dataset.upload); } });
      $('[data-size]').addEventListener('input', (e) => {
        const kind = item().kind, a = G().area(kind);
        st.place[st.side] = G().clampPlace(kind, { ...st.place[st.side], w: (a.w * +e.target.value) / 100 }, ratio());
        if (st.viewer) { st.viewer.update({ place: places(), only: st.side }); renderTools(); } else renderCanvas();
      });
      form.addEventListener('change', (e) => {
        if (e.target.name === 'catalogId') {
          const x = item();
          ['front', 'back'].forEach((sd) => { if (!x.areas.includes(sd)) { st.arts[sd] = null; st.place[sd] = null; } else if (st.place[sd]) st.place[sd] = G().clampPlace(x.kind, st.place[sd], ratio(sd)); });
          if (!x.areas.includes(st.side)) st.side = 'front';
          $('[data-product-info]').innerHTML = productInfo(x);
          form.price.value = ''; st.lastBase = null;
          all();
        }
        if (e.target.closest('[data-swatches]')) { st.colors = [...el.querySelectorAll('[data-swatches] input:checked')].map((i) => i.value); if (!st.colors.includes(st.preview)) st.preview = st.colors[0] || ''; renderCanvas(); }
      });
      // Typing a price: the profit follows it (the back-print adjustment only applies when the sides change)
      form.price.addEventListener('input', () => { st.lastBase = null; renderPrice(); });

      const save = (submit) => {
        const err = $('[data-err]'); err.textContent = '';
        try {
          if (submit && !st.arts.front && !st.arts.back) throw new Error('Upload your design first.');
          const d = S.pod.saveDraft(seller.id, draft());
          st.id = d.id;
          if (!submit) { SA.ui.overlay.toast('Draft saved'); history.replaceState(null, '', `#/sell/studio/${d.id}`); return; }
          const out = S.pod.submit(d.id);
          SA.core.motion?.confetti?.({ count: 70 });
          SA.ui.overlay.toast(out.status === 'live' ? `“${out.title}” is live in the shop` : `“${out.title}” is in review — it shows in your store once it’s approved`, { type: 'success' });
          SA.core.router.go(SA.services.dash.me()?.role === 'seller' ? '/seller/designs' : '/sell');
        } catch (e2) { err.textContent = e2.message; SA.ui.overlay.toast(e2.message, { type: 'error' }); }
      };
      el.querySelector('[data-draft]').addEventListener('click', () => save(false));
      form.addEventListener('submit', (e) => { e.preventDefault(); save(true); });
      all();
      return () => st.viewer?.destroy?.();
    },
  };
})(window.SA);
