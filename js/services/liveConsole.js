/**
 * The admin console's people, bookings and audit on the real API (backend 8.6d). Loaded after liveMoney.
 *
 * In API mode, for a team member with the permission, these admin methods answer from the server instead of the
 * demo data: customers (block, unblock, pay-upfront rule, notes), pitch partners (suspend, reactivate), the pitch
 * bookings list and the audit log. The pages read synchronously, so each list is loaded once (and again after a
 * minute or an action) and the page re-renders when it lands; a drawer repaints on the `adm:refresh` event.
 * What the server doesn't do yet answers with a clear message: wallet credit and Super Points from the drawer,
 * refusal resets, commission deals and closing a pitch from the partner drawer.
 */
(function (SA) {
  const api = SA.services.api;
  const AD = SA.services.admin;
  if (!api || !AD) return;
  const { ApiError } = SA.services.http;
  const demo = { ...AD };
  const session = () => SA.services.session;
  const live = (perm) => api.isLive() && !!session()?.me() && session().can(perm);
  const at = (iso) => (iso ? new Date(iso.endsWith('Z') ? iso : `${iso}Z`).getTime() : 0);
  const toast = (err) => SA.ui.overlay?.toast(err.message, { type: 'error' });
  /** An open drawer repaints itself (a page re-render would close it); the page refreshes when the drawer closes */
  const repaint = () => {
    if (document.querySelector('.adm-drawer')) document.dispatchEvent(new Event('adm:refresh'));
    else SA.app?.rerender?.();
  };

  /** A cached GET: answers what it has (or `empty`), loads when missing or older than a minute, re-renders on arrival */
  const cached = () => {
    const map = new Map();
    return {
      read(key, path, map_, empty) {
        const hit = map.get(key);
        if (!hit || (!hit.loading && Date.now() - hit.at > 60000)) {
          map.set(key, { ...hit, loading: true });
          api.get(path).then((d) => { map.set(key, { data: map_(d), at: Date.now() }); repaint(); },
            (err) => { map.set(key, { data: hit?.data ?? empty, at: Date.now() }); toast(err); });
        }
        return hit?.data ?? empty;
      },
      clear: () => map.clear(),
    };
  };

  /* ---------------- Audit log ---------------- */
  const KIND = { Admin: 'admin', Ceo: 'admin', Partner: 'partner', Seller: 'seller', Factory: 'printer', Site: 'customer', ApiOrAgent: 'system' };
  const audits = cached();
  AD.audit = (f = {}) => {
    if (!live('audit.read')) return demo.audit(f);
    const path = `/admin/audit?pageSize=200${f.from ? `&from=${f.from}` : ''}${f.to ? `&to=${f.to}` : ''}`;
    const rows = audits.read(path, path, (d) => (d.items || d).map((x) => ({
      at: at(x.at), kind: KIND[x.system] || 'system', actorName: x.agent || x.actor || 'System', text: x.summary, action: x.action,
      ref: x.entityId ? `${x.entityType || ''} ${x.entityId}`.trim() : '',
    })), []);
    const s = (f.q || '').trim().toLowerCase();
    return rows.filter((x) => (!f.who || x.kind === f.who) && (!s || `${x.text} ${x.action} ${x.ref} ${x.actorName}`.toLowerCase().includes(s)));
  };

  /* ---------------- Customers ---------------- */
  const PREPAY = { Automatic: 'auto', Always: 'always', Never: 'never' };
  const PREPAY_BACK = { auto: 'Automatic', always: 'Always', never: 'Never' };
  const REFUSE_LIMIT = 2;
  const STATUS = (b) => {
    if (b.status === 'NoShow') return 'no-show';
    if (b.status.startsWith('Cancelled')) return 'cancelled';
    if (b.status === 'Played' || b.status === 'CheckedIn') return 'completed';
    return b.date < SA.core.utils.isoDate(new Date()) ? 'completed' : 'upcoming';
  };
  /** Server note: one line per note, "yyyy-mm-ddTHH:mmZ · Name: text" (UTC, newest first) */
  const notesOf = (note) => (note || '').split('\n').filter(Boolean).map((line) => {
    const m = line.match(/^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}Z) · ([^:]+): (.*)$/);
    return m ? { text: m[3], by: m[2], at: new Date(m[1]).getTime() } : { text: line, by: 'Team', at: Date.now() };
  });
  const fromCustomer = (c, d = null) => ({
    id: `u:${c.id}`, apiId: c.id, live: true, name: c.fullName, email: c.email || '', phone: c.phone || '', city: '',
    bookings: c.bookings, orders: c.orders, items: 0, spent: 0, last: at(c.lastSeenAt) || at(c.createdAt), first: at(c.createdAt),
    refused: c.codRefusals, blocked: c.status !== 'Active', blockedReason: d?.blockedReason || '', prepay: PREPAY[c.mustPrepay] || 'auto',
    needsPrepay: c.mustPrepay === 'Always' || (c.mustPrepay === 'Automatic' && c.codRefusals >= REFUSE_LIMIT), limit: REFUSE_LIMIT,
    notes: notesOf(d?.adminNote), note: d?.adminNote || '', credits: [], podList: [], orderList: [],
    bookingList: (d?.recentBookings || []).map((b) => ({ id: b.code, pitchName: `${b.pitch} · ${b.venue}`, date: b.date, hour: b.startHour, status: STATUS(b), amount: b.total })),
  });
  const people = cached();
  const details = new Map();
  const customerList = () => people.read('customers', '/admin/customers?pageSize=100', (d) => (d.items || d).map((c) => fromCustomer(c)), []);
  const loadCustomer = (apiId) => api.get(`/admin/customers/${apiId}`).then((d) => { details.set(apiId, fromCustomer(d.customer, d)); repaint(); return details.get(apiId); });
  const isLiveId = (id) => String(id).startsWith('u:');
  const liveCustomer = (id) => {
    const apiId = String(id).slice(2);
    if (!details.has(apiId)) {
      const row = customerList().find((c) => c.id === id);
      if (!row) throw new ApiError('Customer not found.', 'not_found');
      details.set(apiId, row);
      loadCustomer(apiId).catch(toast);
    }
    return details.get(apiId);
  };
  /** Runs a customer action on the server, then refreshes the drawer and the list */
  const act = async (id, path, body) => {
    const apiId = String(id).slice(2);
    const d = await api.post(`/admin/customers/${apiId}/${path}`, body);
    details.set(apiId, fromCustomer(d.customer, d));
    people.clear();
    repaint();
  };
  const notOnServer = () => { throw new ApiError('This isn’t on the server yet — it works in the demo only.', 'not_available'); };

  AD.customers = () => (live('customers.read') ? customerList() : demo.customers());
  AD.customer = (id) => (isLiveId(id) ? liveCustomer(id) : demo.customer(id));
  AD.blockCustomer = (id, reason) => {
    if (!isLiveId(id)) return demo.blockCustomer(id, reason);
    if (!reason || reason.trim().length < 3) throw new ApiError('Write the reason.', 'invalid');
    return act(id, 'block', { reason: reason.trim() });
  };
  AD.unblockCustomer = (id) => (isLiveId(id) ? act(id, 'unblock') : demo.unblockCustomer(id));
  AD.setPrepay = (id, mode) => (isLiveId(id) ? act(id, 'prepay', { rule: PREPAY_BACK[mode] || 'Automatic' }) : demo.setPrepay(id, mode));
  AD.noteCustomer = (id, text) => {
    if (!isLiveId(id)) return demo.noteCustomer(id, text);
    const t = String(text || '').trim().replace(/\s+/g, ' ');
    if (t.length < 2) throw new ApiError('Write a note first.', 'invalid');
    const line = `${new Date().toISOString().slice(0, 16)}Z · ${session().me().fullName}: ${t.slice(0, 300)}`;
    return act(id, 'note', { note: [line, liveCustomer(id).note].filter(Boolean).join('\n').slice(0, 1000) });
  };
  AD.creditCustomer = (id, ...rest) => (isLiveId(id) ? notOnServer() : demo.creditCustomer(id, ...rest));
  AD.adjustPoints = (id, ...rest) => (isLiveId(id) ? notOnServer() : demo.adjustPoints(id, ...rest));
  AD.resetRefusals = (id) => (isLiveId(id) ? notOnServer() : demo.resetRefusals(id));

  /* ---------------- Pitch partners ---------------- */
  const fromPartner = (p) => ({
    id: `p:${p.id}`, apiId: p.id, live: true, company: p.businessName, name: p.owner, email: p.email || '', phone: p.phone || '',
    status: p.status === 'Suspended' ? 'suspended' : 'active', suspendedReason: '', pitches: p.pitches, closed: 0, venues: p.venues,
    bookings30: p.bookings30d, occupancy: 0, gross30: 0, commission30: 0, balance: 0, taxId: '',
  });
  const partnerList = () => people.read('partners', '/admin/partners?pageSize=100', (d) => (d.items || d).map(fromPartner), []);
  const isLivePartner = (id) => String(id).startsWith('p:');
  AD.partners = () => (live('partners.read') ? partnerList() : demo.partners());
  AD.setPartnerStatus = (id, status, reason = '') => {
    if (!isLivePartner(id)) return demo.setPartnerStatus(id, status, reason);
    if (status === 'suspended' && reason.trim().length < 3) throw new ApiError('Write the reason.', 'invalid');
    const apiId = String(id).slice(2);
    const call = status === 'suspended' ? api.post(`/admin/partners/${apiId}/suspend`, { reason: reason.trim() }) : api.post(`/admin/partners/${apiId}/reactivate`);
    return call.then(() => { people.clear(); repaint(); });
  };
  ['setCommissionDeal', 'openDashboard'].forEach((name) => {
    if (!demo[name]) return;
    AD[name] = (id, ...rest) => (isLivePartner(id) ? notOnServer() : demo[name](id, ...rest));
  });

  /* ---------------- Pitch bookings (Orders & bookings, first tab) ---------------- */
  const SOURCE = { App: 'Super App', WalkIn: 'Walk-in', Package: 'Weekly package', Series: 'Series', Tournament: 'Tournament', FreeSlot: 'Free slot' };
  const bookings = cached();
  /** null outside API mode (the page keeps the dashboard ledger); else the server's bookings in the range */
  AD.bookingsLive = (r, f = {}) => {
    if (!live('bookings.read')) return null;
    const path = `/admin/bookings?pageSize=100&from=${r.from}&to=${r.to}${f.q ? `&q=${encodeURIComponent(f.q)}` : ''}`;
    const rows = bookings.read(path, path, (d) => (d.items || d).map((b) => ({
      id: b.code, live: true, date: b.date, hour: b.startHour, duration: b.duration, pitchName: `${b.pitch} · ${b.venue}`,
      customer: { name: b.customer || 'Walk-in' }, sourceLabel: SOURCE[b.source] || b.source, status: STATUS(b), amount: b.total,
    })), []);
    return rows.filter((b) => !f.status || b.status === f.status);
  };

  /* ---------------- CEO: reports and the company audit log ---------------- */
  const CE = SA.services.ceo;
  const reports = cached();
  const ceoAudit = cached();
  if (CE) {
    const demoReport = CE.report;
    /** Server report data → the screen's shape: columns [key, header, type], rows, KPIs, and a chart over a dated money column */
    const fromReport = (d) => {
      const sheet = d.sheets[0] || { headers: [], rows: [] };
      const typeOf = (h, i) => {
        if (/\((EGP|USD)\)/.test(h)) return 'money';
        const v = sheet.rows.find((r) => r[i] != null)?.[i];
        return typeof v === 'number' ? 'n' : typeof v === 'string' && /^\d{4}-\d{2}-\d{2}T/.test(v) ? 'date' : 'text';
      };
      // A dated first column is called "date": the screen's chart labels each day with it
      const columns = sheet.headers.map((h, i) => [i === 0 && typeOf(h, i) === 'date' ? 'date' : `c${i}`, h.replace(/ \((EGP|USD)\)$/, ''), typeOf(h, i)]);
      const rows = sheet.rows.map((r) => Object.fromEntries(r.map((v, i) => [columns[i][0], columns[i][2] === 'date' && v ? v.slice(0, 10) : v])));
      const money = columns.find(([, h, t]) => t === 'money' && /^Net/.test(h)) || columns.find(([, , t]) => t === 'money');
      return { columns, rows, kpis: d.figures.map((f) => [f.label, f.value, f.isMoney ? 'money' : 'n']), chart: columns[0]?.[2] === 'date' && money ? money[0] : null, live: true };
    };
    const LATER = { marketing: 'The marketing report isn’t on the server yet.', returns: 'Returns come with the store — it is paused.' };
    CE.report = (kind, r) => {
      if (!api.isLive() || !session()?.has?.('ceo')) return demoReport(kind, r);
      if (LATER[kind]) return { columns: [['c0', 'Report', 'text']], rows: [], kpis: [], note: LATER[kind], live: true };
      const path = `/ceo/reports/${kind}/data?from=${r.from}&to=${r.to}`;
      return reports.read(path, path, fromReport, { columns: [['c0', 'Loading…', 'text']], rows: [], kpis: [], live: true });
    };

    const CEO_KIND = { Ceo: 'ceo', ApiOrAgent: 'agent', Admin: 'admin', Partner: 'partner', Seller: 'seller', Factory: 'factory', Site: 'customer' };
    /** The company audit log from the server (latest 200), in the screen's shape; null outside API mode */
    CE.auditLog = () => {
      if (!api.isLive() || !session()?.has?.('ceo')) return null;
      const path = '/ceo/audit?pageSize=200';
      return ceoAudit.read(path, path, (d) => (d.items || d).map((x) => ({
        at: at(x.at), kind: CEO_KIND[x.system] || 'system', who: x.agent || x.actor || 'System', text: x.summary, action: x.action,
        ref: x.entityId ? `${x.entityType || ''} ${x.entityId}`.trim() : '',
      })), []);
    };
  }

  api.ready.then(() => session()?.subscribe?.(() => { audits.clear(); people.clear(); bookings.clear(); details.clear(); reports.clear(); ceoAudit.clear(); }));
})(window.SA);
