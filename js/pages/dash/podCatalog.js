/** Admin: the print-on-demand catalogue (products the print partner makes), base prices, margins and design categories. */
(function (SA) {
  const { esc, money } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const D = () => SA.services.dash;
  const P = () => SA.services.pod;

  const KINDS = [['tee', 'T-shirt'], ['hoodie', 'Hoodie'], ['cap', 'Cap']];
  const AREAS = [['front', 'Front'], ['back', 'Back']];

  const card = (x) => {
    const margin = x.base - x.partnerCost;
    const used = SA.services.db.all('designs').filter((d) => d.catalogId === x.id && d.status === 'live').length;
    return `<article class="dash-card pc-card ${x.status !== 'active' ? 'is-off' : ''}">
      <div class="pc-media">${SA.ui.garmentView({ kind: x.kind, hex: x.colors[0]?.hex, label: x.name })}</div>
      <div class="pc-body">
        <div class="row-between"><h3 class="h4">${esc(x.name)}</h3><span class="badge ${x.status === 'active' ? 'badge-turf' : 'badge'}">${x.status === 'active' ? 'On sale' : 'Hidden'}</span></div>
        <p class="xs muted">${esc(x.desc)}</p>
        <dl class="kv pc-kv">
          <div><dt>Base price (sellers see)</dt><dd class="num">${money(x.base)}</dd></div>
          <div><dt>Partner cost</dt><dd class="num">${money(x.partnerCost)}</dd></div>
          <div><dt>Our margin per item</dt><dd class="num text-turf">${money(margin)} · ${Math.round((margin / x.base) * 100)}%</dd></div>
          <div><dt>Seller price range</dt><dd class="num">${money(x.minPrice)} – ${money(x.maxPrice)}</dd></div>
          <div><dt>Extras</dt><dd class="small">${x.areas.includes('back') ? `Back print +${x.backExtra}` : 'Front only'}${x.bigSizeExtra ? ` · ${x.bigSizes.join('/')} +${x.bigSizeExtra}` : ''}</dd></div>
        </dl>
        <div class="pc-swatches" aria-label="Colours">${x.colors.map((c) => `<span class="pc-sw" style="--c:${c.hex}" title="${esc(c.name)}"></span>`).join('')}<span class="xs muted">${x.sizes.join(' · ')}</span></div>
        <div class="row-between pc-foot"><span class="xs muted">${used} live design${used === 1 ? '' : 's'}</span>
          <span class="row" style="--gap:6px"><button class="btn btn-ghost btn-sm" data-toggle="${x.id}">${x.status === 'active' ? 'Hide' : 'Put on sale'}</button><button class="btn btn-outline btn-sm" data-edit="${x.id}">${icon('edit', 14)} Edit</button></span></div>
      </div></article>`;
  };

  const colorRow = (c = { name: '', hex: '#141414' }) => `<div class="pc-color" data-color-row><input type="color" value="${esc(c.hex)}" aria-label="Colour" data-hex /><input class="input" value="${esc(c.name)}" placeholder="Colour name" maxlength="24" data-cname /><button type="button" class="icon-btn" data-del-color aria-label="Remove colour">${icon('x', 14)}</button></div>`;

  const form = (x) => `<form id="pc-form" class="form-grid" novalidate>
      <div class="field"><label for="pc-name">Product name</label><input class="input" id="pc-name" name="name" value="${esc(x.name || '')}" maxlength="40" required /></div>
      <div class="field"><label for="pc-kind">Shape</label><select class="select" id="pc-kind" name="kind">${KINDS.map(([v, l]) => `<option value="${v}" ${x.kind === v ? 'selected' : ''}>${l}</option>`).join('')}</select></div>
      <div class="field span-2"><label for="pc-desc">Description for sellers and customers</label><input class="input" id="pc-desc" name="desc" value="${esc(x.desc || '')}" maxlength="140" /></div>
      <div class="field"><label for="pc-cost">Partner cost (EGP)</label><input class="input num" id="pc-cost" name="partnerCost" type="number" min="1" value="${x.partnerCost || ''}" /></div>
      <div class="field"><label for="pc-base">Base price (EGP)</label><input class="input num" id="pc-base" name="base" type="number" min="1" value="${x.base || ''}" /><span class="field-hint" data-margin></span></div>
      <div class="field"><label for="pc-min">Lowest seller price</label><input class="input num" id="pc-min" name="minPrice" type="number" min="1" value="${x.minPrice || ''}" /></div>
      <div class="field"><label for="pc-max">Highest seller price</label><input class="input num" id="pc-max" name="maxPrice" type="number" min="1" value="${x.maxPrice || ''}" /></div>
      <div class="field"><span class="field-label">Print areas</span><div class="row" style="--gap:14px">${AREAS.map(([v, l]) => `<label class="row small" style="--gap:6px"><input type="checkbox" name="area" value="${v}" ${(x.areas || ['front']).includes(v) ? 'checked' : ''} ${v === 'front' ? 'disabled' : ''} /> ${l}</label>`).join('')}</div></div>
      <div class="field"><label for="pc-back">Back print extra (EGP)</label><input class="input num" id="pc-back" name="backExtra" type="number" min="0" value="${x.backExtra ?? 0}" /></div>
      <div class="field"><label for="pc-sizes">Sizes <span class="muted">(comma separated)</span></label><input class="input" id="pc-sizes" name="sizes" value="${esc((x.sizes || []).join(', '))}" /></div>
      <div class="field"><label for="pc-big">Big sizes extra (EGP)</label><input class="input num" id="pc-big" name="bigSizeExtra" type="number" min="0" value="${x.bigSizeExtra ?? 0}" /><span class="field-hint">Applies to 2XL and 3XL</span></div>
      <div class="field span-2"><span class="field-label">Colours</span><div class="pc-colors" data-colors>${(x.colors?.length ? x.colors : [{ name: 'Black', hex: '#141414' }]).map(colorRow).join('')}</div>
        <button type="button" class="btn btn-ghost btn-sm" data-add-color style="margin-top:8px">${icon('plus', 14)} Add colour</button></div>
      <p class="field-error span-2" data-err></p>
    </form>`;

  const open = (x = {}) => {
    SA.ui.overlay.open({
      wide: true, title: x.id ? `Edit ${esc(x.name)}` : 'New print product', subtitle: 'Sellers only see the base price. Everything between the partner cost and the base price is the Super App margin.',
      body: form(x),
      footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-primary" form="pc-form" type="submit">${icon('check', 16)} Save product</button>`,
      onMount(m, close) {
        const f = m.querySelector('#pc-form');
        const margin = () => { const b = +f.base.value, c = +f.partnerCost.value; m.querySelector('[data-margin]').textContent = b && c ? `Margin ${money(b - c)} per item (${Math.round(((b - c) / b) * 100)}%)` : ''; };
        f.addEventListener('input', margin); margin();
        m.querySelector('[data-add-color]').addEventListener('click', () => m.querySelector('[data-colors]').insertAdjacentHTML('beforeend', colorRow({ name: '', hex: '#888888' })));
        m.querySelector('[data-colors]').addEventListener('click', (e) => { const b = e.target.closest('[data-del-color]'); if (b && m.querySelectorAll('[data-color-row]').length > 1) b.closest('[data-color-row]').remove(); });
        f.addEventListener('submit', (e) => {
          e.preventDefault();
          const d = SA.core.utils.formData(f);
          const sizes = String(d.sizes || '').split(',').map((s) => s.trim()).filter(Boolean);
          const colors = [...m.querySelectorAll('[data-color-row]')].map((r) => ({ name: r.querySelector('[data-cname]').value.trim(), hex: r.querySelector('[data-hex]').value })).filter((c) => c.name);
          const areas = ['front', ...[...f.querySelectorAll('[name="area"]:checked')].map((i) => i.value).filter((v) => v !== 'front')];
          try {
            P().saveCatalogItem({ ...x, ...d, areas, sizes, colors, bigSizes: sizes.filter((s) => /^[2-9]XL$/i.test(s)), kind: d.kind });
            close(); SA.ui.overlay.toast('Catalogue saved'); SA.app.rerender();
          } catch (err) { m.querySelector('[data-err]').textContent = err.message; }
        });
      },
    });
  };

  SA.pages.dashPodCatalog = C.guard({
    title: 'Print catalogue — Super App Admin',
    render() {
      if (!D().isAdmin()) return C.layout('pod-catalog', { title: 'Print catalogue', body: `<div class="dash-card">${SA.ui.c.empty({ icon: 'lock', title: 'Super App admins only', text: 'This section is only available to the Super App team.' })}</div>` });
      const items = P().catalog({ all: true });
      const cats = P().categories();
      const live = SA.services.db.all('designs').filter((d) => d.status === 'live');
      const avg = items.length ? Math.round(items.reduce((t, x) => t + (x.base - x.partnerCost) / x.base, 0) / items.length * 100) : 0;
      const body = `
        <div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'Products', value: items.filter((x) => x.status === 'active').length, ic: 'shirt' })}
          ${C.kpi({ label: 'Live designs', value: live.length, ic: 'image', tone: 'k-turf' })}
          ${C.kpi({ label: 'Average margin', value: `${avg}%`, ic: 'percent', tone: 'k-blaze' })}
          ${C.kpi({ label: 'Sellers', value: SA.services.sellers.list().length, ic: 'users' })}
        </div>
        <div class="pc-grid">${items.map(card).join('')}</div>
        <section class="dash-card"><div class="row-between"><div><h2 class="h4">Design categories</h2><p class="small soft">Shown as shop sections. Sellers pick one per design.</p></div></div>
          <div class="chips" style="margin-top:14px">${cats.map((x) => `<span class="chip">${esc(x.name)} <span class="xs muted">${live.filter((d) => d.category === x.id).length}</span></span>`).join('')}</div>
          <form class="row pc-cat-form" data-cat-form novalidate><input class="input" name="name" maxlength="30" placeholder="New category, e.g. Ramadan" aria-label="New category" /><button class="btn btn-outline" type="submit">${icon('plus', 15)} Add</button></form></section>`;
      return C.layout('pod-catalog', { title: 'Print catalogue', subtitle: 'Products made and shipped by the print partner', actions: `<button class="btn btn-primary" data-new>${icon('plus', 16)} New product</button>`, body });
    },
    mount(el) {
      el.addEventListener('click', (e) => {
        if (e.target.closest('[data-new]')) return open({ kind: 'tee', areas: ['front', 'back'], sizes: ['S', 'M', 'L', 'XL', '2XL'], backExtra: 60, bigSizeExtra: 30 });
        const ed = e.target.closest('[data-edit]'); if (ed) return open(P().catalogItem(ed.dataset.edit));
        const tg = e.target.closest('[data-toggle]');
        if (tg) { const x = P().catalogItem(tg.dataset.toggle); P().saveCatalogItem({ ...x, status: x.status === 'active' ? 'hidden' : 'active' }); SA.ui.overlay.toast(x.status === 'active' ? `${x.name} hidden from sellers` : `${x.name} is on sale`); SA.app.rerender(); }
      });
      el.querySelector('[data-cat-form]')?.addEventListener('submit', (e) => {
        e.preventDefault();
        try { P().addCategory(e.target.elements.namedItem('name').value); SA.ui.overlay.toast('Category added'); SA.app.rerender(); }
        catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
      });
    },
  });
})(window.SA);
