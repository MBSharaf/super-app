/**
 * The CEO system and admin console on the real API (backend Phase 7 part 1). Loaded after ceoService and adminService.
 *
 * The CEO pages read their local data through SA.services.ceo. In API mode this file loads the server's kill switches
 * and AI agents into the same local shape (switch state, agent status by name), and sends the CEO's changes to the
 * server: switches (PUT /ceo/switches/kill.*), pausing / resuming an agent, and the emergency stop / release. The pages
 * and their design stay as they are; demo mode (GitHub Pages) is unchanged.
 *
 * Also exposes SA.services.liveAdmin with thin helpers for the API-only parts (agents' keys, org chart, integrations,
 * API catalogue, AI panel, audit, reports) so new screens can use them. Money screens stay on demo data until Phase 5.
 */
(function (SA) {
  const api = SA.services.api;
  const ceo = SA.services.ceo;
  if (!api || !ceo) return;

  const live = () => api.isLive();
  const demo = { setSwitch: ceo.setSwitch, setAgent: ceo.setAgent, stopAll: ceo.stopAll, resumeAll: ceo.resumeAll };
  let agentsByName = {};        // server agent name → { id, status }
  let loading = null;

  const toast = (err) => SA.ui.overlay?.toast(err.message, { type: 'error' });
  const db = () => SA.services.db;
  const saveCfg = (patch) => db().setSettings({ ceo: { ...ceo.cfg(), ...patch } });
  const roleName = (id) => (SA.data.org?.ROLES || []).find((r) => r.id === id)?.name;

  /* ---------------- Server → local ---------------- */

  const load = () => {
    if (!live() || !ceo.ceoMode()) return Promise.resolve();
    if (loading) return loading;
    loading = Promise.all([api.get('/ceo/switches'), api.get('/ceo/agents')]).then(([switches, agents]) => {
      const local = { ...(ceo.cfg().switches || {}) };
      const maintenance = { ...(db().settings().maintenance || {}) };
      switches.forEach((s) => {
        const key = s.key.replace(/^kill\./, '');
        if (key === 'bookings' || key === 'shop') { maintenance[key] = !s.isEnabled; if (!s.isEnabled) maintenance.message = s.reason || maintenance.message; }
        else local[key] = { on: s.isEnabled, reason: s.reason || '', at: s.changedAt ? Date.parse(s.changedAt) : 0 };
      });
      agentsByName = Object.fromEntries(agents.map((a) => [a.name, { id: a.id, status: a.status }]));
      const roles = { ...(ceo.cfg().agents || {}) };
      (SA.data.org?.ROLES || []).filter((r) => r.holder === 'ai' && agentsByName[r.name]).forEach((r) => {
        const a = agents.find((x) => x.name === r.name);
        roles[r.id] = { ...(roles[r.id] || {}), status: a.status === 'Active' ? 'active' : 'paused', reason: a.pausedReason || '', stopAll: a.status === 'EmergencyStopped' };
      });
      saveCfg({ switches: local, agents: roles });
      db().setSettings({ maintenance });
      SA.app.rerender?.();
    }).catch(toast).finally(() => { setTimeout(() => { loading = null; }, 30_000); });
    return loading;
  };

  /* ---------------- Local changes → server ---------------- */

  ceo.setSwitch = (key, on, reason = '') => {
    demo.setSwitch(key, on, reason);
    if (live()) api.put(`/ceo/switches/kill.${key}`, { enabled: !!on, reason: on ? null : reason.trim() }).catch(toast);
  };

  ceo.setAgent = (id, patch, reason = '') => {
    const result = demo.setAgent(id, patch, reason);
    const agent = agentsByName[roleName(id)];
    if (live() && agent && patch.status) {
      const call = patch.status === 'paused' ? api.post(`/ceo/agents/${agent.id}/pause`, { reason: reason.trim() }) : api.post(`/ceo/agents/${agent.id}/resume`);
      call.catch(toast);
    }
    return result;
  };

  ceo.stopAll = (reason) => {
    const n = demo.stopAll(reason);
    if (live()) api.post('/ceo/agents/emergency-stop', { reason: reason.trim() }).catch(toast);
    return n;
  };

  ceo.resumeAll = () => {
    const n = demo.resumeAll();
    if (live()) api.post('/ceo/agents/emergency-release').catch(toast);
    return n;
  };

  /* ---------------- API-only helpers ---------------- */

  const q = (params = {}) => { const s = new URLSearchParams(Object.entries(params).filter(([, v]) => v != null && v !== '')).toString(); return s ? `?${s}` : ''; };

  SA.services.liveAdmin = {
    load,
    agents: () => api.get('/ceo/agents'),
    agent: (id) => api.get(`/ceo/agents/${id}`),
    createAgent: (body) => api.post('/ceo/agents', body),
    /** The key is in the answer once: { key, prefix, header: 'X-Api-Key' } */
    createKey: (id, name, rateLimitPerMinute) => api.post(`/ceo/agents/${id}/keys`, { name, rateLimitPerMinute }),
    revokeKey: (id, keyId) => api.post(`/ceo/agents/${id}/keys/${keyId}/revoke`),
    aiPanel: () => api.get('/ceo/ai'),
    org: () => api.get('/ceo/org'),
    integrations: () => api.get('/ceo/integrations'),
    catalogue: () => api.get('/ceo/api-catalogue'),
    audit: (params) => api.get(`/admin/audit${q(params)}`),
    overview: () => api.get('/admin/overview'),
    customers: (params) => api.get(`/admin/customers${q(params)}`),
    reportUrl: (kind, from, to) => `${api.BASE}/ceo/reports/${kind}${q({ from, to })}`,
  };

  api.ready.then(() => { if (live()) { load(); window.addEventListener('hashchange', () => load()); } });
})(window.SA);
