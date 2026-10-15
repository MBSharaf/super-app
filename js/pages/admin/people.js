/** Admin console: customers, partners (pitch owners, stores, print partner) and sellers — with a details drawer and actions. */
(function (SA) {
  const { esc, money, timeAgo, fmtDate, fmtHour } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const A = SA.pages.adminCommon;
  const AD = () => SA.services.admin;
  const db = () => SA.services.db;
  const SL = () => SA.services.sellers;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });
  const ago = (t) => (t ? timeAgo(t) : '—');

  /**
   * Details drawer: render(id) → html; onClick / onChange return true when something changed.
   * The drawer repaints in place; the page behind refreshes when the drawer closes (a re-render would close it).
   */
  const drawer = ({ title, render, onClick, onChange, onSubmit, id }) => {
    let dirty = false, host = null;
    const paint = () => { if (host) host.innerHTML = render(id); };
    return SA.ui.overlay.open({
      drawer: true, side: 'right', label: title,
      body: `<div class="adm-drawer">${render(id)}</div>`,
      onMount(m, close) {
        host = m.querySelector('.adm-drawer');
        m.addEventListener('click', async (e) => {
          if (e.target.closest('[data-x]')) { close(); return; }
          if (await onClick(e, id, close)) { dirty = true; paint(); }
        });
        m.addEventListener('change', async (e) => { if (onChange && (await onChange(e, id))) { dirty = true; paint(); } });
        m.addEventListener('submit', async (e) => { if (!onSubmit) return; e.preventDefault(); if (await onSubmit(e, id)) { dirty = true; paint(); } });
      },
      onClose() { if (dirty) SA.app.rerender(); },
    });
  };
  const head = (title, sub, badges = '') => `<div class="adm-dh"><div class="grow"><h2 class="h3">${title}</h2><p class="small muted">${sub}</p>${badges ? `<div class="row wrap" style="--gap:6px;margin-top:8px">${badges}</div>` : ''}</div><button class="icon-btn" data-x aria-label="Close">${icon('x')}</button></div>`;
  const stat = (n, l) => `<div><b class="h4 num">${n}</b><span class="xs muted">${l}</span></div>`;
  const filterBar = (base, q, chips) => `<form class="adm-filters" data-search><label class="field grow"><span class="field-label">Search</span><input class="input" name="q" value="${esc(q.q || '')}" placeholder="Name, phone, city, ID…" /></label><button class="btn btn-primary" type="submit" style="align-self:end">Search</button></form>
    ${A.tabs(base, chips, q.tab || '', { q: q.q || undefined })}`;
  const bindSearch = (el, base, q) => el.querySelector('[data-search]')?.addEventListener('submit', (e) => { e.preventDefault(); SA.core.router.go(SA.core.router.href(base, { ...q, q: e.target.q.value.trim() || undefined }).slice(1)); });
  const match = (q, ...fields) => !q || fields.some((f) => String(f || '').toLowerCase().includes(q.toLowerCase()));

  /* ======================= Customers ======================= */
  const PREPAY = { auto: 'Automatic (after refusals)', always: 'Always pay upfront', never: 'Never required' };
  const custBadges = (c) => [c.device ? '<span class="badge badge-info">This device</span>' : '', c.blocked ? '<span class="badge badge-danger">Blocked</span>' : '', c.needsPrepay ? '<span class="badge badge-gold">Pays upfront</span>' : '', c.refused ? `<span class="badge">${c.refused} refused</span>` : ''].filter(Boolean).join(' ');

  const customerDrawer = (id) => {
    const c = AD().customer(id);
    return `${head(esc(c.name), `${esc(c.city || '—')}${c.phone ? ` · ${esc(c.phone)}` : ''}${c.email ? ` · ${esc(c.email)}` : ''}`, custBadges(c))}
      <div class="adm-stats">${stat(c.bookings, 'Bookings')}${stat(c.orders, 'Shop orders')}${stat(c.items, 'Printed items')}${stat(money(Math.round(c.spent)), 'Spent')}</div>
      ${c.device ? (() => { const L = SA.services.loyalty.summary(); return `<p class="small" style="margin-top:10px">${icon('sparkle', 14)} <b class="num">${L.balance.toLocaleString()}</b> Super Points · ${esc(L.tier.name)} tier</p>`; })() : ''}
      ${c.blocked ? `<div class="adm-alert danger">${icon('ban', 16)}<span><b>Blocked</b> ${esc(c.blockedReason)}</span></div>` : ''}
      <section class="adm-ds"><h3 class="h4">Actions</h3>
        <div class="adm-actions">
          ${c.blocked ? `<button class="btn btn-outline btn-sm" data-act="unblock">${icon('check', 14, 2.6)} Unblock</button>` : `<button class="btn btn-outline btn-sm" data-act="block" style="color:var(--danger)">${icon('ban', 14)} Block</button>`}
          <button class="btn btn-outline btn-sm" data-act="credit">${icon('wallet', 14)} Wallet credit</button>
          <button class="btn btn-outline btn-sm" data-act="points">${icon('sparkle', 14)} Super Points</button>
          <button class="btn btn-outline btn-sm" data-act="note">${icon('edit', 14)} Add note</button>
          ${c.refused ? `<button class="btn btn-ghost btn-sm" data-act="reset">${icon('refresh', 14)} Reset ${c.refused} refusal${c.refused > 1 ? 's' : ''}</button>` : ''}
        </div>
        <label class="field" style="margin-top:12px"><span class="field-label">Pay upfront for printed items</span><select class="select" data-prepay>${Object.entries(PREPAY).map(([k, l]) => `<option value="${k}" ${c.prepay === k ? 'selected' : ''}>${l}</option>`).join('')}</select>
          <span class="field-hint">Automatic: after ${c.limit} refused deliveries. Now: ${c.needsPrepay ? 'card or wallet only' : 'cash on delivery allowed'}.</span></label>
      </section>
      ${c.notes.length ? `<section class="adm-ds"><h3 class="h4">Notes</h3><ul class="adm-notes">${c.notes.map((n) => `<li><span class="small">${esc(n.text)}</span><span class="xs muted">${esc(n.by)} · ${timeAgo(n.at)}</span></li>`).join('')}</ul></section>` : ''}
      <section class="adm-ds"><h3 class="h4">Bookings</h3>${c.bookingList.length ? `<ul class="adm-list">${c.bookingList.slice(0, 6).map((b) => `<li><span class="grow"><b class="small">${esc(b.pitchName || b.pitchId)}</b><span class="xs muted" style="display:block">${fmtDate(b.date)} · ${fmtHour(b.hour)} · ${esc(b.id)}</span></span>${C.statusBadge(b.status)}<b class="num small">${money(b.amount)}</b></li>`).join('')}</ul>` : '<p class="small muted">No bookings.</p>'}</section>
      ${c.device ? `<section class="adm-ds"><h3 class="h4">Shop orders</h3>${c.orderList.length ? `<ul class="adm-list">${c.orderList.slice(0, 6).map((o) => `<li><span class="grow"><b class="small">${esc(o.id)}</b><span class="xs muted" style="display:block">${fmtDate(new Date(o.createdAt))} · ${o.lines.length} item${o.lines.length > 1 ? 's' : ''} · ${esc(o.payment?.label || '')}</span></span><b class="num small">${money(o.total)}</b></li>`).join('')}</ul>` : '<p class="small muted">No orders.</p>'}</section>` : ''}
      <section class="adm-ds"><h3 class="h4">Printed items</h3>${c.podList.length ? `<ul class="adm-list">${c.podList.slice(0, 6).map((o) => `<li><span class="grow"><b class="small">${esc(o.title)}</b><span class="xs muted" style="display:block">${fmtDate(new Date(o.placedAt))} · ${esc(o.color)} · ${esc(o.size)}</span></span><span class="badge ${o.status === 'refused' ? 'badge-danger' : o.status === 'delivered' ? 'badge-turf' : ''}">${esc(o.status)}</span><b class="num small">${money(o.price)}</b></li>`).join('')}</ul>` : '<p class="small muted">No printed items.</p>'}</section>
      ${c.credits.length ? `<section class="adm-ds"><h3 class="h4">Credits from Super App</h3><ul class="adm-list">${c.credits.map((x) => `<li><span class="grow small">${esc(x.reason)}<span class="xs muted" style="display:block">${fmtDate(new Date(x.at))}</span></span><b class="num small" style="color:var(--turf)">+${money(x.amount)}</b></li>`).join('')}</ul></section>` : ''}
      ${!c.device ? `<p class="xs muted adm-ds">${icon('info', 12)} Demo: only the shopper on this device receives notifications and wallet credits for real. Others are recorded.</p>` : ''}`;
  };
  const customerClick = async (e, id) => {
    const act = e.target.closest('[data-act]')?.dataset.act;
    try {
      if (act === 'block') { const r = await A.reason({ title: 'Block this customer?', text: 'They can still browse, but can’t book, order, join games or buy a Fantasy Pass.', label: 'Reason (internal)', confirmLabel: 'Block', danger: true }); if (!r) return; AD().blockCustomer(id, r.reason); toast('Customer blocked', 'info'); return true; }
      if (act === 'unblock') { AD().unblockCustomer(id); toast('Customer unblocked'); return true; }
      if (act === 'reset') { AD().resetRefusals(id); toast('Refusals reset'); return true; }
      if (act === 'points') {
        const r = await A.reason({ title: 'Adjust Super Points', text: 'Use a minus sign to remove points. They see the reason in their points history.', label: 'Reason they’ll see', confirmLabel: 'Save', extra: '<div class="field"><label for="pt-a">Points (+ or −)</label><input class="input num" id="pt-a" name="points" type="number" step="10" value="100" /></div>' });
        if (!r) return; AD().adjustPoints(id, r.points, r.reason); toast('Points updated'); return true;
      }
      if (act === 'note') { const r = await A.reason({ title: 'Add a note', text: 'Only the Super App team sees notes.', label: 'Note', confirmLabel: 'Save note' }); if (!r) return; AD().noteCustomer(id, r.reason); toast('Note saved'); return true; }
      if (act === 'credit') {
        const r = await A.reason({ title: 'Credit their wallet', text: 'Super App pays for it. It shows up in the revenue report as a cost.', label: 'Reason they’ll see', confirmLabel: 'Send credit', extra: '<div class="field"><label for="cr-a">Amount (EGP)</label><input class="input num" id="cr-a" name="amount" type="number" min="5" max="5000" value="100" /></div>' });
        if (!r) return; await AD().creditCustomer(id, r.amount, r.reason); toast(`${money(Math.round(+r.amount))} credited`); return true;
      }
    } catch (err) { toast(err.message, 'error'); }
    return false;
  };

  SA.pages.adminCustomers = A.guard({
    title: 'Customers — Super App Admin',
    render(ctx) {
      const q = ctx.query;
      const all = AD().customers();
      const tab = q.tab || '';
      const list = all.filter((c) => (!tab || (tab === 'blocked' ? c.blocked : tab === 'upfront' ? c.needsPrepay : tab === 'refused' ? c.refused > 0 : true)) && match(q.q, c.name, c.phone, c.city, c.id));
      const body = `<div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'Customers', value: all.length, count: all.length, ic: 'user' })}
          ${C.kpi({ label: 'Spent through Super App', value: money(Math.round(all.reduce((t, c) => t + c.spent, 0))), ic: 'wallet', tone: 'k-turf' })}
          ${C.kpi({ label: 'Pay upfront', value: all.filter((c) => c.needsPrepay).length, ic: 'card', sub: 'Printed items: card or wallet only' })}
          ${C.kpi({ label: 'Blocked', value: all.filter((c) => c.blocked).length, ic: 'ban', tone: all.some((c) => c.blocked) ? 'k-blaze' : '' })}
        </div>
        <section class="dash-card">${filterBar('/admin/customers', q, [['', 'All', all.length], ['refused', 'Refused deliveries', all.filter((c) => c.refused).length], ['upfront', 'Pay upfront', all.filter((c) => c.needsPrepay).length], ['blocked', 'Blocked', all.filter((c) => c.blocked).length]])}</section>
        <section class="dash-card table-card"><div class="table-wrap"><table class="table dash-table adm-rows"><thead><tr><th>Customer</th><th class="r">Bookings</th><th class="r">Orders</th><th class="r">Printed</th><th class="r">Spent</th><th>Status</th><th>Last active</th></tr></thead><tbody>
          ${list.map((c) => `<tr data-open="${esc(c.id)}" tabindex="0"><td><span class="row" style="--gap:10px">${SA.ui.c.avatar({ name: c.name, avatar: c.avatar }, 'sm')}<span><b class="small">${esc(c.name)}</b><span class="xs muted" style="display:block">${esc(c.city || '—')}${c.phone ? ` · ${esc(c.phone)}` : ''}</span></span></span></td>
            <td class="r num">${c.bookings}</td><td class="r num">${c.orders}</td><td class="r num">${c.items}</td><td class="r num strong">${money(Math.round(c.spent))}</td><td>${custBadges(c) || '<span class="badge badge-turf">Active</span>'}</td><td class="small muted">${ago(c.last)}</td></tr>`).join('')
            || `<tr><td colspan="7">${SA.ui.c.empty({ icon: 'user', title: 'No customers found', text: 'Try another search.' })}</td></tr>`}
          </tbody></table></div></section>`;
      return C.layout('customers', { title: 'Customers', subtitle: 'Everyone who booked, ordered or bought printed items — block, require upfront payment, credit or add notes', body });
    },
    mount(el, ctx) {
      bindSearch(el, '/admin/customers', ctx.query);
      const open = (id) => drawer({ title: 'Customer', id, render: customerDrawer, onClick: customerClick,
        onChange: (e, id) => { if (!e.target.matches('[data-prepay]')) return false; try { AD().setPrepay(id, e.target.value); toast('Payment rule saved'); return true; } catch (err) { toast(err.message, 'error'); return false; } } });
      el.addEventListener('click', (e) => { const r = e.target.closest('[data-open]'); if (r) open(r.dataset.open); });
      el.addEventListener('keydown', (e) => { const r = e.target.closest('[data-open]'); if (r && e.key === 'Enter') open(r.dataset.open); });
      if (ctx.query.open) setTimeout(() => open(ctx.query.open), 60);
    },
  });

  /* ======================= Partners ======================= */
  const partnerDrawer = (id) => {
    const p = AD().partners().find((x) => x.id === id);
    const pitches = db().all('pitches').filter((x) => x.ownerId === id && !x.deleted);
    const store = db().all('stores').find((s) => s.ownerId === id);
    return `${head(esc(p.company), `${esc(p.name)} · ${esc(p.email)} · ${esc(p.phone || '')}`, `${p.status === 'suspended' ? '<span class="badge badge-danger">Suspended</span>' : '<span class="badge badge-turf">Active</span>'} <span class="badge">Tax ID ${esc(p.taxId || '—')}</span>`)}
      <div class="adm-stats">${stat(p.pitches, 'Pitches')}${stat(`${(p.occupancy * 100).toFixed(0)}%`, 'Occupancy (30d)')}${stat(money(Math.round(p.gross30)), 'Revenue (30d)')}${stat(money(p.balance), 'Wallet balance')}</div>
      ${p.status === 'suspended' ? `<div class="adm-alert danger">${icon('ban', 16)}<span><b>Suspended</b> ${esc(p.suspendedReason)}</span></div>` : ''}
      <section class="adm-ds"><h3 class="h4">Actions</h3><div class="adm-actions">
        <button class="btn btn-outline btn-sm" data-act="open">${icon('arrowUpRight', 14)} Open their dashboard</button>
        <button class="btn btn-outline btn-sm" data-act="adjust">${icon('swap', 14)} Adjust balance</button>
        ${p.status === 'suspended' ? `<button class="btn btn-primary btn-sm" data-act="activate">${icon('check', 14, 2.6)} Reactivate</button>` : `<button class="btn btn-outline btn-sm" data-act="suspend" style="color:var(--danger)">${icon('ban', 14)} Suspend account</button>`}
      </div><p class="xs muted" style="margin-top:8px">Payouts to ${esc(p.payout?.method || '—')} ${esc(p.payout?.detail || '')}</p></section>
      ${(() => { const d = p.commission, g = db().settings().commission; return `<section class="adm-ds"><h3 class="h4">Commission deal</h3><p class="xs muted" style="margin-top:-4px">Leave a box empty to use the platform rate (bookings ${+(g.bookings * 100).toFixed(1)}%, store ${+(g.store * 100).toFixed(1)}%).</p>
        <form class="form-grid" data-deal novalidate style="margin-top:8px"><div class="field"><label for="dl-b">Bookings %</label><input class="input num" id="dl-b" name="bookings" type="number" min="0" max="50" step="0.5" value="${d?.bookings != null ? +(d.bookings * 100).toFixed(1) : ''}" placeholder="${+(g.bookings * 100).toFixed(1)}" /></div>
          <div class="field"><label for="dl-s">Store %</label><input class="input num" id="dl-s" name="store" type="number" min="0" max="50" step="0.5" value="${d?.store != null ? +(d.store * 100).toFixed(1) : ''}" placeholder="${+(g.store * 100).toFixed(1)}" /></div>
          <div class="field span-2"><label for="dl-n">Note (internal)</label><input class="input" id="dl-n" name="note" maxlength="120" value="${esc(d?.note || '')}" placeholder="e.g. 12-month deal, 3 venues" /></div>
          <div class="span-2 row" style="--gap:8px;justify-content:flex-end">${d ? '<button type="button" class="btn btn-ghost btn-sm" data-act="deal-clear">Use platform rate</button>' : ''}<button class="btn btn-primary btn-sm" type="submit">Save deal</button></div></form></section>`; })()}
      <section class="adm-ds"><h3 class="h4">Pitches</h3><ul class="adm-list">${pitches.map((x) => `<li><span class="grow"><b class="small">${esc(x.name)}</b><span class="xs muted" style="display:block">${esc(x.area)} · ${esc(x.format)} · ${money(x.price)}/h</span></span>${x.status === 'active' ? '<span class="badge badge-turf">Open</span>' : `<span class="badge badge-danger" title="${esc(x.closedReason || '')}">Closed</span>`}
        <a class="btn btn-ghost btn-sm" href="#/pitches/${x.id}" target="_blank" rel="noopener">View</a><button class="btn btn-ghost btn-sm" data-pitch="${x.id}" data-to="${x.status === 'active' ? 'closed' : 'active'}">${x.status === 'active' ? 'Close' : 'Reopen'}</button></li>`).join('') || '<li class="small muted">No pitches yet.</li>'}</ul></section>
      ${store ? `<section class="adm-ds"><h3 class="h4">Products</h3><ul class="adm-list">${db().all('products').filter((x) => x.storeId === store.id && !x.deleted).map((x) => `<li class="${x.hiddenByAdmin ? 'is-dim' : ''}"><span class="grow"><b class="small">${esc(x.name)}</b><span class="xs muted" style="display:block">${money(x.price)} · ${x.stock} in stock${x.hiddenByAdmin ? ` · hidden: ${esc(x.hiddenReason)}` : x.status !== 'active' ? ' · draft' : ''}</span></span><a class="btn btn-ghost btn-sm" href="#/shop/${x.id}" target="_blank" rel="noopener">View</a><button class="btn btn-ghost btn-sm" data-hide-product="${x.id}" data-on="${x.hiddenByAdmin ? '' : '1'}">${x.hiddenByAdmin ? 'Show' : 'Hide'}</button></li>`).join('') || '<li class="small muted">No products.</li>'}</ul></section>` : ''}
      ${store ? `<section class="adm-ds"><h3 class="h4">Store</h3><ul class="adm-list"><li><span class="grow"><b class="small">${esc(store.name)}</b><span class="xs muted" style="display:block">${db().all('products').filter((x) => x.storeId === store.id && !x.deleted).length} products</span></span><span class="badge ${store.status === 'active' ? 'badge-turf' : 'badge-danger'}">${store.status === 'active' ? 'Live' : 'Suspended'}</span><a class="btn btn-ghost btn-sm" href="#/shop?store=${store.id}" target="_blank" rel="noopener">View</a></li></ul></section>` : ''}`;
  };
  const partnerClick = async (e, id) => {
    const act = e.target.closest('[data-act]')?.dataset.act;
    try {
      if (act === 'open') { AD().openDashboard(id); window.open(SA.core.router.url('/dash'), '_blank', 'noopener'); return false; }
      if (act === 'adjust') { A.adjustDialog(id); return false; }
      if (act === 'suspend') { const r = await A.reason({ title: 'Suspend this partner?', text: 'They can’t sign in, and their pitches and store disappear from Super App. Upcoming bookings stay — handle them from Orders & bookings.', label: 'Reason they’ll see', confirmLabel: 'Suspend', danger: true }); if (!r) return; AD().setPartnerStatus(id, 'suspended', r.reason); toast('Partner suspended', 'info'); return true; }
      if (act === 'activate') { AD().setPartnerStatus(id, 'active'); toast('Partner reactivated'); return true; }
      if (act === 'deal-clear') { AD().setCommissionDeal(id, {}); toast('Platform rates apply again', 'info'); return true; }
      const hp = e.target.closest('[data-hide-product]');
      if (hp) {
        if (hp.dataset.on) { const r = await A.reason({ title: 'Hide this product?', text: 'It disappears from the shop; the rest of the store stays live. The owner sees the reason.', label: 'Reason the owner sees', placeholder: 'e.g. Looks like a counterfeit of a branded boot.', confirmLabel: 'Hide product', danger: true }); if (!r) return; AD().setProductHidden(hp.dataset.hideProduct, true, r.reason); toast('Product hidden', 'info'); }
        else { AD().setProductHidden(hp.dataset.hideProduct, false); toast('Product is back in the shop'); }
        return true;
      }
      const pt = e.target.closest('[data-pitch]');
      if (pt) {
        if (pt.dataset.to === 'closed') { const r = await A.reason({ title: 'Close this pitch?', text: 'Players can’t book it until it’s reopened. The owner is notified.', label: 'Reason the owner sees', confirmLabel: 'Close pitch', danger: true }); if (!r) return; AD().setPitchStatus(pt.dataset.pitch, 'closed', r.reason); }
        else AD().setPitchStatus(pt.dataset.pitch, 'active');
        toast(pt.dataset.to === 'closed' ? 'Pitch closed' : 'Pitch reopened'); return true;
      }
    } catch (err) { toast(err.message, 'error'); }
    return false;
  };

  SA.pages.adminPartners = A.guard({
    title: 'Partners — Super App Admin',
    render(ctx) {
      const q = ctx.query;
      const all = AD().partners();
      const list = all.filter((p) => (!q.tab || p.status === q.tab) && match(q.q, p.company, p.name, p.email, p.phone));
      const stores = db().all('stores');
      const pr = AD().printerOwed();
      const tot = all.reduce((t, p) => ({ gross: t.gross + p.gross30, comm: t.comm + p.commission30 }), { gross: 0, comm: 0 });
      const body = `<div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'Pitch owners', value: all.length, count: all.length, ic: 'users' })}
          ${C.kpi({ label: 'Pitches on Super App', value: all.reduce((t, p) => t + p.pitches, 0), ic: 'pitch' })}
          ${C.kpi({ label: 'Partner revenue (30d)', value: money(Math.round(tot.gross)), ic: 'percent', tone: 'k-turf' })}
          ${C.kpi({ label: 'Commission earned (30d)', value: money(Math.round(tot.comm)), ic: 'wallet', tone: 'k-blaze' })}
        </div>
        <section class="dash-card">${filterBar('/admin/partners', q, [['', 'All', all.length], ['active', 'Active', all.filter((p) => p.status === 'active').length], ['suspended', 'Suspended', all.filter((p) => p.status === 'suspended').length]])}</section>
        <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Pitch owners</h2><div class="table-wrap"><table class="table dash-table adm-rows"><thead><tr><th>Partner</th><th class="r">Pitches</th><th class="r">Occupancy</th><th class="r">Revenue (30d)</th><th class="r">Commission</th><th class="r">Balance</th><th>Status</th></tr></thead><tbody>
          ${list.map((p) => `<tr data-open="${p.id}" tabindex="0"><td><span class="row">${SA.ui.c.avatar({ name: p.name, avatar: p.avatar }, 'sm')}<span><b class="small">${esc(p.company)}</b><span class="xs muted" style="display:block">${esc(p.name)} · ${esc(p.email)}</span></span></span></td>
            <td class="r num">${p.pitches}${p.closed ? `<span class="xs" style="display:block;color:var(--danger)">${p.closed} closed</span>` : ''}</td><td class="r num">${(p.occupancy * 100).toFixed(1)}%</td><td class="r num">${money(Math.round(p.gross30))}</td><td class="r num strong">${money(Math.round(p.commission30))}</td><td class="r num">${money(p.balance)}</td>
            <td>${p.status === 'suspended' ? '<span class="badge badge-danger">Suspended</span>' : '<span class="badge badge-turf">Active</span>'}</td></tr>`).join('') || `<tr><td colspan="7" class="small muted" style="padding:18px">No partners found.</td></tr>`}
          </tbody></table></div></section>
        <div class="dash-grid g-2-1">
          <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Stores</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Store</th><th>Owner</th><th class="r">Products</th><th>Status</th><th></th></tr></thead><tbody>
            ${stores.map((s) => `<tr><td><b class="small">${esc(s.name)}</b>${s.official ? ' <span class="badge badge-ink">Official</span>' : ''}</td><td class="small">${esc(db().find('accounts', s.ownerId)?.company || '')}</td><td class="r num">${db().all('products').filter((p) => p.storeId === s.id && !p.deleted).length}</td>
              <td><span class="badge ${s.status === 'active' ? 'badge-turf' : 'badge-danger'}">${s.status === 'active' ? 'Live' : 'Suspended'}</span></td>
              <td class="r"><div class="row" style="--gap:6px;justify-content:flex-end"><a class="btn btn-ghost btn-sm" href="#/shop?store=${s.id}" target="_blank" rel="noopener">View</a>${s.official ? '' : `<button class="btn btn-outline btn-sm" data-store-status="${s.id}" data-to="${s.status === 'active' ? 'suspended' : 'active'}">${s.status === 'active' ? 'Suspend' : 'Reactivate'}</button>`}</div></td></tr>`).join('')}
            </tbody></table></div></section>
          <section class="dash-card"><h2 class="h4">${icon('package', 18)} Print partner</h2><p class="small soft" style="margin-top:6px">${esc(pr.account?.company || '—')} · ${esc(pr.account?.email || '')}</p>
            <dl class="kv" style="margin-top:12px"><div><dt>In the print queue</dt><dd class="num">${SA.services.pod.orders({ status: ['received', 'printing'] }).length}</dd></div><div><dt>Owed now</dt><dd class="num">${money(pr.owed)}</dd></div></dl>
            <div class="row wrap" style="--gap:8px;margin-top:12px"><a class="btn btn-outline btn-sm" href="#/admin/print">Print queue</a><a class="btn btn-outline btn-sm" href="#/admin/payouts?tab=print">Settlements</a></div></section>
        </div>`;
      return C.layout('partners', { title: 'Partners', subtitle: 'Pitch owners, stores and the print partner — suspend, close pitches, open their dashboards', body });
    },
    mount(el, ctx) {
      bindSearch(el, '/admin/partners', ctx.query);
      const open = (id) => drawer({ title: 'Partner', id, render: partnerDrawer, onClick: partnerClick, onSubmit: (e, pid) => { if (!e.target.matches('[data-deal]')) return false; try { AD().setCommissionDeal(pid, SA.core.utils.formData(e.target)); toast('Commission deal saved'); return true; } catch (err) { toast(err.message, 'error'); return false; } } });
      el.addEventListener('click', async (e) => {
        const r = e.target.closest('[data-open]'); if (r) { open(r.dataset.open); return; }
        const st = e.target.closest('[data-store-status]');
        if (st) {
          const s = db().find('stores', st.dataset.storeStatus);
          if (st.dataset.to === 'suspended' && !(await SA.ui.overlay.confirm({ title: `Suspend ${esc(s.name)}?`, text: 'All its products disappear from the Super App shop until you reactivate it.', confirmLabel: 'Suspend store', danger: true }))) return;
          SA.services.dash.setStoreStatus(s.id, st.dataset.to); AD().log(`store.${st.dataset.to}`, `${st.dataset.to === 'active' ? 'Reactivated' : 'Suspended'} store ${s.name}`, s.id);
          toast(st.dataset.to === 'active' ? 'Store reactivated' : 'Store suspended'); SA.app.rerender();
        }
      });
      el.addEventListener('keydown', (e) => { const r = e.target.closest('[data-open]'); if (r && e.key === 'Enter') open(r.dataset.open); });
    },
  });

  /* ======================= Sellers ======================= */
  const KYC = { none: ['', 'Not sent'], pending: ['badge-info', 'Waiting'], verified: ['badge-turf', 'Verified'], rejected: ['badge-danger', 'Rejected'] };
  const sellerDrawer = (id) => {
    const s = db().find('sellers', id);
    const lv = SL().levelOf(s), st = lv.stats;
    const e = SA.services.pod.earnings(id);
    const acc = db().find('accounts', s.accountId);
    const live = db().all('designs').filter((d) => d.sellerId === id && d.status === 'live').length;
    return `${head(esc(s.name), `@${esc(s.handle)} · ${esc(acc?.name || '')} · ${esc(acc?.email || '')}`, `<span class="lvl-chip lvl-${lv.id}">${esc(lv.name)}</span> ${s.status === 'suspended' ? '<span class="badge badge-danger">Suspended</span>' : '<span class="badge badge-turf">Active</span>'} <span class="badge ${KYC[s.kyc || 'none'][0]}">ID: ${KYC[s.kyc || 'none'][1]}</span>${s.featuredByAdmin ? ' <span class="badge badge-gold">Featured</span>' : ''}${lv.grace ? ' <span class="badge badge-gold">Grace month</span>' : ''}`)}
      <div class="adm-stats">${stat(st.sales, 'Items sold')}${stat(st.rating ? st.rating.toFixed(1) : '—', 'Rating')}${stat(live, 'Live designs')}${stat(st.refused, 'Refused')}</div>
      <div class="adm-stats">${stat(money(e.available), 'Available')}${stat(money(e.pending), 'Pending')}${stat(money(e.paidOut), 'Paid out')}${stat(money(e.lifetime), 'Lifetime profit')}</div>
      ${s.status === 'suspended' && s.suspendedReason ? `<div class="adm-alert danger">${icon('ban', 16)}<span><b>Suspended</b> ${esc(s.suspendedReason)}</span></div>` : ''}
      <section class="adm-ds"><h3 class="h4">Actions</h3><div class="adm-actions">
        <a class="btn btn-outline btn-sm" href="#/s/${esc(s.handle)}" target="_blank" rel="noopener">${icon('bag', 14)} View store</a>
        <button class="btn btn-outline btn-sm" data-act="open">${icon('arrowUpRight', 14)} Open their dashboard</button>
        <button class="btn btn-outline btn-sm" data-act="adjust">${icon('swap', 14)} Adjust balance</button>
        <button class="btn btn-outline btn-sm" data-act="feature">${icon('sparkle', 14)} ${s.featuredByAdmin ? 'Remove from featured' : 'Feature in the shop'}</button>
        ${s.status === 'suspended' ? `<button class="btn btn-primary btn-sm" data-act="activate">${icon('check', 14, 2.6)} Reactivate</button>` : `<button class="btn btn-outline btn-sm" data-act="suspend" style="color:var(--danger)">${icon('ban', 14)} Suspend</button>`}
      </div></section>
      ${s.kyc === 'pending' ? `<section class="adm-ds"><h3 class="h4">ID check</h3><p class="small">${esc(s.kycInfo?.fullName || '')} · national ID ending ${esc(s.kycInfo?.idLast4 || '••••')} · sent ${ago(s.kycInfo?.submittedAt)}</p><div class="adm-actions" style="margin-top:10px"><button class="btn btn-primary btn-sm" data-act="kyc-ok">Verify</button><button class="btn btn-ghost btn-sm" data-act="kyc-no">Reject</button></div></section>` : ''}
      <section class="adm-ds"><h3 class="h4">Copyright strikes</h3>${(s.strikes || []).length ? `<ul class="adm-list">${s.strikes.map((x, i) => `<li><span class="grow small">${esc(x.reason)}<span class="xs muted" style="display:block">${fmtDate(new Date(x.at))}${Date.now() - x.at < 90 * 864e5 ? ' · active' : ' · expired'}</span></span><button class="btn btn-ghost btn-sm" data-strike="${i}">Remove</button></li>`).join('')}</ul>` : '<p class="small muted">No strikes.</p>'}</section>
      <section class="adm-ds"><h3 class="h4">Level</h3><p class="small">${esc(lv.name)} · ${st.sales} items sold${lv.next ? ` · ${lv.toNext} more to ${esc(lv.next.name)}` : ' · top level'}${lv.frozen ? ` · frozen until ${fmtDate(new Date(lv.frozenUntil))}` : ''}</p></section>`;
  };
  const sellerClick = async (e, id) => {
    const act = e.target.closest('[data-act]')?.dataset.act;
    const s = db().find('sellers', id);
    try {
      if (act === 'open') { AD().openDashboard(s.accountId); window.open(SA.core.router.url('/dash/seller'), '_blank', 'noopener'); return false; }
      if (act === 'adjust') { A.adjustDialog(s.accountId); return false; }
      if (act === 'feature') { AD().featureSeller(id, !s.featuredByAdmin); toast(s.featuredByAdmin ? 'Removed from featured' : 'Featured in the shop'); return true; }
      if (act === 'suspend') { const r = await A.reason({ title: `Suspend ${esc(s.name)}?`, text: 'Their designs and products disappear from the shop. Orders already placed are still printed and delivered.', label: 'Reason they’ll see', confirmLabel: 'Suspend', danger: true }); if (!r) return; AD().setSellerStatus(id, 'suspended', r.reason); toast('Seller suspended', 'info'); return true; }
      if (act === 'activate') { AD().setSellerStatus(id, 'active'); toast('Seller reactivated'); return true; }
      if (act === 'kyc-ok' || act === 'kyc-no') { AD().verifyKyc(id, act === 'kyc-ok'); toast(act === 'kyc-ok' ? 'ID verified — withdrawals unlocked' : 'ID rejected', act === 'kyc-ok' ? 'success' : 'info'); return true; }
      const sk = e.target.closest('[data-strike]');
      if (sk) { const r = await A.reason({ title: 'Remove this strike?', text: 'If no active strikes are left, the level freeze ends too.', label: 'Reason they’ll see', confirmLabel: 'Remove strike' }); if (!r) return; AD().removeStrike(id, +sk.dataset.strike, r.reason); toast('Strike removed'); return true; }
    } catch (err) { toast(err.message, 'error'); }
    return false;
  };

  SA.pages.adminSellers = A.guard({
    title: 'Sellers — Super App Admin',
    render(ctx) {
      const q = ctx.query;
      const all = SL().list().map((s) => ({ s, lv: SL().levelOf(s), acc: db().find('accounts', s.accountId), live: db().all('designs').filter((d) => d.sellerId === s.id && d.status === 'live').length }));
      const tab = q.tab || '';
      const list = all.filter(({ s, lv }) => (!tab || (tab === 'kyc' ? s.kyc === 'pending' : tab === 'suspended' ? s.status === 'suspended' : tab === 'strikes' ? (s.strikes || []).length : lv.id === tab)) && match(q.q, s.name, s.handle, s.id));
      const levels = SL().levels();
      const body = `<div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'Sellers', value: all.length, count: all.length, ic: 'users' })}
          ${C.kpi({ label: 'Live designs', value: all.reduce((t, x) => t + x.live, 0), ic: 'image', tone: 'k-turf' })}
          ${C.kpi({ label: 'ID checks waiting', value: all.filter(({ s }) => s.kyc === 'pending').length, ic: 'shield', tone: all.some(({ s }) => s.kyc === 'pending') ? 'k-blaze' : '' })}
          ${C.kpi({ label: 'Suspended', value: all.filter(({ s }) => s.status === 'suspended').length, ic: 'ban' })}
        </div>
        <section class="dash-card">${filterBar('/admin/sellers', q, [['', 'All', all.length], ...levels.map((l) => [l.id, l.name, all.filter((x) => x.lv.id === l.id).length]), ['kyc', 'ID waiting', all.filter(({ s }) => s.kyc === 'pending').length], ['strikes', 'With strikes', all.filter(({ s }) => (s.strikes || []).length).length], ['suspended', 'Suspended', all.filter(({ s }) => s.status === 'suspended').length]])}</section>
        <section class="dash-card table-card"><div class="table-wrap"><table class="table dash-table adm-rows"><thead><tr><th>Seller</th><th>Level</th><th>Sells</th><th class="r">Items sold</th><th class="r">Rating</th><th class="r">Live designs</th><th>ID</th><th>Status</th></tr></thead><tbody>
          ${list.map(({ s, lv, acc, live }) => `<tr data-open="${s.id}" tabindex="0"><td><span class="row">${SA.ui.c.avatar({ name: s.name, avatar: s.avatar }, 'sm')}<span><b class="small">${esc(s.name)}</b><span class="xs muted" style="display:block">@${esc(s.handle)} · ${esc(acc?.name || '')}</span></span></span></td>
            <td><span class="lvl-chip lvl-${lv.id}">${esc(lv.name)}</span>${lv.grace ? '<span class="xs" style="display:block;color:var(--gold)">Grace month</span>' : ''}</td><td class="small">${{ pod: 'Print on demand', store: 'Own store', both: 'Both' }[s.mode]}</td><td class="r num">${lv.stats.sales}</td><td class="r num">${lv.stats.rating ? lv.stats.rating.toFixed(1) : '—'}</td><td class="r num">${live}</td>
            <td><span class="badge ${KYC[s.kyc || 'none'][0]}">${KYC[s.kyc || 'none'][1]}</span></td><td>${s.status === 'pending' ? '<a class="badge badge-gold" href="#/admin/approvals?tab=sellers">Waiting for approval</a>' : s.status === 'rejected' ? '<span class="badge badge-danger">Not approved</span>' : s.status === 'suspended' ? '<span class="badge badge-danger">Suspended</span>' : (s.strikes || []).length ? `<span class="badge badge-gold">${s.strikes.length} strike${s.strikes.length > 1 ? 's' : ''}</span>` : '<span class="badge badge-turf">Active</span>'}</td></tr>`).join('')
            || `<tr><td colspan="8" class="small muted" style="padding:18px">No sellers found.</td></tr>`}
          </tbody></table></div></section>`;
      return C.layout('sellers', { title: 'Sellers', subtitle: 'Levels, ID checks, strikes and status — feature, suspend or open a seller’s dashboard', body });
    },
    mount(el, ctx) {
      bindSearch(el, '/admin/sellers', ctx.query);
      const open = (id) => drawer({ title: 'Seller', id, render: sellerDrawer, onClick: sellerClick });
      el.addEventListener('click', (e) => { const r = e.target.closest('[data-open]'); if (r) open(r.dataset.open); });
      el.addEventListener('keydown', (e) => { const r = e.target.closest('[data-open]'); if (r && e.key === 'Enter') open(r.dataset.open); });
      if (ctx.query.open) setTimeout(() => open(ctx.query.open), 60);
    },
  });

})(window.SA);
