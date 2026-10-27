/**
 * CEO system: integrations (#/ceo/integrations) and the API catalogue (#/ceo/apis) — server only (backend R1).
 * Integrations: each outside service (payments, email, SMS, maps, football…) with its mode (Simulated / Test / Live),
 * whether its secret is in the server's .env, today's calls and a connection test. Secrets are never shown or typed here.
 * API catalogue: every endpoint group — what it does, who calls it, its permission, agent traffic in 24 hours — and the
 * groups with a kill switch can be turned off. Demo mode (no server) explains that these need the server.
 */
(function (SA) {
  const { esc, timeAgo } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.ceoCommon;
  const A = () => SA.pages.adminCommon;
  const api = () => SA.services.api;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });
  const at = (iso) => (iso ? new Date(iso.endsWith('Z') ? iso : `${iso}Z`).getTime() : 0);
  const serverOnly = (what) => `<section class="dash-card"><p class="small muted">${icon('info', 14)} ${what} come from the server. Open the Command Center on the API site to see them.</p></section>`;

  /* ---------- Integrations ---------- */
  const STATUS = { Healthy: ['badge-turf', 'Working'], Degraded: ['badge-gold', 'Slow or failing'], Down: ['badge-danger', 'Not working'], Unknown: ['', 'Off'] };
  const CATEGORY = { Payment: 'Payments', Courier: 'Delivery', Sms: 'SMS', FootballData: 'Football data', AiModel: 'AI models', StorageOrEInvoice: 'Files and e-receipts', Geocoding: 'Maps and places' };
  const catName = (c) => CATEGORY[c] || c;
  const MODES = [['Simulated', 'Simulated (no real calls)'], ['Test', 'Test account'], ['Live', 'Live']];
  const card = (x) => {
    const [cls, label] = STATUS[x.status] || ['', x.status];
    return `<section class="dash-card" data-int="${esc(x.key)}">
      <div class="row-between" style="align-items:flex-start;gap:10px"><span class="grow"><b class="small">${esc(x.name)}</b>
        <span class="xs muted" style="display:block">${esc(catName(x.category))}${x.isPrimary ? ' · <b>used for this</b>' : ''}</span></span><span class="badge ${cls}">${label}</span></div>
      <p class="xs" style="margin-top:8px">${x.secretName ? `${icon(x.secretSet ? 'check' : 'alert', 12)} <span class="mono">${esc(x.secretName)}</span> ${x.secretSet ? '<span class="muted">is in the server’s .env</span>' : '<span class="t-danger">is missing from the server’s .env</span>'}` : `<span class="muted">${icon('info', 12)} Needs no secret</span>`}</p>
      <p class="xs muted" style="margin-top:4px">${x.today ? `${x.today.calls.toLocaleString()} calls today${x.today.errors ? ` · <span class="t-danger">${x.today.errors} errors</span>` : ''}${x.dailyQuota ? ` · quota ${x.dailyQuota.toLocaleString()}/day` : ''}` : 'No calls today'}${x.lastCallAt ? ` · last call ${timeAgo(at(x.lastCallAt))}` : ''}</p>
      ${x.lastError ? `<p class="xs t-danger" style="margin-top:4px">${icon('alert', 12)} ${esc(x.lastError)}${x.lastErrorAt ? ` · ${timeAgo(at(x.lastErrorAt))}` : ''}</p>` : ''}
      <form class="row wrap" style="--gap:8px;margin-top:12px" data-int-form>
        <select class="input" name="mode" aria-label="${esc(x.name)}: mode">${MODES.map(([v, l]) => `<option value="${v}" ${x.mode === v ? 'selected' : ''}>${l}</option>`).join('')}</select>
        <label class="choice xs"><input type="checkbox" name="isEnabled" ${x.isEnabled ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span>On</label>
        <label class="choice xs"><input type="checkbox" name="isPrimary" ${x.isPrimary ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span>Use this one</label>
        <span class="grow"></span><button class="btn btn-ghost btn-sm" type="button" data-test>${icon('refresh', 13)} Test</button><button class="btn btn-primary btn-sm" type="submit">Save</button></form>
    </section>`;
  };

  SA.pages.ceoIntegrations = C.guard({
    title: 'Integrations — Super App',
    async render() {
      const head = `<p class="small muted">${icon('lock', 12)} Keys and passwords live only in the server’s .env file — the owner puts them there. Here you choose the mode and see whether each one works. Live and Test need their key first.</p>`;
      if (!api().isLive()) return C.layout('integrations', { title: 'Integrations', subtitle: 'Payments, email, SMS, maps, football data and the other outside services', body: head + serverOnly('Integrations') });
      const list = await api().get('/ceo/integrations');
      const cats = [...new Set(list.map((x) => x.category))];
      const body = `${head}${cats.map((cat) => `<h2 class="h4" style="margin:18px 0 10px">${esc(catName(cat))}</h2><div class="ceo-agents">${list.filter((x) => x.category === cat).map(card).join('')}</div>`).join('')}`;
      return C.layout('integrations', { title: 'Integrations', subtitle: 'Payments, email, SMS, maps, football data and the other outside services', body });
    },
    mount(el) {
      el.querySelectorAll('[data-int]').forEach((box) => {
        const key = box.dataset.int;
        const form = box.querySelector('[data-int-form]');
        form.addEventListener('submit', (e) => {
          e.preventDefault();
          C.run(() => api().put(`/ceo/integrations/${key}`, { mode: form.mode.value, isEnabled: form.isEnabled.checked, isPrimary: form.isPrimary.checked, config: null }), 'Saved');
        });
        box.querySelector('[data-test]').addEventListener('click', async () => {
          try { const r = await api().post(`/ceo/integrations/${key}/test`); toast(r.status === 'Healthy' ? 'It works' : `${STATUS[r.status]?.[1] || r.status}${r.lastError ? ` — ${r.lastError}` : ''}`, r.status === 'Healthy' ? 'success' : 'error'); SA.app.rerender(); }
          catch (err) { toast(err.message, 'error'); }
        });
      });
    },
  });

  /* ---------- API catalogue ---------- */
  SA.pages.ceoApis = C.guard({
    title: 'API catalogue — Super App',
    async render() {
      const sub = 'Every part of the API: what it does, who uses it and the AI agents’ traffic';
      if (!api().isLive()) return C.layout('apis', { title: 'API catalogue', subtitle: sub, body: serverOnly('The API groups') });
      const list = await api().get('/ceo/api-catalogue');
      const calls = list.reduce((t, g) => t + g.agentCalls24h, 0);
      const body = `<p class="small muted">${icon('info', 12)} ${list.length} groups · ${calls.toLocaleString()} agent calls in the last 24 hours. A group with a switch can be turned off at once — it answers “paused” everywhere until you turn it back on.</p>
        <section class="dash-card table-card"><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Group</th><th>Who uses it</th><th>Permission</th><th class="r">Agent calls (24 h)</th><th>On</th></tr></thead><tbody>
        ${list.map((g) => `<tr class="${g.enabled === false ? 'is-dim' : ''}"><td class="small"><b>${esc(g.name)}</b><span class="xs muted" style="display:block">${esc(g.purpose)}</span><span class="xs muted mono" style="display:block">${esc(g.path)}</span></td>
          <td class="small">${esc(g.callers)}</td><td class="xs mono">${esc(g.permission)}</td><td class="r num">${g.agentCalls24h.toLocaleString()}</td>
          <td>${g.switchKey ? `<button class="ceo-toggle" role="switch" aria-checked="${g.enabled !== false}" aria-label="${esc(g.name)}" data-group="${esc(g.group)}" data-on="${g.enabled !== false}"><i></i></button>` : '<span class="xs muted">Always on</span>'}</td></tr>`).join('')}
        </tbody></table></div></section>`;
      return C.layout('apis', { title: 'API catalogue', subtitle: sub, body });
    },
    mount(el) {
      el.querySelectorAll('[data-group]').forEach((b) => b.addEventListener('click', async () => {
        const group = b.dataset.group;
        if (b.dataset.on === 'true') {
          const r = await A().reason({ title: `Turn off ${b.getAttribute('aria-label')}?`, text: 'It stops at once for everyone who calls it, and the change is logged.', label: 'Reason', confirmLabel: 'Turn off', danger: true });
          if (r) C.run(() => api().put(`/ceo/api-catalogue/${group}`, { enabled: false, reason: r.reason }), 'Turned off');
        } else C.run(() => api().put(`/ceo/api-catalogue/${group}`, { enabled: true, reason: null }), 'Turned back on');
      }));
    },
  });
})(window.SA);
