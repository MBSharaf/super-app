/** Admin console: advanced platform analytics and a full Excel export. */
(function (SA) {
  const { esc, money, isoDate, parseISO, addDays } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const A = SA.pages.adminCommon;
  const AD = () => SA.services.admin;
  const D = () => SA.services.dash;
  const db = () => SA.services.db;

  const VERT = [['b', 'Pitch bookings', 'turf'], ['s', 'Shop', 'blaze'], ['p', 'Printed items', 'gold'], ['f', 'Fantasy Pass', 'violet']];
  const group = (arr, key, val = () => 1) => { const m = new Map(); arr.forEach((x) => { const k = key(x); if (k == null) return; m.set(k, (m.get(k) || 0) + val(x)); }); return [...m.entries()].sort((a, b) => b[1] - a[1]); };
  const bars = (rows, fmt = (v) => v.toLocaleString(), tone = 'var(--turf)') => { const max = Math.max(1, ...rows.map((r) => r[1])); return `<ul class="adm-mix">${rows.map(([l, v]) => `<li><span class="row-between small"><span>${esc(l)}</span><b class="num">${fmt(v)}</b></span><span class="meter"><span style="width:${Math.max(2, (v / max) * 100)}%;background:${tone}"></span></span></li>`).join('')}</ul>`; };

  /** Everything the page and the export need for a date range */
  const build = (r) => {
    const sum = D().summary({ from: r.from, to: r.to });
    const inR = (t) => { const d = isoDate(new Date(t)); return d >= r.from && d <= r.to; };
    const pod = (db().all('podOrders') || []).filter((o) => !o.sample && inR(o.placedAt));
    const passes = AD().passSales(r.from, r.to);
    const daily = new Map(); for (let d = parseISO(r.from); d <= parseISO(r.to); d = addDays(d, 1)) daily.set(isoDate(d), { date: isoDate(d), b: 0, s: 0, p: 0, f: 0 });
    sum.ledger.forEach((b) => { const x = daily.get(b.date); if (x) x.b += b.money.gross; });
    (sum.tourLines || []).forEach((t) => { const x = daily.get(t.date); if (x) x.b += t.gross; });
    sum.sales.forEach((s) => { const x = daily.get(isoDate(new Date(s.createdAt))); if (x) x.s += s.money.gross; });
    pod.forEach((o) => { if (['refused', 'cancelled'].includes(o.status)) return; const x = daily.get(isoDate(new Date(o.placedAt))); if (x) x.p += o.price * (o.qty || 1); });
    passes.forEach((p) => { const x = daily.get(isoDate(new Date(p.at))); if (x) x.f += p.amount; });
    const rows = [...daily.values()].map((x) => ({ ...x, income: x.b + x.s + x.p + x.f }));
    const active = sum.ledger.filter((b) => b.status !== 'cancelled');
    const byPitch = sum.pitches.map((p) => {
      const L = sum.ledger.filter((b) => b.pitchId === p.id), act = L.filter((b) => b.status !== 'cancelled');
      const days = Math.round((parseISO(r.to) - parseISO(r.from)) / 864e5) + 1, cap = Math.max(1, (p.open[1] - p.open[0]) * days);
      return { p, n: act.length, gross: L.reduce((t, b) => t + b.money.gross, 0), occ: act.reduce((t, b) => t + b.duration, 0) / cap, cancel: L.length ? (L.length - act.length) / L.length : 0, owner: db().find('accounts', p.ownerId)?.company || '' };
    }).sort((a, b) => b.gross - a.gross);
    const products = group(sum.sales, (s) => s.name, (s) => s.money.gross).slice(0, 8);
    const designs = group(pod.filter((o) => o.status !== 'refused'), (o) => o.title, (o) => o.qty || 1).slice(0, 8);
    const cities = group([...pod.map((o) => o.customer?.city), ...sum.sales.map((s) => s.customer?.city)].filter(Boolean), (c) => c).slice(0, 8);
    const payB = group(active.filter((b) => b.source === 'app'), (b) => b.payment);
    const payS = group(sum.sales, (s) => s.payment);
    const sources = group(active, (b) => b.sourceLabel);
    const custNames = [...active.filter((b) => b.source === 'app').map((b) => b.customer?.name), ...sum.sales.map((s) => s.customer?.name), ...pod.map((o) => o.customer?.name)].filter(Boolean);
    const counts = group(custNames, (n) => n);
    const repeat = counts.filter(([, n]) => n > 1).length;
    const tot = { gmv: rows.reduce((t, x) => t + x.income, 0), bookings: active.length, hours: active.reduce((t, b) => t + b.duration, 0), shopOrders: new Set(sum.sales.map((s) => s.orderId)).size, units: sum.sales.reduce((t, s) => t + s.qty, 0),
      printed: pod.filter((o) => o.status !== 'refused' && o.status !== 'cancelled').reduce((t, o) => t + (o.qty || 1), 0), refused: pod.filter((o) => o.status === 'refused').length, passes: passes.length, customers: counts.length, repeat,
      cancelRate: sum.ledger.length ? (sum.ledger.length - active.length) / sum.ledger.length : 0, noShows: sum.ledger.filter((b) => b.status === 'no-show').length, avgBooking: active.length ? active.reduce((t, b) => t + b.money.gross, 0) / active.length : 0 };
    return { sum, rows, byPitch, products, designs, cities, payB, payS, sources, tot, pod, passes };
  };

  SA.pages.adminAnalytics = A.guard({
    title: 'Analytics — Super App Admin',
    render(ctx) {
      const r = C.range(ctx.query, '30');
      const x = build(r), t = x.tot;
      const pct = (v) => `${(v * 100).toFixed(1)}%`;
      const body = `${C.rangePicker(r, '/admin/analytics')}
        <div class="kpi-grid">
          ${C.kpi({ label: 'Money through Super App', value: money(Math.round(t.gmv)), ic: 'trend', tone: 'k-turf', sub: `${C.rangeLabel(r)}` })}
          ${C.kpi({ label: 'Bookings', value: t.bookings.toLocaleString(), ic: 'ticket', sub: `${t.hours.toLocaleString()} hours · ${pct(x.sum.occupancy)} occupancy` })}
          ${C.kpi({ label: 'Shop orders', value: t.shopOrders.toLocaleString(), ic: 'bag', sub: `${t.units.toLocaleString()} items · ${t.printed} printed` })}
          ${C.kpi({ label: 'Customers', value: t.customers.toLocaleString(), ic: 'users', sub: `${t.customers ? Math.round((t.repeat / t.customers) * 100) : 0}% came back more than once` })}
        </div>
        <section class="dash-card"><div class="row-between"><h2 class="h4">Money by day and by product</h2><span class="xs muted">${money(Math.round(t.gmv))}</span></div><div style="margin-top:14px">${A.revChart(x.rows, VERT)}</div></section>
        <div class="dash-grid g-2-1">
          <section class="dash-card"><h2 class="h4">When people play</h2><p class="xs muted" style="margin-top:4px">All pitches · share of open hours booked</p><div style="margin-top:14px">${C.heatmap(D().heatmap(x.sum))}</div></section>
          <section class="dash-card"><h2 class="h4">Bookings at a glance</h2><dl class="kv" style="margin-top:12px"><div><dt>Average booking</dt><dd class="num">${money(Math.round(t.avgBooking))}</dd></div><div><dt>Cancelled</dt><dd class="num">${pct(t.cancelRate)}</dd></div><div><dt>No-shows</dt><dd class="num">${t.noShows}</dd></div><div><dt>Refused print deliveries</dt><dd class="num">${t.refused}</dd></div><div><dt>Fantasy passes</dt><dd class="num">${t.passes}</dd></div></dl>
            <h3 class="small strong" style="margin-top:16px">Where bookings come from</h3>${bars(x.sources)}</section>
        </div>
        <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Pitches</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Pitch</th><th>Owner</th><th class="r">Bookings</th><th class="r">Occupancy</th><th class="r">Cancelled</th><th class="r">Revenue</th></tr></thead><tbody>
          ${x.byPitch.map((p) => `<tr><td class="small"><b>${esc(p.p.name)}</b><span class="xs muted" style="display:block">${esc(p.p.area)} · ${esc(p.p.format)}</span></td><td class="small">${esc(p.owner)}</td><td class="r num">${p.n}</td><td class="r"><span class="adm-occ"><span class="meter"><span style="width:${Math.min(100, p.occ * 100)}%"></span></span><b class="num small">${pct(p.occ)}</b></span></td><td class="r num">${pct(p.cancel)}</td><td class="r num strong">${money(Math.round(p.gross))}</td></tr>`).join('')}
        </tbody></table></div></section>
        <div class="dash-grid g-1-1">
          <section class="dash-card"><h2 class="h4">Best-selling products</h2>${x.products.length ? bars(x.products, (v) => money(Math.round(v)), 'var(--blaze)') : '<p class="small muted">No sales in this period.</p>'}</section>
          <section class="dash-card"><h2 class="h4">Best-selling designs</h2>${x.designs.length ? bars(x.designs, (v) => `${v} sold`, 'var(--gold)') : '<p class="small muted">No printed items in this period.</p>'}</section>
          <section class="dash-card"><h2 class="h4">Where orders go</h2>${x.cities.length ? bars(x.cities, (v) => `${v} orders`, 'var(--info)') : '<p class="small muted">No deliveries in this period.</p>'}</section>
          <section class="dash-card"><h2 class="h4">How people pay</h2><h3 class="xs muted" style="margin-top:8px">Bookings</h3>${bars(x.payB)}<h3 class="xs muted" style="margin-top:12px">Shop</h3>${bars(x.payS, undefined, 'var(--blaze)')}</section>
        </div>`;
      return C.layout('analytics', { title: 'Analytics', subtitle: 'Bookings, shop, printed items, customers and payments across Super App', actions: `<button class="btn btn-primary btn-sm" data-export>${icon('file', 15)} Export everything (Excel)</button>`, body });
    },
    mount(el, ctx) {
      C.bindRange(el, '/admin/analytics');
      A.bindRevChart(el);
      el.querySelector('[data-export]')?.addEventListener('click', () => {
        const r = C.range(ctx.query, '30');
        const x = build(r), t = x.tot;
        const money2 = (v) => Math.round(v);
        C.xlsx(`super-app-analytics-${r.from}-to-${r.to}.xlsx`, [
          { name: 'Summary', columns: [{ header: 'Measure', width: 36 }, { header: 'Value', width: 18 }], rows: [['Period', `${r.from} to ${r.to}`], ['Money through Super App (EGP)', money2(t.gmv)], ['Bookings', t.bookings], ['Hours booked', t.hours], ['Occupancy', `${(x.sum.occupancy * 100).toFixed(1)}%`], ['Average booking (EGP)', money2(t.avgBooking)], ['Cancelled bookings', `${(t.cancelRate * 100).toFixed(1)}%`], ['No-shows', t.noShows], ['Shop orders', t.shopOrders], ['Shop items', t.units], ['Printed items', t.printed], ['Refused print deliveries', t.refused], ['Fantasy passes', t.passes], ['Customers', t.customers], ['Repeat customers', t.repeat]] },
          { name: 'Daily', columns: [{ header: 'Date', width: 12 }, ...VERT.map(([, l]) => ({ header: l, width: 16, type: 'money' })), { header: 'Total', width: 16, type: 'money' }], rows: x.rows.map((d) => [d.date, money2(d.b), money2(d.s), money2(d.p), money2(d.f), money2(d.income)]), totals: ['Total', ...['b', 's', 'p', 'f'].map((k) => money2(x.rows.reduce((s, d) => s + d[k], 0))), money2(t.gmv)] },
          { name: 'Pitches', columns: [{ header: 'Pitch', width: 28 }, { header: 'Owner', width: 24 }, { header: 'Area', width: 16 }, { header: 'Bookings', width: 10, type: 'number' }, { header: 'Occupancy %', width: 12 }, { header: 'Cancelled %', width: 12 }, { header: 'Revenue', width: 14, type: 'money' }], rows: x.byPitch.map((p) => [p.p.name, p.owner, p.p.area, p.n, (p.occ * 100).toFixed(1), (p.cancel * 100).toFixed(1), money2(p.gross)]) },
          { name: 'Bookings', columns: [{ header: 'Booking', width: 26 }, { header: 'Date', width: 12 }, { header: 'Hour', width: 8, type: 'number' }, { header: 'Pitch', width: 26 }, { header: 'Customer', width: 22 }, { header: 'Source', width: 18 }, { header: 'Payment', width: 14 }, { header: 'Status', width: 12 }, { header: 'Amount', width: 12, type: 'money' }, { header: 'Commission', width: 12, type: 'money' }],
            rows: x.sum.ledger.map((b) => [b.id, b.date, b.hour, b.pitchName, b.customer?.name || '', b.sourceLabel, b.payment, b.status, b.amount, b.money.commission]) },
          { name: 'Shop sales', columns: [{ header: 'Order', width: 14 }, { header: 'Date', width: 12 }, { header: 'Store', width: 22 }, { header: 'Product', width: 32 }, { header: 'Qty', width: 6, type: 'number' }, { header: 'City', width: 14 }, { header: 'Payment', width: 16 }, { header: 'Status', width: 11 }, { header: 'Gross', width: 12, type: 'money' }],
            rows: x.sum.sales.map((s) => [s.orderId, isoDate(new Date(s.createdAt)), db().find('stores', s.storeId)?.name || '', s.name, s.qty, s.customer?.city || '', s.payment, s.money.status, s.money.gross]) },
          { name: 'Printed items', columns: [{ header: 'Item', width: 14 }, { header: 'Date', width: 12 }, { header: 'Seller', width: 18 }, { header: 'Design', width: 28 }, { header: 'Colour', width: 14 }, { header: 'Size', width: 7 }, { header: 'City', width: 14 }, { header: 'Status', width: 11 }, { header: 'Price', width: 11, type: 'money' }],
            rows: x.pod.map((o) => [o.id, isoDate(new Date(o.placedAt)), db().find('sellers', o.sellerId)?.name || '', o.title, o.color, o.size, o.customer?.city || '', o.status, o.price * (o.qty || 1)]) },
          { name: 'Customers', columns: [{ header: 'Customer', width: 26 }, { header: 'City', width: 14 }, { header: 'Bookings', width: 10, type: 'number' }, { header: 'Orders', width: 10, type: 'number' }, { header: 'Printed items', width: 12, type: 'number' }, { header: 'Spent', width: 14, type: 'money' }, { header: 'Refused', width: 9, type: 'number' }, { header: 'Blocked', width: 9 }],
            rows: AD().customers().map((c) => [c.name, c.city, c.bookings, c.orders, c.items, Math.round(c.spent), c.refused, c.blocked ? 'Yes' : '']) },
          { name: 'Fantasy Pass', columns: [{ header: 'Date', width: 12 }, { header: 'Plan', width: 16 }, { header: 'Customer', width: 22 }, { header: 'Amount', width: 12, type: 'money' }], rows: x.passes.map((p) => [isoDate(new Date(p.at)), p.plan, p.customer, p.amount]) },
        ]);
        AD().log('export', `Exported platform analytics ${r.from} to ${r.to}`);
      });
    },
  });
})(window.SA);
