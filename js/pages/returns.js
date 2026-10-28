/**
 * Returns & exchanges UI: the customer's "Return or exchange" on the order page, the seller's returns page
 * (#/seller/returns) and Super App's (#/admin/returns, the official store and printed designs).
 */
(function (SA) {
  const { esc, money, timeAgo, fmtDate } = SA.core.utils;
  const { icon } = SA.ui;
  const RT = () => SA.services.returns;
  const media = SA.data.media;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });
  const TONE = { requested: 'badge-gold', approved: 'badge-info', received: 'badge-info', reprinting: 'badge-info', refunded: 'badge-turf', exchanged: 'badge-turf', rejected: 'badge-danger' };
  const ACT = { refund: 'Refund', exchange: 'Exchange', reprint: 'Reprint' };
  const img = (src) => (String(src).startsWith('pod:') ? '' : `<img src="${media.u(src, 96, 96)}" alt="" loading="lazy" />`);

  /* ---------- Customer: on the order page ---------- */
  const orderBlock = (o) => {
    const mine = RT().forOrder(o.id);
    const can = RT().eligible(o).filter((x) => x.ok);
    if (!mine.length && !can.length) return '';
    return `<section class="panel panel-pad rt-block">
      <div class="row-between wrap"><h2 class="h4">${icon('refresh', 18)} Returns & exchanges</h2>${can.length ? `<button class="btn btn-outline btn-sm" data-rt-new>${icon('refresh', 14)} Return or exchange</button>` : ''}</div>
      ${can.length ? `<p class="xs muted" style="margin-top:4px">Within ${RT().windowText()} of delivery — ${RT().leftText(can)}. <a class="link" href="#/legal/refunds">Refunds policy</a></p>` : ''}
      ${mine.map((r) => `<div class="rt-row"><div class="row-between wrap"><b class="small">${r.id} · ${esc(r.storeName)}</b><span class="badge ${TONE[r.status]}">${RT().STATUS[r.status]}</span></div>
        <span class="xs muted">${r.items.map((i) => `${i.qty}× ${esc(i.name)} — ${ACT[i.action]}${i.size ? ` (${esc(i.size)})` : ''}`).join(' · ')} · ${esc(r.reasonLabel)}${r.amount ? ` · ${money(r.amount)} back` : ''}${r.awb ? ` · ${esc(r.courier)} <b class="num">${esc(r.awb)}</b>` : ''}</span>
        ${r.reason2 ? `<span class="xs t-danger" style="display:block">${esc(r.reason2)}</span>` : ''}</div>`).join('')}
    </section>`;
  };
  const openForm = (o) => {
    const can = RT().eligible(o).filter((x) => x.ok);
    const R = RT().REASONS;
    SA.ui.overlay.open({
      label: 'Return or exchange', title: 'Return or exchange', subtitle: `Order ${o.id}`,
      body: `<form class="stack" style="--gap:12px" data-rt-form novalidate>
        <div class="field"><label for="rt-r">Why?</label><select class="select" id="rt-r" name="reason"><option value="">Choose…</option>${Object.entries(R).map(([k, v]) => `<option value="${k}">${v.label}${v.free ? '' : ` (return delivery ${money(SA.data.products.shipping?.standard ?? 60)})`}</option>`).join('')}</select><p class="field-error" data-err="reason"></p></div>
        <div class="rt-items">${can.map((x) => { const p = SA.services.db.find('products', x.line.productId) || (() => { try { return SA.services.shop.getSync(x.line.productId); } catch { return null; } })(); return `<div class="rt-item" data-idx="${x.index}">
          <label class="choice"><input type="checkbox" name="pick" value="${x.index}" /><span class="radio-dot" style="border-radius:6px"></span><span class="grow small"><b>${esc(x.line.name)}</b><span class="xs muted" style="display:block">${esc(x.line.variant.size)} · ${esc(x.line.variant.color)} · ${money(x.line.price)}${x.pod ? ' · printed for you' : ''}</span></span></label>
          <div class="rt-opts" hidden>
            ${x.qty > 1 ? `<label class="xs">Quantity <input class="input num" type="number" name="qty-${x.index}" min="1" max="${x.qty}" value="1" style="width:72px" /></label>` : `<input type="hidden" name="qty-${x.index}" value="1" />`}
            <select class="select" name="act-${x.index}" aria-label="What do you want?">${x.pod ? '<option value="reprint">Reprint it for me</option><option value="refund">Refund me</option>' : `<option value="refund">Refund me</option>${x.exchange !== false && p && (p.sizes || []).length > 1 ? '<option value="exchange">Exchange for another size</option>' : ''}`}</select>
            ${!x.pod && p && (p.sizes || []).length > 1 ? `<select class="select" name="size-${x.index}" aria-label="New size" hidden>${p.sizes.map((s) => `<option ${s === x.line.variant.size ? 'selected' : ''}>${esc(s)}</option>`).join('')}</select>` : ''}
          </div></div>`; }).join('')}</div>
        <p class="field-error" data-err="items"></p>
        <div class="field"><label for="rt-n">Details <span class="muted">(optional)</span></label><textarea class="input" id="rt-n" name="note" rows="2" maxlength="400" placeholder="${SA.config.podEnabled ? 'e.g. The print peeled after the first wash' : 'e.g. The seam came apart on the first day'}"></textarea></div>
        <p class="xs muted">${icon('info', 12)} ${SA.config.podEnabled || !SA.services.api.isLive() ? 'Printed designs are made for you: they’re reprinted or refunded only for a fault, damage, a wrong size or a wrong print.' : 'Returns are for an item with a problem, within 24 hours of delivery. Our courier collects it and the refund goes to your Super App wallet.'}</p>
        <button class="btn btn-primary" type="submit">Send the request</button></form>`,
      onMount(m, close) {
        const f = m.querySelector('[data-rt-form]');
        f.addEventListener('change', (e) => {
          if (e.target.name === 'pick') e.target.closest('.rt-item').querySelector('.rt-opts').hidden = !e.target.checked;
          if (e.target.name?.startsWith('act-')) { const s = f.querySelector(`[name="size-${e.target.name.slice(4)}"]`); if (s) s.hidden = e.target.value !== 'exchange'; }
        });
        f.addEventListener('submit', async (e) => {
          e.preventDefault();
          f.querySelectorAll('[data-err]').forEach((x) => (x.textContent = ''));
          const items = [...f.querySelectorAll('[name="pick"]:checked')].map((b) => ({ index: +b.value, qty: +f[`qty-${b.value}`].value, action: f[`act-${b.value}`].value, size: f[`size-${b.value}`]?.value || '' }));
          try { const out = await RT().request(o.id, { items, reason: f.reason.value, note: f.note.value }); close(); toast(`Return request ${out.map((r) => r.id).join(', ')} sent`); SA.app.rerender(); }
          catch (err) { const d = err.details || {}; Object.entries(d).forEach(([k, v]) => { const n = f.querySelector(`[data-err="${k === 'size' ? 'items' : k}"]`); if (n) n.textContent = v; }); if (!Object.keys(d).length) f.querySelector('[data-err="items"]').textContent = err.message; }
        });
      },
    });
  };
  SA.ui.returns = { orderBlock, openForm };

  /* ---------- Seller and Super App: the returns list ---------- */
  const card = (r, { admin = false } = {}) => `<article class="dash-card rt-card">
    <div class="row-between wrap" style="align-items:flex-start;gap:8px"><span><span class="xs muted">${r.id} · ${esc(r.orderId)} · ${timeAgo(r.createdAt)}${admin ? ` · ${esc(r.storeName)}` : ''}</span><b class="h4" style="display:block">${esc(r.customer.name)} <span class="xs muted">· ${esc(r.customer.city || '')}</span></b></span><span class="badge ${TONE[r.status]}">${RT().STATUS[r.status]}</span></div>
    <ul class="rt-lines">${r.items.map((i) => `<li>${img(i.image)}<span class="grow small"><b>${i.qty}× ${esc(i.name)}</b><span class="xs muted" style="display:block">${esc(i.variant.size)} · ${esc(i.variant.color)} → <b>${ACT[i.action]}</b>${i.size ? ` (${esc(i.size)})` : ''}</span></span><span class="num small">${money(i.price * i.qty)}</span></li>`).join('')}</ul>
    <p class="small"><b>${esc(r.reasonLabel)}</b>${r.note ? ` — “${esc(r.note)}”` : ''}</p>
    <p class="xs muted">${r.amount ? `Refund ${money(r.amount)}${r.fee ? ` (after ${money(r.fee)} return delivery)` : ''}` : 'No refund — exchange or reprint'}${r.awb ? ` · ${esc(r.courier)} pickup <b class="num">${esc(r.awb)}</b>` : ''}</p>
    ${r.reason2 ? `<p class="xs t-danger">${esc(r.reason2)}</p>` : ''}
    <details class="xs muted" style="margin-top:6px"><summary>History</summary><ul>${r.history.map((h) => `<li>${fmtDate(new Date(h.at))} · ${esc(RT().STATUS[h.status] || h.status)} · ${esc(h.by)}${h.note ? ` — ${esc(h.note)}` : ''}</li>`).join('')}</ul></details>
    ${r.status === 'requested' ? `<div class="row" style="--gap:8px;margin-top:10px;flex-wrap:wrap"><button class="btn btn-primary btn-sm" data-rt-ok="${r.id}">${icon('check', 14)} ${r.kind === 'pod' ? 'Accept' : 'Accept & book pickup'}</button><button class="btn btn-ghost btn-sm" data-rt-no="${r.id}">Refuse</button></div>`
      : r.status === 'approved' ? `<div class="row" style="--gap:8px;margin-top:10px"><button class="btn btn-primary btn-sm" data-rt-in="${r.id}">${icon('package', 14)} Item received — ${r.amount ? `refund ${money(r.amount)}` : 'send the replacement'}</button></div>` : ''}
  </article>`;
  const bind = (el) => {
    el.querySelectorAll('[data-rt-ok]').forEach((b) => b.addEventListener('click', async () => { try { await RT().decide(b.dataset.rtOk, true); toast('Accepted — the customer was told'); } catch (e) { toast(e.message, 'error'); } SA.app.rerender(); }));
    el.querySelectorAll('[data-rt-no]').forEach((b) => b.addEventListener('click', async () => {
      const r = await SA.pages.adminCommon.reason({ title: 'Refuse this return?', text: 'The customer sees your reason and can ask Super App to look at it.', confirmLabel: 'Refuse', danger: true, placeholder: 'e.g. The item was worn — the tag is missing' });
      if (!r) return;
      try { await RT().decide(b.dataset.rtNo, false, r.reason); toast('Refused'); } catch (e) { toast(e.message, 'error'); } SA.app.rerender();
    }));
    el.querySelectorAll('[data-rt-in]').forEach((b) => b.addEventListener('click', async () => { try { await RT().receive(b.dataset.rtIn); toast('Done — the customer was told'); } catch (e) { toast(e.message, 'error'); } SA.app.rerender(); }));
  };
  const page = ({ admin }) => ({
    render(ctx) {
      const C = SA.pages.dashCommon;
      const tab = ctx.query.tab || 'open';
      const me = SA.services.dash.me();
      const base = admin ? '/admin/returns' : '/seller/returns';
      const mine = RT().list(admin ? {} : { ownerId: me.id });
      const list = mine.filter((r) => (tab === 'open' ? RT().OPEN.includes(r.status) : tab === 'all' ? true : r.status === tab));
      const body = `${admin ? `<p class="small muted" style="margin-bottom:10px">${icon('info', 12)} Super App decides returns for the official store and printed designs; you can also step in on any seller’s return.</p>` : ''}
        ${SA.pages.adminCommon.tabs(base, [['open', 'To handle', mine.filter((r) => RT().OPEN.includes(r.status)).length], ['refunded', 'Refunded'], ['exchanged', 'Exchanged'], ['rejected', 'Refused'], ['all', 'All']], tab)}
        ${list.length ? `<div class="rt-grid">${list.map((r) => card(r, { admin })).join('')}</div>` : `<div class="dash-card">${SA.ui.c.empty({ icon: 'refresh', title: 'No returns here', text: `Customers can return store products within ${RT().WINDOW} days of delivery; printed designs only for a fault.` })}</div>`}`;
      return C.layout(admin ? 'returns' : 'seller-returns', { title: 'Returns & exchanges', subtitle: `Answer within 2 working days · ${RT().WINDOW}-day return window`, body });
    },
    mount(el) { bind(el); },
  });
  SA.pages.adminReturns = SA.pages.adminCommon.guard({ title: 'Returns — Super App Admin', ...page({ admin: true }) });
  SA.pages.sellerReturns = SA.pages.dashCommon.guard({ title: 'Returns — Super App Seller', roles: ['seller'], ...page({ admin: false }) });
})(window.SA);
