(function (SA) {
  const { esc, money, fmtDate, timeAgo } = SA.core.utils;
  const { icon, cards } = SA.ui;
  const c = SA.ui.c;
  const S = SA.services;
  const media = SA.data.media;
  const P = SA.data.products;

  const summaryBox = (t, { cta = '', delivery } = {}) => {
    const ship = delivery === 'express' ? (t.shipping > 0 ? P.shipping.express : P.shipping.express - P.shipping.standard) : t.shipping;
    return `<dl class="kv">
      <div><dt>Subtotal (${t.count} item${t.count === 1 ? '' : 's'})</dt><dd class="num">${money(t.subtotal)}</dd></div>
      ${t.savings ? `<div><dt>You’re saving</dt><dd class="num" style="color:var(--turf)">${money(t.savings)}</dd></div>` : ''}
      ${t.coupon ? `<div><dt>Code ${esc(t.coupon.code)}</dt><dd class="num" style="color:var(--turf)">${t.couponOff ? '−' + money(t.couponOff) : t.coupon.label}</dd></div>` : ''}
      <div><dt>Delivery${delivery === 'express' ? ' (express)' : ''}</dt><dd class="num">${ship ? money(ship) : 'Free'}</dd></div>
      <div class="total"><dt>Total</dt><dd class="num">${money(t.subtotal - t.couponOff + ship)}</dd></div>
    </dl>${cta}`;
  };

  /* ---------------- Cart ---------------- */
  SA.pages.cart = {
    title: 'Cart — Super App Shop',
    async render() {
      const t = S.shop.totals();
      if (!t.lines.length) {
        const rec = await S.shop.recommended(4);
        return `${c.pageHead({ crumbs: [['Shop', '#/shop'], ['Cart']], title: 'Your cart' })}<div class="container">
          <div class="panel">${c.empty({ icon: 'cart', title: 'Your cart is empty', text: 'Boots, balls and kits you add will show up here.', action: c.cta('Start shopping', { href: '#/shop' }) })}</div>
          <section class="section-tight"><div class="sec-head"><h2 class="h2">You might like</h2></div><div class="product-grid">${rec.map((p) => cards.product(p)).join('')}</div></section></div>`;
      }
      return `${c.pageHead({ crumbs: [['Shop', '#/shop'], ['Cart']], title: 'Your cart', text: `${t.count} item${t.count === 1 ? '' : 's'}` })}
      <div class="container has-action-bar">
        <div class="checkout-layout">
          <div class="stack" style="--gap:16px">
            ${t.freeShipGap > 0 && !t.lines.every((l) => l.product.service) ? `<div class="notice">${icon('truck', 18)}<div class="grow"><span>Add <b>${money(t.freeShipGap)}</b> more for free delivery.</span><div class="meter" style="margin-top:8px"><span style="width:${(t.subtotal / P.shipping.freeOver) * 100}%"></span></div></div></div>` : `<div class="notice turf">${icon('truck', 18)}<span>You’ve unlocked <b>free delivery</b>.</span></div>`}
            <ul class="panel cart-lines">${t.lines.map((l) => `<li class="cart-line" data-key="${esc(l.key)}">
              <a href="#/shop/${l.productId}" class="media cart-thumb"><img src="${media.u(l.product.images[0], 240, 240)}" alt="" /></a>
              <div class="grow">
                <p class="xs muted">${esc(l.product.brand)}</p>
                <h3 class="small strong"><a href="#/shop/${l.productId}">${esc(l.product.name)}</a></h3>
                <p class="xs soft" style="margin-top:4px">${l.product.sizes.length > 1 ? `${l.product.service ? 'Option' : 'Size'} ${esc(l.variant.size)} · ` : ''}${esc(l.variant.color)}</p>
                <div class="row wrap cart-line-actions">
                  <div class="qty qty-sm"><button data-q="-1" aria-label="Decrease">${icon(l.qty === 1 ? 'trash' : 'minus', 14)}</button><output class="num">${l.qty}</output><button data-q="1" aria-label="Increase">${icon('plus', 14)}</button></div>
                  <button class="link xs" data-save>Save for later</button><button class="link xs" data-remove style="color:var(--danger)">Remove</button>
                </div>
              </div>
              <div class="cart-line-price">${c.price(l.product.price * l.qty, l.product.was > l.product.price ? l.product.was * l.qty : 0)}</div>
            </li>`).join('')}</ul>
          </div>
          <aside class="panel panel-pad order-summary">
            <h2 class="h4">Order summary</h2>
            <form class="coupon" data-coupon>
              ${t.coupon ? `<div class="coupon-on">${icon('percent', 16)}<span class="grow small"><b>${esc(t.coupon.code)}</b> applied — ${esc(t.coupon.label)}</span><button type="button" class="link xs" data-uncoupon>Remove</button></div>`
                : `<label class="field-label" for="coupon">Promo code</label><div class="row" style="--gap:8px;margin-top:6px"><input class="input" id="coupon" name="code" placeholder="e.g. KICKOFF10" autocomplete="off" style="text-transform:uppercase" /><button class="btn btn-outline" type="submit">Apply</button></div><p class="field-error" data-cerr></p><p class="xs muted">Try KICKOFF10, PITCH50 or FREESHIP.</p>`}
            </form>
            <div style="margin-top:18px">${summaryBox(t, { cta: `<a class="btn btn-blaze btn-lg btn-block" style="margin-top:18px" href="#/checkout">${icon('lock', 16)} Checkout</a>` })}</div>
            <div class="pay-logos"><span>${SA.ui.payment.visaMark}</span><span class="badge">${icon('wallet', 12)} Wallet</span><span class="badge">Cash on delivery</span></div>
          </aside>
        </div>
      </div>
      <div class="action-bar"><div class="grow"><b class="num">${money(t.total)}</b><span class="xs muted" style="display:block">${t.count} items</span></div><a class="btn btn-blaze" href="#/checkout">Checkout</a></div>`;
    },
    mount(el) {
      el.addEventListener('click', (e) => {
        const line = e.target.closest('[data-key]');
        if (line) {
          const key = line.dataset.key;
          const cur = SA.core.store.get().cart.find((l) => l.key === key);
          const q = e.target.closest('[data-q]');
          if (q) { S.shop.updateQty(key, cur.qty + +q.dataset.q); SA.app.rerender(); }
          if (e.target.closest('[data-remove]')) { const removed = { ...cur }; line.classList.add('is-leaving'); setTimeout(() => { S.shop.removeLine(key); SA.app.rerender(); }, 360); SA.ui.overlay.toast('Removed from cart', { type: 'info', action: 'Undo', onAction: () => { S.shop.addToCart(removed.productId, removed.variant, removed.qty); SA.app.rerender(); } }); }
          if (e.target.closest('[data-save]')) { S.shop.moveToWishlist(key); SA.app.rerender(); SA.ui.overlay.toast('Moved to your wishlist', { type: 'info' }); }
        }
        if (e.target.closest('[data-uncoupon]')) { S.shop.removeCoupon(); SA.app.rerender(); }
      });
      el.querySelector('[data-coupon]')?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const code = e.target.code?.value || '';
        try { const d = await S.shop.applyCoupon(code); SA.core.motion.confetti({ count: 50, spread: .6, y: innerHeight * .4 }); SA.ui.overlay.toast(`${code.toUpperCase()} applied — ${d.label}`); SA.app.rerender(); }
        catch (err) { const f = el.querySelector('[data-coupon]'); f.classList.remove('shake'); void f.offsetWidth; f.classList.add('shake'); el.querySelector('[data-cerr]').textContent = err.message; }
      });
    },
  };

  /* ---------------- Checkout ---------------- */
  SA.pages.checkout = {
    title: 'Checkout — Super App Shop',
    render() {
      const t = S.shop.totals();
      if (!t.lines.length) return `<div class="container section">${c.empty({ icon: 'cart', title: 'Nothing to check out', text: 'Your cart is empty.', action: '<a class="btn btn-primary" href="#/shop">Go to shop</a>' })}</div>`;
      const u = S.user.me();
      return `<div class="container narrow-checkout">
        <div class="page-head"><a class="back-link" href="#/cart">${icon('chevronLeft', 16)} Back to cart</a><h1 class="h1" style="margin-top:10px">Checkout</h1>
          <div style="margin-top:14px">${c.steps(['Cart', 'Delivery', 'Payment', 'Confirmed'], 1)}</div></div>
        <div class="checkout-layout">
          <form id="co-form" class="stack" style="--gap:20px" novalidate>
            <section class="panel panel-pad">
              <div class="row-between"><h2 class="h4">1. Delivery address</h2><button type="button" class="link small" data-newaddr>${icon('plus', 14)} New address</button></div>
              <div class="stack addr-list" style="--gap:10px;margin-top:14px">${u.addresses.map((a, i) => `<label class="choice"><input type="radio" name="address" value="${a.id}" ${a.default || i === 0 ? 'checked' : ''} /><span class="radio-dot"></span><span class="grow"><b>${esc(a.label)}</b><span class="xs soft" style="display:block">${esc(a.line1)}, ${esc(a.line2)}, ${esc(a.city)}</span><span class="xs muted">${esc(a.phone)}</span></span></label>`).join('')}</div>
            </section>
            <section class="panel panel-pad">
              <h2 class="h4">2. Delivery speed</h2>
              <div class="stack" style="--gap:10px;margin-top:14px">
                <label class="choice"><input type="radio" name="delivery" value="standard" checked /><span class="radio-dot"></span><span class="grow"><b>Standard</b><span class="xs muted" style="display:block">2–3 working days</span></span><b class="small">${t.shipping ? money(t.shipping) : 'Free'}</b></label>
                <label class="choice"><input type="radio" name="delivery" value="express" /><span class="radio-dot"></span><span class="grow"><b>Express</b><span class="xs muted" style="display:block">Next day in Cairo & Giza</span></span><b class="small">${money(t.shipping ? P.shipping.express : P.shipping.express - P.shipping.standard)}</b></label>
              </div>
            </section>
            <section class="panel panel-pad" data-paybox>${SA.ui.payment.render({ amount: t.total, allowCOD: true, name: 'copay' })}</section>
          </form>
          <aside class="panel panel-pad order-summary">
            <h2 class="h4">Your order</h2>
            <ul class="co-items">${t.lines.map((l) => `<li><span class="media"><img src="${media.u(l.product.images[0], 120, 120)}" alt="" /><i class="num">${l.qty}</i></span><span class="grow small">${esc(l.product.name)}<span class="xs muted" style="display:block">${esc(l.variant.size)} · ${esc(l.variant.color)}</span></span><span class="small num">${money(l.product.price * l.qty)}</span></li>`).join('')}</ul>
            <div data-sum style="margin-top:16px">${summaryBox(t, { delivery: 'standard' })}</div>
            <button class="btn btn-blaze btn-lg btn-block" style="margin-top:18px" form="co-form" type="submit" data-place>${icon('lock', 16)} Place order</button>
          </aside>
        </div>
      </div>`;
    },
    mount(el) {
      const form = el.querySelector('#co-form');
      if (!form) return;
      const paybox = el.querySelector('[data-paybox]');
      SA.ui.payment.bind(paybox, { name: 'copay' });
      const refreshSum = () => {
        const t = S.shop.totals();
        el.querySelector('[data-sum]').innerHTML = summaryBox(t, { delivery: form.delivery.value });
      };
      form.addEventListener('change', (e) => { if (e.target.name === 'delivery') refreshSum(); });
      el.querySelector('[data-newaddr]').addEventListener('click', () => SA.ui.overlay.open({
        title: 'New delivery address',
        body: `<form id="addr" class="form-grid">
          <div class="field"><label for="a-l">Label</label><input class="input" id="a-l" name="label" placeholder="Home, Office…" required /></div>
          <div class="field"><label for="a-c">City / area</label><select class="select" id="a-c" name="city">${['Cairo', 'New Cairo', 'Giza', '6th of October', 'Sheikh Zayed', 'Alexandria', 'Mansoura', 'Tanta', 'Port Said', 'Hurghada'].map((x) => `<option>${x}</option>`).join('')}</select></div>
          <div class="field span-2"><label for="a-1">Street and building</label><input class="input" id="a-1" name="line1" required /></div>
          <div class="field span-2"><label for="a-2">District, floor, apartment</label><input class="input" id="a-2" name="line2" /></div>
          <div class="field span-2"><label for="a-p">Phone</label><input class="input" id="a-p" name="phone" inputmode="tel" placeholder="+20 1XX XXX XXXX" required /><span class="field-error" data-err="phone"></span></div></form>`,
        footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-primary" form="addr" type="submit">Save address</button>`,
        onMount(m, close) {
          m.querySelector('#addr').addEventListener('submit', async (e) => {
            e.preventDefault();
            const d = SA.core.utils.formData(e.target);
            if (!/^\+?20?\s?1[0125]\d[\s\d]{7,10}$/.test(d.phone.trim())) { m.querySelector('[data-err="phone"]').textContent = 'Enter an Egyptian mobile number, e.g. +20 100 123 4567.'; return; }
            if (!d.label.trim() || !d.line1.trim()) return;
            await S.user.addAddress(d);
            close(); SA.app.rerender(); SA.ui.overlay.toast('Address saved');
          });
        },
      }));
      form.addEventListener('submit', async (e) => {
        e.preventDefault();
        const u = S.user.me();
        const address = u.addresses.find((a) => a.id === form.address.value);
        const payment = SA.ui.payment.read(paybox, { name: 'copay' });
        try {
          const order = await SA.ui.payment.process(() => S.shop.placeOrder({ address, delivery: form.delivery.value, payment }), { root: paybox, label: 'Placing your order', methodHint: payment.method });
          SA.core.router.go(`/order/${order.id}?new=1`, { replace: true });
        } catch { /* shown */ }
      });
    },
  };

  /* ---------------- Order confirmation / detail ---------------- */
  const STAGES = [['confirmed', 'Confirmed', 'check'], ['packed', 'Packed', 'package'], ['shipped', 'On the way', 'truck'], ['delivered', 'Delivered', 'home']];
  SA.pages.order = {
    title: 'Order — Super App Shop',
    async render(ctx) {
      const o = await S.shop.order(ctx.params.id);
      const stage = STAGES.findIndex((s) => s[0] === o.status);
      const multi = o.sellers.length > 1 || o.sellers.some((g) => !g.official);
      const sellerStage = (g) => STAGES.findIndex((s) => s[0] === g.status);
      return `<div class="container confirm-wrap">
        ${ctx.query.new ? `<div class="confirm-hero"><div class="success-mark"><svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"><path d="m4.5 12.5 4.8 4.8L19.5 7"/></svg></div>
          <h1 class="h1">Order confirmed</h1><p class="lead" style="text-align:center">Thanks, ${esc(S.user.me().firstName)}. We’ll notify you when it ships.</p></div>`
          : `<div class="page-head"><a class="back-link" href="#/orders">${icon('chevronLeft', 16)} Order history</a><h1 class="h1" style="margin-top:10px">Order ${o.id}</h1></div>`}
        <article class="ticket">
          <div class="ticket-body">
            <div class="row-between wrap"><div><span class="xs muted">Order number</span><b class="ticket-id num">${o.id}</b></div><div style="text-align:right"><span class="xs muted">Estimated delivery</span><b style="display:block">${fmtDate(o.eta, 'long')}</b></div></div>
            ${o.status === 'cancelled' ? `<div class="notice blaze" style="margin-top:16px">${icon('x', 18)}<span><b>This order was cancelled by the seller.</b> ${o.payment.method !== 'cod' ? `${money(o.cancelledValue)} went back to your wallet.` : 'Nothing was charged.'}</span></div>`
              : `<ol class="track">${STAGES.map(([k, l, ic], i) => `<li class="${i <= stage ? 'is-done' : ''} ${i === stage ? 'is-current' : ''}"><span>${icon(ic, 16, 2.2)}</span><b class="xs">${l}</b></li>`).join('')}</ol>`}
          </div>
          ${multi ? `<div class="ticket-body seller-track">${o.sellers.map((g) => `<div class="st-row">
              <div class="row-between wrap"><b class="small">${icon('bag', 14)} ${esc(g.storeName)}</b><span class="badge ${g.status === 'cancelled' ? 'badge-danger' : g.status === 'delivered' ? 'badge-turf' : 'badge-info'}">${g.status === 'cancelled' ? 'Cancelled' : STAGES[sellerStage(g)]?.[1] || g.status}</span></div>
              <span class="xs muted">${g.lines.length} item${g.lines.length > 1 ? 's' : ''}${g.courier ? ` · ${esc(g.courier)}${g.tracking ? ` — tracking <b class="num">${esc(g.tracking)}</b>` : ''}` : g.status === 'confirmed' ? ' · the seller is preparing your order' : ''}${g.status === 'cancelled' ? ` · ${o.payment.method !== 'cod' ? 'refunded to your wallet' : 'not charged'}` : ''}</span>
            </div>`).join('')}</div>` : ''}
          <div class="ticket-cut"></div>
          <div class="ticket-body">
            <ul class="co-items">${o.lines.map((l) => `<li><span class="media"><img src="${media.u(l.image, 120, 120)}" alt="" /><i class="num">${l.qty}</i></span><span class="grow small">${esc(l.name)}<span class="xs muted" style="display:block">${esc(l.variant.size)} · ${esc(l.variant.color)}</span></span><span class="small num">${money(l.price * l.qty)}</span></li>`).join('')}</ul>
            <div class="ticket-grid" style="padding:18px 0 0">
              <div><span class="xs muted">Deliver to</span><b>${esc(o.address.label)}</b><span class="xs soft">${esc(o.address.line1)}, ${esc(o.address.city)}</span></div>
              <div><span class="xs muted">Delivery</span><b>${o.delivery === 'express' ? 'Express' : 'Standard'}</b></div>
              <div><span class="xs muted">Payment</span><b>${esc(o.payment.label)}</b></div>
              <div><span class="xs muted">Total</span><b class="num">${money(o.total)}</b>${o.couponOff ? `<span class="xs" style="color:var(--turf)">${money(o.couponOff)} saved with ${esc(o.coupon)}</span>` : ''}</div>
            </div>
          </div>
        </article>
        ${SA.ui.returns?.orderBlock(o) || ''}
        <div class="row wrap confirm-actions"><a class="btn btn-primary" href="#/shop">Continue shopping</a><button class="btn btn-outline" data-invoice>${icon('file', 16)} Invoice (PDF)</button><a class="btn btn-outline" href="#/orders">Order history</a><a class="btn btn-ghost" href="#/support?ref=order:${encodeURIComponent(o.id)}">${icon('help', 16)} Get help</a></div>
      </div>`;
    },
    mount(el, ctx) {
      if (ctx.query.new) setTimeout(() => { const m = el.querySelector('.success-mark')?.getBoundingClientRect(); SA.core.motion.confetti(m ? { x: m.left + m.width / 2, y: m.top + m.height / 2 } : {}); }, 350);
      el.querySelector('[data-rt-new]')?.addEventListener('click', async () => SA.ui.returns.openForm(await S.shop.order(ctx.params.id)));
      el.querySelector('[data-invoice]')?.addEventListener('click', async (e) => {
        const btn = e.currentTarget; btn.disabled = true;
        try { await SA.ui.docs.orderInvoice(await S.shop.order(ctx.params.id)); } catch (err) { console.error(err); SA.ui.overlay.toast('Couldn’t create the invoice. Try again.', { type: 'error' }); }
        btn.disabled = false;
      });
    },
  };

  /* ---------------- Order history ---------------- */
  SA.pages.orders = {
    title: 'Orders — Super App',
    async render() {
      const orders = await S.shop.orders();
      const label = { ...Object.fromEntries(STAGES.map(([k, l]) => [k, l])), cancelled: 'Cancelled' };
      return `${c.pageHead({ crumbs: [['Profile', '#/profile'], ['Orders']], title: 'Order history' })}
      <div class="container stack" style="--gap:12px">
        ${orders.length ? orders.map((o) => `<a class="card card-link order-row" href="#/order/${o.id}">
          <div class="order-thumbs">${o.lines.slice(0, 3).map((l) => `<span class="media"><img src="${media.u(l.image, 120, 120)}" alt="" /></span>`).join('')}</div>
          <div class="grow"><div class="row wrap" style="--gap:8px"><b class="num">${o.id}</b><span class="badge ${o.status === 'delivered' ? 'badge-turf' : o.status === 'cancelled' ? 'badge-danger' : 'badge-info'}">${label[o.status]}</span></div>
            <p class="small soft">${o.lines.length} item${o.lines.length > 1 ? 's' : ''} · placed ${timeAgo(o.createdAt)} · ${esc(o.payment.label)}</p></div>
          <b class="num">${money(o.total)}</b>${icon('chevronRight', 18)}</a>`).join('')
          : `<div class="panel">${c.empty({ icon: 'package', title: 'No orders yet', text: 'When you buy something from the shop, you can track it here.', action: '<a class="btn btn-primary" href="#/shop">Visit the shop</a>' })}</div>`}
      </div>`;
    },
  };

  /* ---------------- Wishlist ---------------- */
  SA.pages.wishlist = {
    title: 'Wishlist — Super App',
    render() {
      const items = S.shop.wishlist();
      return `${c.pageHead({ crumbs: [['Profile', '#/profile'], ['Wishlist']], title: 'Wishlist', text: items.length ? `${items.length} saved item${items.length > 1 ? 's' : ''}` : '' })}
      <div class="container">${items.length ? `<div class="product-grid">${items.map((p) => cards.product(p)).join('')}</div>`
        : `<div class="panel">${c.empty({ icon: 'heart', title: 'Nothing saved yet', text: 'Tap the heart on any product to save it for later.', action: '<a class="btn btn-primary" href="#/shop">Browse the shop</a>' })}</div>`}</div>`;
    },
    mount(el) {
      const unsub = SA.core.store.subscribe((s, meta) => { if (meta === 'wishlist') SA.app.rerender(); });
      return unsub;
    },
  };
})(window.SA);
