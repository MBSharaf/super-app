/** Partner Dashboard — store orders: pack, ship (courier + tracking), deliver or cancel; invoices. */
(function (SA) {
  const { esc, money, fmtDate, timeAgo, isoDate } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const D = () => SA.services.dash;
  const media = SA.data.media;

  const ST = { confirmed: ['badge-info', 'New'], packed: ['badge-gold', 'Packed'], shipped: ['badge-blaze', 'On the way'], delivered: ['badge-turf', 'Delivered'], cancelled: ['badge-danger', 'Cancelled'] };
  const badge = (s) => `<span class="badge ${ST[s]?.[0] || ''}">${ST[s]?.[1] || s}</span>`;
  const NEXT = { confirmed: ['pack', 'Mark as packed', 'package'], packed: ['ship', 'Ship order', 'truck'], shipped: ['deliver', 'Mark delivered', 'home'] };
  const COURIERS = ['Bosta', 'Aramex', 'Mylerz', 'J&T Express', 'Egypt Post', 'Own delivery'];
  const STEPS = [['confirmed', 'Order placed'], ['packed', 'Packed'], ['shipped', 'Shipped'], ['delivered', 'Delivered']];
  const tm = (t) => (t ? `${fmtDate(new Date(t))}, ${new Date(t).toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' })}` : '');

  const openOrder = (key) => {
    let o;
    try { o = D().order(key); } catch (err) { return SA.ui.overlay.toast(err.message, { type: 'error' }); }
    const idx = STEPS.findIndex(([k]) => k === o.status);
    const next = NEXT[o.status];
    SA.ui.overlay.open({
      drawer: true, title: `Order ${esc(o.orderId)}`, subtitle: `${esc(o.storeName)} · ${timeAgo(o.createdAt)}`,
      body: `<div class="bk-detail">
        <div class="row-between">${badge(o.status)}<span class="badge">${esc(o.payment)}</span></div>
        ${o.status === 'cancelled' ? `<div class="notice blaze">${icon('x', 16)}<span>Cancelled${o.at.cancelled ? ` ${tm(o.at.cancelled)}` : ''}. Items went back into stock${o.payment !== 'Cash on delivery' ? ' and the customer was refunded' : ''}.</span></div>`
          : `<ol class="ord-steps">${STEPS.map(([k, l], i) => `<li class="${i <= idx ? 'is-done' : ''} ${i === idx ? 'is-current' : ''}"><span>${icon(i <= idx ? 'check' : 'clock', 12, 2.6)}</span><b class="xs">${l}</b><span class="xs muted">${i === 0 ? tm(o.createdAt) : tm(o.at[k])}</span></li>`).join('')}</ol>`}
        ${o.auto ? `<p class="xs muted">${icon('truck', 12)} Delivered by Super App logistics — status updates automatically.</p>` : ''}
        ${o.courier ? `<p class="small">${icon('truck', 14)} ${esc(o.courier)}${o.tracking ? ` · tracking <b class="num">${esc(o.tracking)}</b>` : ''}</p>` : ''}
        <h3 class="small strong">Items</h3>
        <ul class="co-items">${o.lines.map((l) => `<li><span class="media"><img src="${media.u(l.image, 120, 120)}" alt="" /><i class="num">${l.qty}</i></span><span class="grow small">${esc(l.name)}<span class="xs muted" style="display:block">${esc(l.variant?.size || '—')} · ${esc(l.variant?.color || '—')}</span></span><span class="small num">${money(l.total)}</span></li>`).join('')}</ul>
        <dl class="kv">
          <div><dt>Customer</dt><dd>${esc(o.customer?.name || '—')}</dd></div>
          <div><dt>Deliver to</dt><dd>${esc([o.address?.line1, o.address?.line2, o.address?.city || o.customer?.city].filter(Boolean).join(', ') || '—')}</dd></div>
          <div><dt>Phone</dt><dd class="num">${esc(o.address?.phone || o.customer?.phone || '—')}</dd></div>
          <div><dt>Delivery</dt><dd>${o.delivery === 'express' ? 'Express (next day)' : 'Standard'}</dd></div>
        </dl>
        <h3 class="small strong" style="margin-top:14px">Money</h3>
        <dl class="kv"><div><dt>Order value</dt><dd class="num">${money(o.gross)}</dd></div><div><dt>Super App commission</dt><dd class="num">−${money(o.commission)}</dd></div><div class="total"><dt>Your net</dt><dd class="num">${money(o.net)}</dd></div></dl>
        <p class="xs muted">${o.status === 'delivered' ? 'In your store wallet.' : o.status === 'cancelled' ? '' : 'Reaches your store wallet when it’s delivered.'}</p>
        <form class="ship-form" data-ship hidden novalidate>
          <h3 class="small strong">Shipping details</h3>
          <div class="field"><label for="sh-c">Courier</label><select class="select" id="sh-c" name="courier"><option value="">Choose…</option>${COURIERS.map((x) => `<option>${x}</option>`).join('')}</select><span class="field-error" data-err="courier"></span></div>
          <div class="field"><label for="sh-t">Tracking number</label><input class="input num" id="sh-t" name="tracking" placeholder="From the courier’s receipt" /><span class="field-error" data-err="tracking"></span></div>
          <button class="btn btn-blaze btn-block" type="submit">${icon('truck', 16)} Confirm shipment</button>
        </form>
      </div>`,
      footer: `<div class="row wrap" style="width:100%;--gap:8px">
        <button class="btn btn-outline btn-sm" data-doc>${icon('file', 14)} Invoice & packing slip</button>
        ${next ? `<button class="btn btn-primary btn-sm" data-next="${next[0]}" style="margin-left:auto">${icon(next[2], 14)} ${next[1]}</button>` : ''}
        ${['confirmed', 'packed'].includes(o.status) ? `<button class="btn btn-ghost btn-sm" data-cancel style="color:var(--danger)">Cancel order</button>` : ''}</div>`,
      onMount(m, close) {
        const done = (msg) => { close(); SA.ui.overlay.toast(msg, { type: 'success' }); SA.app.rerender(); };
        m.querySelector('[data-doc]').addEventListener('click', async (e) => { e.currentTarget.disabled = true; await SA.ui.docs.storeOrderDoc(o); e.currentTarget.disabled = false; });
        m.querySelector('[data-next]')?.addEventListener('click', async (e) => {
          const act = e.currentTarget.dataset.next;
          if (act === 'ship') { const f = m.querySelector('[data-ship]'); f.hidden = false; f.courier.focus(); f.scrollIntoView({ behavior: 'smooth', block: 'center' }); return; }
          try { await D().orderAction(key, act); done(act === 'pack' ? 'Marked as packed' : 'Marked as delivered — money moved to your store wallet'); } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
        });
        const f = m.querySelector('[data-ship]');
        f.courier.addEventListener('change', () => { f.tracking.placeholder = f.courier.value === 'Own delivery' ? 'Optional for your own driver' : 'From the courier’s receipt'; });
        f.addEventListener('submit', async (e) => {
          e.preventDefault();
          try { await D().orderAction(key, 'ship', { courier: f.courier.value, tracking: f.tracking.value }); done('Shipped — the customer has the tracking details'); }
          catch (err) { if (err.details) C.showErrors(f, err.details); SA.ui.overlay.toast(err.message, { type: 'error' }); }
        });
        m.querySelector('[data-cancel]')?.addEventListener('click', async () => {
          if (!(await SA.ui.overlay.confirm({ title: 'Cancel this order?', text: `The ${o.units} item${o.units === 1 ? '' : 's'} go back into stock${o.payment !== 'Cash on delivery' ? ` and ${esc(o.customer?.name || 'the customer')} gets ${money(o.gross)} back` : ''}.`, confirmLabel: 'Cancel order', danger: true }))) return;
          try { await D().orderAction(key, 'cancel'); done('Order cancelled'); } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
        });
      },
    });
  };

  SA.pages.dashOrders = C.guard({
    title: 'Orders — Super App Partner',
    openOrder,
    render(ctx) {
      const q = ctx.query;
      const sc = C.scope();
      const stores = D().myStores(sc);
      if (!stores.length) return C.layout('orders', { title: 'Orders', body: `<div class="dash-card">${SA.ui.c.empty({ icon: 'package', title: 'No store yet', text: 'Open your store and add products — orders from the Super App shop arrive here.', action: '<a class="btn btn-primary" href="#/dash/store">Open my store</a>' })}</div>` });
      const r = C.range(q, '30');
      const all = D().orders({ ...sc, from: r.from, to: r.to });
      const status = q.status || 'open';
      const F = { open: (o) => ['confirmed', 'packed'].includes(o.status), shipped: (o) => o.status === 'shipped', delivered: (o) => o.status === 'delivered', cancelled: (o) => o.status === 'cancelled', all: () => true };
      const list = all.filter(F[status] || F.open);
      const n = (k) => all.filter(F[k]).length;
      const body = `
        ${C.rangePicker(r, '/dash/orders')}
        <div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'To pack', value: all.filter((o) => o.status === 'confirmed').length, count: all.filter((o) => o.status === 'confirmed').length, ic: 'package', tone: all.some((o) => o.status === 'confirmed') ? 'k-blaze' : '' })}
          ${C.kpi({ label: 'Ready to ship', value: all.filter((o) => o.status === 'packed').length, ic: 'truck' })}
          ${C.kpi({ label: 'On the way', value: n('shipped'), ic: 'truck' })}
          ${C.kpi({ label: 'Delivered — net', value: money(Math.round(all.filter(F.delivered).reduce((t, o) => t + o.net, 0))), ic: 'wallet', tone: 'k-turf', sub: `${n('delivered')} orders` })}
        </div>
        <div class="tabs" role="tablist">${[['open', 'To send'], ['shipped', 'On the way'], ['delivered', 'Delivered'], ['cancelled', 'Cancelled'], ['all', 'All']].map(([v, l]) => `<a class="tab" role="tab" href="${SA.core.router.href('/dash/orders', { ...q, status: v })}" aria-selected="${status === v}">${l} <span class="tab-n">${n(v)}</span></a>`).join('')}</div>
        <section class="dash-card table-card"><div class="table-wrap"><table class="table dash-table">
          <thead><tr><th>Order</th><th>Customer</th><th>Items</th><th class="r">Total</th><th>Payment</th><th>Status</th><th></th></tr></thead>
          <tbody>${list.map((o) => `<tr class="clickable" data-order="${esc(o.key)}" tabindex="0">
            <td><b class="num small">${esc(o.orderId)}</b><span class="xs muted" style="display:block">${timeAgo(o.createdAt)}${stores.length > 1 ? ` · ${esc(o.storeName)}` : ''}</span></td>
            <td>${esc(o.customer?.name || '—')}<span class="xs muted" style="display:block">${esc(o.address?.city || o.customer?.city || '')}${o.delivery === 'express' ? ' · <b style="color:var(--blaze)">Express</b>' : ''}</span></td>
            <td><span class="ord-thumbs">${o.lines.slice(0, 3).map((l) => `<img src="${media.u(l.image, 80, 80)}" alt="" loading="lazy" />`).join('')}</span><span class="xs muted">${o.units} item${o.units === 1 ? '' : 's'}</span></td>
            <td class="r num strong">${money(o.gross)}</td><td class="small">${esc(o.payment)}</td><td>${badge(o.status)}</td>
            <td class="r">${NEXT[o.status] && !o.auto ? `<button class="btn btn-outline btn-sm" data-quick="${NEXT[o.status][0]}">${NEXT[o.status][1]}</button>` : ''}</td></tr>`).join('') || `<tr><td colspan="7">${SA.ui.c.empty({ icon: 'package', title: status === 'open' ? 'Nothing to send' : 'No orders here', text: status === 'open' ? 'New orders from the Super App shop appear here.' : 'Try a wider date range.' })}</td></tr>`}</tbody>
        </table></div></section>`;
      return C.layout('orders', { title: 'Orders', subtitle: `${all.length} order${all.length === 1 ? '' : 's'} · ${C.rangeLabel(r)}`, body });
    },
    mount(el, ctx) {
      C.bindRange(el, '/dash/orders', { status: ctx.query.status });
      el.addEventListener('click', async (e) => {
        const row = e.target.closest('[data-order]'); if (!row) return;
        const quick = e.target.closest('[data-quick]');
        if (quick && quick.dataset.quick !== 'ship') {
          e.stopPropagation();
          try { await D().orderAction(row.dataset.order, quick.dataset.quick); SA.ui.overlay.toast(quick.dataset.quick === 'pack' ? 'Marked as packed' : 'Marked as delivered', { type: 'success' }); SA.app.rerender(); } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
          return;
        }
        openOrder(row.dataset.order);
        if (quick) setTimeout(() => document.querySelector('[data-next="ship"]')?.click(), 350);
      });
      el.addEventListener('keydown', (e) => { const row = e.target.closest('[data-order]'); if (row && e.key === 'Enter') openOrder(row.dataset.order); });
    },
  });
})(window.SA);
