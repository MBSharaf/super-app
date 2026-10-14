(function (SA) {
  const { esc, money, debounce, plural } = SA.core.utils;
  const { icon, cards } = SA.ui;
  const c = SA.ui.c;
  const S = SA.services;
  const media = SA.data.media;

  const catImg = { designs: 'pod:ds-1:Black', boots: 'photo-1511886929837-354d827aae26', balls: 'photo-1614632537190-23e4146777db', kits: 'photo-1552066379-e7bfd22155c5', goalkeeping: 'photo-1632072820781-79f3a064f640', running: 'photo-1542291026-7eec264c27ff', training: 'photo-1584735935682-2f2b69dff9d2', padel: 'photo-1612534847738-b3af9bc31f0c', accessories: 'photo-1622260614153-03223fb72052', services: 'photo-1526232761682-d26e03ac148e' };

  const filterPanel = (f) => {
    const brands = S.shop.brands();
    const sel = (f.brand || '').split(',').filter(Boolean);
    return `<div class="stack" style="--gap:22px">
      <div><h3 class="field-label">Category</h3><ul class="facet">${[{ id: '', name: 'All products' }, ...S.shop.categories()].map((cat) => `<li><button data-set="cat" data-v="${cat.id}" class="${(f.cat || '') === cat.id ? 'is-active' : ''}">${esc(cat.name)}${cat.count != null ? `<span class="muted">${cat.count}</span>` : ''}</button></li>`).join('')}</ul></div>
      <div><h3 class="field-label">Price</h3><div class="chips wrap" style="flex-wrap:wrap">${[['', 'Any'], ['1000', 'Under 1,000'], ['2500', 'Under 2,500'], ['5000', 'Under 5,000']].map(([v, l]) => `<button class="chip" data-set="max" data-v="${v}" aria-pressed="${(f.max || '') === v}">${l}</button>`).join('')}</div></div>
      <div><h3 class="field-label">Brand</h3><div class="stack" style="--gap:8px;margin-top:8px">${brands.map((b) => `<label class="row small" style="--gap:10px"><input type="checkbox" data-brand="${esc(b)}" ${sel.includes(b) ? 'checked' : ''} /> ${esc(b)}</label>`).join('')}</div></div>
      <label class="row small" style="--gap:10px"><input type="checkbox" data-sale ${f.sale ? 'checked' : ''} /> <b>On sale only</b></label>
    </div>`;
  };

  SA.pages.shop = {
    title: 'Shop — Super App',
    async render(ctx) {
      const f = { ...ctx.query };
      const items = await S.shop.list(f);
      const cat = S.shop.categories().find((x) => x.id === f.cat);
      const designs = !f.cat && !f.q && !f.sale && !f.brand && !f.max && !f.store ? (await S.shop.list({ cat: 'designs' })).slice(0, 4) : [];
      const dcats = f.cat === 'designs' ? S.shop.designCategories().filter((x) => x.count) : [];
      const designStrip = designs.length ? `<section class="container section-tight shop-designs">
        <div class="sec-head"><div><p class="eyebrow">${icon('sparkle', 13)} Made to order</p><h2 class="h2">Designs by Egyptian creators</h2><p class="soft">Original tees, hoodies and caps — printed after you order.</p></div>
          <div class="row" style="--gap:8px"><a class="btn btn-outline btn-sm" href="#/sell">${icon('shirt', 14)} Sell your designs</a><a class="btn btn-primary btn-sm" href="#/shop?cat=designs">See all</a></div></div>
        <div class="product-grid">${designs.map((p) => cards.product(p)).join('')}</div></section>` : '';
      const dcatBar = f.cat === 'designs' ? `<div class="container"><div class="chips dcat-bar" role="group" aria-label="Design sections">${[{ id: '', name: 'All designs' }, ...dcats].map((x) => `<a class="chip" href="${SA.core.router.href('/shop', { ...f, dcat: x.id || undefined })}" aria-pressed="${(f.dcat || '') === x.id}">${esc(x.name)}${x.count != null ? ` <span class="chip-count">${x.count}</span>` : ''}</a>`).join('')}</div>
        <div class="sell-banner"><span>${icon('sparkle', 18)}</span><p class="small"><b>Got a design?</b> Put it on a hoodie or tee and earn on every sale — no stock, no upfront cost.</p><a class="btn btn-sm btn-blaze" href="#/sell">Start selling</a></div></div>` : '';
      const isLanding = !f.cat && !f.q && !f.sale && !f.brand && !f.max && !f.store;
      const shopStore = f.store ? S.shop.storeInfo(f.store) : null;
      return `
      ${isLanding ? `<section class="container shop-hero">
        <div class="shop-hero-copy"><h1 class="display" style="font-size:clamp(2.4rem,1.4rem+4vw,4.4rem)">Gear for every game.</h1>
          <p class="lead" style="margin-top:14px">Original boots, balls and kits from adidas, Nike, Puma and more — delivered across Egypt in 1–3 days. Free delivery over ${money(1500)}.</p>
          <div class="row wrap" style="margin-top:22px">${c.cta('Shop boots', { href: '#/shop?cat=boots' })}<a class="btn btn-outline" href="#/shop?sale=1">${icon('percent', 16)} Sale</a></div></div>
        <div class="shop-hero-art"><img src="${media.u('photo-1579952363873-27f3bade9f55', 1000, 1000)}" alt="Football boot resting on a match ball" /><span class="badge badge-glass shop-hero-tag">${icon('truck', 13)} Next-day delivery in Cairo</span></div>
      </section>
      <section class="container"><div class="cat-strip">${S.shop.categories().map((cc) => `<a class="cat-tile" href="#/shop?cat=${cc.id}"><span class="media"><img src="${media.u(catImg[cc.id], 300, 300)}" alt="" loading="lazy" /></span><span class="small strong">${esc(cc.name)}</span></a>`).join('')}</div></section>`
      : shopStore ? `<section class="container storefront"><div class="sf-cover" style="background-image:url('${media.u(shopStore.cover, 1600, 560)}')"></div><div class="sf-info"><span class="sf-logo">${shopStore.logo ? `<img src="${shopStore.logo}" alt="" />` : icon('bag', 26)}</span><div class="grow"><span class="xs muted">Store on Super App${shopStore.official ? ' · Official' : ''}</span><h1 class="h1">${esc(shopStore.name)}</h1><p class="soft">${esc(shopStore.description)}</p></div></div></section>`
      : c.pageHead({ crumbs: [['Shop', '#/shop'], [cat ? cat.name : f.q ? `Search “${f.q}”` : f.sale ? 'Sale' : 'All products']], title: cat ? esc(cat.name) : f.q ? `Results for “${esc(f.q)}”` : f.sale ? 'Sale' : 'All products' })}
      ${designStrip}${dcatBar}

      <div class="container">
        <div class="shop-toolbar">
          <div class="input-icon grow">${icon('search', 18)}<input class="input" id="shop-q" type="search" placeholder="Search boots, balls, jerseys…" value="${esc(f.q || '')}" aria-label="Search products" /></div>
          <button class="btn btn-outline shop-filter-btn" data-open-filters>${icon('filter', 16)} Filters</button>
          <label class="sort-select">${icon('sort', 16)}<span class="sr-only">Sort</span><select class="select" id="shop-sort">${[['featured', 'Featured'], ['price-asc', 'Price: low to high'], ['price-desc', 'Price: high to low'], ['rating', 'Top rated'], ['discount', 'Biggest discount']].map(([v, l]) => `<option value="${v}" ${f.sort === v ? 'selected' : ''}>${l}</option>`).join('')}</select></label>
        </div>
        <div class="shop-layout">
          <aside class="shop-filters" aria-label="Filters">${filterPanel(f)}</aside>
          <div>
            <p class="small muted" style="margin-bottom:14px">${plural(items.length, 'product')}</p>
            ${items.length ? `<div class="product-grid">${items.map((p) => cards.product(p)).join('')}</div>`
              : c.empty({ icon: 'search', title: 'Nothing matches that', text: 'Try a broader search or clear your filters.', action: '<a class="btn btn-primary" href="#/shop">Clear filters</a>' })}
          </div>
        </div>
      </div>`;
    },
    mount(el, ctx) {
      const f = { ...ctx.query };
      const go = (patch) => {
        const next = { ...f, ...patch };
        Object.keys(next).forEach((k) => (next[k] === '' || next[k] == null || next[k] === false) && delete next[k]);
        SA.core.router.go(SA.core.router.href('/shop', next).slice(1));
      };
      const bindPanel = (root, close) => {
        root.querySelectorAll('[data-set]').forEach((b) => b.addEventListener('click', () => { close?.(); go({ [b.dataset.set]: b.dataset.v }); }));
        root.querySelectorAll('[data-brand]').forEach((cb) => cb.addEventListener('change', () => {
          const sel = [...root.querySelectorAll('[data-brand]:checked')].map((x) => x.dataset.brand);
          close?.(); go({ brand: sel.join(',') });
        }));
        root.querySelector('[data-sale]')?.addEventListener('change', (e) => { close?.(); go({ sale: e.target.checked ? '1' : '' }); });
      };
      bindPanel(el.querySelector('.shop-filters'));
      el.querySelector('[data-open-filters]').addEventListener('click', () => SA.ui.overlay.open({ drawer: true, side: 'left', title: 'Filters', body: filterPanel(f), onMount: (m, close) => bindPanel(m, close) }));
      el.querySelector('#shop-sort').addEventListener('change', (e) => go({ sort: e.target.value }));
      const q = el.querySelector('#shop-q');
      q.addEventListener('keydown', (e) => { if (e.key === 'Enter') go({ q: q.value.trim() }); });
      q.addEventListener('search', () => { if (!q.value) go({ q: '' }); });
    },
  };
})(window.SA);
