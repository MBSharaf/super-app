/**
 * The CEO org chart on the real API (backend R2). Loaded after liveAdmin.
 *
 * The departments page, the org chart, the AI workforce and the agent page read SA.data.org (DEPTS, ROLES). In API mode
 * this file loads GET /ceo/org and rewrites those lists in place with the server's departments and positions: titles,
 * departments, who reports to whom, duties and who holds each box (a person, an AI agent or nobody). A server position
 * keeps the site role with the same title (its id, permissions, money limit and admin sections — those move to the server
 * with the team screens); positions the CEO adds get `pos-<id>`. Demo mode (GitHub Pages) is unchanged.
 *
 * SA.services.ceo gains orgLive() (true once the server chart is in), loadOrg() and addPosition / savePosition /
 * removePosition for POST / PUT / DELETE /ceo/org/positions.
 */
(function (SA) {
  const api = SA.services.api;
  const ceo = SA.services.ceo;
  const ORG = SA.data.org;
  if (!api || !ceo || !ORG) return;

  const live = () => api.isLive();
  const base = { depts: ORG.DEPTS.map((d) => ({ ...d })), roles: ORG.ROLES.map((r) => ({ ...r })) };
  let chart = null;             // the last GET /ceo/org
  let loading = null;
  const roleIdOf = new Map();   // position id → site role id

  const toast = (err) => SA.ui.overlay?.toast(err.message, { type: 'error' });

  /** Position id → role id seen before, so renaming a position keeps its role (and its page address) */
  const KNOWN = 'sa.ceo.orgIds';
  const known = (() => { try { return JSON.parse(localStorage.getItem(KNOWN) || '{}'); } catch { return {}; } })();
  const remember = () => { Object.assign(known, Object.fromEntries(roleIdOf)); try { localStorage.setItem(KNOWN, JSON.stringify(known)); } catch { /* private mode */ } };

  /** The site role a server position stands for: the one it was before, same title, else the box at the top is the CEO */
  const match = (p, used) => {
    const before = known[p.id] && base.roles.find((r) => r.id === known[p.id] && !used.has(r.id));
    if (before) return before;
    const byTitle = base.roles.find((r) => r.title === p.titleEn && !used.has(r.id));
    if (byTitle) return byTitle;
    return !p.reportsToId && !p.departmentId && !used.has('ceo') ? base.roles.find((r) => r.id === 'ceo') : null;
  };

  /** Server chart → SA.data.org (in place, so every page and service sees it) */
  const apply = (org) => {
    const deptKey = Object.fromEntries(org.departments.map((d) => [d.id, d.key]));
    const used = new Set();
    roleIdOf.clear();
    const pairs = org.positions.map((p) => { const r = match(p, used); const id = r?.id || `pos-${p.id}`; used.add(id); roleIdOf.set(p.id, id); return [p, r]; });
    const roles = pairs.map(([p, r]) => {
      const h = p.holder || {};
      return {
        ...(r || { sections: [], can: [], limit: 0 }),
        id: roleIdOf.get(p.id), posId: p.id, title: p.titleEn, titleAr: p.titleAr,
        dept: deptKey[p.departmentId] || null, reportsTo: p.reportsToId ? roleIdOf.get(p.reportsToId) || null : null,
        holder: h.kind === 'agent' ? 'ai' : 'human', vacant: h.kind === 'vacant', holderUserId: h.userId || null,
        name: h.name || 'Vacant', duties: p.duties || [], sort: p.sortOrder,
      };
    });
    // The admin manager's role is the fallback for admin accounts without a role — keep it if the server has none
    ['ceo', 'hq'].forEach((id) => { if (!roles.some((r) => r.id === id)) roles.push({ ...base.roles.find((r) => r.id === id) }); });
    ORG.ROLES.splice(0, ORG.ROLES.length, ...roles);
    remember();
    org.departments.forEach((d) => {
      const local = ORG.DEPTS.find((x) => x.key === d.key);
      if (local) Object.assign(local, { name: d.nameEn, nameAr: d.nameAr, deptId: d.id });
      else ORG.DEPTS.push({ key: d.key, name: d.nameEn, nameAr: d.nameAr, deptId: d.id, icon: 'grid', tone: 'info', text: '' });
    });
    chart = org;
  };

  const load = (force = false) => {
    if (!live() || !ceo.ceoMode()) return Promise.resolve();
    if (loading && !force) return loading;
    loading = api.get('/ceo/org').then((org) => { apply(org); SA.app.rerender?.(); })
      .catch(toast).finally(() => { setTimeout(() => { loading = null; }, 30_000); });
    return loading;
  };

  /** Body for POST / PUT: the holder stays as it is (people are placed from the team screens) */
  const body = (v, cur) => ({
    departmentId: ORG.DEPTS.find((d) => d.key === v.dept)?.deptId ?? null,
    reportsToId: v.reportsTo ? ORG.ROLES.find((r) => r.id === v.reportsTo)?.posId ?? null : null,
    titleEn: (v.title || '').trim(), titleAr: (v.titleAr || '').trim(), holderUserId: cur?.holderUserId || null,
    duties: String(v.duties || '').split('\n').map((x) => x.trim()).filter(Boolean), sortOrder: cur?.sort ?? 500,
  });

  Object.assign(ceo, {
    orgLive: () => live() && !!chart,
    loadOrg: load,
    /** Adds a position; resolves with the new role id */
    async addPosition(v) {
      const p = await api.post('/ceo/org/positions', body(v));
      await load(true);
      return roleIdOf.get(p.id);
    },
    async savePosition(id, v) {
      const cur = ORG.ROLES.find((r) => r.id === id);
      await api.put(`/ceo/org/positions/${cur.posId}`, body(v, cur));
      await load(true);
    },
    async removePosition(id) {
      const cur = ORG.ROLES.find((r) => r.id === id);
      await api.del(`/ceo/org/positions/${cur.posId}`);
      await load(true);
    },
  });

  api.ready.then(() => { if (live()) { load(); SA.core.router.onChange(() => load()); } });
})(window.SA);
