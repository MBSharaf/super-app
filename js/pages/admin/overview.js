/** Admin console: overview (platform KPIs, what needs attention, revenue mix) and the audit log. */
(function (SA) {
  const { esc, money, isoDate, addDays, today, timeAgo, fmtDate, parseISO } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const A = SA.pages.adminCommon;
  const AD = () => SA.services.admin;
  const db = () => SA.services.db;

  const TONE = { turf: 'var(--turf)', info: 'var(--info)', blaze: 'var(--blaze)', ink: 'var(--ink-3)', gold: 'var(--gold)', violet: '#7C4DDB' };

  /** Stacked daily revenue columns, one colour per stream (with a text alternative) */
  const revChart = (rows, streams) => {
    const max = Math.max(1, ...rows.map((r) => r.income));
    const step = rows.length > 45 ? 14 : rows.length > 20 ? 7 : rows.length > 10 ? 2 : 1;
    const tot = rows.reduce((t, r) => t + r.income, 0);
    const best = rows.reduce((b, r) => (r.income > b.income ? r : b), rows[0]);
    const alt = `Platform income per day from ${fmtDate(rows[0].date)} to ${fmtDate(rows[rows.length - 1].date)}: ${money(Math.round(tot))} in total. Best day ${fmtDate(best.date)} with ${money(Math.round(best.income))}.`;
    return `<div class="adm-chart" role="img" aria-label="${esc(alt)}">
      <div class="bc-grid">${[1, 0.5].map((f) => `<span style="bottom:${f * 100}%"><i>${money(Math.round(max * f)).replace(' EGP', '')}</i></span>`).join('')}</div>
      <div class="adm-cols">${rows.map((r, i) => `<div class="adm-col" style="--i:${i}" data-tip="${fmtDate(r.date)} — ${money(Math.round(r.income))}">${streams.map(([k, , tone]) => (r[k] > 0 ? `<i style="height:${(r[k] / max) * 100}%;background:${TONE[tone]}"></i>` : '')).join('')}${i % step === 0 ? `<b class="bc-x">${parseISO(r.date).getDate()}</b>` : ''}</div>`).join('')}</div>
    </div>
    <div class="legend xs">${streams.map(([, l, tone]) => `<span><i style="background:${TONE[tone]}"></i>${l}</span>`).join('')}</div>`;
  };

  /** Hover tooltip for the revenue chart */
  const bindRevChart = (el) => el.querySelectorAll('.adm-chart').forEach((ch) => {
    const tip = document.createElement('div'); tip.className = 'bc-tip'; tip.hidden = true; ch.appendChild(tip);
    ch.addEventListener('pointermove', (e) => { const c = e.target.closest('.adm-col'); if (!c) { tip.hidden = true; return; } const r = ch.getBoundingClientRect(); tip.textContent = c.dataset.tip; tip.hidden = false; tip.style.left = `${Math.min(r.width - 200, Math.max(0, e.clientX - r.left - 100))}px`; });
    ch.addEventListener('pointerleave', () => (tip.hidden = true));
  });
  Object.assign(A, { revChart, bindRevChart, TONE });

  SA.pages.adminOverview = A.guard({
    title: 'Overview — Super App Admin',
    render() {
      const to = isoDate(today()), from = isoDate(addDays(today(), -29));
      const pfrom = isoDate(addDays(today(), -59)), pto = isoDate(addDays(today(), -30));
      const r = AD().revenue({ from, to }), prev = AD().revenue({ from: pfrom, to: pto });
      const al = AD().alerts();
      const change = (a, b) => (b ? Math.round(((a - b) / b) * 100) : 0);
      const delta = (a, b) => { const c = change(a, b); return `<span class="adm-delta ${c >= 0 ? 'up' : 'down'}">${c >= 0 ? '▲' : '▼'} ${Math.abs(c)}%</span> vs the 30 days before`; };
      const t = r.total;
      const pending = AD().pendingPayouts ? AD().pendingPayouts() : [];
      const counts = {
        pitches: db().all('pitches').filter((p) => !p.deleted && p.status === 'active').length, partners: db().all('accounts').filter((a) => a.role === 'owner').length,
        sellers: db().all('sellers').filter((s) => s.status === 'active').length, designs: db().all('designs').filter((d) => d.status === 'live').length,
        stores: db().all('stores').filter((s) => s.status === 'active').length, products: db().all('products').filter((p) => !p.deleted && p.status === 'active').length,
      };
      const mix = AD().STREAMS.map(([k, l, tone]) => ({ k, l, tone, v: t[k] })).filter((x) => x.v > 0).sort((a, b) => b.v - a.v);
      const recent = AD().audit().slice(0, 7);
      const body = `
        <div class="kpi-grid">
          ${C.kpi({ label: 'Money through Super App (30 days)', value: money(t.gmv), count: t.gmv, suffix: ' EGP', ic: 'trend', sub: delta(t.gmv, prev.total.gmv) })}
          ${C.kpi({ label: 'Platform revenue (30 days)', value: money(t.net), count: t.net, suffix: ' EGP', ic: 'percent', tone: 'k-turf', sub: delta(t.net, prev.total.net) })}
          ${C.kpi({ label: 'Withdrawals to approve', value: pending.length, count: pending.length, ic: 'wallet', tone: pending.length ? 'k-blaze' : '', sub: pending.length ? money(Math.round(pending.reduce((s, p) => s + p.amount, 0))) : 'Nothing waiting' })}
          ${C.kpi({ label: 'Open to-dos', value: al.reduce((s, x) => s + x.count, 0), count: al.reduce((s, x) => s + x.count, 0), ic: 'bell', sub: al.length ? `${al.length} queue${al.length > 1 ? 's' : ''}` : 'All clear' })}
        </div>
        <div class="dash-grid g-2-1">
          <section class="dash-card"><div class="row-between"><h2 class="h4">Revenue by day</h2><a class="btn btn-ghost btn-sm" href="#/admin/revenue">Full report ${icon('arrowRight', 14)}</a></div>
            <div style="margin-top:14px">${revChart(r.daily, AD().STREAMS)}</div></section>
          <section class="dash-card"><h2 class="h4">Needs your attention</h2>
            ${al.length ? `<ul class="adm-todo">${al.map((x) => `<li><a href="${x.href}"><span class="bp-ico t-${x.tone}">${icon(x.icon, 16)}</span><span class="grow"><b class="small">${esc(x.title)}</b><span class="xs muted" style="display:block">${esc(x.text)}</span></span><span class="nav-badge nb-${x.tone}">${x.count}</span></a></li>`).join('')}</ul>`
              : `<div class="adm-clear">${icon('check', 22, 2.4)}<b>All clear</b><span class="small muted">No withdrawals, reviews or reports waiting.</span></div>`}</section>
        </div>
        <div class="dash-grid g-1-1">
          <section class="dash-card"><h2 class="h4">Where revenue comes from</h2><p class="xs muted" style="margin-top:4px">Last 30 days · costs: ${money(t.cost)} (refused print deliveries, refunds and promo codes paid by Super App)</p>
            <ul class="adm-mix">${mix.map((x) => `<li><span class="row-between small"><span><i class="adm-dot" style="background:${TONE[x.tone]}"></i>${x.l}</span><b class="num">${money(x.v)}</b></span><span class="meter"><span style="width:${Math.max(2, (x.v / t.income) * 100)}%;background:${TONE[x.tone]}"></span></span></li>`).join('')}</ul></section>
          <section class="dash-card"><h2 class="h4">On the platform now</h2>
            <div class="adm-counts">${[['pitch', counts.pitches, 'Live pitches', '#/admin/partners'], ['users', counts.partners, 'Pitch owners', '#/admin/partners'], ['shirt', counts.sellers, 'Active sellers', '#/admin/sellers'], ['image', counts.designs, 'Live designs', '#/admin/designs'], ['bag', counts.stores, 'Live stores', '#/admin/partners'], ['package', counts.products, 'Products for sale', '#/shop']].map(([ic, n, l, h]) => `<a href="${h}"><span class="kpi-ico">${icon(ic, 18)}</span><b class="h3 num">${n}</b><span class="xs muted">${l}</span></a>`).join('')}</div></section>
        </div>
        <section class="dash-card"><div class="row-between"><h2 class="h4">Latest activity</h2><a class="btn btn-ghost btn-sm" href="#/admin/audit">Audit log ${icon('arrowRight', 14)}</a></div>
          <ol class="activity" style="margin-top:10px">${recent.map((x, i) => `<li style="--i:${i}"><span class="ac-ico">${icon(x.kind === 'admin' ? 'shield' : x.kind === 'seller' ? 'shirt' : x.kind === 'customer' ? 'user' : 'clock', 15)}</span><span class="grow"><b class="small">${esc(x.text)}</b><span class="xs muted" style="display:block">${esc(x.actorName)} · ${timeAgo(x.at)}</span></span></li>`).join('')}</ol></section>`;
      return C.layout('overview', { title: 'Overview', subtitle: `Super App platform · ${fmtDate(from)} – ${fmtDate(to)}`, body });
    },
    mount(el) { bindRevChart(el); },
  });

  /* ======================= Audit log ======================= */
  const WHO = [['', 'Everyone'], ['admin', 'Admins'], ['partner', 'Pitch owners'], ['seller', 'Sellers'], ['printer', 'Print partner'], ['customer', 'Customers'], ['system', 'System']];
  SA.pages.adminAudit = A.guard({
    title: 'Audit log — Super App Admin',
    render(ctx) {
      const q = ctx.query;
      const r = q.from || q.range ? C.range(q, '30') : null;
      const list = AD().audit({ who: q.who || '', q: q.q || '', from: r?.from || '', to: r?.to || '' });
      const shown = list.slice(0, +q.n || 120);
      const body = `<section class="dash-card">
          <form class="adm-filters" data-audit>
            <label class="field"><span class="field-label">Who</span><select class="select" name="who">${WHO.map(([v, l]) => `<option value="${v}" ${q.who === v || (!q.who && !v) ? 'selected' : ''}>${l}</option>`).join('')}</select></label>
            <label class="field grow"><span class="field-label">Search</span><input class="input" name="q" value="${esc(q.q || '')}" placeholder="Name, order ID, design, code…" /></label>
            <label class="field"><span class="field-label">From</span><input class="input" type="date" name="from" value="${esc(r?.from || '')}" /></label>
            <label class="field"><span class="field-label">To</span><input class="input" type="date" name="to" value="${esc(r?.to || '')}" /></label>
            <div class="row" style="--gap:8px;align-self:end"><button class="btn btn-primary" type="submit">Filter</button><button class="btn btn-outline" type="button" data-export>${icon('file', 15)} Excel</button></div>
          </form></section>
        <section class="dash-card table-card"><div class="table-wrap"><table class="table dash-table adm-audit"><thead><tr><th>When</th><th>Who</th><th>What happened</th><th>Reference</th></tr></thead><tbody>
          ${shown.map((x) => `<tr><td class="small nowrap">${fmtDate(new Date(x.at))}<span class="xs muted" style="display:block">${new Date(x.at).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })}</span></td>
            <td><span class="badge ${x.kind === 'admin' ? 'badge-blaze' : x.kind === 'seller' ? 'badge-turf' : x.kind === 'partner' ? 'badge-info' : x.kind === 'printer' ? 'badge-gold' : ''}">${WHO.find((w) => w[0] === x.kind)?.[1].replace(/s$/, '') || x.kind}</span><span class="xs" style="display:block;margin-top:4px">${esc(x.actorName)}</span></td>
            <td class="small">${esc(x.text)}<span class="xs muted" style="display:block">${esc(x.action)}</span></td><td class="xs muted mono">${esc(x.ref || '')}</td></tr>`).join('') || `<tr><td colspan="4">${SA.ui.c.empty({ icon: 'clock', title: 'Nothing found', text: 'Try another filter.' })}</td></tr>`}
          </tbody></table></div>
          ${list.length > shown.length ? `<div style="padding:14px 18px"><a class="btn btn-outline btn-sm" href="${SA.core.router.href('/admin/audit', { ...q, n: shown.length + 200 })}" data-replace>Show more (${list.length - shown.length} older)</a></div>` : `<p class="xs muted" style="padding:12px 18px">${list.length} entr${list.length === 1 ? 'y' : 'ies'}. The demo keeps the latest 1,000 on this device.</p>`}</section>`;
      return C.layout('audit', { title: 'Audit log', subtitle: 'Who changed what, and when — admins, partners, sellers, the print partner and the system', body });
    },
    mount(el, ctx) {
      const f = el.querySelector('[data-audit]');
      f?.addEventListener('submit', (e) => {
        e.preventDefault();
        const d = SA.core.utils.formData(f);
        SA.core.router.go(SA.core.router.href('/admin/audit', { who: d.who || undefined, q: d.q || undefined, from: d.from || undefined, to: d.to || undefined }).slice(1));
      });
      el.querySelector('[data-export]')?.addEventListener('click', () => {
        const q = ctx.query, r = q.from ? C.range(q, '30') : null;
        const list = AD().audit({ who: q.who || '', q: q.q || '', from: r?.from || '', to: r?.to || '' });
        C.xlsx(`super-app-audit-${isoDate(today())}.xlsx`, [{ name: 'Audit log', columns: [{ header: 'Date', width: 12 }, { header: 'Time', width: 8 }, { header: 'Who', width: 14 }, { header: 'Name', width: 22 }, { header: 'Action', width: 22 }, { header: 'What happened', width: 70 }, { header: 'Reference', width: 22 }],
          rows: list.map((x) => [isoDate(new Date(x.at)), new Date(x.at).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' }), x.kind, x.actorName, x.action, x.text, x.ref || '']) }]);
        AD().log('export', `Exported the audit log (${list.length} entries)`);
      });
    },
  });
})(window.SA);
