/** CEO system: departments with the org chart, one department, the AI workforce and one agent. */
(function (SA) {
  const { esc, timeAgo } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.ceoCommon;
  const D = SA.pages.dashCommon;
  const CE = () => SA.services.ceo;
  const A = () => SA.pages.adminCommon;
  const SECTIONS = () => A().NAV.filter((n) => n.key);
  const sectionLabel = (k) => (k === '*' ? 'Everything' : SECTIONS().find((n) => n.key === k)?.label || k);
  const actionLabel = (k) => SA.data.org.ACTIONS.find(([x]) => x === k)?.[1] || k;
  const limitText = (r) => (r.limit === Infinity ? 'No limit' : r.limit ? C.egp(r.limit) : 'Can’t move money alone');

  /** One person box in the org chart */
  const node = (r) => `<a class="ceo-node ${r.holder === 'ai' ? 'is-ai' : ''} ${r.status === 'paused' ? 'is-paused' : ''}" href="#/ceo/agent/${r.id}">
    ${C.face(r)}
    <span class="grow"><b class="small">${esc(r.name)}</b><span class="xs muted" style="display:block">${esc(r.title)}</span></span>
    ${r.status === 'paused' ? icon('ban', 14) : r.holder === 'ai' ? icon('bolt', 14) : icon('user', 14)}</a>`;
  /** The org chart: the CEO, then each department head with their people under them */
  const tree = (roles) => {
    const kids = (id) => roles.filter((r) => r.reportsTo === id || (id === 'ceo' && !r.reportsTo && r.id !== 'ceo'));
    const branch = (r) => { const k = kids(r.id); return `<li>${node(r)}${k.length ? `<ul>${k.map(branch).join('')}</ul>` : ''}</li>`; };
    const ceo = roles.find((r) => r.id === 'ceo');
    return `<div class="ceo-tree" role="tree" aria-label="Org chart"><ul><li>${node(ceo)}<ul>${kids('ceo').map(branch).join('')}</ul></li></ul></div>`;
  };
  /** Server only: add or edit a position (title, department, who it reports to, duties). Not the CEO's own box. */
  const below = (roles, id) => { const out = new Set([id]); let grew = true; while (grew) { grew = false; roles.forEach((r) => { if (r.reportsTo && out.has(r.reportsTo) && !out.has(r.id)) { out.add(r.id); grew = true; } }); } return out; };
  const posForm = (r) => {
    const roles = CE().roles();
    const skip = r ? below(roles, r.id) : new Set();
    return `<div class="dash-grid g-1-1" style="margin-top:10px">
      <div class="field"><label for="pf-title">Title</label><input class="input" id="pf-title" name="title" maxlength="120" required value="${esc(r?.title || '')}" /></div>
      <div class="field"><label for="pf-title-ar">Title in Arabic</label><input class="input" id="pf-title-ar" name="titleAr" dir="rtl" maxlength="120" required value="${esc(r?.titleAr || '')}" /></div>
      <div class="field"><label for="pf-dept">Department</label><select class="input" id="pf-dept" name="dept"><option value="">The board (no department)</option>${SA.data.org.DEPTS.map((d) => `<option value="${d.key}" ${r?.dept === d.key ? 'selected' : ''}>${esc(d.name)}</option>`).join('')}</select></div>
      <div class="field"><label for="pf-up">Reports to</label><select class="input" id="pf-up" name="reportsTo">${roles.filter((x) => !skip.has(x.id)).map((x) => `<option value="${x.id}" ${(r ? r.reportsTo : 'ceo') === x.id ? 'selected' : ''}>${esc(x.title)} — ${esc(x.name)}</option>`).join('')}</select></div>
    </div>
    <div class="field"><label for="pf-duties">Duties (one a line)</label><textarea class="input" id="pf-duties" name="duties" rows="4" maxlength="4000">${esc((r?.duties || []).join('\n'))}</textarea></div>`;
  };
  const posValues = (f) => Object.fromEntries(['title', 'titleAr', 'dept', 'reportsTo', 'duties'].map((k) => [k, f.querySelector(`[name="${k}"]`).value]));

  /* ---------- Departments ---------- */
  SA.pages.ceoDepartments = C.guard({
    title: 'Departments — Super App',
    render(ctx) {
      const view = ctx.query.view || 'cards';
      const H = CE().health();
      const B = CE().budgets();
      const roles = CE().roles();
      const body = `
        ${A().tabs('/ceo/departments', [['cards', 'Departments'], ['chart', 'Org chart']], view === 'chart' ? 'chart' : 'cards')}
        ${view === 'chart' ? `<section class="dash-card"><div class="row-between"><h2 class="h4">Who reports to whom</h2><span class="xs muted">${roles.filter((r) => r.holder === 'ai').length} AI agents · ${roles.filter((r) => r.holder === 'human').length} humans</span></div>
          <p class="xs muted">${icon('info', 12)} Tap anyone to see their duties, permissions, limit and work. Humans approve; AI agents do the work and prepare.</p>${tree(roles)}</section>
          ${CE().orgLive?.() ? `<details class="dash-card"><summary class="h4">${icon('plus', 16)} Add a position</summary><form data-pos-add>${posForm(null)}
            <p class="xs muted">${icon('info', 12)} The new box is empty. A person is placed in it from the team screens; an AI agent from the AI panel.</p>
            <button class="btn btn-primary btn-sm" type="submit" style="margin-top:10px">Add the position</button></form></details>` : ''}`
        : `<div class="ceo-depts">${H.map((d, i) => { const b = B.find((x) => x.dept === d.key); return `<a class="dash-card ceo-dcard" href="#/ceo/department/${d.key}" style="--i:${i};--t:${C.TONES[d.tone]}">
            <div class="row-between">${C.ring(d.score, d.tone, 58)}<span class="ceo-dicon" style="--c:${C.TONES[SA.data.org.DEPTS.find((x) => x.key === d.key).tone] || 'var(--turf)'}">${icon(d.icon, 20)}</span></div>
            <h2 class="h4" style="margin-top:10px">${esc(d.name)}</h2><p class="xs muted">${esc(d.text)}</p>
            <p class="xs" style="margin-top:8px"><b>${esc(d.headRole?.name || '')}</b> · ${esc(d.headRole?.title || '')}</p>
            <div class="ceo-dfoot xs"><span>${icon('users', 12)} ${d.people.length} role${d.people.length === 1 ? '' : 's'}</span><span>${icon('wallet', 12)} ${b.pct}% of budget</span></div>
            ${d.issues.length ? `<p class="xs t-danger" style="margin-top:6px">${esc(d.issues.map((x) => x.text).join(' · '))}</p>` : ''}</a>`; }).join('')}</div>`}`;
      return C.layout('departments', { title: 'Departments', subtitle: `${H.length} departments — health, people and budget`, body });
    },
    mount(el) {
      el.querySelector('[data-pos-add]')?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const id = await C.run(() => CE().addPosition(posValues(e.currentTarget)), 'Position added');
        if (id) SA.core.router.go(`/ceo/agent/${id}`);
      });
    },
  });

  SA.pages.ceoDepartment = C.guard({
    title: 'Department — Super App',
    render(ctx) {
      const d = CE().health().find((x) => x.key === ctx.params.key);
      if (!d) throw new SA.services.http.ApiError('Department not found.', 'not_found');
      const b = CE().budgets().find((x) => x.dept === d.key);
      const goals = CE().goals().filter((g) => g.dept === d.key);
      const cal = CE().calendar().filter((x) => x.dept === d.key && !x.done);
      const body = `
        <div class="dash-grid g-2-1">
          <section class="dash-card"><div class="row" style="--gap:14px">${C.ring(d.score, d.tone, 72)}<span class="grow"><h2 class="h4">Health ${d.score}/100</h2><p class="small muted">${esc(d.text)}</p></span></div>
            <ul class="ceo-list" style="margin-top:12px">${d.issues.map((x) => `<li>${icon('alert', 14)}<span class="grow small">${esc(x.text)}</span></li>`).join('') || `<li class="small muted">${icon('check', 14)} Nothing needs attention.</li>`}</ul>
          </section>
          <section class="dash-card"><h2 class="h4">Budget this month</h2>
            <p class="ceo-big num">${C.egp(b.spent)} <span class="small muted">of ${C.egp(b.budget)}</span></p>${C.meter(b.pct, b.pct > 100 ? 'danger' : b.pct > 85 ? 'gold' : 'turf')}
            <p class="xs muted" style="margin-top:6px">Forecast for the month: ${C.egp(b.forecast)}</p>
            <a class="btn btn-ghost btn-sm" href="#/ceo/budgets" style="margin-top:8px">Budgets ${icon('arrowRight', 14)}</a></section>
        </div>
        <section class="dash-card"><h2 class="h4">People and agents</h2>
          <div class="ceo-people">${d.people.map((r) => `<a class="ceo-person" href="#/ceo/agent/${r.id}">${C.face(r)}<span class="grow"><b class="small">${esc(r.name)}</b><span class="xs muted" style="display:block">${esc(r.title)}</span><span class="row" style="--gap:6px;margin-top:4px">${C.roleChip(r)}${C.statusChip(r)}</span></span>${icon('chevronRight', 16)}</a>`).join('')}</div></section>
        <div class="dash-grid g-1-1">
          <section class="dash-card"><h2 class="h4">Goals</h2><ul class="ceo-list">${goals.map((g) => `<li class="ceo-goal"><span class="grow"><b class="small">${esc(g.objective)}</b>${C.meter(g.pct)}</span><b class="num small">${g.pct}%</b></li>`).join('') || '<li class="small muted">No goals for this department yet. <a class="link" href="#/ceo/goals">Add one</a></li>'}</ul></section>
          <section class="dash-card"><h2 class="h4">Deadlines</h2><ul class="ceo-list">${cal.map((x) => `<li><span class="badge ${x.state === 'overdue' ? 'badge-danger' : x.state === 'soon' ? 'badge-gold' : ''}">${SA.core.utils.fmtDate(x.due)}</span><span class="grow small">${esc(x.title)}</span></li>`).join('') || '<li class="small muted">No deadlines.</li>'}</ul></section>
        </div>`;
      return C.layout('departments', { title: esc(d.name), subtitle: `Led by ${esc(d.headRole?.name || '')} — ${esc(d.headRole?.title || '')}`, actions: '<a class="btn btn-ghost btn-sm" href="#/ceo/departments">All departments</a>', body });
    },
  });

  /* ---------- AI workforce ---------- */
  SA.pages.ceoAgents = C.guard({
    title: 'AI workforce — Super App',
    render(ctx) {
      const f = ctx.query.dept || '';
      const all = CE().roles().filter((r) => r.id !== 'ceo');
      const list = all.filter((r) => !f || r.dept === f);
      const ai = all.filter((r) => r.holder === 'ai');
      const cost = ai.reduce((t, r) => t + (r.cost || 0), 0);
      const stopped = ai.some((r) => CE().cfg().agents?.[r.id]?.stopAll);
      const body = `
        <div class="kpi-grid">
          ${D.kpi({ label: 'AI agents', value: ai.length, count: ai.length, ic: 'bolt', sub: `${ai.filter((r) => r.status === 'active').length} working` })}
          ${D.kpi({ label: 'Humans', value: all.length - ai.length + 1, ic: 'user', sub: 'You, the admin manager, the CFO and the lawyers' })}
          ${D.kpi({ label: 'Cost a month', value: C.egp(cost), count: cost, suffix: ' EGP', ic: 'wallet', sub: 'Model usage and tools (estimate)' })}
          ${D.kpi({ label: 'Paused', value: ai.filter((r) => r.status === 'paused').length, ic: 'ban', tone: ai.some((r) => r.status === 'paused') ? 'k-blaze' : '' })}
        </div>
        <section class="dash-card ceo-stop ${stopped ? 'is-on' : ''}">
          <span class="ceo-stop-ico">${icon('alert', 22)}</span>
          <span class="grow"><b>${stopped ? 'Emergency stop is on' : 'Emergency stop'}</b><span class="small muted" style="display:block">${stopped ? 'Every AI agent is paused. Humans keep working.' : 'Pause every AI agent at once — for example if one makes mistakes with money. Humans keep working.'}</span></span>
          ${stopped ? '<button class="btn btn-primary" data-resume-all>Lift the stop</button>' : '<button class="btn btn-blaze" data-stop-all>Stop all agents</button>'}
        </section>
        <div class="chips" role="tablist" aria-label="Departments"><a class="chip" href="#/ceo/agents" aria-pressed="${!f}">All</a>${SA.data.org.DEPTS.map((d) => `<a class="chip" href="${SA.core.router.href('/ceo/agents', { dept: d.key })}" aria-pressed="${f === d.key}">${esc(d.name)}</a>`).join('')}</div>
        <div class="ceo-agents">${list.map((r, i) => { const s = CE().agentStats(r.id); return `<a class="dash-card ceo-agent ${r.status === 'paused' ? 'is-paused' : ''}" href="#/ceo/agent/${r.id}" style="--i:${i}">
          <div class="row" style="--gap:10px">${C.face(r)}<span class="grow"><b>${esc(r.name)}</b><span class="xs muted" style="display:block">${esc(r.title)}</span></span>${C.statusChip(r)}</div>
          <p class="xs muted" style="margin-top:8px">${esc(r.deptInfo?.name || '')}</p>
          <ul class="ceo-duties xs">${r.duties.slice(0, 3).map((x) => `<li>${esc(x)}</li>`).join('')}</ul>
          <div class="ceo-dfoot xs">${C.roleChip(r)}<span>${icon('wallet', 12)} ${limitText(r)}</span>${r.holder === 'ai' ? `<span>${icon('check', 12)} ${s.tasks} tasks</span>` : ''}</div></a>`; }).join('')}</div>`;
      return C.layout('agents', { title: 'AI workforce', subtitle: 'Every role, who holds it, what it does and what it may do', body });
    },
    mount(el) {
      el.querySelector('[data-stop-all]')?.addEventListener('click', async () => {
        const r = await A().reason({ title: 'Stop every AI agent?', text: 'All AI agents are paused at once and can’t sign in to the admin console until you lift the stop.', confirmLabel: 'Stop all agents', danger: true, placeholder: 'e.g. The payables agent prepared a wrong withdrawal' });
        if (r) C.run(() => CE().stopAll(r.reason), (n) => `${n} AI agents paused`);
      });
      el.querySelector('[data-resume-all]')?.addEventListener('click', () => C.run(() => CE().resumeAll(), (n) => `${n} AI agents working again`));
    },
  });

  SA.pages.ceoAgent = C.guard({
    title: 'Agent — Super App',
    render(ctx) {
      const r = CE().getRole(ctx.params.id);
      const s = CE().agentStats(r.id);
      const acts = CE().activity(r.id, { limit: 40 });
      const ai = r.holder === 'ai';
      const editable = r.id !== 'ceo';
      const groups = [...new Set(SA.data.org.ACTIONS.map(([, , g]) => g))];
      const body = `
        <div class="dash-grid g-2-1">
          <section class="dash-card">
            <div class="row" style="--gap:12px">${C.face(r, 'lg')}<span class="grow"><h2 class="h3">${esc(r.name)}</h2><p class="small muted">${esc(r.title)} · ${esc(r.deptInfo?.name || 'Board')}</p><span class="row" style="--gap:6px;margin-top:6px">${C.roleChip(r)}${C.statusChip(r)}</span></span></div>
            ${r.status === 'paused' ? `<p class="fac-banner is-danger" style="margin-top:12px">${icon('ban', 16)} <span>${esc(r.pausedReason)}</span></p>` : ''}
            <h3 class="h5" style="margin-top:16px">Duties</h3>
            <ul class="ceo-duties small">${r.duties.map((x) => `<li>${esc(x)}</li>`).join('')}</ul>
            <h3 class="h5" style="margin-top:16px">Reports to</h3>
            <p class="small">${r.reportsTo ? `<a class="link" href="#/ceo/agent/${r.reportsTo}">${esc(CE().getRole(r.reportsTo).name)} — ${esc(CE().getRole(r.reportsTo).title)}</a>` : 'The board'}</p>
            ${r.accountId && r.id !== 'ceo' ? `<h3 class="h5" style="margin-top:16px">Admin console sections</h3><p class="small">${r.sections.map((k) => `<span class="ceo-tag">${esc(sectionLabel(k))}</span>`).join(' ') || '<span class="muted">None</span>'}</p>` : ''}
          </section>
          <section class="dash-card">
            <h2 class="h4">Work (30 days)</h2>
            <ul class="ceo-list">
              <li><span class="grow small">Tasks done${ai ? ' (demo)' : ''}</span><b class="num">${s.tasks.toLocaleString()}</b></li>
              ${ai ? `<li><span class="grow small">Errors (demo)</span><b class="num ${s.errors ? 't-danger' : ''}">${s.errors}</b></li>
              <li><span class="grow small">Accuracy (demo)</span><b class="num">${s.accuracy}%</b></li>
              <li><span class="grow small">Average time per task (demo)</span><b class="num">${s.avgMinutes} min</b></li>
              <li><span class="grow small">Sent up to a human (demo)</span><b class="num">${s.escalations}</b></li>
              <li><span class="grow small">Model usage (demo)</span><b class="num">${(s.tokens / 1e6).toFixed(1)}M tokens</b></li>
              <li><span class="grow small">Cost a month</span><b class="num">${C.egp(s.cost)}</b></li>` : ''}
              <li><span class="grow small">Money limit</span><b class="num">${limitText(r)}</b></li>
              <li><span class="grow small">Last action</span><b class="small">${s.last ? timeAgo(s.last) : '—'}</b></li>
            </ul>
            ${editable && r.accountId ? `<div class="row" style="--gap:8px;margin-top:14px;flex-wrap:wrap">
              ${r.status === 'paused' ? `<button class="btn btn-primary btn-sm" data-resume>${icon('play', 14)} Resume</button>` : `<button class="btn btn-blaze btn-sm" data-pause>${icon('ban', 14)} Pause</button>`}
              <button class="btn btn-outline btn-sm" data-open-as>${icon('arrowUpRight', 14)} Open the admin console as ${esc(r.name)}</button></div>` : ''}
          </section>
        </div>
        ${editable && CE().orgLive?.() ? `<form class="dash-card" data-pos-form>
          <div class="row-between"><h2 class="h4">Position</h2><span class="row" style="--gap:8px"><button class="btn btn-ghost btn-sm" type="button" data-pos-remove>${icon('trash', 14)} Remove</button><button class="btn btn-primary btn-sm" type="submit">Save</button></span></div>
          <p class="xs muted">${icon('info', 12)} ${r.vacant ? 'Nobody holds this position yet.' : `<span>Held by</span> <b data-i18n-skip>${esc(r.name)}</b>.`} <span>The title, department, who it reports to and the duties are saved on the server.</span></p>
          ${posForm(r)}</form>` : ''}
        ${editable ? `<form class="dash-card" data-perm-form>
          <div class="row-between"><h2 class="h4">Permissions & limit</h2><button class="btn btn-primary btn-sm" type="submit">Save changes</button></div>
          <p class="xs muted">${icon('info', 12)} Separation of duties: whoever prepares a withdrawal doesn’t send it; whoever adjusts balances doesn’t approve them. Money above the limit always comes to you.</p>
          <div class="field" style="max-width:260px;margin-top:10px"><label for="ag-limit">Most money this role may move alone (EGP)</label><input class="input num" id="ag-limit" name="limit" type="number" min="0" step="100" value="${r.limit === Infinity ? '' : r.limit}" /></div>
          <div class="ceo-perms">${groups.map((g) => `<fieldset><legend class="xs muted">${g}</legend>${SA.data.org.ACTIONS.filter(([, , x]) => x === g).map(([k, l]) => `<label class="choice"><input type="checkbox" name="can" value="${k}" ${r.can.includes(k) ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span><span class="grow small">${l}</span></label>`).join('')}</fieldset>`).join('')}</div>
          ${r.accountId ? `<h3 class="h5" style="margin-top:12px">Admin console sections</h3><div class="ceo-secs">${SECTIONS().map((n) => `<label class="choice"><input type="checkbox" name="sec" value="${n.key}" ${r.sections.includes('*') || r.sections.includes(n.key) ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span><span class="grow small">${esc(n.label)}</span></label>`).join('')}</div>` : ''}
        </form>` : ''}
        <section class="dash-card"><div class="row-between"><h2 class="h4">Activity log</h2><span class="xs muted">${icon('info', 12)} Its own actions, and what happened in its area (marked demo until the real agent runs)</span></div>
          <ul class="ceo-log">${acts.map((x) => `<li><span class="xs muted num">${timeAgo(x.at)}</span><span class="grow small">${esc(x.text)}<span class="xs muted" style="display:block">${esc(x.actorName)} · ${esc(x.action)}</span></span>${x.own ? '<span class="badge badge-turf">Own</span>' : '<span class="badge">Demo</span>'}</li>`).join('') || '<li class="small muted">Nothing yet.</li>'}</ul></section>`;
      return C.layout('agents', { title: esc(r.name), subtitle: esc(r.title), actions: '<a class="btn btn-ghost btn-sm" href="#/ceo/agents">All agents</a>', body });
    },
    mount(el, ctx) {
      const id = ctx.params.id;
      el.querySelector('[data-pause]')?.addEventListener('click', async () => {
        const r = await A().reason({ title: 'Pause this role?', text: 'It can’t sign in to the admin console until you resume it.', confirmLabel: 'Pause', danger: true });
        if (r) C.run(() => CE().setAgent(id, { status: 'paused' }, r.reason), 'Paused');
      });
      el.querySelector('[data-resume]')?.addEventListener('click', () => C.run(() => CE().setAgent(id, { status: 'active' }), 'Working again'));
      el.querySelector('[data-open-as]')?.addEventListener('click', () => C.run(() => CE().openAs('admin', CE().getRole(id).accountId)));
      el.querySelector('[data-pos-form]')?.addEventListener('submit', (e) => { e.preventDefault(); C.run(() => CE().savePosition(id, posValues(e.currentTarget)), 'Position saved'); });
      el.querySelector('[data-pos-remove]')?.addEventListener('click', async () => {
        if (!(await SA.ui.overlay.confirm({ title: 'Remove this position?', text: 'An AI agent sitting in it loses the position. Positions that report to it must be moved first.', confirmLabel: 'Remove', danger: true }))) return;
        if (await C.run(() => CE().removePosition(id), 'Position removed')) SA.core.router.go('/ceo/departments', { replace: true });
      });
      el.querySelector('[data-perm-form]')?.addEventListener('submit', (e) => {
        e.preventDefault();
        const f = e.currentTarget;
        const can = [...f.querySelectorAll('[name="can"]:checked')].map((x) => x.value);
        const secs = [...f.querySelectorAll('[name="sec"]:checked')].map((x) => x.value);
        const all = SECTIONS().length === secs.length;
        const lim = f.querySelector('[name="limit"]').value;
        C.run(() => CE().setAgent(id, { can, ...(f.querySelector('[name="sec"]') ? { sections: all ? ['*'] : secs } : {}), ...(lim !== '' ? { limit: lim } : {}) }), 'Saved — the role works with the new permissions now');
      });
    },
  });
})(window.SA);
