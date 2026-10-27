/**
 * The admin console's people, bookings and audit on the real API (backend 8.6d). Loaded after liveMoney.
 *
 * In API mode, for a team member with the permission, these admin methods answer from the server instead of the
 * demo data: customers (block, unblock, pay-upfront rule, notes, wallet credit, Super Points, refusal reset), pitch
 * partners (suspend, reactivate, commission deal, balance adjustment, close / reopen a pitch), the pitch bookings list
 * and the audit log. The pages read synchronously, so each list is loaded once (and again after a minute or an action)
 * and the page re-renders when it lands; a drawer repaints on the `adm:refresh` event. Opening a partner's dashboard
 * as them is not on the server and says so.
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
      /** Reload on the next read but keep answering with what it has (an open drawer keeps its row) */
      stale: () => map.forEach((v) => { v.at = 0; }),
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
    bookings: c.bookings, orders: c.orders, items: 0, spent: c.spent || 0, last: at(c.lastSeenAt) || at(c.createdAt), first: at(c.createdAt),
    refused: c.codRefusals, blocked: c.status !== 'Active', blockedReason: d?.blockedReason || '', prepay: PREPAY[c.mustPrepay] || 'auto',
    needsPrepay: c.mustPrepay === 'Always' || (c.mustPrepay === 'Automatic' && c.codRefusals >= REFUSE_LIMIT), limit: REFUSE_LIMIT,
    notes: notesOf(d?.adminNote), note: d?.adminNote || '', podList: [], orderList: [], wallet: d?.walletBalance ?? null, points: d?.points ?? null,
    credits: (d?.credits || []).map((x) => ({ amount: x.amount, reason: x.reason || '', at: at(x.at) })),
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
    people.stale();
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
  const needReason = (reason) => { if (!reason || reason.trim().length < 3) throw new ApiError('Write the reason.', 'invalid'); return reason.trim(); };
  AD.creditCustomer = (id, amount, reason) => {
    if (!isLiveId(id)) return demo.creditCustomer(id, amount, reason);
    const amt = Math.round(+amount);
    if (!(amt >= 5 && amt <= 5000)) throw new ApiError('Credit between 5 and 5,000 EGP.', 'invalid');
    return act(id, 'credit', { amount: amt, reason: needReason(reason) });
  };
  AD.adjustPoints = (id, points, reason) => {
    if (!isLiveId(id)) return demo.adjustPoints(id, points, reason);
    const p = Math.round(+points);
    if (!p || Math.abs(p) > 100000) throw new ApiError('Enter the points (between −100,000 and 100,000, not 0).', 'invalid');
    return act(id, 'points', { points: p, reason: needReason(reason) });
  };
  AD.resetRefusals = (id) => (isLiveId(id) ? act(id, 'refusals/reset') : demo.resetRefusals(id));

  /* ---------------- Pitch partners ---------------- */
  /** Commission is a percent on the server and a fraction on the site (null = the platform rate) */
  const fromPartner = (p, d = null) => ({
    id: `p:${p.id}`, apiId: p.id, live: true, company: p.businessName, name: p.owner, email: p.email || '', phone: p.phone || '',
    status: p.status === 'Suspended' ? 'suspended' : 'active', suspendedReason: d?.suspendedReason || '', pitches: p.pitches, closed: p.closedPitches || 0, venues: p.venues,
    bookings30: p.bookings30d, occupancy: p.occupancy30d || 0, gross30: p.gross30d || 0, commission30: p.commission30d || 0, balance: p.balance || 0, taxId: '',
    commission: p.bookingCommission == null && p.storeCommission == null ? null
      : { bookings: p.bookingCommission == null ? null : p.bookingCommission / 100, store: p.storeCommission == null ? null : p.storeCommission / 100, note: '' },
    pitchList: d?.pitchList || null,
  });
  const partnerDetails = new Map();
  const partnerList = () => people.read('partners', '/admin/partners?pageSize=100', (d) => (d.items || d).map((p) => ({ ...fromPartner(p), pitchList: partnerDetails.get(p.id)?.pitchList || null })), []);
  const isLivePartner = (id) => String(id).startsWith('p:');
  const loadPartner = (apiId) => api.get(`/admin/partners/${apiId}`).then((d) => { partnerDetails.set(+apiId, fromPartner(d.partner, d)); repaint(); });
  /** Runs a partner action on the server; the answer is the partner drawer */
  const partnerAct = (apiId, call) => call.then((d) => { partnerDetails.set(+apiId, fromPartner(d.partner, d)); people.stale(); repaint(); });
  AD.partners = () => (live('partners.read') ? partnerList() : demo.partners());
  /** The partner's pitches for the drawer (loaded once when the drawer opens) */
  AD.partnerPitches = (id) => {
    const apiId = +String(id).slice(2);
    const hit = partnerDetails.get(apiId);
    if (!hit?.pitchList) { if (!hit?.loading) { partnerDetails.set(apiId, { ...(hit || {}), loading: true }); loadPartner(apiId).catch(toast); } return []; }
    return hit.pitchList;
  };
  AD.setCommissionDeal = (id, { bookings, store, note = '' } = {}) => {
    if (!isLivePartner(id)) return demo.setCommissionDeal(id, { bookings, store, note });
    const val = (v) => (v === '' || v == null ? null : +v);
    const b = val(bookings), s = val(store);
    if ([b, s].some((x) => x != null && !(x >= 0 && x <= 50))) throw new ApiError('Commission must be between 0% and 50%.', 'invalid');
    const apiId = String(id).slice(2);
    return partnerAct(apiId, api.put(`/admin/partners/${apiId}/commission`, { bookings: b, store: s, note: String(note || '').trim() || null }));
  };
  AD.adjustPartnerBalance = (id, amount, reason) => {
    const amt = Math.round(+amount * 100) / 100;
    if (!amt || Math.abs(amt) > 100000) throw new ApiError('Enter the amount (up to 100,000 EGP).', 'invalid');
    const apiId = String(id).slice(2);
    return partnerAct(apiId, api.post(`/admin/partners/${apiId}/adjust`, { amount: amt, reason: needReason(reason) }));
  };
  /** Pitches of a live partner come as `lp:<id>` */
  AD.setPitchStatus = (pitchId, status, reason = '') => {
    if (!String(pitchId).startsWith('lp:')) return demo.setPitchStatus(pitchId, status, reason);
    const id = String(pitchId).slice(3);
    const call = status === 'closed' ? api.post(`/admin/pitches/${id}/close`, { reason: needReason(reason) }) : api.post(`/admin/pitches/${id}/reopen`);
    return call.then((d) => { partnerDetails.set(d.partner.id, fromPartner(d.partner, d)); people.stale(); repaint(); });
  };
  AD.setPartnerStatus = (id, status, reason = '') => {
    if (!isLivePartner(id)) return demo.setPartnerStatus(id, status, reason);
    if (status === 'suspended' && reason.trim().length < 3) throw new ApiError('Write the reason.', 'invalid');
    const apiId = String(id).slice(2);
    const call = status === 'suspended' ? api.post(`/admin/partners/${apiId}/suspend`, { reason: reason.trim() }) : api.post(`/admin/partners/${apiId}/reactivate`);
    return partnerAct(apiId, call);
  };
  ['openDashboard'].forEach((name) => {
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
