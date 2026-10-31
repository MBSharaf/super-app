/**
 * Seller dashboard (role "seller"): overview with level and progress, designs, print-on-demand orders,
 * wallet (print profit + store money, withdrawals, ID check). Sellers with their own store also get
 * the partner store pages (My store, Orders, Discount codes).
 */
(function (SA) {
  const { esc, money, fmtNum, timeAgo, fmtDate, isoDate, addDays, today, MON, parseISO } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const D = () => SA.services.dash;
  const P = () => SA.services.pod;
  const SL = () => SA.services.sellers;
  const DAY = 864e5;

  const seller = () => SL().byAccount(D().me().id);
  /** Orders for the seller's own products (their store), newest first */
  const storeOrders = (s) => { if (!s?.storeId) return []; try { return D().orders({ from: isoDate(addDays(today(), -365)) }).filter((o) => o.storeId === s.storeId); } catch { return []; } };
  const SOS = { confirmed: ['badge-blaze', 'New'], packed: ['badge-gold', 'Packed'], shipped: ['badge-info', 'Shipped'], delivered: ['badge-turf', 'Delivered'], cancelled: ['badge-danger', 'Cancelled'], returned: ['badge-danger', 'Returned'] };
  const ST = { received: ['badge-blaze', 'Received'], printing: ['badge-gold', 'Printing'], shipped: ['badge-info', 'Shipped'], delivered: ['badge-turf', 'Delivered'], refused: ['badge-danger', 'Refused'], cancelled: ['badge-danger', 'Cancelled'] };
  const status = (s) => { const [c, l] = ST[s] || ['badge', s]; return `<span class="badge ${c}">${l}</span>`; };
  const noSeller = (key, title) => C.layout(key, { title, body: `<div class="dash-card">${SA.ui.c.empty({ icon: 'lock', title: 'Sellers only', text: 'Sign in with a seller account to see this page.' })}</div>` });

  /** Daily profit bars (orders placed, refusals excluded) */
  const profitBars = (orders, days = 30) => {
    const t = today();
    const rows = Array.from({ length: days }, (_, i) => { const d = addDays(t, i - days + 1); const k = isoDate(d); return { d, k, v: 0, n: 0 }; });
    const idx = new Map(rows.map((r, i) => [r.k, i]));
    orders.filter((o) => o.status !== 'refused').forEach((o) => { const i = idx.get(isoDate(new Date(o.placedAt))); if (i != null) { rows[i].v += o.profit; rows[i].n += o.qty; } });
    const max = Math.max(1, ...rows.map((r) => r.v));
    const total = rows.reduce((a, r) => a + r.v, 0);
    return `<div class="sl-bars" role="img" aria-label="Profit from orders placed in the last ${days} days: ${money(total)}">${rows.map((r, i) => `<span class="sl-bar" style="--h:${Math.round((r.v / max) * 100)}%" data-tip="${fmtDate(r.d)} — ${money(r.v)} · ${r.n} item${r.n === 1 ? '' : 's'}">${i % 7 === 0 ? `<i>${r.d.getDate()} ${MON[r.d.getMonth()]}</i>` : ''}</span>`).join('')}</div>`;
  };

  /* ---------- Overview ---------- */
  SA.pages.dashSeller = C.guard({
    roles: ['seller'],
    title: 'Seller dashboard — Super App',
    render() {
      const s = seller(); if (!s) return noSeller('seller', 'Overview');
      const l = SL().levelOf(s);
      const since = Date.now() - 30 * DAY;
      const orders = P().orders({ sellerId: s.id });
      const recent = orders.filter((o) => o.placedAt >= since && o.status !== 'refused');
      const items = recent.reduce((t, o) => t + o.qty, 0);
      const profit = recent.reduce((t, o) => t + o.profit, 0);
      const designs = P().list({ sellerId: s.id, status: ['live', 'review', 'paused', 'draft', 'rejected'] });
      const live = designs.filter((d) => d.status === 'live');
      const views = live.reduce((t, d) => t + (d.views || 0), 0);
      const e = P().earnings(s.id);
      const top = live.slice().sort((a, b) => (b.sales || 0) - (a.sales || 0)).slice(0, 5);
      const sos = storeOrders(s).filter((o) => o.createdAt >= since && !['cancelled', 'returned'].includes(o.status));
      const storeUnits = sos.reduce((t, o) => t + o.units, 0), storeNet = Math.round(sos.reduce((t, o) => t + o.net, 0));
      // Print on demand off: no levels, designs or "printed" numbers — own products only, held 24 hours after delivery
      const pod = SA.config.podEnabled;
      const body = pod ? podBody() : `
        <div class="dash-grid g-1-1">
          <section class="dash-card"><h2 class="h4">Wallet</h2>
            <dl class="kv" style="margin-top:12px"><div><dt>Available to withdraw</dt><dd class="num text-turf">${money(e.available)}</dd></div><div><dt>On hold (until 24 hours after delivery)</dt><dd class="num">${money(e.pending)}</dd></div><div><dt>Earned so far</dt><dd class="num">${money(e.lifetime)}</dd></div></dl>
            <a class="btn btn-outline btn-sm" href="#/dash/seller/wallet" style="margin-top:14px">${icon('wallet', 14)} Open wallet</a></section>
          <section class="dash-card"><h2 class="h4">Your store</h2>
            <dl class="kv" style="margin-top:12px"><div><dt>Items sold (30 days)</dt><dd class="num">${fmtNum(storeUnits)}</dd></div><div><dt>Earned (30 days)</dt><dd class="num">${money(storeNet)}</dd></div><div><dt>Orders (30 days)</dt><dd class="num">${fmtNum(sos.length)}</dd></div></dl>
            <div class="row" style="--gap:8px;margin-top:14px"><a class="btn btn-primary btn-sm" href="#/dash/seller/products">${icon('plus', 14)} Add product</a><a class="btn btn-outline btn-sm" href="#/dash/seller/orders?kind=store">Orders</a></div></section>
        </div>`;
      function podBody() { return `
        <div class="dash-grid g-1-1">
          ${SA.ui.seller.levelCard(s)}
          <section class="dash-card"><h2 class="h4">Wallet</h2>
            <dl class="kv" style="margin-top:12px"><div><dt>Available to withdraw</dt><dd class="num text-turf">${money(e.available)}</dd></div><div><dt>On hold (until ${P().cfg().holdDays} days after delivery)</dt><dd class="num">${money(e.pending)}</dd></div><div><dt>Earned so far</dt><dd class="num">${money(e.lifetime)}</dd></div></dl>
            <a class="btn btn-outline btn-sm" href="#/dash/seller/wallet" style="margin-top:14px">${icon('wallet', 14)} Open wallet</a></section>
        </div>
        <div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'Items sold (30 days)', value: fmtNum(items + storeUnits), ic: 'shirt', tone: 'k-turf', sub: s.storeId ? `${fmtNum(items)} printed · ${fmtNum(storeUnits)} your products` : '' })}
          ${C.kpi({ label: 'Earned (30 days)', value: money(profit + storeNet), ic: 'wallet', tone: 'k-blaze', sub: s.storeId ? `${money(profit)} printed · ${money(storeNet)} your products` : '' })}
          ${C.kpi({ label: 'Design views', value: fmtNum(views), ic: 'eye' })}
          ${C.kpi({ label: 'Conversion', value: views ? `${((live.reduce((t, d) => t + (d.sales || 0), 0) / views) * 100).toFixed(1)}%` : '—', ic: 'trend', sub: 'sales ÷ views' })}
        </div>
        <section class="dash-card"><div class="row-between"><h2 class="h4">Profit by day</h2><span class="xs muted">Orders placed · last 30 days</span></div>${profitBars(orders)}<div class="bc-tip sl-tip" hidden></div></section>
        <div class="dash-grid g-1-1">
          <section class="dash-card"><div class="row-between"><h2 class="h4">Best designs</h2><a class="btn btn-ghost btn-sm" href="#/dash/seller/designs">All designs</a></div>
            ${top.length ? `<ul class="sl-top">${top.map((d) => `<li><span class="sl-thumb"><img src="${SA.data.media.u(`pod:${d.id}:${d.colors[0]}`)}" alt="" /></span><span class="grow"><b class="small">${esc(d.title)}</b><span class="xs muted" style="display:block">${esc(d.product?.name)} · ${fmtNum(d.views || 0)} views</span></span><b class="num small">${fmtNum(d.sales || 0)} sold</b></li>`).join('')}</ul>` : `<p class="small soft" style="margin-top:10px">No live designs yet.</p>`}
            <a class="btn btn-primary btn-sm" href="#/sell/studio" style="margin-top:12px">${icon('plus', 14)} New design</a></section>
          <section class="dash-card"><div class="row-between"><h2 class="h4">Latest orders</h2><a class="btn btn-ghost btn-sm" href="#/dash/seller/orders${s.mode === 'store' ? '?kind=store' : ''}">All orders</a></div>
            ${sos.length ? `<p class="xs muted" style="margin-top:6px">${icon('bag', 12)} ${sos.length} order${sos.length > 1 ? 's' : ''} for your own products in 30 days · <a class="link" href="#/dash/seller/orders?kind=store">see them</a></p>` : ''}
            ${orders.length ? `<ul class="sl-orders">${orders.slice(0, 6).map((o) => `<li><span class="grow"><b class="small">${esc(o.title)}</b><span class="xs muted" style="display:block">${esc(o.color)} · ${esc(o.size)} · ${timeAgo(o.placedAt)} · ${esc(o.customer.city || '')}</span></span><span class="sl-o-r">${status(o.status)}<b class="num xs">${o.status === 'refused' ? '—' : `+${money(o.profit)}`}</b></span></li>`).join('')}</ul>` : `<p class="small soft" style="margin-top:10px">No orders yet. Share your store link to get the first one.</p>`}</section>
        </div>
        <section class="dash-card"><h2 class="h4">Seller levels</h2><div style="margin-top:12px">${SA.ui.seller.ladder(l.index)}</div></section>`; }
      const gate = s.status === 'pending' ? `<div class="notice gold">${icon('clock', 18)}<span><b>Waiting for approval.</b> The Super App team checks new sellers within one working day. ${pod ? 'Your designs and products go live once you’re approved.' : 'You can add products once you’re approved.'}</span></div>` : s.status === 'rejected' ? `<div class="notice blaze">${icon('alert', 18)}<span><b>Not approved.</b> ${esc(s.reviewReason || '')} Contact Help & support to appeal.</span></div>` : '';
      return C.layout('seller', { title: `Hi, ${esc(D().me().name.split(' ')[0])}`, subtitle: `${esc(s.name)} · superapp.eg/s/${esc(s.handle)}`, actions: `<a class="btn btn-outline" href="#/s/${esc(s.handle)}" target="_blank" rel="noopener">${icon('eye', 15)} View store</a>`, body: gate + body });
    },
    mount(el) {
      const bars = el.querySelector('.sl-bars'), tip = el.querySelector('.sl-tip');
      bars?.addEventListener('pointermove', (e) => {
        const b = e.target.closest('.sl-bar'); if (!b) { tip.hidden = true; return; }
        const r = bars.getBoundingClientRect(); tip.textContent = b.dataset.tip; tip.hidden = false;
        tip.style.left = `${Math.min(r.width - 200, Math.max(0, e.clientX - r.left - 100))}px`;
      });
      bars?.addEventListener('pointerleave', () => (tip.hidden = true));
    },
  });

  /* ---------- Designs ---------- */
  SA.pages.dashSellerDesigns = C.guard({
    roles: ['seller'],
    title: 'My designs — Super App',
    render(ctx) {
      const s = seller(); if (!s) return noSeller('seller-designs', 'Designs');
      const st = ctx.query.status || '';
      const all = P().list({ sellerId: s.id, status: ['live', 'review', 'paused', 'draft', 'rejected'] }).sort((a, b) => b.updatedAt - a.updatedAt);
      const list = st ? all.filter((d) => d.status === st) : all;
      const lim = SL().canAddDesign(s);
      const tabs = [['', 'All'], ['live', 'Live'], ['review', 'In review'], ['draft', 'Drafts'], ['rejected', 'Rejected'], ['paused', 'Paused']];
      const body = `<p class="small soft">${lim.used} of ${lim.limit == null ? 'unlimited' : lim.limit} active designs used${lim.limit != null && !lim.ok ? ' — pause or delete one, or level up to add more' : ''}.</p>
        <div class="chips" role="tablist">${tabs.map(([k, l]) => `<a class="chip" role="tab" href="#/dash/seller/designs${k ? `?status=${k}` : ''}" aria-pressed="${st === k}" aria-selected="${st === k}">${l} <span class="chip-count">${k ? all.filter((d) => d.status === k).length : all.length}</span></a>`).join('')}</div>
        <section class="dash-card table-card"><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Design</th><th>Status</th><th class="r">Price</th><th class="r">You earn</th><th class="r">Views</th><th class="r">Sold</th><th></th></tr></thead><tbody>
        ${list.map((d) => `<tr><td><span class="row" style="--gap:10px"><span class="sl-thumb"><img src="${SA.data.media.u(`pod:${d.id}:${d.colors[0] || ''}`)}" alt="" /></span><span><b class="small">${esc(d.title || 'Untitled')}</b><span class="xs muted" style="display:block">${esc(d.product?.name)} · ${esc(P().category(d.category)?.name || 'No category')}</span>${d.status === 'rejected' ? `<span class="xs text-danger" style="display:block">${esc(d.reason)}</span>` : ''}</span></span></td>
          <td>${SA.ui.seller.designStatus(d)}</td><td class="r num">${money(d.price)}</td><td class="r num">${money(d.profit)}</td><td class="r num">${fmtNum(d.views || 0)}</td><td class="r num">${fmtNum(d.sales || 0)}</td>
          <td class="r"><span class="row" style="--gap:6px;justify-content:flex-end">${d.status !== 'rejected' ? `<button class="btn btn-ghost btn-sm" data-sample="${d.id}">${icon('shirt', 13)} Sample</button>` : ''}<a class="btn btn-outline btn-sm" href="#/sell/studio/${d.id}">${icon('edit', 13)} Edit</a>${['live', 'paused'].includes(d.status) ? `<button class="btn btn-ghost btn-sm" data-pause="${d.id}" data-to="${d.status === 'live' ? 1 : 0}">${d.status === 'live' ? 'Pause' : 'Resume'}</button>` : ''}${d.status === 'live' ? `<a class="btn btn-ghost btn-sm" href="#/shop/pod-${d.id}" target="_blank" rel="noopener" aria-label="View in shop">${icon('arrowUpRight', 13)}</a>` : ''}</span></td></tr>`).join('') || `<tr><td colspan="7" class="small soft">Nothing here.</td></tr>`}
        </tbody></table></div></section>
        ${(() => { const sm = P().samples(s.id); const SS = { requested: ['badge-gold', 'Waiting for approval'], approved: ['badge-info', 'Printing'], delivered: ['badge-turf', 'Delivered'], rejected: ['badge-danger', 'Not approved'] }; return sm.length ? `<section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">${icon('shirt', 18)} Samples</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Design</th><th>Ordered</th><th>Status</th><th class="r">Paid</th></tr></thead><tbody>
          ${sm.map((x) => { const o = x.orderId && SA.services.db.find('podOrders', x.orderId); const st = o && x.status === 'approved' && o.status === 'shipped' ? ['badge-info', 'Shipped'] : SS[x.status] || ['', x.status]; return `<tr><td class="small"><b>${esc(x.title)}</b><span class="xs muted" style="display:block">${esc(x.color)} · ${esc(x.size)}</span></td><td class="small">${fmtDate(new Date(x.createdAt))}</td><td><span class="badge ${st[0]}">${st[1]}</span>${x.reason ? `<span class="xs muted" style="display:block">${esc(x.reason)}</span>` : ''}</td><td class="r num">${x.free ? 'Free' : money(x.price)}</td></tr>`; }).join('')}
          </tbody></table></div></section>` : ''; })()}`;
      return C.layout('seller-designs', { title: 'Designs', subtitle: 'Print-on-demand designs in your store', actions: `<a class="btn btn-outline" href="#/sell/bulk">${icon('image', 16)} Upload many</a><a class="btn btn-primary" href="#/sell/studio">${icon('plus', 16)} New design</a>`, body });
    },
    mount(el) {
      el.addEventListener('click', (e) => {
        const sm = e.target.closest('[data-sample]');
        if (sm) { sampleDialog(sm.dataset.sample); return; }
        const p = e.target.closest('[data-pause]');
        if (p) { try { P().setPaused(p.dataset.pause, p.dataset.to === '1'); SA.ui.overlay.toast(p.dataset.to === '1' ? 'Design paused' : 'Design is live again'); SA.app.rerender(); } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); } }
      });
    },
  });

  /** Order a sample of a design (price depends on the level: base price, Pro 30% off, Elite one free a month) */
  const sampleDialog = (designId) => {
    const d = SA.services.db.find('designs', designId);
    const cat = SA.services.db.find('podCatalog', d.catalogId);
    const acc = D().me();
    const priceLine = (size) => { const p = P().samplePrice(designId, size); return p.free ? `<b>Free</b> — your Elite sample this month` : `<b class="num">${money(p.price)}</b>${p.discount ? ` <span class="xs muted">(${Math.round(p.discount * 100)}% off the ${money(p.base)} base price)</span>` : ' <span class="xs muted">(base price)</span>'}`; };
    SA.ui.overlay.open({
      title: `Order a sample: ${esc(d.title)}`, subtitle: 'Check the print quality before customers do. It’s paid from your available balance.',
      body: `<form class="form-grid" data-smp novalidate>
        <div class="field"><label for="sm-c">Colour</label><select class="select" id="sm-c" name="color">${d.colors.map((c) => `<option>${esc(c)}</option>`).join('')}</select></div>
        <div class="field"><label for="sm-s">Size</label><select class="select" id="sm-s" name="size">${cat.sizes.map((z) => `<option ${z === 'L' ? 'selected' : ''}>${esc(z)}</option>`).join('')}</select></div>
        <div class="field span-2"><label for="sm-a">Deliver to</label><input class="input" id="sm-a" name="address" value="${esc(SA.services.sellers.byAccount(acc.id)?.sampleAddress || '')}" placeholder="Street, building, area, city" /></div>
        <p class="small span-2" data-sm-price>${priceLine('L')}</p><p class="field-error span-2" data-err="form"></p>
        <div class="span-2 row" style="--gap:8px;justify-content:flex-end"><button type="button" class="btn btn-ghost" data-close>Cancel</button><button class="btn btn-primary" type="submit">Request sample</button></div></form>`,
      onMount(m, close) {
        const f = m.querySelector('[data-smp]');
        f.size.addEventListener('change', () => { m.querySelector('[data-sm-price]').innerHTML = priceLine(f.size.value); });
        f.addEventListener('submit', (e) => {
          e.preventDefault();
          try { P().requestSample(designId, SA.core.utils.formData(f)); close(); SA.ui.overlay.toast('Sample requested — the Super App team approves it shortly'); SA.app.rerender(); }
          catch (err) { m.querySelector('[data-err="form"]').textContent = err.message; }
        });
      },
    });
  };

  /* ---------- Print orders ---------- */
  SA.pages.dashSellerOrders = C.guard({
    roles: ['seller'],
    title: 'Orders — Super App',
    render(ctx) {
      const s = seller(); if (!s) return noSeller('seller-orders', 'Orders');
      const kind = s.mode === 'store' ? 'store' : s.mode === 'pod' ? 'pod' : (ctx.query.kind || 'pod');
      const sos = storeOrders(s);
      const kindTabs = s.mode === 'both' ? `<div class="chips" role="tablist" aria-label="Order type"><a class="chip" href="#/dash/seller/orders" aria-pressed="${kind === 'pod'}">${icon('shirt', 13)} Printed designs <span class="chip-count">${P().orders({ sellerId: s.id }).filter((o) => !o.sample).length}</span></a><a class="chip" href="#/dash/seller/orders?kind=store" aria-pressed="${kind === 'store'}">${icon('bag', 13)} My products <span class="chip-count">${sos.length}</span></a></div>` : '';
      if (kind === 'store') {
        const body = `${kindTabs}<p class="small soft">You pack and ship these. Open an order to mark it packed or shipped, print the packing slip and see what you earn.</p>
          <section class="dash-card table-card"><div class="table-wrap"><table class="table dash-table adm-rows"><thead><tr><th>Order</th><th>Customer</th><th>Placed</th><th>Status</th><th class="r">Items</th><th class="r">Total</th><th class="r">You earn</th></tr></thead><tbody>
          ${sos.map((o) => `<tr data-store-order="${esc(o.key)}" tabindex="0"><td><b class="small num">${esc(o.orderId)}</b></td><td class="small">${esc(o.customer?.name || '')}</td><td class="small">${fmtDate(new Date(o.createdAt))}</td><td><span class="badge ${(SOS[o.status] || ['', o.status])[0]}">${(SOS[o.status] || ['', o.status])[1]}</span></td><td class="r num">${o.units}</td><td class="r num">${money(o.gross)}</td><td class="r num strong">${money(Math.round(o.net))}</td></tr>`).join('') || `<tr><td colspan="7">${SA.ui.c.empty({ icon: 'bag', title: 'No orders for your products yet', text: 'Share your store link to get your first order.' })}</td></tr>`}
          </tbody></table></div></section>`;
        return C.layout('seller-orders', { title: 'Orders', subtitle: 'Your own products — you pack and ship', body });
      }
      const st = ctx.query.status || '';
      const all = P().orders({ sellerId: s.id }).filter((o) => !o.sample);
      const list = st ? all.filter((o) => (st === 'open' ? ['received', 'printing', 'shipped'].includes(o.status) : o.status === st)) : all;
      const tabs = [['', 'All'], ['open', 'In progress'], ['delivered', 'Delivered'], ['refused', 'Refused']];
      const cnt = (k) => (k === 'open' ? all.filter((o) => ['received', 'printing', 'shipped'].includes(o.status)).length : k ? all.filter((o) => o.status === k).length : all.length);
      const body = `${kindTabs}<p class="small soft">The factory prints and ships every order — nothing to do on your side. Profit is released ${P().cfg().holdDays} days after delivery. Refused deliveries cost you nothing.</p>
        <div class="chips" role="tablist">${tabs.map(([k, l]) => `<a class="chip" role="tab" href="#/dash/seller/orders${k ? `?status=${k}` : ''}${kind === 'store' ? '' : ''}" aria-pressed="${st === k}" aria-selected="${st === k}">${l} <span class="chip-count">${cnt(k)}</span></a>`).join('')}</div>
        <section class="dash-card table-card"><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Item</th><th>Customer</th><th>Placed</th><th>Status</th><th class="r">Price</th><th class="r">Your profit</th></tr></thead><tbody>
        ${list.map((o) => `<tr><td><b class="small">${esc(o.title)}</b><span class="xs muted" style="display:block">${esc(o.id)} · ${o.qty} × ${esc(o.color)} · ${esc(o.size)}</span></td><td class="small">${esc(o.customer.name.split(' ')[0])} · ${esc(o.customer.city || '')}</td>
          <td class="small">${fmtDate(new Date(o.placedAt))}</td><td>${status(o.status)}${o.tracking ? `<span class="xs muted" style="display:block">${esc(o.courier)} ${esc(o.tracking)}</span>` : ''}</td><td class="r num">${money(o.price * o.qty)}</td><td class="r num ${o.status === 'refused' ? 'muted' : 'strong'}">${o.status === 'refused' ? '—' : money(o.profit)}</td></tr>`).join('') || `<tr><td colspan="6" class="small soft">No orders here yet.</td></tr>`}
        </tbody></table></div></section>`;
      return C.layout('seller-orders', { title: 'Orders', subtitle: 'Printed designs — the factory prints and ships', body });
    },
    mount(el) {
      const open = (row) => SA.pages.dashOrders.openOrder(row.dataset.storeOrder);
      el.addEventListener('click', (e) => { const r = e.target.closest('[data-store-order]'); if (r) open(r); });
      el.addEventListener('keydown', (e) => { const r = e.target.closest('[data-store-order]'); if (r && e.key === 'Enter') open(r); });
    },
  });

  /* ---------- Wallet ---------- */
  SA.pages.dashSellerWallet = C.guard({
    roles: ['seller'],
    title: 'Seller wallet — Super App',
    render() {
      const s = seller(); if (!s) return noSeller('seller-wallet', 'Wallet');
      const e = P().earnings(s.id);
      const acc = D().me();
      let store = null;
      if (s.storeId && !s.live) { try { store = D().wallet({ ownerId: acc.id }); } catch { store = null; } }   // live: one seller wallet for everything
      const payouts = SA.services.db.all('sellerPayouts').filter((p) => p.sellerId === s.id);
      const kyc = { none: ['badge-gold', 'Not verified'], pending: ['badge-info', 'Under review'], verified: ['badge-turf', 'Verified'], rejected: ['badge-danger', 'Rejected — send again'] }[s.kyc || 'none'];
      const canWithdraw = s.kyc === 'verified' && e.available >= e.minWithdraw;
      const body = `
        <div class="dash-grid g-1-1">
          <section class="dash-card"><div class="row-between wrap" style="--gap:8px"><h2 class="h4">${icon('wallet', 18)} Earnings from your orders</h2>${s.live ? `<a class="btn btn-ghost btn-sm" href="/api/v1/wallet/statement.xlsx?type=Seller" download>${icon('file', 14)} Statement (Excel)</a>` : `<button class="btn btn-ghost btn-sm" data-statement>${icon('file', 14)} Statement (PDF)</button>`}</div>
            <p class="xs muted">${s.mode === 'store' ? 'Your own products.' : s.storeId ? 'Printed designs below; your own products are in “Store money”.' : 'Your printed designs.'} ${s.live ? 'The Excel statement lists every sale, refund and withdrawal.' : 'The PDF lists every order — printed or your own product — and every withdrawal.'}</p>
            <div class="wl-big"><span class="xs muted">Available to withdraw</span><b class="num">${money(e.available)}</b></div>
            <dl class="kv"><div><dt>On hold</dt><dd class="num">${money(e.pending)}</dd></div><div><dt>Withdrawn</dt><dd class="num">${money(e.paidOut)}</dd></div><div><dt>Earned so far</dt><dd class="num">${money(e.lifetime)}</dd></div></dl>
            ${e.nextClear.length ? `<p class="xs muted" style="margin-top:10px">${icon('clock', 12)} Next release: ${money(e.nextClear[0].amount)} on ${fmtDate(new Date(e.nextClear[0].at))}</p>` : ''}
            <form class="sl-withdraw" data-withdraw novalidate><div class="field"><label for="wd-a">Amount (EGP)</label><input class="input num" id="wd-a" name="amount" type="number" min="${e.minWithdraw}" max="${e.available}" value="${Math.max(e.minWithdraw, e.available)}" ${canWithdraw ? '' : 'disabled'} /></div>
              <button class="btn btn-primary" type="submit" ${canWithdraw ? '' : 'disabled'}>${icon('wallet', 15)} Withdraw</button></form>
            <p class="xs muted">${s.kyc !== 'verified' ? 'Verify your ID to withdraw.' : e.available < e.minWithdraw ? `You can withdraw once you have ${money(e.minWithdraw)} available — ${money(e.minWithdraw - e.available)} to go.` : `Paid to ${esc(acc.payout?.method || 'InstaPay')} ${esc(acc.payout?.detail || '')} within 2 working days.`}</p></section>
          <section class="dash-card"><div class="row-between"><h2 class="h4">${icon('shield', 18)} Identity check</h2><span class="badge ${kyc[0]}">${kyc[1]}</span></div>
            ${s.kyc === 'verified' ? `<p class="small soft" style="margin-top:10px">Your ID is verified. Withdrawals go to ${esc(acc.payout?.method || 'your payout account')}.</p>`
              : s.kyc === 'pending' ? `<p class="small soft" style="margin-top:10px">We’re checking your documents — usually within one working day.</p>`
              : `<p class="small soft" style="margin-top:10px">Needed once before your first withdrawal. Your documents are only used to verify you.</p>
              <form class="form-grid" data-kyc novalidate style="margin-top:12px">
                <div class="field span-2"><label for="ky-n">Full name (as on your ID)</label><input class="input" id="ky-n" name="fullName" maxlength="80" autocomplete="name" /></div>
                <div class="field span-2"><label for="ky-i">National ID number</label><input class="input num" id="ky-i" name="nationalId" inputmode="numeric" maxlength="14" autocomplete="off" /></div>
                <div class="field"><label for="ky-f">ID front photo</label><input class="input" id="ky-f" name="front" type="file" accept="image/*" /></div>
                <div class="field"><label for="ky-b">ID back photo</label><input class="input" id="ky-b" name="back" type="file" accept="image/*" /></div>
                <p class="field-error span-2" data-err></p>
                <div class="span-2"><button class="btn btn-primary" type="submit">Send for verification</button></div></form>`}
          </section>
        </div>
        ${store ? `<section class="dash-card"><h2 class="h4">${icon('bag', 18)} Store money</h2><p class="small soft" style="margin-top:6px">Sales from your own products, after the ${Math.round(SL().levelOf(s).commission * 100)}% commission for your level. Paid out every Monday.</p>
          <a class="btn btn-outline btn-sm" href="#/dash/wallet" style="margin-top:12px">Open store wallet</a></section>` : ''}
        <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Withdrawals</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Requested</th><th>To</th><th>Status</th><th class="r">Amount</th></tr></thead><tbody>
          ${payouts.map((p) => `<tr><td class="small">${fmtDate(new Date(p.createdAt))}</td><td class="small">${esc(p.method)} ${esc(p.detail)}</td><td><span class="badge ${p.status === 'paid' ? 'badge-turf' : p.status === 'rejected' ? 'badge-danger' : 'badge-gold'}">${p.status === 'paid' ? 'Paid' : p.status === 'rejected' ? 'Rejected' : 'Processing'}</span>${p.status === 'paid' ? ` <button class="btn btn-ghost btn-sm" data-payout-pdf="${p.id}">${icon('file', 13)} PDF</button>` : ''}${p.status === 'rejected' && p.reason ? `<span class="xs muted" style="display:block">${esc(p.reason)}</span>` : ''}${p.status === 'paid' && p.ref ? `<span class="xs muted" style="display:block">Ref ${esc(p.ref)}</span>` : ''}</td><td class="r num">${money(p.amount)}</td></tr>`).join('') || '<tr><td colspan="4" class="small soft">No withdrawals yet.</td></tr>'}
        </tbody></table></div></section>
        ${e.adjustments?.length ? `<section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Balance adjustments</h2><p class="xs muted" style="padding:4px 18px 0">Made by the Super App team — refund shares, sample charges and corrections.</p><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Date</th><th>Reason</th><th class="r">Amount</th></tr></thead><tbody>
          ${e.adjustments.slice().sort((x, y) => y.at - x.at).map((x) => `<tr><td class="small">${fmtDate(new Date(x.at))}</td><td class="small">${esc(x.reason)}</td><td class="r num strong" style="color:${x.amount > 0 ? 'var(--turf)' : 'var(--danger)'}">${x.amount > 0 ? '+' : '−'}${money(Math.abs(x.amount))}</td></tr>`).join('')}
        </tbody></table></div></section>` : ''}`;
      return C.layout('seller-wallet', { title: 'Wallet', subtitle: `Minimum withdrawal ${money(e.minWithdraw)} · ${s.live ? 'money released 24 hours after delivery' : `profit released ${P().cfg().holdDays} days after delivery`}`, body });
    },
    _statement() { const s = seller(); return SA.ui.docs.sellerStatement({ seller: s, account: D().me(), pod: P().orders({ sellerId: s.id }).filter((o) => !o.sample), store: storeOrders(s), payouts: SA.services.db.all('sellerPayouts').filter((p) => p.sellerId === s.id), earnings: P().earnings(s.id) }); },
    mount(el) {
      el.querySelector('[data-statement]')?.addEventListener('click', () => SA.pages.dashSellerWallet._statement().catch((e) => SA.ui.overlay.toast(e.message, { type: 'error' })));
      const s = seller(); if (!s) return;
      el.querySelector('[data-withdraw]')?.addEventListener('submit', (e) => {
        e.preventDefault();
        try { const p = P().withdraw(s.id, e.target.amount.value); SA.ui.overlay.toast(`Withdrawal of ${money(p.amount)} requested`, { type: 'success' }); SA.app.rerender(); }
        catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
      });
      el.querySelector('[data-kyc]')?.addEventListener('submit', (e) => {
        e.preventDefault();
        const f = e.target;
        try {
          SL().submitKyc(s.id, { fullName: f.fullName.value, nationalId: f.nationalId.value.trim(), front: f.front.files[0], back: f.back.files[0] });
          SA.ui.overlay.toast('Documents sent — we’ll verify them shortly', { type: 'success' }); SA.app.rerender();
        } catch (err) { f.querySelector('[data-err]').textContent = err.message; }
      });
    },
  });
  /* ---------- Store settings ---------- */
  const ACCENTS = ['#17795A', '#E4572E', '#1C2541', '#5B1A24', '#C9963A', '#6C3FB5', '#0E7C86', '#141414'];
  const MODES = [['pod', 'Print on demand'], ['both', 'Print on demand + my own products'], ['store', 'My own products only']];
  /** Store header preview — the same look as the public store page */
  const headPreview = (s) => {
    const cover = s.cover ? SA.data.media.u(s.cover, 1600, 560) : '';
    return `<div class="ss-cover ${cover ? '' : 'is-empty'}" style="${cover ? `background-image:url('${cover}');background-position:center ${s.coverY ?? 50}%;` : ''}--accent:${s.accent || '#17795A'}">
        ${cover ? '' : `<span class="ss-cover-hint small">${icon('image', 16)} Add a cover photo · 1600 × 560 looks best</span>`}
        <div class="ss-cover-tools"><button type="button" class="btn btn-sm ss-glass" data-pick="cover">${icon('camera', 14)} ${cover ? 'Change cover' : 'Upload cover'}</button>${cover ? `<button type="button" class="btn btn-sm ss-glass" data-remove="cover" aria-label="Remove cover">${icon('trash', 14)}</button>` : ''}</div></div>
      <div class="ss-head">
        <button type="button" class="ss-logo" data-pick="logo" aria-label="${s.logo ? 'Change logo' : 'Upload logo'}" style="--accent:${s.accent || '#17795A'}">${s.logo ? `<img src="${esc(s.logo)}" alt="" />` : `<span>${esc(SA.core.utils.initials(s.name))}</span>`}<i>${icon('camera', 14)}</i></button>
        <div class="grow"><b class="h3">${esc(s.name)}</b><span class="small soft" style="display:block">${(s.tags || []).length ? s.tags.map((t) => `#${esc(t)}`).join(' ') : 'Creator on Super App'}</span></div>
      </div>`;
  };

  SA.pages.dashSellerStore = C.guard({
    roles: ['seller'],
    title: 'Store settings — Super App',
    render() {
      const s = seller(); if (!s) return noSeller('seller-store', 'Store settings');
      const acc = D().me();
      const live = P().list({ sellerId: s.id, status: 'live' });
      const brk = SL().onBreak(s);
      const body = `
        <form id="ss-form" class="ss-layout" novalidate>
          <div class="stack" style="--gap:16px">
            <section class="dash-card ss-preview" data-preview>${headPreview(s)}</section>
            <section class="dash-card">
              <h2 class="h4">Store details</h2>
              <div class="form-grid" style="margin-top:14px">
                <div class="field"><label for="ss-name">Store name</label><input class="input" id="ss-name" name="name" maxlength="40" value="${esc(s.name)}" required /></div>
                <div class="field"><label for="ss-tag">Tags <span class="muted">(up to 5, comma between them)</span></label><input class="input" id="ss-tag" name="tags" maxlength="120" value="${esc((s.tags || []).join(', '))}" placeholder="e.g. streetwear, ahly, retro" /><span class="field-hint">Shoppers find you when they search or tap these tags in the shop. You see how many found you in Analytics.</span></div>
                <div class="field span-2"><label for="ss-handle">Store link</label>
                  <div class="input-affix"><span class="affix" data-i18n-skip>superapp.eg/s/</span><input class="input" id="ss-handle" name="handle" maxlength="30" value="${esc(s.handle)}" dir="ltr" autocomplete="off" spellcheck="false" /></div>
                  <div class="row wrap" style="--gap:8px;margin-top:8px"><code class="ss-url num" data-store-url dir="ltr">${esc(SA.core.router.url(`/s/${s.handle}`))}</code><button type="button" class="btn btn-outline btn-sm" data-copy-link>${icon('copy', 14)} Copy link</button></div>
                  <span class="field-hint" data-handle-hint>Changing it breaks links you already shared.</span></div>
                <div class="field span-2"><label for="ss-bio">About the store</label><textarea class="textarea" id="ss-bio" name="bio" maxlength="280" placeholder="What do you make? Who is it for?">${esc(s.bio || '')}</textarea><span class="field-hint" data-count>${(s.bio || '').length} / 280</span></div>
              </div>
            </section>
            <section class="dash-card">
              <h2 class="h4">Look</h2>
              <div class="field" style="margin-top:14px"><span class="field-label">Store colour</span>
                <div class="ss-accents" role="radiogroup" aria-label="Store colour">${ACCENTS.map((c) => `<label class="ss-accent"><input type="radio" name="accent" value="${c}" ${(s.accent || '#17795A') === c ? 'checked' : ''} /><span style="--c:${c}" title="${c}"></span></label>`).join('')}
                  <label class="ss-accent ss-accent-custom" title="Custom colour"><input type="color" name="accentCustom" value="${s.accent && !ACCENTS.includes(s.accent) ? s.accent : '#17795A'}" aria-label="Custom colour" /></label></div></div>
              <div class="field" style="margin-top:14px" ${s.cover ? '' : 'hidden'} data-covery><label for="ss-cy">Cover position</label><input id="ss-cy" name="coverY" type="range" min="0" max="100" value="${s.coverY ?? 50}" /><span class="field-hint">Slide to choose which part of the cover shows.</span></div>
              <div class="field" style="margin-top:14px"><label for="ss-feat">Featured design</label><select class="select" id="ss-feat" name="featured"><option value="">None</option>${live.map((d) => `<option value="${d.id}" ${s.featured === d.id ? 'selected' : ''}>${esc(d.title)} — ${esc(d.product?.name)}</option>`).join('')}</select><span class="field-hint">Shown first on your store page with a “Featured” badge.</span></div>
            </section>
            <section class="dash-card">
              <h2 class="h4">Selling</h2>
              <div class="stack" style="--gap:8px;margin-top:12px">${MODES.map(([v, l]) => `<label class="choice"><input type="radio" name="mode" value="${v}" ${s.mode === v ? 'checked' : ''} /><span class="radio-dot"></span><span class="grow small"><b>${l}</b></span></label>`).join('')}</div>
              <p class="xs muted" style="margin-top:8px">Turning on your own products opens Products and Store orders in the menu. Turning it off hides your products from the shop.</p>
              <label class="choice" style="margin-top:16px"><input type="checkbox" name="vacation" ${s.vacation ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span><span class="grow small"><b>Take a break</b><span class="xs muted" style="display:block">Hides your designs and products from the shop. Orders already placed are still made and delivered.</span></span></label>
              <div class="form-grid" data-vac ${s.vacation ? '' : 'hidden'} style="margin-top:12px">
                <div class="field"><label for="ss-vu">Back on <span class="muted">(optional)</span></label><input class="input" id="ss-vu" type="date" name="vacationUntil" value="${esc(s.vacationUntil || '')}" min="${SA.core.utils.isoDate(SA.core.utils.today())}" /></div>
                <div class="field"><label for="ss-vn">Message for visitors</label><input class="input" id="ss-vn" name="vacationNote" maxlength="120" value="${esc(s.vacationNote || '')}" placeholder="New drop coming soon!" /></div>
              </div>
              ${brk ? `<p class="small text-danger" style="margin-top:10px">${icon('info', 14)} Your store is on a break — nothing shows in the shop.</p>` : ''}
            </section>
            <section class="dash-card">
              <h2 class="h4">Payout account</h2>
              <div class="form-grid" style="margin-top:14px">
                <div class="field"><label for="ss-pm">Method</label><select class="select" id="ss-pm" name="payoutMethod">${['InstaPay', 'Vodafone Cash', 'Bank transfer'].map((m) => `<option ${acc.payout?.method === m ? 'selected' : ''}>${m}</option>`).join('')}</select></div>
                <div class="field"><label for="ss-pd">Account</label><input class="input" id="ss-pd" name="payoutDetail" maxlength="60" value="${esc(acc.payout?.detail || '')}" placeholder="Phone, email or IBAN" dir="ltr" /></div>
              </div>
            </section>
            <p class="field-error" data-err></p>
          </div>
          <aside class="ss-aside">
            <div class="dash-card">
              <b class="small">Your store page</b>
              <p class="xs muted" style="margin-top:4px">superapp.eg/s/<span data-handle-view>${esc(s.handle)}</span></p>
              <button class="btn btn-primary btn-block" type="submit" style="margin-top:14px">${icon('check', 15, 2.6)} Save changes</button>
              <a class="btn btn-outline btn-block" href="#/s/${esc(s.handle)}" style="margin-top:8px">${icon('eye', 15)} View store</a>
            </div>
          </aside>
        </form>`;
      return C.layout('seller-store', { title: 'Store settings', subtitle: 'Name, logo, cover, link and how your store looks', body });
    },
    mount(el) {
      const s0 = seller(); const form = el.querySelector('#ss-form'); if (!s0 || !form) return;
      // Working copy: images apply on Save
      const st = { logo: s0.logo || null, cover: s0.cover || null, coverY: s0.coverY ?? 50 };
      const draftSeller = () => { const f = SA.core.utils.formData(form); return { ...s0, ...st, name: f.name || s0.name, tags: SL().cleanTags(f.tags), accent: accent() }; };
      const accent = () => form.querySelector('[name="accent"]:checked')?.value || form.accentCustom.value;
      const preview = () => { el.querySelector('[data-preview]').innerHTML = headPreview(draftSeller()); el.querySelector('[data-covery]').hidden = !st.cover; };
      form.addEventListener('input', (e) => {
        if (e.target.name === 'coverY') { st.coverY = +e.target.value; preview(); return; }
        if (e.target.name === 'accentCustom') form.querySelectorAll('[name="accent"]').forEach((r) => (r.checked = false));
        if (e.target.name === 'bio') el.querySelector('[data-count]').textContent = `${e.target.value.length} / 280`;
        if (e.target.name === 'handle') {
          e.target.value = e.target.value.toLowerCase().replace(/[^a-z0-9-]/g, '');
          const h = e.target.value, hint = el.querySelector('[data-handle-hint]');
          el.querySelector('[data-handle-view]').textContent = h;
          const ok = h === s0.handle || SL().handleFree(h);
          hint.textContent = h === s0.handle ? 'Changing it breaks links you already shared.' : !SL().HANDLE.test(h) ? '3–30 letters, numbers or dashes' : ok ? 'Available' : 'Taken — try another';
          hint.className = `field-hint ${h !== s0.handle && ok && SL().HANDLE.test(h) ? 'text-turf' : h !== s0.handle ? 'text-danger' : ''}`;
        }
        if (['name', 'tags', 'accent', 'accentCustom'].includes(e.target.name)) preview();
        if (e.target.name === 'handle') el.querySelector('[data-store-url]').textContent = SA.core.router.url(`/s/${e.target.value}`);
      });
      form.addEventListener('change', (e) => { if (e.target.name === 'vacation') el.querySelector('[data-vac]').hidden = !e.target.checked; if (e.target.name === 'accent') preview(); });
      el.addEventListener('click', async (e) => {
        const pk = e.target.closest('[data-pick]');
        if (pk) {
          e.preventDefault();
          const logo = pk.dataset.pick === 'logo';
          try { const url = await SA.ui.pickImage(logo ? { aspect: 1, max: 400, alpha: true } : { aspect: 16 / 5.6, max: 1600 }); if (url) { st[logo ? 'logo' : 'cover'] = url; preview(); SA.ui.overlay.toast(logo ? 'Logo ready — save to publish it' : 'Cover ready — save to publish it'); } }
          catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
          return;
        }
        if (e.target.closest('[data-copy-link]')) { e.preventDefault(); const url = SA.core.router.url(`/s/${s0.handle}`); try { await navigator.clipboard.writeText(url); SA.ui.overlay.toast('Store link copied — share it anywhere'); } catch { SA.ui.overlay.toast(url, { type: 'info' }); } return; }
        const rm = e.target.closest('[data-remove]');
        if (rm) { e.preventDefault(); st[rm.dataset.remove] = null; preview(); }
      });
      form.addEventListener('submit', (e) => {
        e.preventDefault();
        const f = SA.core.utils.formData(form);
        const err = el.querySelector('[data-err]'); err.textContent = '';
        try {
          SL().update(s0.id, { name: f.name, tags: f.tags, bio: f.bio, handle: f.handle, accent: accent(), featured: f.featured || '', mode: f.mode,
            logo: st.logo, cover: st.cover, vacation: !!form.vacation.checked, vacationUntil: f.vacationUntil || '', vacationNote: f.vacationNote || '' });
          SA.services.db.update('sellers', s0.id, { coverY: st.coverY });
          if ((f.payoutDetail || '').trim()) SL().setPayout(s0.id, { method: f.payoutMethod, detail: f.payoutDetail });
          SA.ui.overlay.toast('Store saved — your page is updated', { type: 'success' });
          SA.app.rerender();
        } catch (e2) {
          err.textContent = e2.message; SA.ui.overlay.toast(e2.message, { type: 'error' });
          if (e2.message.includes('storage') || e2.code === 'storage_full') SA.ui.overlay.toast('Images are too big for this device — try smaller ones.', { type: 'error' });
        }
      });
    },
  });

  /* ---------- Analytics (Pro: advanced, Elite: advanced + Excel export) ---------- */
  const meters = (rows, fmt = (v) => fmtNum(v), tone = 'var(--turf)') => { const max = Math.max(1, ...rows.map((r) => r[1])); return rows.length ? `<ul class="adm-mix">${rows.map(([l, v]) => `<li><span class="row-between small"><span>${esc(l)}</span><b class="num">${fmt(v)}</b></span><span class="meter"><span style="width:${Math.max(2, (v / max) * 100)}%;background:${tone}"></span></span></li>`).join('')}</ul>` : '<p class="small muted">Nothing yet.</p>'; };
  const groupBy = (arr, key, val) => { const m = new Map(); arr.forEach((x) => { const k = key(x); m.set(k, (m.get(k) || 0) + val(x)); }); return [...m.entries()].sort((a, b) => b[1] - a[1]); };
  const sellerData = (s, days) => {
    const since = Date.now() - days * 864e5;
    const orders = P().orders({ sellerId: s.id }).filter((o) => !o.sample && o.placedAt >= since);
    const ok = orders.filter((o) => !['refused', 'cancelled'].includes(o.status));
    const designs = P().list({ sellerId: s.id, status: ['live', 'paused', 'review'] }).map((d) => { const os = ok.filter((o) => o.designId === d.id); const sold = os.reduce((t, o) => t + o.qty, 0); return { d, sold, views: d.views || 0, conv: d.views ? (d.sales || 0) / d.views : 0, revenue: os.reduce((t, o) => t + o.price * o.qty, 0), profit: os.reduce((t, o) => t + o.profit, 0) }; }).sort((a, b) => b.profit - a.profit);
    const WD = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
    return {
      orders, ok, designs,
      sold: ok.reduce((t, o) => t + o.qty, 0), revenue: ok.reduce((t, o) => t + o.price * o.qty, 0), profit: ok.reduce((t, o) => t + o.profit, 0),
      refused: orders.filter((o) => o.status === 'refused').length, views: designs.reduce((t, x) => t + x.views, 0), lifetimeSales: designs.reduce((t, x) => t + (x.d.sales || 0), 0),
      colors: groupBy(ok, (o) => o.color, (o) => o.qty).slice(0, 6), sizes: groupBy(ok, (o) => o.size, (o) => o.qty), cities: groupBy(ok, (o) => o.customer?.city || '—', (o) => o.qty).slice(0, 6),
      weekdays: WD.map((w, i) => [w, ok.filter((o) => new Date(o.placedAt).getDay() === i).reduce((t, o) => t + o.qty, 0)]),
    };
  };
  /** How shoppers find the seller through their store tags (views of the tag in the shop + taps on it) */
  const tagCard = (s) => (s.tags || []).length ? `<section class="dash-card"><div class="row-between"><h2 class="h4">${icon('tag', 16)} Your tags</h2><a class="link xs" href="#/dash/seller/store">Edit tags</a></div>
      <ul class="fac-list">${s.tags.map((t) => { const st = SA.services.shop.tagStats(t); return `<li class="row-between"><a class="tag-chip" href="#/shop?tag=${encodeURIComponent(t)}" target="_blank" rel="noopener">#${esc(t)}</a><span class="small num">${fmtNum(st.views)} views · ${fmtNum(st.taps)} taps</span></li>`; }).join('')}</ul></section>`
    : `<section class="dash-card"><h2 class="h4">${icon('tag', 16)} Tags</h2><p class="small soft">Add tags to your store so shoppers find you when they search or tap a tag. <a class="link" href="#/dash/seller/store">Add tags</a></p></section>`;
  SA.pages.dashSellerAnalytics = C.guard({
    roles: ['seller'],
    title: 'Analytics — Super App',
    render(ctx) {
      const s = seller(); if (!s) return noSeller('seller-analytics', 'Analytics');
      const lv = SL().levelOf(s);
      const days = +ctx.query.days === 90 ? 90 : 30;
      const x = sellerData(s, days);
      const conv = x.views ? (x.lifetimeSales / x.views) * 100 : 0;
      const head = `<div class="kpi-grid kpi-sm">
          ${C.kpi({ label: `Items sold (${days} days)`, value: fmtNum(x.sold), ic: 'shirt' })}
          ${C.kpi({ label: 'Your profit', value: money(x.profit), ic: 'wallet', tone: 'k-turf' })}
          ${C.kpi({ label: 'Design views', value: fmtNum(x.views), ic: 'eye' })}
          ${C.kpi({ label: 'Views that became a sale', value: `${conv.toFixed(1)}%`, ic: 'percent', sub: 'All time, live designs' })}
        </div>`;
      const body = `<div class="chips" role="tablist">${[[30, 'Last 30 days'], [90, 'Last 90 days']].map(([d, l]) => `<a class="chip" role="tab" href="#/dash/seller/analytics${d === 90 ? '?days=90' : ''}" aria-pressed="${days === d}" aria-selected="${days === d}">${l}</a>`).join('')}</div>
        ${head}
        <section class="dash-card"><div class="row-between"><h2 class="h4">Profit by day</h2><span class="xs muted">${money(x.profit)} · ${x.refused} refused (cost you nothing)</span></div>${profitBars(x.orders, days)}</section>
        <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Designs</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Design</th><th class="r">Views</th><th class="r">Sold (${days}d)</th><th class="r">Views → sale</th><th class="r">Revenue</th><th class="r">Your profit</th></tr></thead><tbody>
          ${x.designs.map((r) => `<tr><td><span class="row" style="--gap:10px"><span class="sl-thumb"><img src="${SA.data.media.u(`pod:${r.d.id}:${r.d.colors[0] || ''}`)}" alt="" /></span><b class="small">${esc(r.d.title)}</b></span></td><td class="r num">${fmtNum(r.views)}</td><td class="r num">${r.sold}</td><td class="r num">${(r.conv * 100).toFixed(1)}%</td><td class="r num">${money(r.revenue)}</td><td class="r num strong">${money(r.profit)}</td></tr>`).join('')}
        </tbody></table></div></section>
        ${tagCard(s)}
        ${lv.analytics === 'basic' ? (() => { const pro = SL().levels().find((l) => l.analytics !== 'basic'); return `<section class="dash-card adm-lock">${icon('lock', 26)}<h2 class="h3">More at ${esc(pro.name)}: colours, sizes, cities and best days</h2>
          <p class="soft">You already see your profit, designs and tags. At ${esc(pro.name)} you also see the colours, sizes and cities that sell and the best days to post — and export it all at Elite.</p>
          <p class="small" style="margin-top:8px">${lv.next ? `<b class="num">${fmtNum(Math.max(0, pro.sales - lv.stats.sales))}</b> more delivered items to reach ${esc(pro.name)}.` : ''}</p><a class="btn btn-outline" href="#/dash/seller" style="margin-top:14px">See your level</a></section>`; })() : `<div class="dash-grid g-1-1">
          <section class="dash-card"><h2 class="h4">Colours that sell</h2>${meters(x.colors, (v) => `${v} sold`, 'var(--blaze)')}</section>
          <section class="dash-card"><h2 class="h4">Sizes</h2>${meters(x.sizes, (v) => `${v} sold`, 'var(--info)')}</section>
          <section class="dash-card"><h2 class="h4">Cities</h2>${meters(x.cities, (v) => `${v} sold`)}</section>
          <section class="dash-card"><h2 class="h4">Best days</h2><p class="xs muted" style="margin-top:4px">Post new designs and share your link the day before the busiest days.</p>${meters(x.weekdays, (v) => `${v} sold`, 'var(--gold)')}</section>
        </div>`}`;
      const exp = lv.analytics === 'export';
      return C.layout('seller-analytics', { title: 'Analytics', subtitle: `${esc(s.name)} · ${esc(lv.name)} level${exp ? ' · export included' : ''}`, actions: exp ? `<button class="btn btn-outline btn-sm" data-export>${icon('file', 15)} Export (Excel)</button>` : `<span class="xs muted">${icon('lock', 12)} Excel export at Elite</span>`, body });
    },
    mount(el, ctx) {
      el.querySelector('[data-export]')?.addEventListener('click', () => {
        const s = seller(); const days = +ctx.query.days === 90 ? 90 : 30; const x = sellerData(s, days);
        C.xlsx(`${s.handle}-analytics-${days}d.xlsx`, [
          { name: 'Designs', columns: [{ header: 'Design', width: 30 }, { header: 'Views', width: 10, type: 'number' }, { header: `Sold (${days}d)`, width: 12, type: 'number' }, { header: 'Views to sale %', width: 14 }, { header: 'Revenue', width: 12, type: 'money' }, { header: 'Profit', width: 12, type: 'money' }],
            rows: x.designs.map((r) => [r.d.title, r.views, r.sold, (r.conv * 100).toFixed(1), r.revenue, r.profit]), totals: ['Total', x.views, x.sold, '', x.revenue, x.profit] },
          { name: 'Orders', columns: [{ header: 'Item', width: 14 }, { header: 'Date', width: 12 }, { header: 'Design', width: 28 }, { header: 'Colour', width: 14 }, { header: 'Size', width: 7 }, { header: 'City', width: 14 }, { header: 'Status', width: 11 }, { header: 'Price', width: 11, type: 'money' }, { header: 'Profit', width: 11, type: 'money' }],
            rows: x.orders.map((o) => [o.id, isoDate(new Date(o.placedAt)), o.title, o.color, o.size, o.customer?.city || '', o.status, o.price * o.qty, ['refused', 'cancelled'].includes(o.status) ? 0 : o.profit]) },
          { name: 'Breakdown', columns: [{ header: 'Type', width: 12 }, { header: 'Value', width: 20 }, { header: 'Items sold', width: 12, type: 'number' }], rows: [...x.colors.map(([k, v]) => ['Colour', k, v]), ...x.sizes.map(([k, v]) => ['Size', k, v]), ...x.cities.map(([k, v]) => ['City', k, v]), ...x.weekdays.map(([k, v]) => ['Weekday', k, v])] },
        ]);
      });
    },
  });

  // Sellers with their own store use the partner store pages too
  // (only sellers who sell their own products — print-on-demand sellers have no store to manage)
  const hasStore = (a) => a.role === 'seller' && !!SL().byAccount(a.id)?.storeId;
  ['dashStore', 'dashProductEdit', 'dashOrders', 'dashWallet', 'dashCodes'].forEach((k) => { if (SA.pages[k]) SA.pages[k].roles = hasStore; });
})(window.SA);
