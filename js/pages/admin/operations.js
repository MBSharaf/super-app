/** Admin console: every booking, shop order and printed item in one place, plus support tickets and disputes. */
(function (SA) {
  const { esc, money, timeAgo, fmtDate, fmtHour } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const A = SA.pages.adminCommon;
  const AD = () => SA.services.admin;
  const SUP = () => SA.services.support;
  const db = () => SA.services.db;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });
  const match = (q, ...f) => !q || f.some((x) => String(x || '').toLowerCase().includes(q.toLowerCase()));

  /* ======================= Orders & bookings ======================= */
  const PST = { received: ['badge-blaze', 'New'], printing: ['badge-gold', 'Printing'], shipped: ['badge-info', 'Shipped'], delivered: ['badge-turf', 'Delivered'], refused: ['badge-danger', 'Refused'], cancelled: ['badge-danger', 'Cancelled'] };
  const SST = { confirmed: ['badge-blaze', 'To pack'], packed: ['badge-gold', 'Packed'], shipped: ['badge-info', 'Shipped'], delivered: ['badge-turf', 'Delivered'], cancelled: ['badge-danger', 'Cancelled'] };
  const badge = (map, s) => { const [c, l] = map[s] || ['', s]; return `<span class="badge ${c}">${l}</span>`; };

  SA.pages.adminOrders = A.guard({
    title: 'Orders & bookings — Super App Admin',
    render(ctx) {
      const q = ctx.query;
      const tab = q.tab || '';
      const r = C.range(q, '7');
      const D = SA.services.dash;
      const n = +q.n || 150;
      const extra = { range: q.range, from: q.from, to: q.to, q: q.q };
      let table = '', count = 0;
      if (!tab) {
        const all = D.ledger({ from: r.from, to: r.to }).filter((b) => (!q.status || b.status === q.status) && match(q.q, b.id, b.customer?.name, b.customer?.phone, b.pitchName));
        count = all.length;
        table = `<thead><tr><th>Booking</th><th>When</th><th>Pitch</th><th>Customer</th><th>Source</th><th>Status</th><th class="r">Amount</th></tr></thead><tbody>
          ${all.slice(0, n).map((b) => `<tr data-booking="${esc(b.id)}" tabindex="0"><td class="xs mono">${esc(b.id.length > 22 ? `${b.id.slice(0, 20)}…` : b.id)}</td><td class="small nowrap">${fmtDate(b.date)}<span class="xs muted" style="display:block">${fmtHour(b.hour)} · ${b.duration} h</span></td><td class="small">${esc(b.pitchName)}</td><td class="small">${esc(b.customer?.name || '')}</td><td class="xs">${esc(b.sourceLabel)}</td><td>${C.statusBadge(b.status)}</td><td class="r num">${money(b.amount)}</td></tr>`).join('')}`;
      } else if (tab === 'shop') {
        const all = D.orders({ from: r.from, to: r.to }).filter((o) => (!q.status || o.status === q.status) && match(q.q, o.orderId, o.customer?.name, o.storeName));
        count = all.length;
        table = `<thead><tr><th>Order</th><th>Store</th><th>Customer</th><th class="r">Items</th><th>Payment</th><th>Status</th><th class="r">Total</th></tr></thead><tbody>
          ${all.slice(0, n).map((o) => `<tr data-order="${esc(o.key)}" tabindex="0"><td><b class="small num">${esc(o.orderId)}</b><span class="xs muted" style="display:block">${timeAgo(o.createdAt)}</span></td><td class="small">${esc(o.storeName)}${o.official ? ' <span class="badge badge-ink">Official</span>' : ''}</td><td class="small">${esc(o.customer?.name || '')}<span class="xs muted" style="display:block">${esc(o.customer?.city || '')}</span></td><td class="r num">${o.units}</td><td class="xs">${esc(o.payment)}</td><td>${badge(SST, o.status)}</td><td class="r num">${money(o.gross)}</td></tr>`).join('')}`;
      } else if (tab === 'packages') {
        const all = AD().packages().filter((x) => match(q.q, x.id, x.series.customer?.name, x.pitch?.name) && (!q.status || (q.status === 'active' ? !x.expired : x.expired)));
        count = all.length;
        table = `<thead><tr><th>Package</th><th>Customer</th><th>Pitch & time</th><th class="r">Scanned</th><th class="r">Played</th><th class="r">Missed</th><th class="r">Left</th><th>Exception</th><th></th></tr></thead><tbody>
          ${all.slice(0, n).map((x) => `<tr><td><b class="small num">${esc(x.id)}</b><span class="xs muted" style="display:block">${x.total} games · ${x.expired ? 'Used up' : 'Active'}</span></td><td class="small">${esc(x.series.customer?.name || '')}</td>
            <td class="small">${esc(x.pitch?.name || '')}<span class="xs muted" style="display:block">${SA.core.utils.DAY[x.series.weekday]} · ${fmtHour(x.series.hour)}</span></td>
            <td class="r num">${x.scans.length}×</td><td class="r num">${x.played}</td><td class="r num" style="${x.missed ? 'color:var(--danger)' : ''}">${x.missed}</td><td class="r num">${x.left}</td>
            <td class="small">${x.exception ? `<span class="badge badge-gold" title="${esc(x.exception.note)}">Used</span>` : '<span class="badge">Available</span>'}</td>
            <td class="r">${!x.exception && x.missed ? `<button class="btn btn-ghost btn-sm" data-excuse="${esc(x.id)}">Excuse a missed game</button>` : ''}</td></tr>`).join('')}`;
      } else {
        const all = (db().all('podOrders') || []).filter((o) => !o.sample && SA.core.utils.isoDate(new Date(o.placedAt)) >= r.from && SA.core.utils.isoDate(new Date(o.placedAt)) <= r.to && (!q.status || o.status === q.status) && match(q.q, o.id, o.orderId, o.title, o.customer?.name)).sort((a, b) => b.placedAt - a.placedAt);
        count = all.length;
        table = `<thead><tr><th>Item</th><th>Design</th><th>Seller</th><th>Customer</th><th>Status</th><th class="r">Price</th><th></th></tr></thead><tbody>
          ${all.slice(0, n).map((o) => `<tr><td><b class="xs mono">${esc(o.id)}</b><span class="xs muted" style="display:block">${esc(o.orderId)} · ${timeAgo(o.placedAt)}</span></td><td class="small">${esc(o.title)}<span class="xs muted" style="display:block">${esc(o.color)} · ${esc(o.size)} × ${o.qty || 1}</span></td><td class="small">${esc(db().find('sellers', o.sellerId)?.name || '')}</td><td class="small">${esc(o.customer?.name || '')}</td><td>${badge(PST, o.status)}${o.reprints ? ` <span class="badge">${o.reprints} reprint</span>` : ''}</td><td class="r num">${money(o.price * (o.qty || 1))}</td>
            <td class="r">${['received', 'printing'].includes(o.status) ? `<button class="btn btn-ghost btn-sm" data-cancel-print="${o.id}" style="color:var(--danger)">Cancel</button>` : ''}</td></tr>`).join('')}`;
      }
      const statuses = tab === 'packages' ? [['', 'Any status'], ['active', 'Active'], ['used', 'Used up']] : !tab ? [['', 'Any status'], ['upcoming', 'Upcoming'], ['in-progress', 'Playing now'], ['completed', 'Completed'], ['cancelled', 'Cancelled'], ['no-show', 'No-show']]
        : tab === 'shop' ? [['', 'Any status'], ...Object.entries(SST).map(([k, [, l]]) => [k, l])] : [['', 'Any status'], ...Object.entries(PST).map(([k, [, l]]) => [k, l])];
      const body = `${A.tabs('/admin/orders', [['', 'Pitch bookings'], ['packages', 'Weekly packages'], ['shop', 'Shop orders'], ['print', 'Printed items']], tab, extra)}
        ${tab === 'packages' ? '<p class="small muted">One QR code per package: it works once per game, then it expires. A game the customer doesn’t come to counts as used. One missed game per package can be excused after you agree it with the pitch manager — the customer gets a make-up game.</p>' : C.rangePicker(r, '/admin/orders', { tab: tab || undefined, q: q.q, status: q.status })}
        <section class="dash-card"><form class="adm-filters" data-f>
          <label class="field grow"><span class="field-label">Search</span><input class="input" name="q" value="${esc(q.q || '')}" placeholder="ID, customer, pitch, store, design…" /></label>
          <label class="field"><span class="field-label">Status</span><select class="select" name="status">${statuses.map(([v, l]) => `<option value="${v}" ${q.status === v || (!q.status && !v) ? 'selected' : ''}>${l}</option>`).join('')}</select></label>
          <button class="btn btn-primary" type="submit" style="align-self:end">Filter</button></form></section>
        <section class="dash-card table-card"><div class="table-wrap"><table class="table dash-table adm-rows">${table}
          ${count ? '' : `<tr><td colspan="7">${SA.ui.c.empty({ icon: 'ticket', title: 'Nothing found', text: 'Try another date range or filter.' })}</td></tr>`}</tbody></table></div>
          <div class="row-between" style="padding:12px 18px"><span class="xs muted">${count.toLocaleString()} result${count === 1 ? '' : 's'}${tab !== 'print' ? ' · click a row for details and actions' : ''}</span>${count > n ? `<a class="btn btn-outline btn-sm" href="${SA.core.router.href('/admin/orders', { ...q, n: n + 200 })}" data-replace>Show more</a>` : ''}</div></section>`;
      return C.layout('orders', { title: 'Orders & bookings', subtitle: 'Every pitch booking, shop order and printed item on Super App — open one to cancel or refund', body });
    },
    mount(el, ctx) {
      const tab = ctx.query.tab || '';
      C.bindRange(el, '/admin/orders', { tab: tab || undefined, q: ctx.query.q, status: ctx.query.status });
      el.querySelector('[data-f]')?.addEventListener('submit', (e) => { e.preventDefault(); const f = SA.core.utils.formData(e.target); SA.core.router.go(SA.core.router.href('/admin/orders', { ...ctx.query, q: f.q || undefined, status: f.status || undefined, n: undefined }).slice(1)); });
      const open = (row) => { if (row.dataset.booking) SA.pages.dashBookings.openBooking(row.dataset.booking); else if (row.dataset.order) SA.pages.dashOrders.openOrder(row.dataset.order); };
      el.addEventListener('click', async (e) => {
        const ex = e.target.closest('[data-excuse]');
        if (ex) {
          const pk = SA.services.bookings.packageStatus(ex.dataset.excuse);
          const missed = pk.sessions.filter((x) => x.state === 'missed');
          const r = await A.reason({ title: `Excuse a missed game · ${pk.id}`, text: 'Only once per package. The game won’t count, and the customer gets a make-up game at the same time after the package ends. The customer and the pitch manager are told.', label: 'What did you agree with the pitch manager?', confirmLabel: 'Excuse the game',
            extra: `<div class="field"><label for="ex-g">Missed game</label><select class="select" id="ex-g" name="game">${missed.map((x) => `<option value="${x.id}">Game ${x.no} · ${SA.core.utils.fmtDate(x.date, 'long')}</option>`).join('')}</select></div>` });
          if (!r) return;
          try { const out = AD().excuseMissed(pk.id, r.game, r.reason); toast(`Excused — make-up game on ${SA.core.utils.fmtDate(out.date, 'long')}`); SA.app.rerender(); } catch (err) { toast(err.message, 'error'); }
          return;
        }
        const cp = e.target.closest('[data-cancel-print]');
        if (cp) {
          const r = await A.reason({ title: 'Cancel this printed item?', text: 'The customer is refunded if they paid online. If printing already started, the cost is on Super App.', label: 'Reason the seller and customer see', confirmLabel: 'Cancel item', danger: true });
          if (!r) return;
          try { await AD().cancelPrint(cp.dataset.cancelPrint, r.reason); toast('Item cancelled', 'info'); SA.app.rerender(); } catch (err) { toast(err.message, 'error'); }
          return;
        }
        const row = e.target.closest('[data-booking],[data-order]'); if (row) open(row);
      });
      el.addEventListener('keydown', (e) => { const row = e.target.closest('[data-booking],[data-order]'); if (row && e.key === 'Enter') open(row); });
    },
  });

  /* ======================= Support & disputes ======================= */
  const FROM = { customer: ['badge-info', 'Customer'], partner: ['badge-turf', 'Pitch owner'], seller: ['badge-gold', 'Seller'], printer: ['badge-gold', 'Print partner'] };
  const TST = { open: ['badge-blaze', 'Waiting for us'], pending: ['badge-info', 'Waiting for them'], resolved: ['badge-turf', 'Resolved'] };

  SA.pages.adminSupport = A.guard({
    title: 'Support & disputes — Super App Admin',
    render(ctx) {
      const q = ctx.query;
      const all = SUP().all();
      const tab = q.tab || 'open';
      const F = { open: (t) => t.status === 'open', pending: (t) => t.status === 'pending', disputes: (t) => SUP().CATS[t.category]?.dispute && t.status !== 'resolved', resolved: (t) => t.status === 'resolved', all: () => true };
      const list = all.filter(F[tab] || F.open).filter((t) => match(q.q, t.id, t.subject, t.from?.name));
      const refunds = (db().all('refunds') || []).filter((x) => x.ticketId);
      const body = `<div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'Waiting for us', value: all.filter(F.open).length, ic: 'message', tone: all.some(F.open) ? 'k-blaze' : '' })}
          ${C.kpi({ label: 'Open disputes', value: all.filter(F.disputes).length, ic: 'alert' })}
          ${C.kpi({ label: 'Resolved', value: all.filter(F.resolved).length, ic: 'check', tone: 'k-turf' })}
          ${C.kpi({ label: 'Refunded for disputes', value: money(refunds.reduce((t, x) => t + x.amount, 0)), ic: 'wallet', sub: `${refunds.filter((x) => x.bearer === 'platform').length} paid by Super App` })}
        </div>
        <section class="dash-card"><form class="adm-filters" data-s><label class="field grow"><span class="field-label">Search</span><input class="input" name="q" value="${esc(q.q || '')}" placeholder="Ticket, subject, name…" /></label><button class="btn btn-primary" type="submit" style="align-self:end">Search</button></form>
          ${A.tabs('/admin/support', [['open', 'Waiting for us', all.filter(F.open).length], ['pending', 'Waiting for them', all.filter(F.pending).length], ['disputes', 'Disputes', all.filter(F.disputes).length], ['resolved', 'Resolved', all.filter(F.resolved).length], ['all', 'All', all.length]], tab, { q: q.q })}</section>
        <section class="dash-card table-card"><ul class="adm-tickets">${list.map((t) => `<li><a href="#/admin/support/${t.id}">
            <span class="bp-ico t-${SUP().CATS[t.category]?.dispute ? 'danger' : 'info'}">${icon(SUP().CATS[t.category]?.icon || 'message', 16)}</span>
            <span class="grow"><span class="row wrap" style="--gap:6px"><b class="small">${esc(t.subject)}</b>${t.priority === 'high' ? '<span class="badge badge-danger">High</span>' : ''}${SUP().CATS[t.category]?.dispute ? '<span class="badge">Dispute</span>' : ''}${t.refund ? `<span class="badge badge-turf">Refunded ${money(t.refund.amount)}</span>` : ''}</span>
              <span class="xs muted" style="display:block;margin-top:2px">${esc(t.id)} · ${esc(t.from?.name || '')} <span class="badge ${FROM[t.from?.kind]?.[0] || ''}">${FROM[t.from?.kind]?.[1] || ''}</span> · ${t.messages.length} message${t.messages.length > 1 ? 's' : ''} · ${timeAgo(t.updatedAt)}</span>
              <span class="xs soft adm-snip">${esc(t.messages[t.messages.length - 1].text.slice(0, 140))}</span></span>
            ${(() => { const [c, l] = TST[t.status]; return `<span class="badge ${c}">${l}</span>`; })()}</a></li>`).join('') || `<li>${SA.ui.c.empty({ icon: 'message', title: 'Nothing here', text: 'New requests from customers, partners and sellers show up here.' })}</li>`}</ul></section>`;
      return C.layout('support', { title: 'Support & disputes', subtitle: 'Requests from customers, pitch owners, sellers and the print partner — reply, resolve and refund', body });
    },
    mount(el, ctx) {
      el.querySelector('[data-s]')?.addEventListener('submit', (e) => { e.preventDefault(); SA.core.router.go(SA.core.router.href('/admin/support', { ...ctx.query, q: e.target.q.value.trim() || undefined }).slice(1)); });
    },
  });

  SA.pages.adminTicket = A.guard({
    title: 'Ticket — Super App Admin',
    render(ctx) {
      const t = SUP().get(ctx.params.id);
      if (!t) return C.layout('support', { title: 'Not found', body: `<div class="dash-card">${SA.ui.c.empty({ icon: 'message', title: 'This ticket doesn’t exist', action: '<a class="btn btn-primary" href="#/admin/support">All tickets</a>' })}</div>` });
      const cat = SUP().CATS[t.category];
      const against = t.against ? db().find('accounts', t.against) : null;
      const fromLink = t.from.kind === 'customer' ? `#/admin/customers?open=${encodeURIComponent(t.from.id)}` : t.from.kind === 'seller' ? `#/admin/sellers?open=${SA.services.sellers.byAccount(t.from.id)?.id || ''}` : '#/admin/partners';
      const [sc, sl] = TST[t.status];
      const body = `<div class="dash-grid g-2-1">
        <section class="dash-card"><div class="row-between wrap" style="--gap:8px"><div class="row wrap" style="--gap:6px"><span class="badge ${sc}">${sl}</span>${t.priority === 'high' ? '<span class="badge badge-danger">High priority</span>' : ''}<span class="badge">${esc(cat?.label || t.category)}</span></div><span class="xs muted">Opened ${timeAgo(t.createdAt)}</span></div>
          <ol class="adm-thread">${t.messages.map((m) => `<li class="by-${m.by}"><div class="adm-bubble"><span class="xs strong">${esc(m.name)}${m.by === 'admin' ? ' · Super App' : ''}</span><p class="small">${esc(m.text)}</p><span class="xs muted">${fmtDate(new Date(m.at))} · ${new Date(m.at).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })}</span></div></li>`).join('')}</ol>
          ${t.status !== 'resolved' ? `<form class="stack" style="--gap:10px;margin-top:16px" data-reply novalidate><label class="field-label" for="tk-r">Reply</label><textarea class="input" id="tk-r" name="text" rows="4" maxlength="2000" placeholder="Write to ${esc(t.from.name.split(' ')[0])}…"></textarea><p class="field-error" data-err="text"></p>
            <div class="row wrap" style="--gap:8px;justify-content:flex-end"><button class="btn btn-outline" type="submit" name="act" value="send">Send reply</button><button class="btn btn-primary" type="submit" name="act" value="resolve">${icon('check', 15, 2.6)} Send and resolve</button></div></form>`
            : `<div class="row-between wrap" style="margin-top:16px"><span class="small muted">${icon('check', 14, 2.4)} Resolved ${timeAgo(t.resolvedAt || t.updatedAt)}</span><button class="btn btn-outline btn-sm" data-reopen>Reopen</button></div>`}
        </section>
        <aside class="stack" style="--gap:18px">
          <section class="dash-card"><h2 class="h4">From</h2><p class="small" style="margin-top:8px"><b>${esc(t.from.name)}</b> <span class="badge ${FROM[t.from.kind]?.[0] || ''}">${FROM[t.from.kind]?.[1] || ''}</span></p>${t.from.phone ? `<p class="xs muted">${esc(t.from.phone)}</p>` : ''}<a class="btn btn-ghost btn-sm" href="${fromLink}" style="margin-top:8px">Open profile ${icon('arrowRight', 14)}</a></section>
          ${t.ref ? `<section class="dash-card"><h2 class="h4">About</h2><p class="small" style="margin-top:8px">${esc(t.ref.label || t.ref.id)}</p>${t.amount ? `<p class="xs muted">Paid: ${money(t.amount)}</p>` : ''}${against ? `<p class="xs muted" style="margin-top:6px">Involves: <b>${esc(against.company)}</b></p>` : ''}${t.ref.type === 'booking' && SA.services.dash.findBooking(t.ref.id) ? `<button class="btn btn-ghost btn-sm" data-open-booking="${esc(t.ref.id)}" style="margin-top:6px">Open booking</button>` : ''}</section>` : ''}
          ${cat?.dispute ? `<section class="dash-card"><h2 class="h4">Refund</h2>${t.refund ? `<p class="small" style="margin-top:8px;color:var(--turf)">${icon('check', 14, 2.4)} ${money(t.refund.amount)} refunded ${timeAgo(t.refund.at)} — paid by ${t.refund.bearer === 'partner' ? esc(against?.company || 'the partner') : 'Super App'}.</p>`
            : `<form class="stack" style="--gap:10px;margin-top:10px" data-refund novalidate><div class="field"><label for="rf-a">Amount (EGP)</label><input class="input num" id="rf-a" name="amount" type="number" min="1" ${t.amount ? `max="${t.amount}" value="${Math.round(t.amount / 2)}"` : ''} /></div>
              <fieldset class="field"><legend class="field-label">Who pays for it</legend><div class="seg"><label class="seg-opt"><input type="radio" name="bearer" value="platform" checked /><span>Super App</span></label>${against ? `<label class="seg-opt"><input type="radio" name="bearer" value="partner" /><span>${esc(against.company)}</span></label>` : ''}</div></fieldset>
              <div class="field"><label for="rf-n">Note (optional)</label><input class="input" id="rf-n" name="note" maxlength="140" /></div><p class="field-error" data-err="refund"></p>
              <button class="btn btn-blaze" type="submit">${icon('wallet', 15)} Send refund</button>
              <p class="xs muted">The customer gets it in their Super App wallet. If the partner or seller pays, it’s taken from their balance with this ticket as the reason.</p></form>`}</section>` : ''}
          <section class="dash-card"><h2 class="h4">Ticket</h2><div class="stack" style="--gap:10px;margin-top:10px">
            <label class="field"><span class="field-label">Priority</span><select class="select" data-priority><option value="normal" ${t.priority !== 'high' ? 'selected' : ''}>Normal</option><option value="high" ${t.priority === 'high' ? 'selected' : ''}>High</option></select></label>
            ${t.status !== 'resolved' ? '<button class="btn btn-outline btn-sm" data-resolve>Mark resolved without reply</button>' : ''}</div></section>
        </aside></div>`;
      return C.layout('support', { title: esc(t.subject), subtitle: `${esc(t.id)} · ${esc(cat?.label || '')}`, actions: '<a class="btn btn-ghost btn-sm" href="#/admin/support">All tickets</a>', body });
    },
    mount(el, ctx) {
      const id = ctx.params.id;
      el.querySelector('[data-reply]')?.addEventListener('submit', (e) => {
        e.preventDefault();
        const act = e.submitter?.value || 'send';
        try { AD().replyTicket(id, e.target.text.value, { resolve: act === 'resolve' }); toast(act === 'resolve' ? 'Reply sent and ticket resolved' : 'Reply sent'); SA.app.rerender(); }
        catch (err) { e.target.querySelector('[data-err="text"]').textContent = err.message; }
      });
      el.querySelector('[data-refund]')?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const f = SA.core.utils.formData(e.target);
        if (!(await SA.ui.overlay.confirm({ title: `Refund ${money(Math.round(+f.amount || 0))}?`, text: 'This can’t be undone from here.', confirmLabel: 'Send refund' }))) return;
        try { await AD().refundTicket(id, f); toast('Refund sent'); SA.app.rerender(); } catch (err) { e.target.querySelector('[data-err="refund"]').textContent = err.message; }
      });
      el.querySelector('[data-priority]')?.addEventListener('change', (e) => { AD().setTicket(id, { priority: e.target.value }); toast('Priority updated'); });
      el.querySelector('[data-resolve]')?.addEventListener('click', () => { AD().setTicket(id, { status: 'resolved' }); toast('Ticket resolved'); SA.app.rerender(); });
      el.querySelector('[data-reopen]')?.addEventListener('click', () => { AD().setTicket(id, { status: 'open' }); toast('Ticket reopened', 'info'); SA.app.rerender(); });
      el.querySelector('[data-open-booking]')?.addEventListener('click', (e) => SA.pages.dashBookings.openBooking(e.currentTarget.dataset.openBooking));
    },
  });
})(window.SA);
