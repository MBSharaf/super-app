(function (SA) {
  const { esc, money, fmtHour, fmtDate, fmtRange, timeAgo } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const D = () => SA.services.dash;
  const PER_PAGE = 25;

  const filterRows = (rows, q) => rows.filter((b) =>
    (!q.status || b.status === q.status) && (!q.source || b.source === q.source) && (!q.pitch || b.pitchId === q.pitch) &&
    (!q.q || `${b.id} ${b.customer.name} ${b.customer.phone} ${b.pitchName}`.toLowerCase().includes(q.q.toLowerCase())));

  /** Booking detail drawer, shared with Availability */
  const openBooking = (id) => {
    const b = D().findBooking(id);
    if (!b) return SA.ui.overlay.toast('Booking not found.', { type: 'error' });
    const canAct = !['cancelled', 'completed', 'no-show'].includes(b.status);
    SA.ui.overlay.open({
      drawer: true, title: `Booking ${esc(b.id)}`, subtitle: `${esc(b.pitchName)} · created ${timeAgo(b.createdAt)}`,
      body: `<div class="bk-detail">
        <div class="row-between">${C.statusBadge(b.status)}<span class="badge">${esc(b.sourceLabel)}</span></div>
        ${b.checkedInAt ? `<p class="small" style="color:var(--turf)">${icon('check', 14, 2.4)} Checked in at ${new Date(b.checkedInAt).toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' })}</p>` : ''}
        ${b.seriesId ? `<p class="small">${icon('repeat', 14)} ${b.source === 'team' ? 'Team booking' : 'Weekly booking'} · game ${b.sessionNo || '—'} <a class="link xs" href="#/dash/teams">Teams</a></p>` : ''}
        <div class="bk-when"><b class="h3 num">${fmtRange(b.hour, b.duration)}</b><span class="soft">${fmtDate(b.date, 'long')}</span></div>
        <dl class="kv">
          <div><dt>Customer</dt><dd>${esc(b.customer.name)}</dd></div>
          <div><dt>Phone</dt><dd class="num">${esc(b.customer.phone || '—')}</dd></div>
          <div><dt>Pitch</dt><dd>${esc(b.pitchName)}</dd></div>
          <div><dt>Payment</dt><dd>${esc(b.payment)}</dd></div>
        </dl>
        <h3 class="small strong" style="margin-top:18px">Money</h3>
        <dl class="kv" style="margin-top:8px">
          <div><dt>Pitch price</dt><dd class="num">${money(b.amount)}</dd></div>
          ${b.discountAmount || b.discount ? `<div><dt>Includes weekly discount</dt><dd class="num">${Math.round((b.discount || 0) * 100)}%</dd></div>` : ''}
          <div><dt>Super App commission (${b.source === 'app' ? Math.round(D().commission().bookings * 100) : '0'}%)</dt><dd class="num">−${money(b.money.commission)}</dd></div>
          ${b.status === 'cancelled' ? `<div><dt>Refunded to customer</dt><dd class="num" style="color:var(--danger)">${money(b.money.refund)}</dd></div>` : ''}
          ${b.money.retained ? `<div><dt>Kept under the cancellation policy</dt><dd class="num">${money(b.money.gross)}</dd></div>` : ''}
          <div class="total"><dt>Your earnings</dt><dd class="num">${money(b.money.net)}</dd></div>
        </dl>
        <p class="xs muted" style="margin-top:8px">${b.money.retained ? 'Late cancellation — the part the policy didn’t refund is yours.' : b.money.online ? 'Paid online — added to your wallet after the match.' : b.status === 'cancelled' ? '' : b.source === 'app' ? 'Paid at the venue — you collected the cash; the commission is deducted from your wallet.' : 'Paid at the venue — no Super App commission.'}</p>
        <p class="xs muted">${icon('shield', 12)} ${esc(SA.services.pitches.policyOf(b.policy).name)} cancellation policy — ${esc(SA.services.pitches.policyOf(b.policy).summary)}</p>
        <button class="btn btn-outline btn-sm" data-invoice style="margin-top:12px">${icon('file', 14)} Invoice (PDF)</button>
      </div>`,
      footer: canAct ? `<div class="row wrap" style="width:100%;--gap:8px">
          ${b.status === 'upcoming' || b.status === 'in-progress' ? `<button class="btn btn-turf btn-sm" data-act="check-in">${icon('check', 14)} Check in</button>` : ''}
          ${b.status !== 'upcoming' ? `<button class="btn btn-outline btn-sm" data-act="no-show">Mark no-show</button>` : ''}
          <button class="btn btn-ghost btn-sm" data-act="cancel" style="color:var(--danger);margin-left:auto">Cancel & refund</button></div>` : '',
      onMount(m, close) {
        m.querySelector('[data-invoice]')?.addEventListener('click', async (e) => { e.currentTarget.disabled = true; await SA.ui.docs.bookingInvoice({ ...b, total: b.amount + (b.fee || 0) }); e.currentTarget.disabled = false; });
        m.querySelectorAll('[data-act]').forEach((btn) => btn.addEventListener('click', async () => {
          const act = btn.dataset.act;
          if (act === 'cancel' && !(await SA.ui.overlay.confirm({ title: 'Cancel this booking?', text: `${esc(b.customer.name)} gets a full refund of ${money(b.amount + (b.fee || 0))} and the time becomes bookable again.`, confirmLabel: 'Cancel booking', cancelLabel: 'Keep booking', danger: true }))) return;
          await D().bookingAction(b.id, act);
          close();
          SA.ui.overlay.toast({ 'check-in': 'Checked in', 'no-show': 'Marked as no-show', cancel: 'Booking cancelled and refunded' }[act]);
          SA.app.rerender();
        }));
      },
    });
  };

  const bookingSheets = (rows, r) => {
    const live = rows.filter((b) => b.status !== 'cancelled');
    const sum = (f) => live.reduce((t, b) => t + f(b), 0);
    return [
      { name: 'Bookings', title: `Bookings ${r.from} to ${r.to}`, columns: [
        { header: 'Booking ID', width: 20 }, { header: 'Date', width: 12 }, { header: 'Start', width: 10 }, { header: 'Hours', width: 8, type: 'number' }, { header: 'Pitch', width: 26 },
        { header: 'Customer', width: 22 }, { header: 'Phone', width: 18 }, { header: 'Source', width: 11 }, { header: 'Payment', width: 15 }, { header: 'Status', width: 12 },
        { header: 'Pitch price', width: 13, type: 'money' }, { header: 'Commission', width: 13, type: 'money' }, { header: 'Net earnings', width: 14, type: 'money' }, { header: 'Refund', width: 12, type: 'money' }],
        rows: rows.map((b) => [b.id, b.date, fmtHour(b.hour), b.duration, b.pitchName, b.customer.name, b.customer.phone || '', b.sourceLabel, b.payment, C.STATUS[b.status]?.[1] || b.status,
          b.status === 'cancelled' ? 0 : b.amount, b.money.commission, b.money.net, b.money.refund]),
        totals: ['Total', '', '', sum((b) => b.duration), `${live.length} bookings`, '', '', '', '', '', sum((b) => b.amount), sum((b) => b.money.commission), sum((b) => b.money.net), rows.reduce((t, b) => t + b.money.refund, 0)] },
    ];
  };

  SA.pages.dashBookings = C.guard({
    title: 'Bookings — Super App Partner',
    openBooking, bookingSheets, filterRows,
    render(ctx) {
      const q = ctx.query;
      const r = C.range(q, '30');
      const sc = C.scope();
      const pitches = D().myPitches({ ...sc, includeDeleted: true });
      const all = D().ledger({ ...r, pitchIds: pitches.filter((p) => !sc.ownerId || p.ownerId === sc.ownerId).map((p) => p.id) });
      const rows = filterRows(all, q);
      const live = rows.filter((b) => b.status !== 'cancelled');
      const page = Math.max(1, +q.page || 1), pages = Math.max(1, Math.ceil(rows.length / PER_PAGE));
      const slice = rows.slice((page - 1) * PER_PAGE, page * PER_PAGE);
      const href = (patch) => SA.core.router.href('/dash/bookings', { ...q, ...patch });
      const sel = (name, opts) => `<select class="select" name="${name}" data-filter>${opts.map(([v, l]) => `<option value="${v}" ${(q[name] || '') === v ? 'selected' : ''}>${l}</option>`).join('')}</select>`;

      const body = `
        ${C.rangePicker(r, '/dash/bookings')}
        <div class="filters-bar">
          <div class="input-icon grow">${icon('search', 17)}<input class="input" type="search" name="q" value="${esc(q.q || '')}" placeholder="Search booking ID, customer, phone" data-search /></div>
          ${sel('pitch', [['', 'All pitches'], ...pitches.map((p) => [p.id, p.name + (p.deleted ? ' (removed)' : '')])])}
          ${sel('status', [['', 'All statuses'], ...Object.entries(C.STATUS).map(([k, [, l]]) => [k, l])])}
          ${sel('source', [['', 'All sources'], ['app', 'Super App'], ['walk-in', 'Walk-in'], ['team', 'Team booking']])}
        </div>
        <div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'Bookings', value: live.length, count: live.length, sub: `${rows.length - live.length} cancelled`, ic: 'ticket' })}
          ${C.kpi({ label: 'Hours booked', value: live.reduce((t, b) => t + b.duration, 0), ic: 'clock' })}
          ${C.kpi({ label: 'Pitch revenue', value: money(live.reduce((t, b) => t + b.amount, 0)), ic: 'percent', tone: 'k-turf' })}
          ${C.kpi({ label: 'Your net', value: money(live.reduce((t, b) => t + b.money.net, 0)), ic: 'wallet', tone: 'k-ink' })}
        </div>
        <section class="dash-card table-card">
          <div class="table-wrap"><table class="table dash-table">
            <thead><tr><th>Booking</th><th>When</th><th>Pitch</th><th>Customer</th><th>Payment</th><th>Status</th><th class="r">Price</th><th class="r">Net</th></tr></thead>
            <tbody>${slice.map((b) => `<tr class="clickable" data-booking="${b.id}" tabindex="0">
              <td><b class="num small">${esc(b.id.length > 18 ? b.id.slice(0, 18) + '…' : b.id)}</b><span class="xs muted" style="display:block">${esc(b.sourceLabel)}</span></td>
              <td class="num">${fmtDate(b.date)}<span class="xs muted" style="display:block">${fmtRange(b.hour, b.duration)}</span></td>
              <td>${esc(b.pitchName)}</td><td>${esc(b.customer.name)}<span class="xs muted" style="display:block">${esc(b.customer.phone || '')}</span></td>
              <td class="small">${esc(b.payment)}</td><td>${C.statusBadge(b.status)}</td>
              <td class="r num">${b.status === 'cancelled' ? `<s class="muted">${money(b.amount)}</s>` : money(b.amount)}</td><td class="r num strong">${money(b.money.net)}</td></tr>`).join('') || `<tr><td colspan="8">${SA.ui.c.empty({ icon: 'ticket', title: 'No bookings match', text: 'Try a wider date range or clear the filters.' })}</td></tr>`}</tbody>
          </table></div>
          ${pages > 1 ? `<div class="pager"><span class="small muted">${(page - 1) * PER_PAGE + 1}–${Math.min(rows.length, page * PER_PAGE)} of ${rows.length}</span>
            <div class="row" style="--gap:6px"><a class="btn btn-outline btn-sm ${page <= 1 ? 'is-disabled' : ''}" href="${href({ page: page - 1 })}">${icon('chevronLeft', 14)} Previous</a><a class="btn btn-outline btn-sm ${page >= pages ? 'is-disabled' : ''}" href="${href({ page: page + 1 })}">Next ${icon('chevronRight', 14)}</a></div></div>` : ''}
        </section>`;
      return C.layout('bookings', { title: 'Bookings', subtitle: `${rows.length} booking${rows.length === 1 ? '' : 's'} · ${C.rangeLabel(r)}`, actions: `<button class="btn btn-primary btn-sm" data-export>${icon('arrowUpRight', 15)} Export to Excel</button>`, body });
    },
    mount(el, ctx) {
      const q = ctx.query;
      C.bindRange(el, '/dash/bookings', { pitch: q.pitch, status: q.status, source: q.source, q: q.q });
      const go = (patch) => SA.core.router.go(SA.core.router.href('/dash/bookings', { ...q, ...patch, page: undefined }).slice(1));
      el.querySelectorAll('[data-filter]').forEach((s) => s.addEventListener('change', () => go({ [s.name]: s.value || undefined })));
      el.querySelector('[data-search]')?.addEventListener('keydown', (e) => { if (e.key === 'Enter') go({ q: e.target.value.trim() || undefined }); });
      el.querySelector('[data-search]')?.addEventListener('search', (e) => { if (!e.target.value) go({ q: undefined }); });
      el.addEventListener('click', (e) => { const row = e.target.closest('[data-booking]'); if (row) openBooking(row.dataset.booking); });
      el.addEventListener('keydown', (e) => { const row = e.target.closest('[data-booking]'); if (row && e.key === 'Enter') openBooking(row.dataset.booking); });
      el.querySelector('[data-export]')?.addEventListener('click', () => {
        const r = C.range(q, '30'); const sc = C.scope();
        const pitches = D().myPitches({ ...sc, includeDeleted: true });
        const rows = filterRows(D().ledger({ ...r, pitchIds: pitches.map((p) => p.id) }), q);
        C.xlsx(`super-app-bookings-${r.from}-to-${r.to}.xlsx`, bookingSheets(rows, r));
      });
    },
  });
})(window.SA);
