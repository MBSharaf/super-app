/** CEO system: command overview, morning briefing and the approvals inbox. */
(function (SA) {
  const { esc, fmtDate, timeAgo } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.ceoCommon;
  const D = SA.pages.dashCommon;
  const CE = () => SA.services.ceo;

  const delta = (a, b) => { const c = b ? Math.round(((a - b) / Math.abs(b)) * 100) : 0; return `<span class="adm-delta ${c >= 0 ? 'up' : 'down'}">${c >= 0 ? '▲' : '▼'} ${Math.abs(c)}%</span> vs the 30 days before`; };
  const reqCard = (x) => `<li class="ceo-req">
    <span class="ceo-req-ico t-${x.need === 2 ? 'danger' : 'blaze'}">${icon(x.kind === 'refund' || x.kind === 'credit' ? 'gift' : 'wallet', 18)}</span>
    <span class="grow"><b class="small">${CE().KIND[x.kind]} · <span class="num">${C.egp(x.amount)}</span></b><span class="xs muted" style="display:block">${esc(x.title)}</span>
      <span class="xs muted">${esc(x.requestedBy?.name || '')} · ${timeAgo(x.at)}${x.need === 2 ? ` · <b class="t-danger">Two approvals</b>` : ''}</span></span>
    <a class="btn btn-outline btn-sm" href="#/ceo/approvals">Review</a></li>`;

  /* ---------- Command overview ---------- */
  SA.pages.ceoOverview = C.guard({
    title: 'Command overview — Super App',
    render() {
      const K = CE().kpis();
      const H = CE().health(K);
      const pend = CE().approvals({ status: 'pending' });
      const cal = CE().calendar().filter((x) => ['overdue', 'soon'].includes(x.state));
      const G = CE().goals();
      const al = (() => { try { return SA.services.admin.alerts(); } catch { return []; } })();
      const body = `
        <div class="kpi-grid">
          ${D.kpi({ label: 'Platform revenue (30 days)', value: C.egp(K.revenue), count: K.revenue, suffix: ' EGP', ic: 'percent', tone: 'k-turf', sub: delta(K.revenue, K.revenuePrev) })}
          ${D.kpi({ label: 'Money through Super App (30 days)', value: C.egp(K.gmv), count: K.gmv, suffix: ' EGP', ic: 'trend', sub: delta(K.gmv, K.gmvPrev) })}
          ${D.kpi({ label: 'Waiting for your decision', value: pend.length, count: pend.length, ic: 'check', tone: pend.length ? 'k-blaze' : '', sub: pend.length ? C.egp(pend.reduce((t, x) => t + x.amount, 0)) : 'Nothing waiting' })}
          ${D.kpi({ label: 'AI agents working', value: `${K.agentsActive}/${K.agents}`, ic: 'bolt', sub: `${C.egp(K.agentCost)} a month` })}
        </div>
        <div class="kpi-grid ceo-mini">
          ${[['Bookings (30 days)', K.bookings, 'pitch'], ['Orders (30 days)', K.orders, 'bag'], ['Customers', K.customers, 'user'], ['Pitch partners', K.partners, 'pitch'], ['Active sellers', K.sellers, 'users'], ['Live designs', K.designs, 'image'], ['Open tickets', K.ticketsOpen, 'message'], ['Average rating', K.rating, 'star']].map(([l, v, ic]) => `<div class="ceo-stat">${icon(ic, 16)}<span class="grow xs muted">${l}</span><b class="num">${(+v).toLocaleString()}</b></div>`).join('')}
        </div>
        <div class="dash-grid g-2-1">
          <section class="dash-card"><div class="row-between"><h2 class="h4">Department health</h2><a class="btn btn-ghost btn-sm" href="#/ceo/departments">Departments ${icon('arrowRight', 14)}</a></div>
            <div class="ceo-health">${H.map((d, i) => `<a class="ceo-dept" href="#/ceo/department/${d.key}" style="--i:${i}">${C.ring(d.score, d.tone, 54)}<span class="grow"><b class="small">${esc(d.name)}</b><span class="xs muted" style="display:block">${d.issues.length ? esc(d.issues.map((x) => x.text).join(' · ')) : 'All good'}</span></span></a>`).join('')}</div>
          </section>
          <section class="dash-card"><div class="row-between"><h2 class="h4">Needs your decision</h2><span class="badge ${pend.length ? 'badge-blaze' : ''}">${pend.length}</span></div>
            ${pend.length ? `<ul class="ceo-reqs">${pend.slice(0, 4).map(reqCard).join('')}</ul>` : `<p class="small muted" style="margin-top:10px">${icon('check', 14)} Nothing waiting. Payouts over ${C.egp(CE().limits().payout)} and refunds over ${C.egp(CE().limits().refund)} come here.</p>`}
            <h3 class="h5" style="margin-top:18px">Deadlines this week</h3>
            <ul class="ceo-list">${cal.slice(0, 4).map((x) => `<li><span class="badge ${x.state === 'overdue' ? 'badge-danger' : 'badge-gold'}">${x.state === 'overdue' ? `${Math.abs(x.left)}d late` : x.left === 0 ? 'Today' : `${x.left}d`}</span><span class="grow small">${esc(x.title)}</span></li>`).join('') || '<li class="small muted">No deadlines in the next 7 days.</li>'}</ul>
          </section>
        </div>
        <div class="dash-grid g-2-1">
          <section class="dash-card"><div class="row-between"><h2 class="h4">Net revenue by day</h2><a class="btn btn-ghost btn-sm" href="#/ceo/reports?kind=finance">Finance report ${icon('arrowRight', 14)}</a></div>
            ${C.columns(K.daily, { key: 'net', label: 'Net revenue' })}
            <p class="xs muted">${fmtDate(K.daily[0]?.date)} – ${fmtDate(K.daily[K.daily.length - 1]?.date)} · income ${C.egp(K.income)} · costs ${C.egp(K.cost)}</p>
          </section>
          <section class="dash-card"><div class="row-between"><h2 class="h4">Goals</h2><a class="btn btn-ghost btn-sm" href="#/ceo/goals">All goals ${icon('arrowRight', 14)}</a></div>
            <ul class="ceo-list">${G.map((g) => `<li class="ceo-goal"><span class="grow"><b class="small">${esc(g.objective)}</b>${C.meter(g.pct, g.pct >= 70 ? 'turf' : g.pct >= 40 ? 'gold' : 'blaze')}</span><b class="num small">${g.pct}%</b></li>`).join('')}</ul>
          </section>
        </div>
        <section class="dash-card"><div class="row-between"><h2 class="h4">Smart alerts from every system</h2><a class="btn btn-ghost btn-sm" href="#/ceo/systems">All systems ${icon('arrowRight', 14)}</a></div>
          <ul class="ceo-alerts">${al.map((x) => `<li><span class="bp-ico t-${x.tone}">${icon(x.icon, 16)}</span><span class="grow"><b class="small">${esc(x.title)}</b><span class="xs muted" style="display:block">${esc(x.text || '')}</span></span><a class="btn btn-ghost btn-sm" href="${x.href}" target="_blank" rel="noopener">Open ${icon('arrowUpRight', 13)}</a></li>`).join('') || `<li class="small muted">${icon('check', 14)} The admin team has nothing waiting.</li>`}</ul>
        </section>`;
      return C.layout('overview', { title: 'Good to see you, Sharaf', subtitle: `${fmtDate(SA.core.utils.isoDate(SA.core.utils.today()), 'long')} — everything happening in Super App`, actions: '<a class="btn btn-primary btn-sm" href="#/ceo/briefing">Today’s briefing</a>', body });
    },
    mount(el) { SA.core.motion?.countUp?.(el); },
  });

  /* ---------- Morning briefing ---------- */
  SA.pages.ceoBriefing = C.guard({
    title: 'Morning briefing — Super App',
    render() {
      const b = CE().briefing();
      const H = b.health;
      const body = `
        <section class="dash-card ceo-brief">
          <span class="badge badge-gold">${icon('sun', 13)} ${fmtDate(SA.core.utils.isoDate(SA.core.utils.today()), 'long')}</span>
          <h2 class="h3" style="margin-top:10px">Here’s what happened yesterday (${fmtDate(b.date)}) and what needs you today.</h2>
          <p class="xs muted">Written by the Conductor agent from live data across all systems. With the backend it arrives every morning at 8:00 by email and WhatsApp.</p>
          <ul class="ceo-brief-list">${b.lines.map((l, i) => `<li style="--i:${i}"><span class="bp-ico t-${l.tone}">${icon(l.icon, 16)}</span><span class="grow">${esc(l.text)}</span>${l.href ? `<a class="btn btn-ghost btn-sm" href="${l.href}">Open</a>` : ''}</li>`).join('')}</ul>
        </section>
        <div class="dash-grid g-1-1">
          <section class="dash-card"><h2 class="h4">Yesterday in numbers</h2>
            <ul class="ceo-list">${SA.services.admin.STREAMS.map(([k, l]) => `<li><span class="grow small">${l}</span><b class="num small">${C.egp(b.revenue[k])}</b></li>`).join('')}
              <li><span class="grow small">Costs (refused deliveries, refunds, promo codes)</span><b class="num small t-danger">−${C.egp(b.revenue.cost)}</b></li>
              <li class="ceo-total"><span class="grow"><b>Net</b></span><b class="num">${C.egp(b.revenue.net)}</b></li></ul>
          </section>
          <section class="dash-card"><h2 class="h4">Departments</h2>
            <ul class="ceo-list">${H.map((d) => `<li>${C.ring(d.score, d.tone, 36)}<span class="grow small"><b>${esc(d.name)}</b>${d.issues.length ? `<span class="xs muted" style="display:block">${esc(d.issues.map((x) => x.text).join(' · '))}</span>` : ''}</span></li>`).join('')}</ul>
          </section>
        </div>`;
      return C.layout('briefing', { title: 'Morning briefing', subtitle: 'Your day in two minutes', actions: '<button class="btn btn-outline btn-sm" data-print>Print / PDF</button>', body });
    },
    mount(el) { el.querySelector('[data-print]')?.addEventListener('click', () => window.print()); },
  });

  /* ---------- Approvals inbox ---------- */
  SA.pages.ceoApprovals = C.guard({
    title: 'Approvals — Super App',
    render(ctx) {
      const tab = ctx.query.tab || 'pending';
      const all = CE().approvals();
      const list = all.filter((x) => (tab === 'all' ? true : x.status === tab));
      const L = CE().limits();
      const row = (x) => `<li class="ceo-appr ${x.status}">
        <div class="row-between" style="align-items:flex-start;gap:12px">
          <span class="grow"><span class="xs muted">${x.id} · ${timeAgo(x.at)}</span>
            <b class="ceo-appr-title">${CE().KIND[x.kind]} · <span class="num">${C.egp(x.amount)}</span></b>
            <span class="small" style="display:block">${esc(x.title)}</span>
            <span class="xs muted" style="display:block">Asked by ${esc(x.requestedBy?.name || '')} (${esc(x.requestedBy?.title || '')})</span></span>
          ${x.status === 'pending' ? `<span class="badge ${x.need === 2 ? 'badge-danger' : 'badge-blaze'}">${x.need === 2 ? 'Two approvals' : 'Your approval'}</span>` : `<span class="badge ${x.status === 'approved' ? 'badge-turf' : 'badge-danger'}">${x.status === 'approved' ? 'Approved' : 'Rejected'}</span>`}
        </div>
        <ul class="ceo-why">${(x.why || []).map((w) => `<li>${icon('info', 12)} ${esc(w)}</li>`).join('')}</ul>
        ${x.approvals?.length ? `<p class="xs muted">${icon('check', 12)} ${x.approvals.map((p) => `${esc(p.name)} ${timeAgo(p.at)}`).join(' · ')}</p>` : ''}
        ${x.lastError ? `<p class="xs t-danger">${icon('alert', 12)} ${esc(x.lastError)}</p>` : ''}
        ${x.reason ? `<p class="xs t-danger">${esc(x.reason)}</p>` : ''}
        ${x.status === 'pending' ? `<div class="row" style="--gap:8px;margin-top:10px;flex-wrap:wrap">
          <button class="btn btn-primary btn-sm" data-approve="${x.id}">${icon('check', 14)} ${x.need === 2 && !x.approvals?.some((p) => p.role === 'ceo') ? 'Approve (1 of 2)' : x.need === 2 ? 'CFO confirms' : 'Approve'}</button>
          <button class="btn btn-ghost btn-sm" data-reject="${x.id}">Reject</button></div>` : ''}
      </li>`;
      const body = `
        <section class="dash-card ceo-rules">
          <div class="ceo-rule">${icon('wallet', 18)}<span><b class="num">${C.egp(L.payout)}</b><span class="xs muted" style="display:block">Withdrawals, settlements and balance changes above this need you</span></span></div>
          <div class="ceo-rule">${icon('gift', 18)}<span><b class="num">${C.egp(L.refund)}</b><span class="xs muted" style="display:block">Refunds and credits above this need you</span></span></div>
          <div class="ceo-rule">${icon('users', 18)}<span><b class="num">${C.egp(L.dual)}</b><span class="xs muted" style="display:block">Above this: you and the CFO (two-person rule)</span></span></div>
          <a class="btn btn-ghost btn-sm" href="#/ceo/control#limits">Change limits</a>
        </section>
        ${SA.pages.adminCommon.tabs('/ceo/approvals', [['pending', 'Waiting', all.filter((x) => x.status === 'pending').length], ['approved', 'Approved'], ['rejected', 'Rejected'], ['all', 'All']], tab)}
        ${list.length ? `<ul class="ceo-apprs">${list.map(row).join('')}</ul>` : `<div class="dash-card">${SA.ui.c.empty({ icon: 'check', title: tab === 'pending' ? 'Nothing waiting for you' : 'Nothing here yet', text: 'When an agent or the admin team moves money above your limits, it waits here until you decide.' })}</div>`}`;
      return C.layout('approvals', { title: 'Approvals', subtitle: 'Money that needs the CEO — the request runs only after you approve it', body });
    },
    mount(el) {
      el.querySelectorAll('[data-approve]').forEach((b) => b.addEventListener('click', async () => {
        const id = b.dataset.approve;
        const x = SA.services.db.find('ceoApprovals', id);
        const ok = await SA.ui.overlay.confirm({ title: `Approve ${C.egp(x.amount)}?`, text: `${esc(x.title)}. The money moves as soon as ${x.need === 2 ? 'the CFO confirms too' : 'you approve'}.`, confirmLabel: 'Approve', cancelLabel: 'Not yet' });
        if (!ok) return;
        try {
          const r = await CE().approve(id);
          if (r.waiting) {
            C.pinPad({ title: 'Laila Mansour — CFO', subtitle: 'Second approval: the CFO enters their PIN (demo: 7777)', check: (pin) => CE().approve(id, { cfoPin: pin }), done: () => { SA.ui.overlay.toast('Approved by you and the CFO — money sent'); SA.app.rerender(); } });
            SA.app.rerender();
            return;
          }
          SA.ui.overlay.toast('Approved — done');
          SA.app.rerender();
        } catch (e) { SA.ui.overlay.toast(e.message, { type: 'error' }); SA.app.rerender(); }
      }));
      el.querySelectorAll('[data-reject]').forEach((b) => b.addEventListener('click', async () => {
        const r = await SA.pages.adminCommon.reason({ title: 'Reject this request?', text: 'Whoever asked is told, with your reason.', confirmLabel: 'Reject', danger: true });
        if (r) C.run(() => CE().reject(b.dataset.reject, r.reason), 'Rejected');
      }));
    },
  });
})(window.SA);
