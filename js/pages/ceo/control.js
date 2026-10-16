/** CEO system: control room, goals, budgets, compliance calendar, what-if simulator, all systems and the audit log. */
(function (SA) {
  const { esc, fmtDate, timeAgo, isoDate, today, addDays } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.ceoCommon;
  const D = SA.pages.dashCommon;
  const CE = () => SA.services.ceo;
  const A = () => SA.pages.adminCommon;
  const db = () => SA.services.db;
  const DEPTS = () => SA.data.org.DEPTS;
  const deptName = (k) => DEPTS().find((d) => d.key === k)?.name || k;

  /* ---------- Control room ---------- */
  SA.pages.ceoControl = C.guard({
    title: 'Control room — Super App',
    render() {
      const S = CE().switches();
      const L = CE().limits();
      const st = db().settings();
      const ship = SA.data.products.shipping || {};
      const roles = CE().roles().filter((r) => r.id !== 'ceo');
      const acts = SA.data.org.ACTIONS;
      const body = `
        <section class="dash-card"><div class="row-between"><h2 class="h4">Kill switches</h2><span class="xs muted">${icon('info', 12)} Turning one off works at once in every system and is logged</span></div>
          <div class="ceo-switches">${S.map((s) => `<div class="ceo-switch ${s.on ? '' : 'is-off'}">
            <span class="ceo-sw-ico">${icon(s.icon, 18)}</span><span class="grow"><b class="small">${s.label}</b><span class="xs muted" style="display:block">${s.text}</span>${!s.on && s.reason ? `<span class="xs t-danger" style="display:block">${esc(s.reason)}</span>` : ''}</span>
            <button class="ceo-toggle" role="switch" aria-checked="${s.on}" aria-label="${s.label}" data-switch="${s.key}" data-on="${s.on}"><i></i></button></div>`).join('')}</div>
        </section>
        <form class="dash-card" id="limits" data-limits>
          <div class="row-between"><h2 class="h4">Approval limits</h2><button class="btn btn-primary btn-sm" type="submit">Save limits</button></div>
          <div class="ceo-form3">
            <div class="field"><label for="l-p">Withdrawals & settlements above (EGP)</label><input class="input num" id="l-p" name="payout" type="number" min="0" step="500" value="${L.payout}" /></div>
            <div class="field"><label for="l-r">Refunds & credits above (EGP)</label><input class="input num" id="l-r" name="refund" type="number" min="0" step="100" value="${L.refund}" /></div>
            <div class="field"><label for="l-d">Two approvals above (EGP)</label><input class="input num" id="l-d" name="dual" type="number" min="0" step="1000" value="${L.dual}" /></div>
          </div>
        </form>
        <form class="dash-card" data-rates>
          <div class="row-between"><h2 class="h4">Company settings</h2><button class="btn btn-primary btn-sm" type="submit">Save settings</button></div>
          <p class="xs muted">${icon('info', 12)} Try changes in the <a class="link" href="#/ceo/whatif">what-if simulator</a> first. Partners and the admin team are told.</p>
          <div class="ceo-form3">
            <div class="field"><label for="r-b">Booking commission (%)</label><input class="input num" id="r-b" name="bookingRate" type="number" min="0" max="50" step="0.5" value="${+((st.commission?.bookings ?? 0.1) * 100).toFixed(1)}" /></div>
            <div class="field"><label for="r-s">Store commission (%)</label><input class="input num" id="r-s" name="storeRate" type="number" min="0" max="50" step="0.5" value="${+((st.commission?.store ?? 0.12) * 100).toFixed(1)}" /></div>
            <div class="field"><label for="r-f">Booking service fee (%)</label><input class="input num" id="r-f" name="serviceFee" type="number" min="0" max="15" step="0.5" value="${+((st.serviceFee ?? 0.05) * 100).toFixed(1)}" /></div>
            <div class="field"><label for="r-ds">Standard delivery (EGP)</label><input class="input num" id="r-ds" name="standard" type="number" min="0" value="${ship.standard ?? 60}" /></div>
            <div class="field"><label for="r-de">Express delivery (EGP)</label><input class="input num" id="r-de" name="express" type="number" min="0" value="${ship.express ?? 120}" /></div>
            <div class="field"><label for="r-fo">Free delivery from (EGP)</label><input class="input num" id="r-fo" name="freeOver" type="number" min="0" value="${ship.freeOver ?? 1500}" /></div>
          </div>
          <p class="xs muted" style="margin-top:6px">Super Points: 20 points = 1 EGP, vouchers from 2,000 points · courier: ${esc(SA.services.shipping?.current().name || '')} (${esc(SA.services.shipping?.cfg().mode || '')}) — change in the <a class="link" href="#/admin/factories?tab=shipping" target="_blank" rel="noopener">admin console</a>.</p>
        </form>
        <section class="dash-card"><div class="row-between"><h2 class="h4">Permissions matrix</h2><span class="xs muted">Tap a name to change its permissions</span></div>
          <div class="table-wrap"><table class="table ceo-matrix"><thead><tr><th>Role</th>${acts.map(([, l]) => `<th><span>${l}</span></th>`).join('')}<th class="num">Limit</th></tr></thead>
          <tbody>${roles.map((r) => `<tr class="${r.status === 'paused' ? 'is-paused' : ''}"><th><a class="link" href="#/ceo/agent/${r.id}">${esc(r.name)}</a><span class="xs muted" style="display:block">${esc(r.title)}</span></th>${acts.map(([k]) => `<td>${r.can.includes(k) ? `<span class="ceo-yes" aria-label="Yes">${icon('check', 13)}</span>` : '<span class="ceo-no" aria-label="No">·</span>'}</td>`).join('')}<td class="num small">${r.limit === Infinity ? '∞' : r.limit ? C.egp(r.limit) : '—'}</td></tr>`).join('')}</tbody></table></div>
        </section>`;
      return C.layout('control', { title: 'Control room', subtitle: 'Switches, limits, rates and who may do what', body });
    },
    mount(el) {
      el.querySelectorAll('[data-switch]').forEach((b) => b.addEventListener('click', async () => {
        const on = b.dataset.on === 'true';
        if (on) {
          const r = await A().reason({ title: `Turn off ${CE().SWITCHES.find(([k]) => k === b.dataset.switch)[1].toLowerCase()}?`, text: 'It stops at once everywhere. Customers see your message for bookings and the shop.', label: 'Reason / message', confirmLabel: 'Turn off', danger: true, placeholder: 'e.g. Payment provider problem — back within the hour' });
          if (r) C.run(() => CE().setSwitch(b.dataset.switch, false, r.reason), 'Turned off');
        } else C.run(() => CE().setSwitch(b.dataset.switch, true), 'Turned back on');
      }));
      el.querySelector('[data-limits]')?.addEventListener('submit', (e) => { e.preventDefault(); C.run(() => CE().setLimits(SA.core.utils.formData(e.currentTarget)), 'Limits saved'); });
      el.querySelector('[data-rates]')?.addEventListener('submit', (e) => {
        e.preventDefault();
        const d = SA.core.utils.formData(e.currentTarget);
        C.run(() => { CE().applyRates(d); SA.services.admin.setFees({ standard: d.standard, express: d.express, freeOver: d.freeOver, serviceFee: d.serviceFee }); }, 'Settings saved');
      });
    },
  });

  /* ---------- Goals (OKRs) ---------- */
  const goalForm = (g = null) => {
    const krs = g?.krs?.length ? g.krs : [{ text: '', metric: 'revenue30', target: '' }];
    const krRow = (k, i) => `<div class="ceo-kr" data-kr><input class="input" name="kt" maxlength="120" placeholder="Key result" value="${esc(k.text || '')}" aria-label="Key result ${i + 1}" />
      <select class="input" name="km" aria-label="Measured by">${Object.entries(CE().METRICS).map(([key, [l]]) => `<option value="${key}" ${k.metric === key ? 'selected' : ''}>${l}</option>`).join('')}</select>
      <input class="input num" name="kg" type="number" step="any" placeholder="Target" value="${k.target ?? ''}" aria-label="Target" />
      <input class="input num" name="kc" type="number" step="any" placeholder="Now (by hand)" value="${k.metric === 'manual' ? k.current ?? '' : ''}" aria-label="Current value (by hand)" />
      <label class="choice xs"><input type="checkbox" name="kl" ${k.lower ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span>Lower is better</label></div>`;
    return `<form class="stack" style="--gap:12px" data-goal novalidate>
      <div class="field"><label for="g-o">Goal</label><input class="input" id="g-o" name="objective" maxlength="120" value="${esc(g?.objective || '')}" placeholder="e.g. Become the first choice for 5-a-side in Cairo" /><p class="field-error" data-err="objective"></p></div>
      <div class="ceo-form3"><div class="field"><label for="g-d">Department</label><select class="input" id="g-d" name="dept">${DEPTS().map((d) => `<option value="${d.key}" ${g?.dept === d.key ? 'selected' : ''}>${esc(d.name)}</option>`).join('')}</select></div>
        <div class="field"><label for="g-q">Quarter</label><select class="input" id="g-q" name="quarter">${['Q1', 'Q2', 'Q3', 'Q4'].map((q) => `<option ${(g?.quarter || 'Q4') === q ? 'selected' : ''}>${q}</option>`).join('')}</select></div></div>
      <b class="small">Key results</b><div data-krs>${krs.map(krRow).join('')}</div>
      <button type="button" class="btn btn-ghost btn-sm" data-add-kr>${icon('plus', 14)} Add a key result</button>
      <div class="row" style="--gap:8px;justify-content:flex-end"><button class="btn btn-primary" type="submit">Save goal</button></div></form>`;
  };
  const openGoal = (id = null) => {
    const g = id ? db().find('ceoGoals', id) : null;
    SA.ui.overlay.open({ label: 'Goal', title: g ? 'Edit goal' : 'New goal', body: goalForm(g), onMount(m, close) {
      const f = m.querySelector('[data-goal]');
      m.querySelector('[data-add-kr]').addEventListener('click', () => { const box = m.querySelector('[data-krs]'); const t = document.createElement('div'); t.innerHTML = goalForm().match(/<div class="ceo-kr"[\s\S]*?<\/label><\/div>/)[0]; box.appendChild(t.firstElementChild); });
      f.addEventListener('submit', (e) => {
        e.preventDefault();
        const krs = [...f.querySelectorAll('[data-kr]')].map((r) => ({ text: r.querySelector('[name="kt"]').value, metric: r.querySelector('[name="km"]').value, target: r.querySelector('[name="kg"]').value, current: r.querySelector('[name="kc"]').value, lower: r.querySelector('[name="kl"]').checked }));
        try { CE().saveGoal({ objective: f.objective.value, dept: f.dept.value, quarter: f.quarter.value, krs }, id); close(); SA.ui.overlay.toast('Goal saved'); SA.app.rerender(); }
        catch (err) { D.showErrors(f, err.details || {}); if (!err.details) SA.ui.overlay.toast(err.message, { type: 'error' }); }
      });
    } });
  };
  SA.pages.ceoGoals = C.guard({
    title: 'Goals — Super App',
    render() {
      const G = CE().goals();
      const body = `<div class="ceo-goals">${G.map((g, i) => `<section class="dash-card" style="--i:${i}">
          <div class="row-between"><span><span class="xs muted">${esc(deptName(g.dept))} · ${esc(g.quarter)}</span><h2 class="h4">${esc(g.objective)}</h2></span><span class="row" style="--gap:6px">${C.ring(g.pct, g.pct >= 70 ? 'turf' : g.pct >= 40 ? 'gold' : 'blaze', 52)}</span></div>
          <ul class="ceo-list">${g.krs.map((k) => `<li class="ceo-goal"><span class="grow"><span class="small">${esc(k.text)}</span>${C.meter(k.pct, k.pct >= 70 ? 'turf' : k.pct >= 40 ? 'gold' : 'blaze')}<span class="xs muted">${C.fmtVal(k.current, CE().METRICS[k.metric]?.[1])} of ${C.fmtVal(k.target, CE().METRICS[k.metric]?.[1])}${k.lower ? ' (lower is better)' : ''}${k.metric !== 'manual' ? ' · live' : ''}</span></span><b class="num small">${k.pct}%</b></li>`).join('')}</ul>
          <div class="row" style="--gap:6px;margin-top:10px"><button class="btn btn-ghost btn-sm" data-edit="${g.id}">${icon('edit', 14)} Edit</button><button class="btn btn-ghost btn-sm" data-del="${g.id}">${icon('trash', 14)} Remove</button></div></section>`).join('')}</div>`;
      return C.layout('goals', { title: 'Goals (OKRs)', subtitle: 'Each key result measures itself from live data, or you enter it', actions: `<button class="btn btn-primary btn-sm" data-new>${icon('plus', 14)} New goal</button>`, body });
    },
    mount(el) {
      el.querySelector('[data-new]')?.addEventListener('click', () => openGoal());
      el.querySelectorAll('[data-edit]').forEach((b) => b.addEventListener('click', () => openGoal(b.dataset.edit)));
      el.querySelectorAll('[data-del]').forEach((b) => b.addEventListener('click', async () => { if (await SA.ui.overlay.confirm({ title: 'Remove this goal?', text: 'It’s logged in the audit log.', confirmLabel: 'Remove', danger: true })) C.run(() => CE().removeGoal(b.dataset.del), 'Goal removed'); }));
    },
  });

  /* ---------- Budgets ---------- */
  SA.pages.ceoBudgets = C.guard({
    title: 'Budgets — Super App',
    render() {
      const B = CE().budgets();
      const tot = B.reduce((t, b) => ({ budget: t.budget + b.budget, spent: t.spent + b.spent, forecast: t.forecast + b.forecast }), { budget: 0, spent: 0, forecast: 0 });
      const ex = CE().expenses().slice(0, 30);
      const body = `
        <div class="kpi-grid">
          ${D.kpi({ label: 'Monthly budget', value: C.egp(tot.budget), ic: 'wallet' })}
          ${D.kpi({ label: 'Spent this month', value: C.egp(tot.spent), ic: 'percent', tone: tot.spent > tot.budget ? 'k-blaze' : '', sub: `${tot.budget ? Math.round((tot.spent / tot.budget) * 100) : 0}% of the budget` })}
          ${D.kpi({ label: 'Forecast for the month', value: C.egp(tot.forecast), ic: 'trend', tone: tot.forecast > tot.budget ? 'k-blaze' : 'k-turf' })}
          ${D.kpi({ label: 'AI agents (this month)', value: C.egp(B.reduce((t, b) => t + b.parts.agents, 0)), ic: 'bolt' })}
        </div>
        <section class="dash-card"><h2 class="h4">By department</h2>
          <div class="table-wrap"><table class="table"><thead><tr><th>Department</th><th class="num">Budget / month</th><th class="num">AI agents</th><th class="num">Expenses</th><th class="num">Refunds, promos, refusals</th><th class="num">Spent</th><th>Used</th><th class="num">Forecast</th></tr></thead>
          <tbody>${B.map((b) => `<tr><th>${icon(b.icon, 14)} ${esc(b.name)}</th><td class="num"><input class="input num ceo-bud" type="number" min="0" step="1000" value="${b.budget}" data-budget="${b.dept}" aria-label="${esc(b.name)} budget" /></td><td class="num">${C.egp(b.parts.agents)}</td><td class="num">${C.egp(b.parts.expenses)}</td><td class="num">${C.egp(b.parts.auto)}</td><td class="num"><b>${C.egp(b.spent)}</b></td><td style="min-width:120px">${C.meter(b.pct, b.pct > 100 ? 'danger' : b.pct > 85 ? 'gold' : 'turf')}<span class="xs muted">${b.pct}%</span></td><td class="num ${b.forecast > b.budget ? 't-danger' : ''}">${C.egp(b.forecast)}</td></tr>`).join('')}</tbody></table></div>
          <p class="xs muted" style="margin-top:8px">${icon('info', 12)} Change a budget and leave the box to save. Refunds count for support, promo codes for marketing, refused deliveries for operations.</p>
        </section>
        <div class="dash-grid g-1-1">
          <form class="dash-card stack" style="--gap:10px" data-expense novalidate><h2 class="h4">Record an expense</h2>
            <div class="field"><label for="e-d">Department</label><select class="input" id="e-d" name="dept">${DEPTS().map((d) => `<option value="${d.key}">${esc(d.name)}</option>`).join('')}</select><p class="field-error" data-err="dept"></p></div>
            <div class="field"><label for="e-t">What for</label><input class="input" id="e-t" name="text" maxlength="120" placeholder="e.g. TikTok ads — derby weekend" /><p class="field-error" data-err="text"></p></div>
            <div class="field"><label for="e-a">Amount (EGP)</label><input class="input num" id="e-a" name="amount" type="number" min="1" /><p class="field-error" data-err="amount"></p></div>
            <button class="btn btn-primary" type="submit">Add expense</button></form>
          <section class="dash-card"><h2 class="h4">Latest expenses</h2><ul class="ceo-list">${ex.map((x) => `<li><span class="grow small">${esc(x.text)}<span class="xs muted" style="display:block">${esc(deptName(x.dept))} · ${timeAgo(x.at)}</span></span><b class="num small">${C.egp(x.amount)}</b></li>`).join('') || '<li class="small muted">No expenses yet.</li>'}</ul></section>
        </div>`;
      return C.layout('budgets', { title: 'Budgets', subtitle: `${fmtDate(isoDate(today()), 'long')} — this month`, body });
    },
    mount(el) {
      el.querySelectorAll('[data-budget]').forEach((i) => i.addEventListener('change', () => C.run(() => CE().setBudget(i.dataset.budget, i.value), 'Budget saved')));
      el.querySelector('[data-expense]')?.addEventListener('submit', (e) => {
        e.preventDefault();
        const f = e.currentTarget;
        try { CE().addExpense(SA.core.utils.formData(f)); SA.ui.overlay.toast('Expense recorded'); SA.app.rerender(); }
        catch (err) { D.showErrors(f, err.details || {}); if (!err.details) SA.ui.overlay.toast(err.message, { type: 'error' }); }
      });
    },
  });

  /* ---------- Compliance calendar ---------- */
  const EVERY = { once: 'Once', monthly: 'Every month', quarterly: 'Every 3 months', yearly: 'Every year' };
  SA.pages.ceoCalendar = C.guard({
    title: 'Compliance calendar — Super App',
    render(ctx) {
      const show = ctx.query.tab || 'open';
      const all = CE().calendar();
      const list = all.filter((x) => (show === 'done' ? x.done : !x.done));
      const roles = CE().roles();
      const body = `
        ${A().tabs('/ceo/calendar', [['open', 'To do', all.filter((x) => !x.done).length], ['done', 'Done']], show)}
        <ul class="ceo-cal">${list.map((x) => `<li class="dash-card ceo-cal-item is-${x.state}">
          <span class="ceo-cal-date"><b class="num">${SA.core.utils.parseISO(x.due).getDate()}</b><span class="xs">${fmtDate(x.due).split(' ').pop()}</span></span>
          <span class="grow"><b class="small">${esc(x.title)}</b><span class="xs muted" style="display:block">${esc(x.note || '')}</span>
            <span class="xs muted">${esc(deptName(x.dept))} · ${esc(x.ownerRole?.name || '')} — ${esc(x.ownerRole?.title || '')} ·${EVERY[x.every] || ''}</span></span>
          ${x.done ? '<span class="badge badge-turf">Done</span>' : `<span class="badge ${x.state === 'overdue' ? 'badge-danger' : x.state === 'soon' ? 'badge-gold' : ''}">${x.state === 'overdue' ? `${Math.abs(x.left)} days late` : x.left === 0 ? 'Today' : `In ${x.left} days`}</span><button class="btn btn-outline btn-sm" data-done="${x.id}">${icon('check', 14)} Done</button>`}
        </li>`).join('') || `<li class="dash-card small muted">Nothing here.</li>`}</ul>
        <form class="dash-card" data-cal novalidate><h2 class="h4">Add a deadline</h2>
          <div class="ceo-form3">
            <div class="field"><label for="c-t">What is due</label><input class="input" id="c-t" name="title" maxlength="100" placeholder="e.g. Renew the payment provider contract" /><p class="field-error" data-err="title"></p></div>
            <div class="field"><label for="c-d">Due date</label><input class="input" id="c-d" name="due" type="date" value="${isoDate(addDays(today(), 14))}" /><p class="field-error" data-err="due"></p></div>
            <div class="field"><label for="c-e">Repeats</label><select class="input" id="c-e" name="every">${Object.entries(EVERY).map(([k, l]) => `<option value="${k}">${l}</option>`).join('')}</select></div>
            <div class="field"><label for="c-dp">Department</label><select class="input" id="c-dp" name="dept">${DEPTS().map((d) => `<option value="${d.key}" ${d.key === 'legal' ? 'selected' : ''}>${esc(d.name)}</option>`).join('')}</select></div>
            <div class="field"><label for="c-o">Owner</label><select class="input" id="c-o" name="owner">${roles.map((r) => `<option value="${r.id}" ${r.id === 'compliance' ? 'selected' : ''}>${esc(r.name)} — ${esc(r.title)}</option>`).join('')}</select></div>
            <div class="field"><label for="c-n">Note</label><input class="input" id="c-n" name="note" maxlength="140" /></div>
          </div>
          <button class="btn btn-primary" type="submit" style="margin-top:8px">Add deadline</button></form>`;
      return C.layout('calendar', { title: 'Compliance calendar', subtitle: 'Taxes, contracts, data protection and checks — repeating ones come back when done', body });
    },
    mount(el) {
      el.querySelectorAll('[data-done]').forEach((b) => b.addEventListener('click', () => C.run(() => CE().doneItem(b.dataset.done), 'Marked done')));
      el.querySelector('[data-cal]')?.addEventListener('submit', (e) => {
        e.preventDefault(); const f = e.currentTarget;
        try { CE().addItem(SA.core.utils.formData(f)); SA.ui.overlay.toast('Deadline added'); SA.app.rerender(); }
        catch (err) { D.showErrors(f, err.details || {}); if (!err.details) SA.ui.overlay.toast(err.message, { type: 'error' }); }
      });
    },
  });

  /* ---------- What-if simulator ---------- */
  const SLIDERS = [
    ['bookingRate', 'Booking commission', '%', 0, 30, 0.5], ['storeRate', 'Store commission', '%', 0, 30, 0.5], ['serviceFee', 'Booking service fee', '%', 0, 15, 0.5],
    ['demand', 'Change in demand (your guess)', '%', -50, 100, 5], ['podMargin', 'Print margin change', '%', -50, 100, 5], ['marketing', 'Extra marketing spend', ' EGP', 0, 200000, 5000], ['agents', 'AI agents cost change', '%', -50, 200, 10],
  ];
  const simOut = (w) => `
    <div class="ceo-sim-res ${w.change >= 0 ? 'up' : 'down'}"><span class="xs muted">Net revenue in the next 30 days</span><b class="num">${C.egp(w.then.net)}</b>
      <span class="adm-delta ${w.change >= 0 ? 'up' : 'down'}">${w.change >= 0 ? '▲' : '▼'} ${C.egp(Math.abs(w.change))} (${w.pct >= 0 ? '+' : ''}${w.pct}%)</span><span class="xs muted">Last 30 days: ${C.egp(w.now.net)}</span></div>
    <div class="table-wrap"><table class="table"><thead><tr><th>Line</th><th class="num">Last 30 days</th><th class="num">With your changes</th></tr></thead><tbody>
      ${SA.services.admin.STREAMS.map(([k, l]) => `<tr><td>${l}</td><td class="num">${C.egp(w.base[k])}</td><td class="num">${C.egp(w.next[k])}</td></tr>`).join('')}
      <tr><td>Costs (refunds, promos, refusals)</td><td class="num">−${C.egp(w.base.cost)}</td><td class="num">−${C.egp(w.next.cost)}</td></tr>
      <tr><td>AI agents and extra marketing</td><td class="num">−${C.egp(w.agentCost)}</td><td class="num">−${C.egp(w.extra)}</td></tr></tbody></table></div>`;
  SA.pages.ceoWhatIf = C.guard({
    title: 'What-if simulator — Super App',
    render() {
      const cur = CE().whatIf({ bookingRate: 10, storeRate: 12, serviceFee: 5 }).current;
      const vals = { ...cur, demand: 0, podMargin: 0, marketing: 0, agents: 0 };
      this._vals = vals;
      const body = `<div class="dash-grid g-1-1">
        <form class="dash-card" data-sim><h2 class="h4">Change something</h2><p class="xs muted">${icon('info', 12)} Based on the last 30 days. Nothing changes until you press Apply.</p>
          ${SLIDERS.map(([k, l, u, min, max, step]) => `<div class="field ceo-slider"><label for="s-${k}">${l} <b class="num" data-val="${k}">${vals[k]}${u}</b></label><input id="s-${k}" type="range" name="${k}" min="${min}" max="${max}" step="${step}" value="${vals[k]}" /></div>`).join('')}
          <div class="row" style="--gap:8px;margin-top:10px"><button class="btn btn-ghost btn-sm" type="reset">Reset</button><button class="btn btn-primary btn-sm" type="button" data-apply>Apply the new rates</button></div></form>
        <section class="dash-card"><h2 class="h4">Result</h2><div data-sim-out>${simOut(CE().whatIf(vals))}</div></section></div>`;
      return C.layout('whatif', { title: 'What-if simulator', subtitle: 'See what a change in prices or costs does to revenue before you make it', body });
    },
    mount(el) {
      const f = el.querySelector('[data-sim]'), out = el.querySelector('[data-sim-out]');
      const read = () => Object.fromEntries(SLIDERS.map(([k]) => [k, +f[k].value]));
      const paint = () => { const v = read(); SLIDERS.forEach(([k, , u]) => { el.querySelector(`[data-val="${k}"]`).textContent = `${(+v[k]).toLocaleString()}${u}`; }); out.innerHTML = simOut(CE().whatIf(v)); };
      f.addEventListener('input', paint);
      f.addEventListener('reset', () => setTimeout(paint, 0));
      el.querySelector('[data-apply]').addEventListener('click', async () => {
        const v = read();
        if (await SA.ui.overlay.confirm({ title: 'Apply the new rates?', text: `Booking commission ${v.bookingRate}%, store commission ${v.storeRate}%, service fee ${v.serviceFee}%. They apply to new bookings and orders. Demand, margin, marketing and agent changes are only guesses and aren’t saved.`, confirmLabel: 'Apply' })) C.run(() => CE().applyRates(v), 'New rates applied');
      });
    },
  });

  /* ---------- All systems ---------- */
  SA.pages.ceoSystems = C.guard({
    title: 'All systems — Super App',
    render() {
      const partners = db().all('accounts').filter((a) => a.role === 'owner');
      const sellers = db().all('sellers').filter((s) => s.status !== 'rejected');
      const staff = db().all('factoryStaff') || [];
      const admins = db().all('accounts').filter((a) => a.role === 'admin');
      const box = (key, ic, title, text, list, sub) => `<section class="dash-card ceo-sys"><div class="row" style="--gap:10px"><span class="ceo-sys-ico">${icon(ic, 20)}</span><span class="grow"><h2 class="h4">${title}</h2><p class="xs muted">${text}</p></span></div>
        ${list ? `<div class="row" style="--gap:8px;margin-top:12px"><select class="input" data-pick="${key}" aria-label="${title}: open as">${list.map(([id, l]) => `<option value="${id}">${esc(l)}</option>`).join('')}</select><button class="btn btn-primary btn-sm" data-open="${key}">Open ${icon('arrowUpRight', 13)}</button></div>` : `<button class="btn btn-primary btn-sm" data-open="${key}" style="margin-top:12px">Open ${icon('arrowUpRight', 13)}</button>`}
        ${sub ? `<p class="xs muted" style="margin-top:8px">${sub}</p>` : ''}</section>`;
      const body = `<p class="small muted">${icon('info', 12)} Every system opens in a new tab, signed in as the person you choose. Each opening is written in the audit log.</p>
        <div class="ceo-systems">
          ${box('site', 'home', 'Super App', 'The site customers use: book, play, shop, fantasy and communities.', null)}
          ${box('admin', 'shield', 'Admin console', 'Open it as yourself (everything) or as any role — see exactly what each agent sees.', admins.map((a) => [a.id, `${a.name} — ${CE().roleOfAccount(a)?.title || ''}`]), `${admins.length} admin accounts`)}
          ${box('partner', 'pitch', 'Partner dashboard', 'Any pitch partner’s own dashboard.', partners.map((a) => [a.id, `${a.company} — ${a.name}`]), `${partners.length} partners`)}
          ${box('seller', 'users', 'Seller dashboard', 'Any seller’s own dashboard.', sellers.map((s) => [s.id, `${s.name} (@${s.handle})`]), `${sellers.length} sellers`)}
          ${box('factory', 'truck', 'Factory system', 'The factory as any member of staff.', staff.map((x) => [x.id, `${x.name} — ${SA.services.factory?.ROLES[x.role]?.label || x.role}`]), `${staff.length} staff`)}
        </div>`;
      return C.layout('systems', { title: 'All systems', subtitle: 'Every system Super App runs — open any of them as anyone', body });
    },
    mount(el) {
      el.querySelectorAll('[data-open]').forEach((b) => b.addEventListener('click', () => { const k = b.dataset.open; const id = el.querySelector(`[data-pick="${k}"]`)?.value || ''; C.run(() => CE().openAs(k, id)); }));
    },
  });

  /* ---------- Every page (site map with test shortcuts) ---------- */
  SA.pages.ceoSiteMap = C.guard({
    title: 'Every page — Super App',
    render() {
      const n = (SA.ui.siteMapGroups || []).reduce((t, g) => t + g.links.length, 0);
      return C.layout('sitemap', { title: 'Every page', subtitle: `${n} pages in every system — each opens in a new tab`, body: `<p class="small muted">${icon('info', 12)} Dashboards need an account: tap a "Sign in as" shortcut first, then open their pages. Test shortcuts (a test order, a finished game, a big request) only change this device’s demo data.</p>${SA.ui.siteMap?.() || ''}` });
    },
  });

  /* ---------- Company audit log ---------- */
  SA.pages.ceoAudit = C.guard({
    title: 'Company audit log — Super App',
    render(ctx) {
      const q = (ctx.query.q || '').trim().toLowerCase();
      const who = ctx.query.who || '';
      const accs = new Map(db().all('accounts').map((a) => [a.id, a]));
      const kindOf = (x) => { const a = accs.get(x.actor); if (x.action?.startsWith('ceo.') || a?.teamRole === 'ceo') return 'ceo'; if (a?.agent) return 'agent'; if (a?.role === 'admin') return 'admin'; if (a?.role === 'owner') return 'partner'; if (a?.role === 'seller') return 'seller'; if (a?.role === 'printer' || x.action?.startsWith('factory.')) return 'factory'; return x.actor === 'customer' ? 'customer' : 'system'; };
      const all = db().all('activity').map((x) => ({ ...x, kind: kindOf(x), who: accs.get(x.actor)?.name || (x.actor === 'customer' ? 'Customer' : 'System') }));
      const list = all.filter((x) => (!who || x.kind === who) && (!q || `${x.text} ${x.action} ${x.who} ${x.ref || ''}`.toLowerCase().includes(q)));
      const K = [['', 'Everyone'], ['ceo', 'CEO'], ['agent', 'AI agents'], ['admin', 'Admin team'], ['partner', 'Partners'], ['seller', 'Sellers'], ['factory', 'Factory'], ['customer', 'Customers'], ['system', 'System']];
      this._list = list;
      const body = `
        <form class="row ceo-search" style="--gap:8px" data-q><input class="input" type="search" name="q" value="${esc(ctx.query.q || '')}" placeholder="Search the log — e.g. refund, PO-, Atlas" aria-label="Search the audit log" /><button class="btn btn-primary btn-sm" type="submit">${icon('search', 14)} Search</button><button class="btn btn-outline btn-sm" type="button" data-xlsx>${icon('file', 14)} Excel</button></form>
        <div class="chips" role="tablist" aria-label="Who">${K.map(([k, l]) => `<a class="chip" href="${SA.core.router.href('/ceo/audit', { who: k || undefined, q: ctx.query.q || undefined })}" aria-pressed="${who === k}">${l} <span class="chip-count">${all.filter((x) => !k || x.kind === k).length}</span></a>`).join('')}</div>
        <section class="dash-card"><ul class="ceo-log">${list.slice(0, 200).map((x) => `<li><span class="xs muted num">${timeAgo(x.at)}</span><span class="grow small">${esc(x.text)}<span class="xs muted" style="display:block">${esc(x.who)} · ${esc(x.action)}${x.ref ? ` · ${esc(x.ref)}` : ''}</span></span><span class="badge ${x.kind === 'ceo' ? 'badge-gold' : x.kind === 'agent' ? 'badge-info' : ''}">${K.find(([k]) => k === x.kind)?.[1] || ''}</span></li>`).join('') || '<li class="small muted">Nothing matches.</li>'}</ul>
          <p class="xs muted" style="margin-top:8px">${icon('lock', 12)} Nobody can delete or change this log — not even the CEO. ${list.length > 200 ? `Showing 200 of ${list.length}; the Excel file has all.` : ''}</p></section>`;
      return C.layout('audit', { title: 'Company audit log', subtitle: 'Every action in every system, signed by who did it', body });
    },
    mount(el, ctx) {
      el.querySelector('[data-q]')?.addEventListener('submit', (e) => { e.preventDefault(); SA.core.router.go(SA.core.router.href('/ceo/audit', { who: ctx.query.who || undefined, q: e.currentTarget.q.value.trim() || undefined }).slice(1)); });
      el.querySelector('[data-xlsx]')?.addEventListener('click', () => {
        const rows = this._list.map((x) => [new Date(x.at).toISOString().slice(0, 16).replace('T', ' '), x.who, x.kind, x.action, x.text, x.ref || '']);
        D.xlsx(`super-app-audit-log-${isoDate(today())}.xlsx`, SA.core.xlsx.dashboard({ title: 'Super App — Company audit log', subtitle: `${rows.length} actions`, data: { name: 'Audit log', columns: [{ header: 'When', width: 18 }, { header: 'Who', width: 22 }, { header: 'Kind', width: 12 }, { header: 'Action', width: 26 }, { header: 'What happened', width: 70 }, { header: 'Reference', width: 16 }], rows }, kpis: [{ label: 'Actions', f: 'COUNT', col: 'When' }], groups: [{ title: 'Actions by kind', by: 'Kind', chart: 'pie' }, { title: 'Actions by person', by: 'Who', chart: 'bar', limit: 20 }] }));
        CE().log('report', 'Downloaded the company audit log (Excel)');
      });
    },
  });
})(window.SA);
