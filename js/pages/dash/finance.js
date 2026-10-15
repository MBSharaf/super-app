(function (SA) {
  const { esc, money, fmtDate, fmtHour, isoDate, addDays, today, timeAgo } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const D = () => SA.services.dash;
  const pct = (x) => `${(x * 100).toFixed(1)}%`;
  const vatIn = (x) => D().vatPart(x);
  const vatPct = () => Math.round(D().vat() * 100);
  const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

  const perPitchRows = (s) => s.pitches.map((p) => {
    const L = s.ledger.filter((b) => b.pitchId === p.id);
    const live = L.filter((b) => b.status !== 'cancelled');
    const sum = (f) => live.reduce((t, b) => t + f(b), 0);
    return { p, n: live.length, hours: sum((b) => b.duration), gross: sum((b) => b.money.gross), commission: sum((b) => b.money.commission), net: sum((b) => b.money.net), online: sum((b) => b.money.online), cash: sum((b) => b.money.cash), refunds: L.reduce((t, b) => t + b.money.refund, 0) };
  });
  const productRows = (s) => {
    const m = new Map();
    s.sales.forEach((x) => { const r = m.get(x.productId) || { name: x.name, units: 0, gross: 0, commission: 0, net: 0 }; r.units += x.qty; r.gross += x.money.gross; r.commission += x.money.commission; r.net += x.money.net; m.set(x.productId, r); });
    return [...m.values()].sort((a, b) => b.net - a.net);
  };

  SA.pages.dashFinance = C.guard({
    title: 'Finance — Super App Partner',
    render(ctx) {
      const r = C.range(ctx.query, '30');
      const sc = C.scope();
      const s = D().summary({ ...r, ...sc });
      const rows = D().daily(s, r.from, r.to);
      const pp = perPitchRows(s);
      const prods = productRows(s);
      const total = s.net + s.storeNet;
      const admin = D().isAdmin();
      const fees = s.ledger.filter((b) => b.status !== 'cancelled' && b.source === 'app').reduce((t, b) => t + (b.fee || 0), 0);

      const body = `
        ${C.rangePicker(r, '/dash/finance')}
        ${admin && !sc.ownerId ? `<div class="platform-card"><div><span class="xs">Super App platform revenue · ${C.rangeLabel(r)}</span><b class="num" data-countup="${Math.round(s.commission + s.storeCommission + fees)}" data-suffix=" EGP">${money(Math.round(s.commission + s.storeCommission + fees))}</b></div>
          <div class="pc-split"><span><i style="background:#3ddc97"></i>Booking commission ${money(Math.round(s.commission))}</span><span><i style="background:#FFB48F"></i>Store commission ${money(Math.round(s.storeCommission))}</span><span><i style="background:#fff"></i>Service fees ${money(Math.round(fees))}</span></div></div>` : ''}
        <div class="money-split">
          <div class="ms-head"><span class="small strong">Your net earnings</span><b class="h2 num">${money(Math.round(total))}</b></div>
          <div class="ms-bar"><span class="ms-b" style="--w:${total ? (s.net / total) * 100 : 0}%"></span><span class="ms-s" style="--w:${total ? (s.storeNet / total) * 100 : 0}%"></span></div>
          <div class="legend xs"><span><i style="background:var(--turf)"></i>Pitch bookings ${money(Math.round(s.net))} (${total ? Math.round((s.net / total) * 100) : 0}%)</span><span><i style="background:var(--blaze)"></i>Store sales ${money(Math.round(s.storeNet))} (${total ? Math.round((s.storeNet / total) * 100) : 0}%)</span></div>
        </div>
        <div class="fin-cols">
          <section class="dash-card fin-block"><h2 class="h4">${icon('pitch', 18)} Pitch bookings</h2>
            <dl class="kv" style="margin-top:12px">
              <div><dt>Bookings</dt><dd class="num">${s.bookings} · ${s.hours} h</dd></div>
              <div><dt>Gross pitch revenue</dt><dd class="num">${money(Math.round(s.gross))}</dd></div>
              <div class="kv-sub"><dt>of which VAT ${vatPct()}% (you declare it)</dt><dd class="num">${money(Math.round(vatIn(s.gross)))}</dd></div>
              ${s.retained ? `<div class="kv-sub"><dt>includes late-cancellation income</dt><dd class="num">${money(Math.round(s.retained))}</dd></div>` : ''}
              <div><dt>Super App commission (${+(D().bookingRate(C.scope().ownerId || D().me().id) * 100).toFixed(1)}% on app bookings)</dt><dd class="num">−${money(Math.round(s.commission))}</dd></div>
              <div><dt>Refunded to customers</dt><dd class="num">${money(Math.round(s.refunds))}</dd></div>
              ${s.tourGross ? `<div class="kv-sub"><dt>includes tournament entry fees (${s.tourTeams} team${s.tourTeams === 1 ? '' : 's'})</dt><dd class="num">${money(Math.round(s.tourGross))}</dd></div>` : ''}
              <div class="total"><dt>Net from bookings</dt><dd class="num">${money(Math.round(s.net))}</dd></div>
            </dl>
            <h3 class="small strong" style="margin-top:18px">How it was collected</h3>
            <div class="collect"><div><span class="xs muted">Online (card + wallet)</span><b class="num">${money(Math.round(s.online))}</b><span class="meter"><span style="width:${s.gross ? (s.online / s.gross) * 100 : 0}%"></span></span></div>
              <div><span class="xs muted">Cash at the venue</span><b class="num">${money(Math.round(s.cash))}</b><span class="meter is-hot"><span style="width:${s.gross ? (s.cash / s.gross) * 100 : 0}%"></span></span></div></div>
          </section>
          <section class="dash-card fin-block"><h2 class="h4">${icon('bag', 18)} Store sales</h2>
            <dl class="kv" style="margin-top:12px">
              <div><dt>Orders · units</dt><dd class="num">${s.storeOrders} · ${s.storeUnits}</dd></div>
              <div><dt>Gross sales</dt><dd class="num">${money(Math.round(s.storeGross))}</dd></div>
              <div class="kv-sub"><dt>of which VAT ${vatPct()}%</dt><dd class="num">${money(Math.round(vatIn(s.storeGross)))}</dd></div>
              <div><dt>Super App commission</dt><dd class="num">−${money(Math.round(s.storeCommission))}</dd></div>
              <div class="total"><dt>Net from store</dt><dd class="num">${money(Math.round(s.storeNet))}</dd></div>
            </dl>
            <h3 class="small strong" style="margin-top:18px">Top products</h3>
            <ul class="top-prod">${prods.slice(0, 4).map((x) => `<li><span class="grow small truncate">${esc(x.name)}</span><span class="xs muted">${x.units} sold</span><b class="num small">${money(Math.round(x.net))}</b></li>`).join('') || `<li class="small muted">No store sales in this period. <a class="link" href="#/dash/store">Open or stock your store</a></li>`}</ul>
          </section>
        </div>
        <section class="dash-card"><div class="row-between"><h2 class="h4">Daily net earnings</h2><span class="xs muted">${C.rangeLabel(r)}</span></div>${C.barChart(rows)}</section>
        <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">By pitch</h2>
          <div class="table-wrap"><table class="table dash-table"><thead><tr><th>Pitch</th><th class="r">Bookings</th><th class="r">Hours</th><th class="r">Gross</th><th class="r">Commission</th><th class="r">Online</th><th class="r">Cash</th><th class="r">Net</th></tr></thead>
          <tbody>${s.tourGross ? `<tr><td><b>${icon('trophy', 14)} Tournament entry fees</b></td><td class="r num">${s.tourTeams} teams</td><td class="r num">—</td><td class="r num">${money(Math.round(s.tourGross))}</td><td class="r num">−${money(Math.round(s.tourGross - s.tourNet))}</td><td class="r num">${money(Math.round((s.tourLines || []).reduce((t, x) => t + x.online, 0)))}</td><td class="r num">${money(Math.round((s.tourLines || []).reduce((t, x) => t + x.cash, 0)))}</td><td class="r num strong">${money(Math.round(s.tourNet))}</td></tr>` : ''}${pp.map((x) => `<tr><td><b>${esc(x.p.name)}</b>${x.p.deleted ? ' <span class="badge">Removed</span>' : ''}</td><td class="r num">${x.n}</td><td class="r num">${x.hours}</td><td class="r num">${money(Math.round(x.gross))}</td><td class="r num">−${money(Math.round(x.commission))}</td><td class="r num">${money(Math.round(x.online))}</td><td class="r num">${money(Math.round(x.cash))}</td><td class="r num strong">${money(Math.round(x.net))}</td></tr>`).join('')}
            <tr class="tr-total"><td>Total</td><td class="r num">${s.bookings}</td><td class="r num">${s.hours}</td><td class="r num">${money(Math.round(s.gross))}</td><td class="r num">−${money(Math.round(s.commission))}</td><td class="r num">${money(Math.round(s.online))}</td><td class="r num">${money(Math.round(s.cash))}</td><td class="r num">${money(Math.round(s.net))}</td></tr></tbody></table></div></section>`;
      return C.layout('finance', { title: 'Finance', subtitle: `Every booking and sale, after Super App commission · ${C.rangeLabel(r)}`, actions: `<button class="btn btn-outline btn-sm" data-statement>${icon('file', 15)} Monthly statement (PDF)</button><button class="btn btn-primary btn-sm" data-export>${icon('arrowUpRight', 15)} Export finance report</button>`, body });
    },
    mount(el, ctx) {
      C.bindRange(el, '/dash/finance'); C.bindChart(el);
      el.querySelector('[data-statement]')?.addEventListener('click', () => {
        const sc = C.scope();
        const t = today();
        const months = Array.from({ length: 6 }, (_, i) => new Date(t.getFullYear(), t.getMonth() - i, 1)).filter((d) => { const s0 = new Date(SA.services.db.raw().platformSince); return d >= new Date(s0.getFullYear(), s0.getMonth(), 1); });
        SA.ui.overlay.open({
          title: 'Monthly statement', subtitle: 'A PDF with your earnings, VAT, Super App commission (as a tax invoice) and payouts for one month.',
          body: `<form id="stm" class="stack" style="--gap:10px" novalidate>${months.map((d, i) => `<label class="choice"><input type="radio" name="month" value="${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}" ${i === (months.length > 1 ? 1 : 0) ? 'checked' : ''} /><span class="grow"><b>${MONTHS[d.getMonth()]} ${d.getFullYear()}</b>${i === 0 ? '<span class="xs muted" style="display:block">Month to date</span>' : ''}</span>${icon('file', 16)}</label>`).join('')}</form>`,
          footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-primary" form="stm" type="submit">${icon('arrowUpRight', 15)} Download PDF</button>`,
          onMount(m, close) {
            m.querySelector('#stm').addEventListener('submit', async (e) => {
              e.preventDefault();
              const btn = m.querySelector('[form="stm"]'); btn.disabled = true; btn.textContent = 'Preparing…';
              try { await SA.ui.docs.statement(D().statement({ ownerId: sc.ownerId, month: e.target.month.value })); close(); }
              catch (err) { console.error(err); SA.ui.overlay.toast('Couldn’t create the PDF. Try again.', { type: 'error' }); btn.disabled = false; btn.textContent = 'Download PDF'; }
            });
          },
        });
      });
      el.querySelector('[data-export]')?.addEventListener('click', () => {
        const r = C.range(ctx.query, '30'); const sc = C.scope();
        const s = D().summary({ ...r, ...sc });
        const daily = D().daily(s, r.from, r.to);
        const pp = perPitchRows(s);
        const w = D().wallet(sc.ownerId ? { ownerId: sc.ownerId } : {});
        const who = sc.ownerId ? SA.services.db.find('accounts', sc.ownerId) : D().me();
        C.xlsx(`super-app-finance-${r.from}-to-${r.to}.xlsx`, [
          { name: 'Summary', title: `${who.company} — finance report ${r.from} to ${r.to}`, columns: [{ header: 'Item', width: 46 }, { header: 'Amount', width: 18, type: 'money' }], rows: [
            ['Pitch bookings — gross revenue (incl. VAT)', Math.round(s.gross)], [`Pitch bookings — VAT ${vatPct()}% included`, Math.round(vatIn(s.gross))], ['Pitch bookings — kept from late cancellations', Math.round(s.retained || 0)], ['Pitch bookings — Super App commission', -Math.round(s.commission)], ['Pitch bookings — net', Math.round(s.net)],
            ['Collected online (card + wallet)', Math.round(s.online)], ['Collected at the venue (cash)', Math.round(s.cash)], ['Refunded to customers', Math.round(s.refunds)],
            ['Store — gross sales (incl. VAT)', Math.round(s.storeGross)], [`Store — VAT ${vatPct()}% included`, Math.round(vatIn(s.storeGross))], ['Store — Super App commission', -Math.round(s.storeCommission)], ['Store — net', Math.round(s.storeNet)],
            [`Super App commission — VAT ${vatPct()}% included (reclaimable)`, Math.round(vatIn(s.commission + s.storeCommission))],
            ['Wallet — available to withdraw now', w.balance], ['Wallet — pending (bookings not yet played)', Math.round(w.bookings.pending)], ['Wallet — pending (orders not yet delivered)', Math.round(w.store.pending)],
          ], totals: ['Total net earnings (bookings + store)', Math.round(s.net + s.storeNet)] },
          { name: 'Daily', columns: [{ header: 'Date', width: 12 }, { header: 'Bookings', width: 10, type: 'number' }, { header: 'Hours', width: 8, type: 'number' }, { header: 'Bookings net', width: 15, type: 'money' }, { header: 'Store net', width: 14, type: 'money' }, { header: 'Total net', width: 14, type: 'money' }],
            rows: daily.map((d) => [d.date, d.bookings, d.hours, Math.round(d.bookingsNet), Math.round(d.storeNet), Math.round(d.bookingsNet + d.storeNet)]),
            totals: ['Total', s.bookings, s.hours, Math.round(s.net), Math.round(s.storeNet), Math.round(s.net + s.storeNet)] },
          { name: 'By pitch', columns: [{ header: 'Pitch', width: 28 }, { header: 'Bookings', width: 10, type: 'number' }, { header: 'Hours', width: 8, type: 'number' }, { header: 'Gross', width: 14, type: 'money' }, { header: 'Commission', width: 14, type: 'money' }, { header: 'Online', width: 14, type: 'money' }, { header: 'Cash', width: 14, type: 'money' }, { header: 'Refunds', width: 12, type: 'money' }, { header: 'Net', width: 14, type: 'money' }],
            rows: [...pp.map((x) => [x.p.name, x.n, x.hours, Math.round(x.gross), Math.round(x.commission), Math.round(x.online), Math.round(x.cash), Math.round(x.refunds), Math.round(x.net)]),
              ...(s.tourGross ? [['Tournament entry fees', s.tourTeams, 0, Math.round(s.tourGross), Math.round(s.tourGross - s.tourNet), Math.round((s.tourLines || []).reduce((t, x) => t + x.online, 0)), Math.round((s.tourLines || []).reduce((t, x) => t + x.cash, 0)), 0, Math.round(s.tourNet)]] : [])],
            totals: ['Total', s.bookings, s.hours, Math.round(s.gross), Math.round(s.commission), Math.round(s.online), Math.round(s.cash), Math.round(s.refunds), Math.round(s.net)] },
          ...SA.pages.dashBookings.bookingSheets(s.ledger, r),
          { name: 'Store sales', columns: [{ header: 'Order', width: 14 }, { header: 'Date', width: 12 }, { header: 'Product', width: 34 }, { header: 'Qty', width: 6, type: 'number' }, { header: 'Unit price', width: 12, type: 'money' }, { header: 'Customer', width: 20 }, { header: 'City', width: 12 }, { header: 'Payment', width: 16 }, { header: 'Status', width: 11 }, { header: 'Gross', width: 12, type: 'money' }, { header: 'Commission', width: 12, type: 'money' }, { header: 'Net', width: 12, type: 'money' }],
            rows: s.sales.map((x) => [x.orderId, isoDate(new Date(x.createdAt)), x.name, x.qty, x.price, x.customer?.name || '', x.customer?.city || '', x.payment, x.money.status, x.money.gross, x.money.commission, x.money.net]),
            totals: ['Total', '', `${s.storeOrders} orders`, s.storeUnits, '', '', '', '', '', Math.round(s.storeGross), Math.round(s.storeCommission), Math.round(s.storeNet)] },
          { name: 'Payouts', columns: [{ header: 'Payout ID', width: 22 }, { header: 'Date', width: 12 }, { header: 'Type', width: 18 }, { header: 'Method', width: 16 }, { header: 'Account', width: 26 }, { header: 'Status', width: 12 }, { header: 'Amount', width: 14, type: 'money' }],
            rows: w.payouts.map((p) => [p.id, isoDate(new Date(p.createdAt)), p.auto ? 'Weekly settlement' : 'Withdrawal request', p.method, p.detail, p.status, p.amount]),
            totals: ['Total', '', '', '', '', '', w.payouts.filter((p) => p.status !== 'rejected').reduce((t, p) => t + p.amount, 0)] },
        ]);
      });
    },
  });

  /* ======================= Wallet ======================= */
  SA.pages.dashWallet = C.guard({
    title: 'Wallet — Super App Partner',
    render(ctx) {
      const sc = C.scope();
      const w = D().wallet(sc.ownerId ? { ownerId: sc.ownerId } : {});
      const acct = w.account || D().me();
      const tab = ctx.query.tab || 'all';
      const since = addDays(today(), -29).getTime();
      // Daily earnings entries (last 30 days)
      const days = new Map();
      const add = (t, kind, amt) => { const k = isoDate(new Date(t)); const d = days.get(k) || { date: k, bookings: 0, store: 0, nb: 0, ns: 0 }; d[kind] += amt; d[kind === 'bookings' ? 'nb' : 'ns']++; days.set(k, d); };
      w.ledger.forEach((b) => { const end = b.startsAt + b.duration * 36e5; if (b.status !== 'cancelled' && end <= Date.now() && end >= since) add(end, 'bookings', b.money.wallet); });
      w.sales.forEach((x) => { const at = x.createdAt + 48 * 36e5; if (x.money.delivered && at >= since && at <= Date.now()) add(at, 'store', x.money.net); });
      const entries = [...days.values()].sort((a, b) => b.date.localeCompare(a.date));
      const bTot = w.bookings.available + w.bookings.pending, sTot = w.store.available + w.store.pending;
      const nextPayout = addDays(new Date(w.lastSettlement), 7);

      const body = `
        <div class="wallet-top">
          <div class="flip-card" data-flip tabindex="0" role="button" aria-label="Wallet card — press to see the breakdown">
            <div class="flip-inner">
              <div class="flip-face flip-front">
                <div class="row-between"><span class="xs fc-dim">Available to withdraw</span><span class="fc-chip"></span></div>
                <b class="fc-bal num" data-countup="${w.balance}" data-suffix=" EGP">${money(w.balance)}</b>
                <span class="xs fc-dim">Pending ${money(Math.round(w.bookings.pending + w.store.pending))} · next automatic payout ${fmtDate(nextPayout)}</span>
                <div class="row-between fc-foot"><span><span class="xs fc-dim">Paid to</span><b class="small" style="display:block">${esc(acct.payout?.method || 'Not set')} ${esc(acct.payout?.detail || '')}</b></span><span class="fc-brand">SUPER APP</span></div>
              </div>
              <div class="flip-face flip-back">
                <span class="xs fc-dim">Where your balance comes from</span>
                <dl class="kv kv-dark"><div><dt>Bookings — available</dt><dd class="num">${money(Math.round(w.bookings.available))}</dd></div><div><dt>Store — available</dt><dd class="num">${money(Math.round(w.store.available))}</dd></div><div><dt>Withdrawals in progress / paid</dt><dd class="num">−${money(Math.round(w.withdrawn))}</dd></div><div class="total"><dt>Available</dt><dd class="num">${money(w.balance)}</dd></div></dl>
                <span class="xs fc-dim">Tap to flip back</span>
              </div>
            </div>
          </div>
          <div class="pockets">
            <div class="pocket pk-b"><div class="row"><span class="pk-ico">${icon('pitch', 18)}</span><b>Bookings wallet</b></div>
              <b class="pk-amt num">${money(Math.round(w.bookings.available))}</b><span class="xs muted">available now</span>
              <dl class="kv" style="margin-top:12px"><div><dt>Pending (matches not played yet)</dt><dd class="num">${money(Math.round(w.bookings.pending))}</dd></div><div><dt>Commission on cash bookings</dt><dd class="num">−${money(Math.round(w.bookings.cashCommission))}</dd></div><div><dt>Settled to you so far</dt><dd class="num">${money(Math.round(w.bookings.settled))}</dd></div></dl></div>
            <div class="pocket pk-s"><div class="row"><span class="pk-ico">${icon('bag', 18)}</span><b>Store wallet</b></div>
              <b class="pk-amt num">${money(Math.round(w.store.available))}</b><span class="xs muted">available now</span>
              <dl class="kv" style="margin-top:12px"><div><dt>Pending (orders not delivered yet)</dt><dd class="num">${money(Math.round(w.store.pending))}</dd></div><div><dt>Settled to you so far</dt><dd class="num">${money(Math.round(w.store.settled))}</dd></div></dl></div>
          </div>
        </div>
        <div class="notice turf">${icon('info', 18)}<span>Online booking money becomes available after the match is played, and store money once the order is delivered. Everything available is paid out automatically every Monday — or withdraw it any time.</span></div>
        <div class="dash-grid g-1-1">
          <section class="dash-card"><div class="row-between"><h2 class="h4">Earnings — last 30 days</h2>
            <div class="tabs">${[['all', 'All'], ['bookings', 'Bookings'], ['store', 'Store']].map(([v, l]) => `<a class="tab" href="${SA.core.router.href('/dash/wallet', { tab: v })}" aria-selected="${tab === v}">${l}</a>`).join('')}</div></div>
            <ul class="earn-list">${entries.filter((d) => tab === 'all' || (tab === 'bookings' ? d.nb : d.ns)).map((d) => `<li><span class="el-date"><b class="num">${new Date(d.date).getDate()}</b><span class="xs">${SA.core.utils.MON[new Date(d.date).getMonth()]}</span></span>
              <span class="grow">${tab !== 'store' && d.nb ? `<span class="small"><i class="dot-b"></i>${d.nb} booking${d.nb > 1 ? 's' : ''} <b class="num">${money(Math.round(d.bookings))}</b></span>` : ''}${tab !== 'bookings' && d.ns ? `<span class="small" style="display:block"><i class="dot-s"></i>${d.ns} store sale${d.ns > 1 ? 's' : ''} <b class="num">${money(Math.round(d.store))}</b></span>` : ''}</span>
              <b class="num">+${money(Math.round(tab === 'bookings' ? d.bookings : tab === 'store' ? d.store : d.bookings + d.store))}</b></li>`).join('') || '<li class="small muted">No earnings in the last 30 days.</li>'}</ul></section>
          <section class="dash-card"><div class="row-between"><h2 class="h4">Payouts</h2><span class="xs muted">${w.payouts.length} total</span></div>
            ${w.adjustments?.length ? `<ul class="payout-list" style="margin-bottom:10px">${w.adjustments.slice().sort((x, y) => y.at - x.at).slice(0, 6).map((x) => `<li><span class="po-ico ${x.amount > 0 ? 'paid' : 'rejected'}">${icon(x.amount > 0 ? 'plus' : 'minus', 14, 2.4)}</span><span class="grow"><b class="small">Balance adjustment</b><span class="xs muted" style="display:block">${fmtDate(new Date(x.at))} · ${esc(x.reason)}</span></span><b class="num" style="color:${x.amount > 0 ? 'var(--turf)' : 'var(--danger)'}">${x.amount > 0 ? '+' : '−'}${money(Math.abs(x.amount))}</b></li>`).join('')}</ul>` : ''}
            <ul class="payout-list">${w.payouts.slice(0, 14).map((p) => `<li><span class="po-ico ${p.status}">${icon(p.status === 'paid' ? 'check' : p.status === 'rejected' ? 'x' : 'clock', 14, 2.4)}</span><span class="grow"><b class="small">${p.auto ? 'Weekly settlement' : 'Withdrawal'}</b><span class="xs muted" style="display:block">${fmtDate(new Date(p.createdAt))} · ${esc(p.method)} ${esc(p.detail)}</span></span><span style="text-align:right"><b class="num">${money(p.amount)}</b><span class="xs ${p.status === 'paid' ? '' : 'muted'}" style="display:block;color:${p.status === 'paid' ? 'var(--turf)' : ''}">${p.status === 'paid' ? 'Paid' : p.status === 'processing' ? 'Waiting for approval' : 'Rejected'}</span>${p.status === 'rejected' && p.reason ? `<span class="xs muted" style="display:block;max-width:180px">${esc(p.reason)}</span>` : ''}</span></li>`).join('') || '<li class="small muted">No payouts yet.</li>'}</ul></section>
        </div>`;
      return C.layout('wallet', { title: 'Wallet', subtitle: `${esc(acct.company)} · booking money and store money kept separate`, actions: `<button class="btn btn-outline btn-sm" data-export>${icon('arrowUpRight', 15)} Statement (Excel)</button><button class="btn btn-blaze btn-sm" data-withdraw ${w.balance < 100 ? 'disabled' : ''}>${icon('wallet', 15)} Withdraw</button>`, body });
    },
    mount(el, ctx) {
      const card = el.querySelector('[data-flip]');
      const flip = () => card.classList.toggle('is-flipped');
      card?.addEventListener('click', flip);
      card?.addEventListener('keydown', (e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); flip(); } });
      const sc = C.scope();
      el.querySelector('[data-withdraw]')?.addEventListener('click', () => {
        const w = D().wallet(sc.ownerId ? { ownerId: sc.ownerId } : {});
        const acct = w.account || D().me();
        SA.ui.overlay.open({
          title: 'Withdraw money', subtitle: `Up to ${money(w.balance)} available now`,
          body: `<form id="wd" novalidate class="stack" style="--gap:14px">
            <div class="field"><label for="wd-amt">Amount (EGP)</label><input class="input num" id="wd-amt" name="amount" type="number" min="100" max="${w.balance}" step="50" value="${w.balance}" /><div class="chips" style="margin-top:8px">${[0.25, 0.5, 1].map((f) => `<button type="button" class="chip" data-frac="${f}">${f === 1 ? 'Everything' : `${f * 100}%`}</button>`).join('')}</div><span class="field-error" data-err="amount"></span></div>
            <fieldset class="field"><legend class="field-label">Send to</legend><div class="seg">${['Bank transfer', 'InstaPay', 'Vodafone Cash'].map((m) => `<label class="seg-opt"><input type="radio" name="method" value="${m}" ${(acct.payout?.method || 'Bank transfer') === m ? 'checked' : ''} /><span>${m}</span></label>`).join('')}</div><span class="field-error" data-err="method"></span></fieldset>
            <div class="field"><label for="wd-det">Account details</label><input class="input" id="wd-det" name="detail" value="${esc(acct.payout?.detail || '')}" placeholder="IBAN, InstaPay address or wallet number" /><span class="field-error" data-err="detail"></span></div>
            <p class="xs muted">Withdrawals are processed within one working day. Demo: no real transfer is made.</p></form>`,
          footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-blaze" form="wd" type="submit">Request withdrawal</button>`,
          onMount(m, close) {
            const f = m.querySelector('#wd');
            m.querySelectorAll('[data-frac]').forEach((b) => b.addEventListener('click', () => (f.amount.value = Math.floor(w.balance * +b.dataset.frac))));
            f.addEventListener('submit', (e) => {
              e.preventDefault();
              const d = SA.core.utils.formData(f);
              try { D().requestPayout(d, sc.ownerId); close(); SA.core.motion.confetti({ count: 50, spread: 0.6 }); SA.ui.overlay.toast(`Withdrawal of ${money(+d.amount)} requested`); SA.app.rerender(); }
              catch (err) { if (err.details) C.showErrors(f, err.details); else SA.ui.overlay.toast(err.message, { type: 'error' }); }
            });
          },
        });
      });
      el.querySelector('[data-export]')?.addEventListener('click', () => {
        const w = D().wallet(sc.ownerId ? { ownerId: sc.ownerId } : {});
        const acct = w.account || D().me();
        const cut = w.lastSettlement;
        const avB = w.ledger.filter((b) => b.status !== 'cancelled' && b.startsAt + b.duration * 36e5 >= cut && b.startsAt + b.duration * 36e5 <= Date.now());
        const avS = w.sales.filter((x) => x.money.delivered && x.createdAt + 48 * 36e5 >= cut);
        C.xlsx(`super-app-wallet-statement-${isoDate(today())}.xlsx`, [
          { name: 'Wallet', title: `${acct.company} — wallet statement ${isoDate(today())}`, columns: [{ header: 'Item', width: 46 }, { header: 'Amount', width: 18, type: 'money' }], rows: [
            ['Bookings wallet — available', Math.round(w.bookings.available)], ['Bookings wallet — pending', Math.round(w.bookings.pending)], ['Bookings wallet — settled to date', Math.round(w.bookings.settled)],
            ['Store wallet — available', Math.round(w.store.available)], ['Store wallet — pending', Math.round(w.store.pending)], ['Store wallet — settled to date', Math.round(w.store.settled)],
            ['Withdrawals requested', -Math.round(w.withdrawn)]], totals: ['Available to withdraw', w.balance] },
          { name: 'Available — bookings', columns: [{ header: 'Booking ID', width: 22 }, { header: 'Date', width: 12 }, { header: 'Start', width: 9 }, { header: 'Pitch', width: 26 }, { header: 'Payment', width: 15 }, { header: 'Wallet effect', width: 15, type: 'money' }],
            rows: avB.map((b) => [b.id, b.date, fmtHour(b.hour), b.pitchName, b.payment, b.money.wallet]), totals: ['Total', '', '', '', '', Math.round(w.bookings.available)] },
          { name: 'Available — store', columns: [{ header: 'Order', width: 14 }, { header: 'Date', width: 12 }, { header: 'Product', width: 34 }, { header: 'Qty', width: 6, type: 'number' }, { header: 'Net', width: 14, type: 'money' }],
            rows: avS.map((x) => [x.orderId, isoDate(new Date(x.createdAt)), x.name, x.qty, x.money.net]), totals: ['Total', '', '', avS.reduce((t, x) => t + x.qty, 0), Math.round(w.store.available)] },
          { name: 'Payouts', columns: [{ header: 'Payout ID', width: 22 }, { header: 'Date', width: 12 }, { header: 'Type', width: 18 }, { header: 'Method', width: 16 }, { header: 'Account', width: 26 }, { header: 'Status', width: 12 }, { header: 'Amount', width: 14, type: 'money' }],
            rows: w.payouts.map((p) => [p.id, isoDate(new Date(p.createdAt)), p.auto ? 'Weekly settlement' : 'Withdrawal request', p.method, p.detail, p.status, p.amount]) },
        ]);
      });
    },
  });
})(window.SA);
