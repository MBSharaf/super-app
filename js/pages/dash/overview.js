(function (SA) {
  const { esc, money, isoDate, today, fmtHour, fmtDate, timeAgo, relDay } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const D = () => SA.services.dash;

  SA.pages.dashOverview = C.guard({
    title: 'Overview — Super App Partner',
    render(ctx) {
      const r = C.range(ctx.query, '30');
      const sc = C.scope();
      const s = D().summary({ ...r, ...sc });
      const rows = D().daily(s, r.from, r.to);
      const w = D().wallet(sc.ownerId ? { ownerId: sc.ownerId } : {});
      const a = D().me();
      const todayISO = isoDate(today());
      const upcoming = D().ledger({ from: todayISO, to: isoDate(SA.core.utils.addDays(today(), 3)), pitchIds: s.pitches.map((p) => p.id) }).filter((b) => ['upcoming', 'in-progress', 'checked-in'].includes(b.status)).sort((x, y) => x.startsAt - y.startsAt).slice(0, 6);
      const perPitch = s.pitches.map((p) => {
        const L = s.ledger.filter((b) => b.pitchId === p.id && b.status !== 'cancelled');
        return { p, net: L.reduce((t, b) => t + b.money.net, 0), n: L.length, hours: L.reduce((t, b) => t + b.duration, 0) };
      }).sort((x, y) => y.net - x.net);
      const greeting = new Date().getHours() < 12 ? 'Good morning' : new Date().getHours() < 18 ? 'Good afternoon' : 'Good evening';
      const store = D().myStore(sc.ownerId);
      const lowStock = store ? D().lowStock(sc.ownerId) : [];

      const body = `
        ${C.rangePicker(r, '/dash')}
        <div class="kpi-grid">
          ${C.kpi({ label: 'Net earnings', value: money(Math.round(s.net + s.storeNet)), count: Math.round(s.net + s.storeNet), suffix: ' EGP', sub: `Bookings ${money(Math.round(s.net))} · Store ${money(Math.round(s.storeNet))}`, ic: 'wallet', tone: 'k-turf' })}
          ${C.kpi({ label: 'Bookings', value: s.bookings, count: s.bookings, sub: `${s.hours} hours · ${s.walkIns} walk-in${s.walkIns === 1 ? '' : 's'} · ${s.cancelled} cancelled`, ic: 'ticket' })}
          ${C.kpi({ label: 'Occupancy', value: `${(s.occupancy * 100).toFixed(1)}%`, count: +(s.occupancy * 100).toFixed(1), suffix: '%', decimals: 1, sub: `${s.hours} of ${s.capacity} open hours booked`, ic: 'pitch', tone: 'k-blaze' })}
          ${C.kpi({ label: 'Available to withdraw', value: money(w.balance), count: w.balance, suffix: ' EGP', sub: `Pending ${money(Math.round(w.bookings.pending + w.store.pending))}`, ic: 'card', tone: 'k-ink' })}
        </div>

        ${s.pitches.length === 0 && !store ? `<div class="dash-card onboard">
          <div><h2 class="h3">Welcome to Super App, ${esc(a.name.split(' ')[0])}.</h2><p class="soft" style="margin-top:6px">Add your first pitch to start taking bookings, or open your store to sell products on Super App.</p></div>
          <div class="row wrap"><a class="btn btn-primary" href="#/dash/pitches/new">${icon('plus', 16)} Add a pitch</a><a class="btn btn-outline" href="#/dash/store">${icon('bag', 16)} Open my store</a></div></div>` : ''}

        <div class="dash-grid g-2-1">
          <section class="dash-card"><div class="row-between"><h2 class="h4">Earnings by day</h2><span class="xs muted">${C.rangeLabel(r)}</span></div>${C.barChart(rows)}</section>
          <section class="dash-card">
            <h2 class="h4">Today at your pitches</h2>
            <div class="today-board">${s.pitches.filter((p) => !p.deleted).slice(0, 8).map((p) => {
              const sl = D().slots(p.id, todayISO);
              const booked = sl.filter((x) => ['booked', 'yours'].includes(x.status)).length;
              return `<a class="tb-row" href="#/dash/availability?pitch=${p.id}"><span class="tb-name small"><b class="truncate">${esc(p.name)}</b><span class="xs muted">${p.status === 'closed' ? 'Closed' : `${booked}/${sl.length} hours booked`}</span></span>
                <span class="tb-strip">${sl.map((x, i) => `<i class="tb-${x.status}" style="--i:${i}" title="${fmtHour(x.hour)} — ${x.status}"></i>`).join('')}</span></a>`;
            }).join('') || '<p class="small muted">No pitches yet.</p>'}</div>
            <div class="legend xs" style="margin-top:12px"><span><i class="tb-booked"></i>Booked</span><span><i class="tb-available"></i>Free</span><span><i class="tb-blocked"></i>Blocked</span><span><i class="tb-past"></i>Passed</span></div>
          </section>
        </div>

        <div class="dash-grid g-1-1">
          <section class="dash-card"><div class="row-between"><h2 class="h4">When your pitches are busiest</h2><span class="xs muted">${C.rangeLabel(r)}</span></div><div style="margin-top:14px">${C.heatmap(D().heatmap(s))}</div></section>
          <section class="dash-card">
            <div class="row-between"><h2 class="h4">Next bookings</h2><a class="link small" href="#/dash/bookings?range=7">All bookings</a></div>
            <ul class="next-list">${upcoming.map((b) => `<li><span class="nl-time"><b class="num">${fmtHour(b.hour)}</b><span class="xs">${relDay(b.date)}</span></span><span class="grow"><b class="small">${esc(b.customer.name)}</b><span class="xs muted" style="display:block">${esc(b.pitchName)} · ${b.duration * 60} min · ${b.source === 'walk-in' ? 'Walk-in' : b.payment}</span></span>${C.statusBadge(b.status)}</li>`).join('') || '<li class="small muted">No bookings in the next three days.</li>'}</ul>
          </section>
        </div>

        <div class="dash-grid g-1-1">
          <section class="dash-card"><h2 class="h4">Pitch performance</h2>
            <div class="table-wrap"><table class="table"><thead><tr><th>Pitch</th><th class="r">Bookings</th><th class="r">Hours</th><th class="r">Net</th></tr></thead><tbody>
              ${perPitch.map((x) => `<tr><td><a class="strong" href="#/dash/pitches/${x.p.id}">${esc(x.p.name)}</a> ${x.p.status === 'closed' ? '<span class="badge badge-danger">Closed</span>' : ''}</td><td class="r num">${x.n}</td><td class="r num">${x.hours}</td><td class="r num strong">${money(Math.round(x.net))}</td></tr>`).join('') || '<tr><td colspan="4" class="muted">No pitches yet.</td></tr>'}
            </tbody></table></div></section>
          <section class="dash-card"><div class="row-between"><h2 class="h4">Recent activity</h2><a class="link small" href="#/dash/activity">All activity</a></div>
            ${lowStock.length ? `<div class="notice gold" style="margin-top:12px">${icon('alert', 16)}<span><b>Low stock:</b> ${lowStock.map((p) => `${esc(p.name)} (${p.stock})`).join(', ')}. <a class="link" href="#/dash/store?low=1">Restock</a></span></div>` : ''}
            <ul class="act-list">${D().activity(6).map((x) => `<li><span class="act-dot"></span><span class="grow small">${esc(x.text)}</span><span class="xs muted">${timeAgo(x.at)}</span></li>`).join('')}</ul></section>
        </div>`;

      return C.layout('overview', {
        title: `${greeting}, ${esc(a.name.split(' ')[0])}`,
        subtitle: `${s.pitches.length} pitch${s.pitches.length === 1 ? '' : 'es'}${store ? ` · ${esc(store.name)}` : ''} · ${C.rangeLabel(r)}`,
        actions: `<a class="btn btn-outline btn-sm" href="#/dash/availability">${icon('calendar', 15)} Block a slot</a><a class="btn btn-primary btn-sm" href="#/dash/pitches/new">${icon('plus', 15)} Add pitch</a>`,
        body,
      });
    },
    mount(el) { C.bindRange(el, '/dash'); C.bindChart(el); },
  });
})(window.SA);
