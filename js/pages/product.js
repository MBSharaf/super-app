(function (SA) {
  const { esc, money } = SA.core.utils;
  const { icon, cards } = SA.ui;
  const c = SA.ui.c;
  const S = SA.services;
  const media = SA.data.media;

  SA.pages.product = {
    title: 'Product — Super App Shop',
    async render(ctx) {
      const p = await S.shop.get(ctx.params.id);
      const [reviews, related, recommended] = await Promise.all([S.shop.reviews(p.id), S.shop.related(p.id, 4), S.shop.recommended(4, [p.id])]);
      this.title = `${p.name} — Super App Shop`;
      const breakdown = S.shop.ratingBreakdown(p);
      const lowStock = p.stock <= 8;
      return `<div class="container has-action-bar">
        <div class="page-head" style="padding-bottom:18px"><nav class="crumbs" aria-label="Breadcrumb"><a href="#/shop">Shop</a>${icon('chevronRight', 14)}<a href="#/shop?cat=${p.cat}">${esc(p.category.name)}</a>${icon('chevronRight', 14)}<span aria-current="page" class="truncate">${esc(p.name)}</span></nav></div>
        <div class="pdp">
          <div class="pdp-gallery">
            <div class="pdp-thumbs">${p.images.map((im, i) => `<button class="pdp-thumb media ${i === 0 ? 'is-active' : ''}" data-img="${im}" aria-label="Image ${i + 1}"><img src="${media.u(im, 200, 200)}" alt="" /></button>`).join('')}</div>
            ${p.pod ? '<div class="pdp-main" data-g3d data-fly>' : `<div class="pdp-main media" data-zoom><img src="${media.u(p.images[0], 1200, 1200)}" alt="${esc(p.name)}" data-main data-fly />`}
              ${p.discount ? `<span class="badge badge-danger pdp-off">−${p.discount}%</span>` : ''}</div>
          </div>
          <div class="pdp-info">
            <p class="small muted">${p.pod ? esc(p.productName) : esc(p.brand)}${p.seller && !p.seller.official ? ` · ${p.pod ? 'Designed by' : 'Sold by'} <a class="link" href="${p.seller.href}">${esc(p.seller.name)}</a>` : ''}</p>
            <h1 class="h2" style="margin-top:6px">${esc(p.name)}</h1>
            <button type="button" data-to-reviews class="row small" style="margin-top:12px;--gap:8px">${p.reviews ? `${c.stars(p.rating, 15)}<b>${p.rating.toFixed(1)}</b><span class="muted">${p.reviews} reviews</span>` : '<span class="muted">No reviews yet</span>'}</button>
            <div class="pdp-price">${c.price(p.price, p.was)}${p.discount ? `<span class="small" style="color:var(--turf)">You save ${money(p.was - p.price)}</span>` : ''}</div>
            <p class="soft" style="margin-top:14px">${esc(p.desc)}</p>
            ${[...new Set([...(p.tags || []), ...(p.seller?.tags || [])].map((t) => String(t).toLowerCase()))].length ? `<div class="tag-row">${[...new Set([...(p.tags || []), ...(p.seller?.tags || [])].map((t) => String(t).toLowerCase()))].slice(0, 8).map((t) => `<a class="tag-chip" href="#/shop?tag=${encodeURIComponent(t)}" data-tag="${esc(t)}">#${esc(t)}</a>`).join('')}</div>` : ''}
            ${p.pod && p.bigSizeExtra ? `<p class="xs muted" style="margin-top:6px">${p.bigSizes.join('/')} +${money(p.bigSizeExtra)}</p>` : ''}

            ${p.colors.length ? `<div class="pdp-opt"><span class="field-label">Colour: <span class="soft" data-color-name>${esc(p.colors[0].name)}</span></span>
              <div class="row" style="--gap:10px;margin-top:10px">${p.colors.map((col, i) => `<button class="swatch ${i === 0 ? 'is-active' : ''}" data-color="${esc(col.name)}" style="--sw:${col.hex}" aria-label="${esc(col.name)}" aria-pressed="${i === 0}"></button>`).join('')}</div></div>` : ''}
            ${p.sizes.length > 1 ? `<div class="pdp-opt"><div class="row-between"><span class="field-label">${p.service ? 'Option' : 'Size'}</span>${['boots', 'running', 'kits'].includes(p.cat) || p.sizeChart ? `<button class="link small" data-sizeguide>${p.sizeChart ? 'Size chart' : 'Size guide'}</button>` : ''}</div>
              <div class="size-grid" style="margin-top:10px" role="radiogroup">${p.sizes.map((s) => `<button class="size-btn" role="radio" aria-checked="false" data-size="${esc(s)}">${esc(s)}</button>`).join('')}</div><p class="field-error" data-size-err></p></div>` : ''}

            <div class="pdp-buy">
              <div class="qty"><button type="button" data-q="-1" aria-label="Decrease">${icon('minus', 16)}</button><output data-qty>1</output><button type="button" data-q="1" aria-label="Increase">${icon('plus', 16)}</button></div>
              <button class="btn btn-primary btn-lg grow" data-add ${p.soldOut ? 'disabled' : ''}>${icon('cart', 18)} ${p.soldOut ? 'Sold out' : 'Add to cart'}</button>
              <button class="icon-btn fav-btn big ${p.wished ? 'is-on' : ''}" data-action="wish" data-id="${p.id}" aria-pressed="${p.wished}" aria-label="Wishlist">${icon(p.wished ? 'heartFill' : 'heart', 20)}</button>
            </div>
            <button class="btn btn-outline btn-block" data-buynow style="margin-top:10px">Buy now</button>
            ${p.pod ? `<p class="small" style="margin-top:14px;color:var(--turf)"><b>Made to order</b> · printed after you order</p>
            <ul class="assurance small pdp-assure">
              <li>${icon('truck', 16)} Printed and shipped in 3–5 days</li>
              <li>${icon('refresh', 16)} Free reprint if there’s a print fault — made-to-order items can’t be returned otherwise</li>
              <li>${icon('sparkle', 16)} Original design by ${esc(p.seller?.name || 'a Super App seller')}</li>
            </ul>` : `<p class="small" style="margin-top:14px;color:${lowStock ? 'var(--blaze-2)' : 'var(--turf)'}"><b>${p.soldOut ? 'Sold out — check back soon' : lowStock ? `Only ${p.stock} left in stock` : 'In stock'}</b>${p.service ? ' · booked after checkout' : ' · ships from Cairo'}</p>
            <ul class="assurance small pdp-assure">
              <li>${icon('truck', 16)} ${p.service ? 'We’ll call within 24 hours to schedule' : 'Delivery in 1–3 days · free over 1,500 EGP'}</li>
              <li>${icon('refresh', 16)} A problem with the item? Return it within 24 hours of delivery</li>
              <li>${icon('shield', 16)} ${p.seller && !p.seller.official ? 'Checked by Super App before it went on sale' : '100% original — authorised retailer'}</li>
            </ul>`}
            <details class="pdp-details" open><summary>Specifications</summary><dl class="kv">${p.specs.map(([k, v]) => `<div><dt>${esc(k)}</dt><dd>${esc(v)}</dd></div>`).join('')}</dl></details>
            ${p.sizeChart ? `<details class="pdp-details"><summary>Size chart (cm)</summary>${SA.ui.sizeChart(p)}</details>` : ''}
            ${p.pod ? `<button class="link xs muted pdp-report" data-report>${icon('flag', 13)} Report this design</button>` : ''}
          </div>
        </div>

        <section class="section" id="reviews">
          <div class="reviews-layout">
            <div class="panel panel-pad review-summary">
              <b class="gw-big num">${p.rating.toFixed(1)}</b>${c.stars(p.rating, 18)}<p class="small muted">${p.reviews} reviews</p>
              <ul class="bars">${breakdown.map((n, i) => `<li><span class="xs">${5 - i}★</span><span class="meter"><span style="width:${(n / p.reviews) * 100}%"></span></span><span class="xs muted num">${n}</span></li>`).join('')}</ul>
            </div>
            <div class="review-list">${reviews.map((r) => `<article class="review">
              <div class="row">${c.avatar({ name: r.name, avatar: r.avatar })}<div class="grow"><b class="small">${esc(r.name)}</b><span class="xs muted" style="display:block">${r.daysAgo} days ago${r.verified ? ' · verified purchase' : ''}</span></div>${c.stars(r.rating, 13)}</div>
              <h3 class="small strong" style="margin-top:12px">${esc(r.title)}</h3><p class="small soft" style="margin-top:4px">${esc(r.text)}</p></article>`).join('')}</div>
          </div>
        </section>

        <section class="section-tight"><div class="sec-head"><h2 class="h2">Related products</h2></div><div class="product-grid">${related.map((x) => cards.product(x)).join('')}</div></section>
        <section class="section-tight"><div class="sec-head"><h2 class="h2">Recommended for you</h2></div><div class="product-grid">${recommended.map((x) => cards.product(x)).join('')}</div></section>
      </div>
      <div class="action-bar"><div class="grow"><b class="num">${money(p.price)}</b>${p.discount ? `<s class="xs muted" style="display:block">${money(p.was)}</s>` : ''}</div><button class="btn btn-primary" data-add>${icon('cart', 16)} Add to cart</button></div>`;
    },
    mount(el, ctx) {
      const p = S.shop.getSync(ctx.params.id);
      const sel = { size: p.sizes.length === 1 ? p.sizes[0] : null, color: p.colors[0]?.name, qty: 1 };
      // Printed-to-order items: interactive 3D garment (drag to turn, colour swatches recolour it)
      let viewer = null;
      if (p.pod) {
        const d = S.pod.get(p.designId);
        const g3 = el.querySelector('[data-g3d]');
        viewer = SA.ui.garment3d.mount(g3, { kind: d.product.kind, hex: p.colors[0]?.hex, art: d.src, artBack: d.srcBack !== d.src ? d.srcBack : '', place: d.placement, artRatio: d.ratioFront, artRatioBack: d.ratioBack, label: p.name, cacheKey: `${d.id}|${d.updatedAt}` });
        el.querySelectorAll('[data-img]').forEach((b) => b.addEventListener('click', () => {
          el.querySelectorAll('[data-img]').forEach((x) => x.classList.toggle('is-active', x === b));
          const [, , color, side] = b.dataset.img.split(':');
          const col = p.colors.find((k) => k.name === color);
          if (col) viewer.update({ hex: col.hex });
          viewer.show(side === 'back' ? 'back' : 'front');
        }));
      }
      const main = el.querySelector('[data-main]') || el.querySelector('[data-g3d]');
      const zoomBox = el.querySelector('[data-zoom]') || main;
      el.querySelector('[data-to-reviews]').addEventListener('click', () => el.querySelector('#reviews').scrollIntoView({ behavior: 'smooth' }));
      if (!p.pod) {
        el.querySelectorAll('[data-img]').forEach((b) => b.addEventListener('click', () => {
          el.querySelectorAll('[data-img]').forEach((x) => x.classList.toggle('is-active', x === b));
          zoomBox.classList.add('is-swapping'); setTimeout(() => { main.src = media.u(b.dataset.img, 1200, 1200); main.onload = () => zoomBox.classList.remove('is-swapping'); }, 220);
        }));
        // Hover zoom on desktop
        const zoom = el.querySelector('[data-zoom]');
        zoom.addEventListener('pointermove', (e) => { if (e.pointerType !== 'mouse') return; const r = zoom.getBoundingClientRect(); main.style.transformOrigin = `${((e.clientX - r.left) / r.width) * 100}% ${((e.clientY - r.top) / r.height) * 100}%`; main.style.transform = 'scale(1.7)'; });
        zoom.addEventListener('pointerleave', () => { main.style.transform = ''; });
      }

      el.querySelectorAll('[data-color]').forEach((b) => b.addEventListener('click', () => {
        sel.color = b.dataset.color; el.querySelector('[data-color-name]').textContent = sel.color;
        el.querySelectorAll('[data-color]').forEach((x) => { x.classList.toggle('is-active', x === b); x.setAttribute('aria-pressed', x === b); });
        if (viewer) viewer.update({ hex: p.colors.find((k) => k.name === sel.color)?.hex });
      }));
      el.querySelector('[data-report]')?.addEventListener('click', () => SA.ui.overlay.open({
        title: 'Report this design', subtitle: 'Tell us if it copies your work, uses a logo or crest without permission, or breaks our rules. We check every report.',
        body: `<form id="rp-form" class="stack" style="--gap:10px" novalidate>
          ${[['copyright', 'It copies my artwork or photo'], ['trademark', 'It uses a logo, crest or brand name'], ['offensive', 'It’s offensive or inappropriate'], ['other', 'Something else']].map(([v, l], i) => `<label class="choice"><input type="radio" name="reason" value="${v}" ${i === 0 ? 'checked' : ''} /><span class="radio-dot"></span><span class="grow small">${l}</span></label>`).join('')}
          <div class="field"><label for="rp-d">Details</label><textarea class="textarea" id="rp-d" name="detail" maxlength="600" placeholder="Link to the original work or explain what’s wrong"></textarea></div>
          <div class="field"><label for="rp-c">Your email or phone <span class="muted">(optional)</span></label><input class="input" id="rp-c" name="contact" maxlength="120" /></div></form>`,
        footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-primary" form="rp-form" type="submit">Send report</button>`,
        onMount(m, close) {
          m.querySelector('#rp-form').addEventListener('submit', (e) => {
            e.preventDefault();
            try { S.pod.report(p.designId, SA.core.utils.formData(e.target)); close(); SA.ui.overlay.toast('Thanks — our team will review this design.', { type: 'success' }); }
            catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
          });
        },
      }));
      el.querySelectorAll('[data-size]').forEach((b) => b.addEventListener('click', () => {
        sel.size = b.dataset.size; el.querySelector('[data-size-err]').textContent = '';
        el.querySelectorAll('[data-size]').forEach((x) => { x.classList.toggle('is-active', x === b); x.setAttribute('aria-checked', x === b); });
      }));
      el.querySelectorAll('[data-q]').forEach((b) => b.addEventListener('click', () => { sel.qty = Math.max(1, Math.min(10, sel.qty + +b.dataset.q)); el.querySelector('[data-qty]').textContent = sel.qty; }));
      const add = async () => {
        if (!sel.size) {
          el.querySelector('[data-size-err]').textContent = 'Choose a size to continue.';
          el.querySelector('.size-grid').scrollIntoView({ behavior: 'smooth', block: 'center' });
          el.querySelector('.size-grid').classList.add('shake'); setTimeout(() => el.querySelector('.size-grid').classList.remove('shake'), 400);
          return false;
        }
        try { const r = await S.shop.addToCart(p.id, { size: sel.size, color: sel.color }, sel.qty); sel.added = r.added; if (r.limited) SA.ui.overlay.toast(`Only ${p.stock} in stock — we added what’s left.`, { type: 'info' }); } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); return false; }
        SA.core.motion.flyTo(main);
        return true;
      };
      el.querySelectorAll('[data-add]').forEach((b) => b.addEventListener('click', async () => { if (await add()) { const html0 = b.innerHTML; b.classList.add('is-added'); b.innerHTML = 'Added to cart ✓'; setTimeout(() => { b.classList.remove('is-added'); b.innerHTML = html0; }, 1600); } if (sel.size && !sel.limitedShown) SA.ui.overlay.toast(`Added ${sel.added ?? sel.qty} × ${esc(p.name)}`, { action: 'View cart', onAction: () => SA.core.router.go('/cart') }); }));
      el.querySelector('[data-buynow]').addEventListener('click', async () => { if (await add()) SA.core.router.go('/checkout'); });
      el.querySelector('[data-sizeguide]')?.addEventListener('click', () => SA.ui.overlay.open(p.sizeChart ? {
        title: 'Size chart', subtitle: `${p.productName} · in centimetres. Lay a T-shirt you like flat and compare.`, body: SA.ui.sizeChart(p),
      } : {
        title: 'Size guide', subtitle: 'EU sizes. If you’re between sizes, go up for turf boots.',
        body: `<table class="table"><thead><tr><th>EU</th><th>UK</th><th>US</th><th>Foot length</th></tr></thead><tbody>${[[39, 6, 6.5, 24.5], [40, 6.5, 7, 25], [41, 7.5, 8, 25.8], [42, 8, 8.5, 26.5], [43, 9, 9.5, 27.3], [44, 9.5, 10, 28], [45, 10.5, 11, 28.8]].map((r) => `<tr><td>${r[0]}</td><td>${r[1]}</td><td>${r[2]}</td><td>${r[3]} cm</td></tr>`).join('')}</tbody></table>`,
      }));
    },
  };
})(window.SA);
