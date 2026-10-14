/**
 * Print partner (and admin): the print queue — download print files, move items through
 * Received → Printing → Shipped → Delivered, record refused deliveries and reprint faulty items.
 */
(function (SA) {
  const { esc, money, timeAgo, fmtNum } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const D = () => SA.services.dash;
  const P = () => SA.services.pod;
  const DAY = 864e5;

  const TABS = [['todo', 'To print'], ['shipped', 'On the way'], ['done', 'Delivered'], ['refused', 'Refused']];
  const TAB_STATUS = { todo: ['received', 'printing'], shipped: ['shipped'], done: ['delivered'], refused: ['refused', 'cancelled'] };
  const sampleTag = (o) => (o.sample ? ' <span class="badge badge-gold">Sample → seller</span>' : '');
  const BADGE = { received: ['badge-blaze', 'New'], printing: ['badge-gold', 'Printing'], shipped: ['badge-info', 'Shipped'], delivered: ['badge-turf', 'Delivered'], refused: ['badge-danger', 'Refused'], cancelled: ['badge-danger', 'Cancelled'] };
  const COURIERS = ['Bosta', 'Aramex', 'Mylerz', 'Egypt Post'];

  const sideMock = (d, o, side) => {
    const col = d.product?.colors.find((k) => k.name === o.color) || d.product?.colors[0];
    return SA.ui.garmentView({ kind: d.product?.kind, hex: col?.hex, art: d.src, place: d.placement?.[side], side, artRatio: d.artW ? d.artH / d.artW : 1, label: `${d.title} ${side}` });
  };

  const row = (o, admin) => {
    const d = P().get(o.designId);
    const [cls, label] = BADGE[o.status] || ['badge', o.status];
    const sides = d ? ['front', 'back'].filter((x) => d.placement?.[x]) : [];
    const next = { received: ['printing', 'Start printing', 'bolt'], printing: ['shipped', 'Mark shipped', 'truck'], shipped: ['delivered', 'Delivered', 'check'] }[o.status];
    return `<article class="dash-card pr-item" data-po="${o.id}">
      <div class="pr-media">${d ? sides.map((x) => `<div class="pr-mock">${sideMock(d, o, x)}<span class="xs muted">${x === 'front' ? 'Front' : 'Back'}</span></div>`).join('') : '<span class="xs muted">Design removed</span>'}</div>
      <div class="pr-body">
        <div class="row-between" style="--gap:8px"><b class="num">${esc(o.id)}${sampleTag(o)}</b><span class="badge ${cls}">${label}</span></div>
        <p class="small strong">${o.qty} × ${esc(d?.product?.name || o.catalogId)} · ${esc(o.color)} · ${esc(o.size)}</p>
        <p class="xs muted">“${esc(o.title)}” · order ${esc(o.orderId)} · ${timeAgo(o.placedAt)} · ${esc(o.customer.city || '')}${o.reprints ? ` · reprinted ${o.reprints}×` : ''}</p>
        ${o.tracking ? `<p class="xs">${icon('truck', 12)} ${esc(o.courier)} · <span class="num">${esc(o.tracking)}</span></p>` : ''}
        <p class="xs muted">${admin ? `Customer paid ${money(o.price * o.qty)} · partner cost ${money(o.partnerCost * o.qty)} · seller earns ${money(o.profit)}` : `Production price ${money(o.partnerCost * o.qty)}`}${o.loss ? ` · <span class="text-danger">loss ${money(o.loss)}</span>` : ''}</p>
        <div class="pr-actions">
          ${d ? `<button class="btn btn-ghost btn-sm" data-file="${o.id}">${icon('file', 13)} Print file</button>` : ''}
          ${next ? `<button class="btn btn-primary btn-sm" data-next="${o.id}" data-to="${next[0]}">${icon(next[2], 13)} ${next[1]}</button>` : ''}
          ${o.status === 'shipped' ? `<button class="btn btn-outline btn-sm" data-refused="${o.id}" style="color:var(--danger)">Refused</button>` : ''}
          ${['shipped', 'delivered'].includes(o.status) ? `<button class="btn btn-ghost btn-sm" data-reprint="${o.id}">${icon('refresh', 13)} Reprint (fault)</button>` : ''}
        </div>
      </div></article>`;
  };

  /** Print file: the artwork plus a JSON job sheet with exact placement */
  const download = (o) => {
    const d = P().get(o.designId);
    const job = { order: o.id, product: d.product.name, colour: o.color, size: o.size, qty: o.qty,
      placement: Object.fromEntries(['front', 'back'].filter((x) => d.placement[x]).map((x) => [x, { centreX: d.placement[x].x, centreY: d.placement[x].y, width: d.placement[x].w, unit: 'fraction of the 400×440 garment template' }])),
      artwork: { width: d.artW || 'vector', height: d.artH || 'vector' } };
    const ext = /^data:image\/(\w+)/.exec(d.src)?.[1]?.replace('svg+xml', 'svg') || 'png';
    const a = document.createElement('a'); a.href = d.src; a.download = `${o.id}-artwork.${ext}`; a.click();
    const b = document.createElement('a'); b.href = URL.createObjectURL(new Blob([JSON.stringify(job, null, 2)], { type: 'application/json' })); b.download = `${o.id}-job.json`; b.click();
    setTimeout(() => URL.revokeObjectURL(b.href), 2000);
  };

  SA.pages.dashPrint = C.guard({
    roles: ['printer', 'admin'],
    title: 'Print queue — Super App',
    render(ctx) {
      const me = D().me();
      if (!['printer', 'admin'].includes(me.role)) return C.layout('print', { title: 'Print queue', body: `<div class="dash-card">${SA.ui.c.empty({ icon: 'lock', title: 'Print partner only', text: 'This page is for the print partner and the Super App team.' })}</div>` });
      const admin = me.role === 'admin';
      const tab = TAB_STATUS[ctx.query.tab] ? ctx.query.tab : 'todo';
      const all = P().orders();
      const since = Date.now() - 30 * DAY;
      const list = all.filter((o) => TAB_STATUS[tab].includes(o.status)).sort((a, b) => (tab === 'todo' ? a.placedAt - b.placedAt : b.placedAt - a.placedAt));
      const recent = all.filter((o) => o.placedAt >= since);
      const cnt = (st) => all.filter((o) => TAB_STATUS[st].includes(o.status)).length;
      const lost = recent.filter((o) => o.status === 'refused').reduce((t, o) => t + (o.loss || 0), 0);
      const body = `
        <div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'New', value: fmtNum(all.filter((o) => o.status === 'received').length), ic: 'package', tone: 'k-blaze' })}
          ${C.kpi({ label: 'Printing', value: fmtNum(all.filter((o) => o.status === 'printing').length), ic: 'bolt' })}
          ${C.kpi({ label: 'Delivered (30 days)', value: fmtNum(recent.filter((o) => o.status === 'delivered').length), ic: 'check', tone: 'k-turf' })}
          ${C.kpi({ label: admin ? 'Refused-delivery loss (30 days)' : 'Refused (30 days)', value: admin ? money(lost) : fmtNum(recent.filter((o) => o.status === 'refused').length), ic: 'alert' })}
        </div>
        <div class="row-between wrap" style="--gap:10px">
          <div class="chips" role="tablist" aria-label="Print queue">${TABS.map(([k, l]) => `<a class="chip" role="tab" href="#/dash/print?tab=${k}" aria-pressed="${tab === k}" aria-selected="${tab === k}">${l} <span class="chip-count">${cnt(k)}</span></a>`).join('')}</div>
          ${tab === 'todo' && list.some((o) => o.status === 'received') ? `<button class="btn btn-outline btn-sm" data-start-all>${icon('bolt', 14)} Start all new</button>` : ''}
        </div>
        ${list.length ? `<div class="pr-list">${list.map((o) => row(o, admin)).join('')}</div>` : `<div class="dash-card">${SA.ui.c.empty({ icon: 'check', title: tab === 'todo' ? 'Nothing to print right now' : 'Nothing here', text: 'New print-on-demand orders appear here as soon as customers pay.' })}</div>`}`;
      return C.layout('print', { title: admin ? 'Print orders' : 'Print queue', subtitle: admin ? 'Every print-on-demand item made by the print partner' : 'Partner Print House · orders from Super App', body });
    },
    mount(el) {
      const go = (fn, msg) => { try { fn(); if (msg) SA.ui.overlay.toast(msg); SA.app.rerender(); } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); } };
      el.addEventListener('click', async (e) => {
        const f = e.target.closest('[data-file]'); if (f) return download(P().orders().find((o) => o.id === f.dataset.file));
        if (e.target.closest('[data-start-all]')) { const ids = P().orders({ status: 'received' }).map((o) => o.id); return go(() => ids.forEach((id) => P().advance(id, 'printing')), `${ids.length} item${ids.length > 1 ? 's' : ''} moved to printing`); }
        const n = e.target.closest('[data-next]');
        if (n) {
          const id = n.dataset.next;
          if (n.dataset.to !== 'shipped') return go(() => P().advance(id, n.dataset.to), n.dataset.to === 'printing' ? 'Moved to printing' : 'Marked delivered — the seller’s profit is on hold for 7 days');
          return SA.ui.overlay.open({
            title: `Ship ${esc(id)}`, subtitle: 'The customer gets the tracking number straight away.',
            body: `<form id="sh-form" class="form-grid" novalidate><div class="field"><label for="sh-c">Courier</label><select class="select" id="sh-c" name="courier">${COURIERS.map((x) => `<option>${x}</option>`).join('')}</select></div>
              <div class="field"><label for="sh-t">Tracking number</label><input class="input" id="sh-t" name="tracking" maxlength="40" autocomplete="off" /></div><p class="field-error span-2" data-err></p></form>`,
            footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-primary" form="sh-form" type="submit">${icon('truck', 15)} Mark shipped</button>`,
            onMount(m, close) {
              m.querySelector('#sh-form').addEventListener('submit', (ev) => {
                ev.preventDefault();
                const d = SA.core.utils.formData(ev.target);
                try { P().advance(id, 'shipped', { courier: d.courier, tracking: d.tracking.trim() }); close(); SA.ui.overlay.toast('Marked shipped'); SA.app.rerender(); }
                catch (err) { m.querySelector('[data-err]').textContent = err.message; }
              });
            },
          });
        }
        const r = e.target.closest('[data-refused]');
        if (r && await SA.ui.overlay.confirm({ title: 'Customer refused the delivery?', text: 'The item comes back to you. Super App covers the production and shipping cost; the seller earns nothing on it. Customers who refuse twice must pay online next time.', confirmLabel: 'Record refusal', danger: true })) {
          return go(() => P().advance(r.dataset.refused, 'refused'), 'Refusal recorded');
        }
        const rp = e.target.closest('[data-reprint]');
        if (rp && await SA.ui.overlay.confirm({ title: 'Reprint this item?', text: 'Use this for print faults (wrong colour, misaligned or damaged print). The new item is made at the print partner’s cost; the order, the price and the seller’s profit stay the same.', confirmLabel: 'Reprint' })) {
          return go(() => P().reprint(rp.dataset.reprint, 'Print fault'), 'Reprint logged');
        }
      });
    },
  });
})(window.SA);
