/**
 * Bulk design upload (#/sell/bulk): many images at once with shared settings (product, colours, price, category, tags,
 * placement). Each row can change its title and price. Save them all as drafts or send them all for review — the
 * seller's level limit on active designs still applies, and each design is checked like in the studio.
 */
(function (SA) {
  const { esc, money } = SA.core.utils;
  const { icon } = SA.ui;
  const c = SA.ui.c;
  const S = SA.services;
  const MAX = 20;
  const titleOf = (name) => name.replace(/\.[^.]+$/, '').replace(/[-_]+/g, ' ').replace(/\s+/g, ' ').trim().replace(/^./, (x) => x.toUpperCase()).slice(0, 60);

  SA.pages.bulkUpload = {
    title: 'Bulk upload — Super App',
    render() {
      const s = S.sellers.current();
      const head = (t) => c.pageHead({ crumbs: [['Sell on Super App', '#/sell'], ['Bulk upload']], title: 'Upload many designs', text: t });
      if (!s) return `${head('')}<div class="container"><div class="panel">${c.empty({ icon: 'shirt', title: 'Start selling first', text: 'Apply to sell, then upload your designs here.', action: '<a class="btn btn-primary" href="#/sell">Start selling</a>' })}</div></div>`;
      if (s.mode === 'store') return `${head('')}<div class="container"><div class="panel">${c.empty({ icon: 'shirt', title: 'Print on demand is off for your store', text: 'Turn it on from the design studio to upload designs.', action: '<a class="btn btn-primary" href="#/sell/studio">Design studio</a>' })}</div></div>`;
      const lim = S.sellers.canAddDesign(s);
      const room = lim.limit == null ? MAX : Math.max(0, Math.min(MAX, lim.limit - lim.used));
      const items = S.pod.catalog();
      const cats = S.pod.categories();
      const first = items[0];
      this._room = room;
      return `${head(`Up to ${room} design${room === 1 ? '' : 's'} now${lim.limit != null ? ` · ${lim.used} of ${lim.limit} active designs used at your level` : ''}. Same product and settings for all; change titles and prices per design.`)}
      <div class="container bu-page">
        ${room ? '' : `<div class="notice blaze">${icon('lock', 18)}<span>Your level’s design limit is reached. Pause or remove a design, or level up, to add more.</span></div>`}
        <form class="bu-grid" data-bulk novalidate>
          <section class="panel panel-pad bu-settings"><h2 class="h4">Settings for all</h2>
            <div class="field"><label for="bu-prod">Product</label><select class="select" id="bu-prod" name="catalogId">${items.map((x) => `<option value="${x.id}">${esc(x.name)} · base ${money(x.base)}</option>`).join('')}</select></div>
            <div class="field"><span class="label">Colours</span><div class="bu-colors" data-colors></div><p class="field-error" data-err="colors"></p></div>
            <div class="form-grid">
              <div class="field"><label for="bu-price">Price (EGP)</label><input class="input num" id="bu-price" name="price" type="number" value="${first ? first.base + 150 : 450}" /><p class="xs muted" data-price-hint></p></div>
              <div class="field"><label for="bu-cat">Category</label><select class="select" id="bu-cat" name="category"><option value="">Choose…</option>${cats.map((x) => `<option value="${x.id}">${esc(x.name)}</option>`).join('')}</select><p class="field-error" data-err="category"></p></div>
            </div>
            <div class="field"><label for="bu-tags">Search tags <span class="muted">(comma separated)</span></label><input class="input" id="bu-tags" name="tags" maxlength="120" placeholder="football, cairo, gift" /></div>
            <div class="field"><span class="label">Print on</span><div class="chips"><label class="chip"><input type="radio" name="side" value="front" checked hidden />Front</label><label class="chip"><input type="radio" name="side" value="back" hidden />Back</label></div></div>
            <p class="xs muted">${icon('info', 12)} By sending you confirm you own the rights to every design (<a class="link" href="#/legal/seller" target="_blank" rel="noopener">seller agreement</a>). Fine-tune placement later in the studio.</p>
          </section>
          <section class="panel panel-pad bu-files">
            <label class="studio-drop bu-drop" data-drop><input type="file" accept="image/png,image/jpeg,image/webp,image/svg+xml" multiple data-files hidden ${room ? '' : 'disabled'} />${icon('image', 28)}<b>Drop images here or tap to choose</b><span class="xs muted">PNG, JPG, WebP or SVG · up to 8 MB each · up to ${room}</span></label>
            <p class="field-error" data-err="files"></p>
            <ul class="bu-rows" data-rows></ul>
            <div class="row bu-actions" style="--gap:8px;flex-wrap:wrap"><button class="btn btn-outline" type="button" data-save="draft" disabled>${icon('file', 16)} Save all as drafts</button><button class="btn btn-blaze" type="submit" data-save="submit" disabled>${icon('check', 16)} Send all for review</button></div>
            <div data-result></div>
          </section>
        </form>
      </div>`;
    },
    mount(el) {
      const f = el.querySelector('[data-bulk]'); if (!f) return;
      const room = this._room;
      const rows = [];
      const $ = (q) => el.querySelector(q);
      const prod = () => S.pod.catalog().find((x) => x.id === f.catalogId.value);
      const paintColors = () => {
        const p = prod(); if (!p) return;
        $('[data-colors]').innerHTML = p.colors.map((k, i) => `<label class="bu-color"><input type="checkbox" name="color" value="${esc(k.name)}" ${i < 2 ? 'checked' : ''} /><span style="--c:${k.hex}"></span>${esc(k.name)}</label>`).join('');
        $('[data-price-hint]').textContent = `${SA.i18n.t('Between')} ${p.minPrice} – ${p.maxPrice} EGP`;
      };
      const paintRows = () => {
        $('[data-rows]').innerHTML = rows.map((r, i) => `<li class="bu-row ${r.state || ''}"><img src="${r.src}" alt="" /><span class="grow"><input class="input" data-title="${i}" maxlength="60" value="${esc(r.title)}" aria-label="Title ${i + 1}" />${r.msg ? `<span class="xs ${r.state === 'bad' ? 't-danger' : 'muted'}" style="display:block">${esc(r.msg)}</span>` : `<span class="xs muted" style="display:block">${r.w}×${r.h}px${r.w < 2400 && !r.vector ? ' · low resolution for a full print' : ''}</span>`}</span>
          <input class="input num bu-price" data-price="${i}" type="number" placeholder="${esc(f.price.value)}" value="${r.price || ''}" aria-label="Price ${i + 1}" /><button class="icon-btn" type="button" data-del="${i}" aria-label="Remove">${icon('x', 16)}</button></li>`).join('');
        el.querySelectorAll('[data-save]').forEach((b) => (b.disabled = !rows.some((r) => r.state !== 'ok')));
      };
      const add = async (files) => {
        const err = $('[data-err="files"]'); err.textContent = '';
        const list = [...files].slice(0, Math.max(0, room - rows.length));
        if (files.length > list.length) err.textContent = SA.i18n.t(`Only ${room} designs fit right now — the rest were skipped.`);
        for (const file of list) {
          try { const a = await SA.ui.readArt(file); rows.push({ title: titleOf(file.name), src: a.src, w: a.w, h: a.h, vector: !!a.vector }); }
          catch (e) { err.textContent = `${file.name}: ${e.message}`; }
        }
        paintRows();
      };
      paintColors();
      f.catalogId.addEventListener('change', paintColors);
      $('[data-files]').addEventListener('change', (e) => { add(e.target.files); e.target.value = ''; });
      const drop = $('[data-drop]');
      ['dragenter', 'dragover'].forEach((t) => drop.addEventListener(t, (e) => { e.preventDefault(); drop.classList.add('is-over'); }));
      ['dragleave', 'drop'].forEach((t) => drop.addEventListener(t, (e) => { e.preventDefault(); drop.classList.remove('is-over'); }));
      drop.addEventListener('drop', (e) => add(e.dataTransfer.files));
      $('[data-rows]').addEventListener('input', (e) => { const t = e.target; if (t.dataset.title) rows[+t.dataset.title].title = t.value; if (t.dataset.price) rows[+t.dataset.price].price = +t.value || 0; });
      $('[data-rows]').addEventListener('click', (e) => { const b = e.target.closest('[data-del]'); if (b) { rows.splice(+b.dataset.del, 1); paintRows(); } });
      el.querySelectorAll('[data-chip-side], .bu-settings .chip').forEach((ch) => ch.addEventListener('click', () => el.querySelectorAll('.bu-settings .chip').forEach((x) => x.setAttribute('aria-pressed', String(x === ch)))));
      el.querySelector('.bu-settings .chip')?.setAttribute('aria-pressed', 'true');
      const run = (submit) => {
        const s = S.sellers.current();
        const colors = [...f.querySelectorAll('[name="color"]:checked')].map((x) => x.value);
        f.querySelectorAll('[data-err]').forEach((x) => (x.textContent = ''));
        if (submit && !colors.length) { $('[data-err="colors"]').textContent = SA.i18n.t('Pick at least one colour.'); return; }
        if (submit && !f.category.value) { $('[data-err="category"]').textContent = SA.i18n.t('Choose a category.'); return; }
        const side = f.querySelector('[name="side"]:checked')?.value || 'front';
        const place = { front: side === 'front' ? { x: 0.5, y: 0.36, w: 0.42 } : null, back: side === 'back' ? { x: 0.5, y: 0.3, w: 0.4 } : null };
        const tags = f.tags.value.split(',').map((x) => x.trim().toLowerCase()).filter(Boolean).slice(0, 10);
        let ok = 0;
        rows.forEach((r) => {
          if (r.state === 'ok') return;
          try {
            if (submit && !S.sellers.canAddDesign(s).ok) throw new Error(SA.i18n.t('Your level’s design limit is reached.'));
            const d = S.pod.saveDraft(s.id, { title: r.title, desc: '', tags, category: f.category.value, catalogId: f.catalogId.value, colors, price: r.price || +f.price.value, art: r.src, artW: r.w, artH: r.h, placement: place });
            if (submit) { const out = S.pod.submit(d.id); r.msg = out.status === 'live' ? SA.i18n.t('Live in the shop') : SA.i18n.t('Sent for review'); } else r.msg = SA.i18n.t('Saved as a draft');
            r.state = 'ok'; r.id = d.id; ok++;
          } catch (e) { r.state = 'bad'; r.msg = e.message; }
        });
        paintRows();
        const bad = rows.filter((r) => r.state === 'bad').length;
        $('[data-result]').innerHTML = `<div class="notice ${bad ? 'gold' : 'turf'}" style="margin-top:12px">${icon(bad ? 'alert' : 'check', 18)}<span>${ok} ${SA.i18n.t(submit ? 'sent' : 'saved')}${bad ? ` · ${bad} ${SA.i18n.t('need a fix (see the red notes)')}` : ''}. <a class="link" href="#/seller/designs">${SA.i18n.t('See your designs')}</a></span></div>`;
        if (ok) { SA.ui.overlay.toast(`${ok} ${SA.i18n.t(submit ? 'designs sent' : 'drafts saved')}`); S.db.log(s.accountId, 'design.bulk', `${s.name} uploaded ${ok} design${ok > 1 ? 's' : ''} at once (${submit ? 'sent for review' : 'drafts'})`); }
      };
      f.addEventListener('submit', (e) => { e.preventDefault(); run(true); });
      el.querySelector('[data-save="draft"]').addEventListener('click', () => run(false));
    },
  };
})(window.SA);
