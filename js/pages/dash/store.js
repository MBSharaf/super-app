(function (SA) {
  const { esc, money, isoDate, addDays, today, timeAgo } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const D = () => SA.services.dash;
  const media = SA.data.media;
  const CATS = SA.data.products.categories;
  const PRODUCT_LIBRARY = ['photo-1511886929837-354d827aae26', 'photo-1529900748604-07564a03e7a6', 'photo-1600679472829-3044539ce8ed', 'photo-1614632537190-23e4146777db', 'photo-1575361204480-aadea25e6e68', 'photo-1587384474964-3a06ce1ce699', 'photo-1551958219-acbc608c6377', 'photo-1552066379-e7bfd22155c5', 'photo-1616124619460-ff4ed8f4683c', 'photo-1632072820781-79f3a064f640', 'photo-1584735935682-2f2b69dff9d2', 'photo-1612534847738-b3af9bc31f0c', 'photo-1622260614153-03223fb72052', 'photo-1623789658209-00c06091e6eb', 'photo-1774414048830-bff390f869a4', 'photo-1542291026-7eec264c27ff'];
  const COVER_LIBRARY = ['photo-1579952363873-27f3bade9f55', 'photo-1551958219-acbc608c6377', 'photo-1529900748604-07564a03e7a6', 'photo-1622260614153-03223fb72052', 'photo-1614632537190-23e4146777db', media.photos.stadiumNight, media.photos.turfLine];

  const storeForm = (s = {}) => `<form id="store-form" class="form-grid" novalidate>
    <div class="field span-2"><label for="st-name">Store name</label><input class="input" id="st-name" name="name" maxlength="40" value="${esc(s.name || '')}" placeholder="e.g. Turf 22 Pro Shop" /><span class="field-hint" data-slug>${s.slug ? `superapp.example/shop?store=${esc(s.id)}` : 'Your storefront link is created from the name.'}</span><span class="field-error" data-err="name"></span></div>
    <div class="field span-2"><label for="st-tag">Tagline</label><input class="input" id="st-tag" name="tagline" maxlength="70" value="${esc(s.tagline || '')}" placeholder="One line about what you sell" /></div>
    <div class="field span-2"><label for="st-desc">About your store</label><textarea class="textarea" id="st-desc" name="description" maxlength="400" placeholder="What you sell, who it’s for, delivery areas…">${esc(s.description || '')}</textarea><span class="field-error" data-err="description"></span></div>
    <div class="field"><label for="st-phone">Contact phone</label><input class="input" id="st-phone" name="phone" inputmode="tel" value="${esc(s.phone || '')}" placeholder="+20 1XX XXX XXXX" /></div>
    <div class="field"><span class="field-label">Logo</span><div class="logo-up"><span class="logo-prev" data-logo-prev>${s.logo ? `<img src="${s.logo}" alt="" />` : icon('bag', 22)}</span><label class="btn btn-outline btn-sm">${icon('plus', 14)} Upload logo<input type="file" accept="image/*" hidden data-logo /></label>${s.logo ? '<button type="button" class="link xs" data-logo-remove>Remove</button>' : ''}</div></div>
    <div class="field span-2"><span class="field-label">Cover image</span><div data-cover style="margin-top:6px"></div></div>
  </form>`;

  const bindStoreForm = (root, s = {}) => {
    let logo = s.logo || null;
    const cover = C.photoManager(root.querySelector('[data-cover]'), { images: s.cover ? [s.cover] : [], library: COVER_LIBRARY, max: 1, aspect: '16 / 6', name: 'cover' });
    root.querySelector('[data-logo]')?.addEventListener('change', async (e) => {
      try { logo = await C.readImage(e.target.files[0], 400, 0.86); root.querySelector('[data-logo-prev]').innerHTML = `<img src="${logo}" alt="" />`; } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
    });
    root.querySelector('[data-logo-remove]')?.addEventListener('click', () => { logo = null; root.querySelector('[data-logo-prev]').innerHTML = icon('bag', 22); });
    root.querySelector('[name="name"]').addEventListener('input', (e) => { const slug = e.target.value.trim().toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, ''); root.querySelector('[data-slug]').textContent = slug ? `Storefront link: superapp.example/store/${slug}` : 'Your storefront link is created from the name.'; });
    return () => ({ ...SA.core.utils.formData(root.querySelector('#store-form')), logo, cover: cover.get()[0] || null });
  };

  SA.pages.dashStore = C.guard({
    title: 'My store — Super App Partner',
    render(ctx) {
      const sc = C.scope();
      const store = D().myStore(sc.ownerId);
      const a = D().me();
      const viewingOther = D().isAdmin() && sc.ownerId;
      if (!store) {
        if (viewingOther) return C.layout('store', { title: 'Store', body: `<div class="dash-card">${SA.ui.c.empty({ icon: 'bag', title: 'This partner hasn’t opened a store yet', text: 'Partners open their own store from their dashboard.' })}</div>` });
        return C.layout('store', {
          title: 'Open your store', subtitle: 'Sell products to every Super App player. One store per account.',
          body: `<div class="store-create">
            <section class="dash-card">
              <ol class="sc-steps"><li class="is-on"><span>1</span>Name and brand your store</li><li><span>2</span>Add products</li><li><span>3</span>Get paid to your store wallet</li></ol>
              ${storeForm()}
              <div class="row" style="justify-content:flex-end;margin-top:18px"><button class="btn btn-blaze btn-lg" form="store-form" type="submit">${icon('bag', 17)} Open my store</button></div>
            </section>
            <aside class="sc-perks">
              <div class="sc-perk"><span>${icon('users', 18)}</span><div><b>Every Super App player</b><p class="small soft">Your products appear in the Super App shop, search and recommendations.</p></div></div>
              <div class="sc-perk"><span>${icon('truck', 18)}</span><div><b>We deliver</b><p class="small soft">Super App handles payment and delivery across Egypt.</p></div></div>
              <div class="sc-perk"><span>${icon('wallet', 18)}</span><div><b>Separate store wallet</b><p class="small soft">Sales land in your store wallet after delivery, minus ${Math.round(D().commission().store * 100)}% commission — kept apart from your booking money.</p></div></div>
            </aside></div>`,
        });
      }
      const allProducts = D().products(store.id);
      const isLow = (p) => p.stock <= (p.lowStockAt ?? 5);
      const lowOnly = ctx.query.low === '1';
      const products = lowOnly ? allProducts.filter(isLow) : allProducts;
      const lowList = allProducts.filter((p) => p.status === 'active' && isLow(p));
      const from = isoDate(addDays(today(), -29)), to = isoDate(today());
      const sales = D().salesFor([store.id], from, to);
      const by = new Map(); sales.forEach((x) => by.set(x.productId, (by.get(x.productId) || 0) + x.qty));
      const net = sales.reduce((t, x) => t + x.money.net, 0);
      const body = `
        <section class="store-hero" style="--cover:url('${media.u(store.cover, 1600, 600)}')">
          <div class="sh-bg" data-parallax="0.08"></div>
          <div class="sh-inner"><span class="sh-logo">${store.logo ? `<img src="${store.logo}" alt="" />` : icon('bag', 26)}</span>
            <div class="grow"><div class="row wrap" style="--gap:8px"><h2 class="h2" style="color:#fff">${esc(store.name)}</h2>${store.official ? '<span class="badge badge-glass">Official</span>' : ''}<span class="badge ${store.status === 'active' ? 'badge-turf' : 'badge-danger'}">${store.status === 'active' ? 'Live on Super App' : 'Suspended'}</span></div>
              <p style="color:rgba(255,255,255,.8)">${esc(store.tagline || store.description)}</p></div>
            <div class="row wrap" style="--gap:8px"><a class="btn btn-sm sh-btn" href="#/dash/orders">${icon('package', 14)} Orders</a><a class="btn btn-sm sh-btn" href="#/shop?store=${store.id}" target="_blank" rel="noopener">${icon('eye', 14)} View storefront</a><button class="btn btn-sm sh-btn" data-edit-store>${icon('edit', 14)} Edit store</button></div></div>
        </section>
        ${store.status !== 'active' ? `<div class="notice blaze">${icon('alert', 18)}<span>This store is suspended by Super App — its products are hidden from the shop. Contact partner support.</span></div>` : ''}
        <div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'Products', value: allProducts.length, count: allProducts.length, sub: `${allProducts.filter((p) => p.status === 'active').length} published`, ic: 'bag' })}
          ${C.kpi({ label: 'Units sold (30d)', value: sales.reduce((t, x) => t + x.qty, 0), ic: 'package' })}
          ${C.kpi({ label: 'Store net (30d)', value: money(Math.round(net)), count: Math.round(net), suffix: ' EGP', ic: 'wallet', tone: 'k-blaze' })}
          ${C.kpi({ label: 'Low stock', value: lowList.length, ic: 'alert', tone: lowList.length ? 'k-gold' : '', sub: 'At or below each product’s alert level' })}
        </div>
        ${lowList.length ? `<div class="notice gold low-alert">${icon('alert', 18)}<span class="grow"><b>Running low:</b> ${lowList.map((p) => `${esc(p.name)} (${p.stock} left)`).join(', ')}.</span>${lowOnly ? `<a class="btn btn-outline btn-sm" href="#/dash/store">Show all</a>` : `<a class="btn btn-outline btn-sm" href="#/dash/store?low=1">Show only these</a>`}</div>` : ''}
        <section class="dash-card table-card">
          <div class="row-between" style="padding:18px 18px 0"><h2 class="h4">${lowOnly ? 'Products running low' : 'Products'}</h2><a class="btn btn-primary btn-sm" href="#/dash/store/product/new">${icon('plus', 15)} Add product</a></div>
          ${products.length ? `<div class="table-wrap"><table class="table dash-table"><thead><tr><th>Product</th><th>Category</th><th class="r">Price</th><th class="r">Stock</th><th class="r">Sold (30d)</th><th>Status</th><th></th></tr></thead><tbody>
            ${products.map((p) => `<tr>
              <td>${p.hiddenByAdmin ? `<span class="badge badge-danger" title="${esc(p.hiddenReason || '')}">Hidden by Super App</span> ` : ''}<a class="prod-cell" href="#/dash/store/product/${p.id}"><span class="media pc-thumb"><img src="${media.u(p.images[0], 120, 120)}" alt="" loading="lazy" /></span><span><b class="small">${esc(p.name)}</b><span class="xs muted" style="display:block">${esc(p.brand)} · updated ${timeAgo(p.updatedAt)}</span></span></a></td>
              <td class="small">${esc(CATS.find((c) => c.id === p.cat)?.name || p.cat)}</td>
              <td class="r num">${money(p.price)}${p.was > p.price ? `<s class="xs muted" style="display:block">${money(p.was)}</s>` : ''}</td>
              <td class="r num ${isLow(p) ? 'low-stock' : ''}">${p.stock}${p.stock === 0 ? '<span class="xs" style="display:block">Sold out</span>' : isLow(p) ? `<span class="xs" style="display:block">Low (alert at ${p.lowStockAt ?? 5})</span>` : ''}<button class="link xs" data-restock="${p.id}" style="display:block;margin-left:auto">+ Restock</button></td>
              <td class="r num">${by.get(p.id) || 0}</td>
              <td><button class="switch sm ${p.status === 'active' ? 'is-on' : ''}" role="switch" aria-checked="${p.status === 'active'}" data-pub="${p.id}" aria-label="${p.status === 'active' ? 'Unpublish' : 'Publish'} ${esc(p.name)}"><span class="switch-knob"></span><span class="switch-label xs">${p.status === 'active' ? 'Published' : 'Draft'}</span></button></td>
              <td class="r"><div class="row" style="--gap:2px;justify-content:flex-end"><a class="icon-btn" href="#/shop/${p.id}" target="_blank" rel="noopener" aria-label="View ${esc(p.name)} on the site">${icon('eye', 16)}</a><a class="icon-btn" href="#/dash/store/product/${p.id}" aria-label="Edit ${esc(p.name)}">${icon('edit', 16)}</a><button class="icon-btn" data-del="${p.id}" aria-label="Delete ${esc(p.name)}" style="color:var(--danger)">${icon('trash', 16)}</button></div></td></tr>`).join('')}
          </tbody></table></div>` : SA.ui.c.empty({ icon: 'bag', title: 'Add your first product', text: 'Products you publish appear in the Super App shop straight away.', action: `<a class="btn btn-primary" href="#/dash/store/product/new">${icon('plus', 16)} Add product</a>` })}
        </section>`;
      return C.layout('store', { title: viewingOther ? `Store · ${esc(store.name)}` : 'My store', subtitle: `${products.length} product${products.length === 1 ? '' : 's'} · ${Math.round(D().commission().store * 100 * (store.official ? 0 : 1))}% Super App commission on sales`, actions: `<a class="btn btn-primary btn-sm" href="#/dash/store/product/new">${icon('plus', 15)} Add product</a>`, body });
    },
    mount(el) {
      const sc = C.scope();
      const store = D().myStore(sc.ownerId);
      const create = el.querySelector('.store-create');
      if (create) {
        const read = bindStoreForm(create);
        el.querySelector('#store-form').addEventListener('submit', (e) => {
          e.preventDefault();
          try { const s = D().createStore(read()); SA.core.motion.confetti(); SA.ui.overlay.toast(`${s.name} is open on Super App — add your first product`); SA.core.router.go('/dash/store/product/new'); }
          catch (err) { if (err.details) C.showErrors(e.target, err.details); SA.ui.overlay.toast(err.message, { type: 'error' }); }
        });
        return;
      }
      el.querySelector('[data-edit-store]')?.addEventListener('click', () => {
        SA.ui.overlay.open({
          wide: true, title: 'Edit store', body: storeForm(store), footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-primary" form="store-form" type="submit">Save store</button>`,
          onMount(m, close) {
            const read = bindStoreForm(m, store);
            m.querySelector('#store-form').addEventListener('submit', (e) => {
              e.preventDefault();
              try { D().updateStore(store.id, read()); close(); SA.ui.overlay.toast('Store updated'); SA.app.rerender(); }
              catch (err) { if (err.details) C.showErrors(e.target, err.details); SA.ui.overlay.toast(err.message, { type: 'error' }); }
            });
          },
        });
      });
      el.addEventListener('click', async (e) => {
        const pub = e.target.closest('[data-pub]');
        if (pub) { const p = D().product(pub.dataset.pub); D().setProductStatus(p.id, p.status === 'active' ? 'draft' : 'active'); SA.ui.overlay.toast(p.status === 'active' ? 'Unpublished — hidden from the shop' : 'Published to the Super App shop'); SA.app.rerender(); return; }
        const rs = e.target.closest('[data-restock]');
        if (rs) {
          const p = D().product(rs.dataset.restock);
          SA.ui.overlay.open({
            title: `Restock ${esc(p.name)}`, subtitle: `${p.stock} in stock now · alert at ${p.lowStockAt ?? 5}`,
            body: `<form id="rs" novalidate class="stack" style="--gap:12px"><div class="field"><label for="rs-n">Units that arrived</label><input class="input num" id="rs-n" name="add" type="number" min="1" step="1" value="10" /><div class="chips" style="margin-top:8px">${[5, 10, 25, 50].map((n) => `<button type="button" class="chip" data-n="${n}">+${n}</button>`).join('')}</div><span class="field-error" data-err="add"></span></div></form>`,
            footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-primary" form="rs" type="submit">${icon('plus', 15)} Add to stock</button>`,
            onMount(m, close) {
              const f = m.querySelector('#rs');
              m.querySelectorAll('[data-n]').forEach((b) => b.addEventListener('click', () => (f.add.value = b.dataset.n)));
              f.addEventListener('submit', (ev) => { ev.preventDefault(); try { const out = D().restock(p.id, f.add.value); close(); SA.ui.overlay.toast(`${out.name}: ${out.stock} in stock`, { type: 'success' }); SA.app.rerender(); } catch (err) { if (err.details) C.showErrors(f, err.details); SA.ui.overlay.toast(err.message, { type: 'error' }); } });
            },
          });
          return;
        }
        const del = e.target.closest('[data-del]');
        if (del) {
          const p = D().product(del.dataset.del);
          if (!(await SA.ui.overlay.confirm({ title: `Delete ${esc(p.name)}?`, text: 'It’s removed from the Super App shop and from shoppers’ carts. Past sales stay in your reports.', confirmLabel: 'Delete product', danger: true }))) return;
          D().deleteProduct(p.id); SA.ui.overlay.toast('Product deleted'); SA.app.rerender();
        }
      });
    },
  });

  /* ======================= Product editor ======================= */
  const blank = () => ({ name: '', brand: '', cat: 'boots', price: '', was: '', stock: 10, lowStockAt: 5, sizes: [], colors: [{ name: 'Black', hex: '#141414' }], images: [], desc: '', specs: [['', '']], status: 'active' });

  SA.pages.dashProductEdit = C.guard({
    title: 'Product — Super App Partner',
    render(ctx) {
      const sc = C.scope();
      const store = D().myStore(sc.ownerId);
      if (!store) return C.layout('store', { title: 'Open your store first', body: `<div class="dash-card">${SA.ui.c.empty({ icon: 'bag', title: 'You need a store to add products', action: '<a class="btn btn-primary" href="#/dash/store">Open my store</a>' })}</div>` });
      const isNew = !ctx.params.id;
      const p = isNew ? blank() : D().product(ctx.params.id);
      this._p = p; this._store = store;
      const colorRow = (c, i) => `<div class="color-row" data-color-row><input type="color" value="${esc(c.hex)}" aria-label="Colour ${i + 1}" /><input class="input" value="${esc(c.name)}" placeholder="Colour name" aria-label="Colour name ${i + 1}" /><button type="button" class="icon-btn" data-rm-color aria-label="Remove colour">${icon('x', 15)}</button></div>`;
      const specRow = ([k, v]) => `<div class="spec-row" data-spec-row><input class="input" value="${esc(k)}" placeholder="e.g. Material" aria-label="Specification name" /><input class="input" value="${esc(v)}" placeholder="e.g. Synthetic leather" aria-label="Specification value" /><button type="button" class="icon-btn" data-rm-spec aria-label="Remove">${icon('x', 15)}</button></div>`;
      const body = `<form id="prod-form" class="pe" novalidate>
        <div class="pe-main">
          <section class="dash-card"><h2 class="h4">Product</h2><div class="form-grid" style="margin-top:14px">
            <div class="field span-2"><label for="pr-name">Product name</label><input class="input" id="pr-name" name="name" maxlength="80" value="${esc(p.name)}" placeholder="e.g. Predator League FG Football Boots" /><span class="field-error" data-err="name"></span></div>
            <div class="field"><label for="pr-brand">Brand</label><input class="input" id="pr-brand" name="brand" value="${esc(p.brand)}" placeholder="e.g. adidas" /></div>
            <div class="field"><label for="pr-cat">Category</label><select class="select" id="pr-cat" name="cat">${CATS.map((cc) => `<option value="${cc.id}" ${cc.id === p.cat ? 'selected' : ''}>${esc(cc.name)}</option>`).join('')}</select><span class="field-error" data-err="cat"></span></div>
          </div></section>
          <section class="dash-card"><h2 class="h4">Price and stock</h2><div class="form-grid" style="margin-top:14px">
            <div class="field"><label for="pr-price">Selling price (EGP)</label><input class="input num" id="pr-price" name="price" type="number" min="1" step="1" value="${p.price}" /><span class="field-error" data-err="price"></span></div>
            <div class="field"><label for="pr-was">Original price <span class="muted">(optional, shows a discount)</span></label><input class="input num" id="pr-was" name="was" type="number" min="1" step="1" value="${p.was > p.price ? p.was : ''}" /><span class="field-error" data-err="was"></span></div>
            <div class="field"><label for="pr-stock">Units in stock</label><input class="input num" id="pr-stock" name="stock" type="number" min="0" step="1" value="${p.stock}" /><span class="field-error" data-err="stock"></span></div>
            <div class="field"><label for="pr-low">Low-stock alert at</label><input class="input num" id="pr-low" name="lowStockAt" type="number" min="0" step="1" value="${p.lowStockAt ?? 5}" /><span class="field-hint">You’ll get an alert when stock reaches this number</span><span class="field-error" data-err="lowStockAt"></span></div>
            <div class="field"><span class="field-label">You receive per unit</span><b class="h4 num" data-you-get>—</b><span class="field-hint">After ${Math.round(D().commission().store * 100 * (store.official ? 0 : 1))}% Super App commission</span></div>
          </div></section>
          <section class="dash-card"><h2 class="h4">Options</h2>
            <div class="field" style="margin-top:12px"><label for="pr-sizes">Sizes <span class="muted">(separated by commas — leave empty for one size)</span></label><input class="input" id="pr-sizes" name="sizes" value="${esc(p.sizes.join(', '))}" placeholder="e.g. 40, 41, 42, 43 or S, M, L" /></div>
            <div class="field" style="margin-top:14px"><span class="field-label">Colours</span><div data-colors class="stack" style="--gap:8px;margin-top:6px">${p.colors.map(colorRow).join('')}</div><button type="button" class="link small" data-add-color style="margin-top:8px">${icon('plus', 14)} Add colour</button></div>
          </section>
          <section class="dash-card"><div class="row-between"><h2 class="h4">Photos</h2><span class="xs muted">Up to 4 · first is the main photo</span></div><div data-photos style="margin-top:12px"></div></section>
          <section class="dash-card"><h2 class="h4">Description</h2>
            <div class="field" style="margin-top:12px"><label for="pr-desc">Description</label><textarea class="textarea" id="pr-desc" name="desc" maxlength="600" placeholder="What makes it good, who it’s for, how it fits…">${esc(p.desc)}</textarea><span class="field-error" data-err="desc"></span></div>
            <div class="field" style="margin-top:14px"><span class="field-label">Specifications</span><div data-specs class="stack" style="--gap:8px;margin-top:6px">${(p.specs.length ? p.specs : [['', '']]).map(specRow).join('')}</div><button type="button" class="link small" data-add-spec style="margin-top:8px">${icon('plus', 14)} Add specification</button></div>
          </section>
        </div>
        <aside class="pe-side">
          <div class="pe-preview"><p class="xs muted" style="margin-bottom:8px">${icon('eye', 13)} Live preview in the shop</p><div data-preview class="pv-product"></div></div>
          <div class="dash-card"><h2 class="h4">Visibility</h2><div class="seg" style="margin-top:12px"><label class="seg-opt"><input type="radio" name="status" value="active" ${p.status !== 'draft' ? 'checked' : ''} /><span>Published</span></label><label class="seg-opt"><input type="radio" name="status" value="draft" ${p.status === 'draft' ? 'checked' : ''} /><span>Draft</span></label></div><p class="xs muted" style="margin-top:10px">Drafts are saved but hidden from shoppers.</p></div>
        </aside>
        <div class="save-bar"><span class="small soft grow">${isNew ? `New product for ${esc(store.name)}` : 'Changes go live in the shop when you save.'}</span><a class="btn btn-outline" href="#/dash/store">Cancel</a><button class="btn btn-primary" type="submit">${icon('check', 16)} ${isNew ? 'Save product' : 'Save changes'}</button></div>
      </form>`;
      this._colorRow = colorRow; this._specRow = specRow;
      return C.layout('store', { title: isNew ? 'Add a product' : `Edit ${esc(p.name)}`, subtitle: esc(store.name), actions: !isNew ? `<a class="btn btn-outline btn-sm" href="#/shop/${p.id}" target="_blank" rel="noopener">${icon('eye', 15)} View in shop</a>` : '', body });
    },
    mount(el, ctx) {
      const form = el.querySelector('#prod-form'); if (!form) return;
      const p = this._p, store = this._store;
      const photos = C.photoManager(el.querySelector('[data-photos]'), { images: p.images, library: PRODUCT_LIBRARY, max: 4, aspect: '1 / 1' });
      const read = () => {
        const fd = new FormData(form);
        return { name: fd.get('name') || '', brand: fd.get('brand') || '', cat: fd.get('cat'), price: fd.get('price'), was: fd.get('was'), stock: fd.get('stock'), lowStockAt: fd.get('lowStockAt'),
          sizes: (fd.get('sizes') || '').split(',').map((x) => x.trim()).filter(Boolean),
          colors: [...el.querySelectorAll('[data-color-row]')].map((r) => ({ hex: r.querySelector('input[type=color]').value, name: r.querySelector('.input').value.trim() })).filter((x) => x.name),
          images: photos.get(), desc: fd.get('desc') || '', specs: [...el.querySelectorAll('[data-spec-row]')].map((r) => [...r.querySelectorAll('.input')].map((i) => i.value.trim())), status: fd.get('status') };
      };
      const preview = () => {
        const d = read();
        const price = +d.price || 0, was = +d.was > price ? +d.was : price;
        const pv = { id: 'preview', name: d.name || 'Product name', brand: d.brand || store.name, price, was, discount: was > price ? Math.round((1 - price / was) * 100) : 0, rating: p.rating || 5, reviews: p.reviews || 0, images: d.images.length ? d.images : ['photo-1551958219-acbc608c6377'], sizes: d.sizes.length ? d.sizes : ['One size'], badges: [], wished: false };
        el.querySelector('[data-preview]').innerHTML = SA.ui.cards.product(pv).replace(/href="#\/shop\/preview"/g, 'href="#" tabindex="-1"');
        const rate = D().storeRate(store);
        el.querySelector('[data-you-get]').textContent = price ? money(Math.round(price * (1 - rate))) : '—';
      };
      form.addEventListener('input', preview); form.addEventListener('change', preview);
      el.querySelector('[data-photos]').addEventListener('click', () => setTimeout(preview, 50));
      el.querySelector('[data-photos]').addEventListener('change', () => setTimeout(preview, 700));
      el.addEventListener('click', (e) => {
        if (e.target.closest('[data-add-color]')) { el.querySelector('[data-colors]').insertAdjacentHTML('beforeend', this._colorRow({ name: '', hex: '#FF5B14' }, el.querySelectorAll('[data-color-row]').length)); }
        if (e.target.closest('[data-rm-color]')) { e.target.closest('[data-color-row]').remove(); preview(); }
        if (e.target.closest('[data-add-spec]')) { el.querySelector('[data-specs]').insertAdjacentHTML('beforeend', this._specRow(['', ''])); }
        if (e.target.closest('[data-rm-spec]')) e.target.closest('[data-spec-row]').remove();
      });
      form.addEventListener('submit', (e) => {
        e.preventDefault();
        try {
          const out = ctx.params.id ? D().updateProduct(p.id, read()) : D().createProduct(store.id, read());
          SA.core.motion.confetti({ count: 50, spread: 0.6 });
          SA.ui.overlay.toast(out.status === 'active' ? `${out.name} is live in the Super App shop` : `${out.name} saved as a draft`, out.status === 'active' ? { action: 'View', onAction: () => window.open(`#/shop/${out.id}`, '_blank') } : {});
          SA.core.router.go(D().adminMode() ? '/admin/store' : '/dash/store');
        } catch (err) {
          if (err.details) C.showErrors(form, err.details);
          SA.ui.overlay.toast(err.message === 'storage_full' ? 'Not enough storage on this device — use fewer or smaller photos.' : err.message, { type: 'error' });
        }
      });
      preview();
    },
  });
})(window.SA);
