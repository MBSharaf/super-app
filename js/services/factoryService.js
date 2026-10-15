/**
 * Factory system (#/factory): the factories that print, pack and hand print-on-demand items to the courier.
 *
 * - Every POD item placed in the shop becomes a job at a factory (routing picks an active factory that is taking
 *   orders — there is one today, the code is ready for more). The job is due within the factory's SLA hours.
 * - Inside the factory a job moves through stages: new → printing → qc → packing → ready. A failed quality check
 *   sends it back to printing (a reprint at the factory's cost, counted in its defect rate).
 * - When every item of a customer order is packed, a shipment is created with Super App's courier automatically
 *   (SA.services.shipping); the courier's updates move the item to shipped → delivered / refused.
 * - `podOrders.status` keeps its meaning for customers, sellers and the admin (received / printing / shipped /
 *   delivered / refused / cancelled); the factory's own steps live in `stage` and `steps`.
 * - Staff sign in with their name and a 4-digit PIN (a shared tablet on the floor works). Every step is signed
 *   with the staff member's name. Roles: owner, supervisor, printer, qc, packer.
 * - The factory owns its materials and stock. Super App only checks quality and delivery: deadlines, defects,
 *   complaints, prices and payments.
 */
(function (SA) {
  const { ApiError } = SA.services.http;
  const db = () => SA.services.db;
  const N = () => SA.services.notify;
  const P = () => SA.services.pod;
  const H = 36e5, DAY = 864e5;
  const SESSION = 'superapp.factory.session';
  const sum = (a, f) => a.reduce((t, x) => t + (f(x) || 0), 0);

  const ROLES = {
    owner: { label: 'Owner', text: 'Everything: team, money, prices and settings', can: ['print', 'qc', 'pack', 'assign', 'ship', 'reports', 'team', 'money', 'settings', 'intake'] },
    supervisor: { label: 'Production supervisor', text: 'All production and shipping, assigns work, reports', can: ['print', 'qc', 'pack', 'assign', 'ship', 'reports', 'intake'] },
    printer: { label: 'DTF printer', text: 'Prints the jobs in the queue', can: ['print'] },
    qc: { label: 'Quality check', text: 'Passes or fails every printed item', can: ['qc'] },
    packer: { label: 'Packing & shipping', text: 'Packs, prints labels, hands over to the courier', can: ['pack', 'ship'] },
  };
  const STAGES = ['new', 'printing', 'qc', 'packing', 'ready', 'shipped', 'delivered', 'refused', 'cancelled'];
  const ACTIVE = ['new', 'printing', 'qc', 'packing'];
  const STAGE_LABEL = { new: 'New', printing: 'Printing', qc: 'Quality check', packing: 'Packing', ready: 'Ready for pickup', shipped: 'With the courier', delivered: 'Delivered', refused: 'Refused', cancelled: 'Cancelled' };
  // The step a job takes from each stage, who may take it and the button label
  const NEXT = {
    new: { to: 'printing', perm: 'print', label: 'Start printing', icon: 'bolt' },
    printing: { to: 'qc', perm: 'print', label: 'Printed — send to QC', icon: 'check' },
    qc: { to: 'packing', perm: 'qc', label: 'Passed quality check', icon: 'shield' },
    packing: { to: 'ready', perm: 'pack', label: 'Packed', icon: 'package' },
  };
  const ISSUES = ['Out of this colour or size', 'Print file problem', 'Machine down', 'Damaged blank', 'Other'];

  /* ---------- Seed + migration (runs once per device) ---------- */
  const STAFF_SEED = [
    ['fs-owner', 'Hany Mostafa', 'owner', '1111', 2], ['fs-sup', 'Reem Adel', 'supervisor', '2222', 10], ['fs-pr1', 'Mahmoud Samir', 'printer', '3333', 3],
    ['fs-pr2', 'Omar Fawzy', 'printer', '3344', 5], ['fs-qc', 'Nour Hassan', 'qc', '4444', 8], ['fs-pack', 'Islam Gamal', 'packer', '5555', 1],
  ];
  const migrate = () => {
    const d = db().raw();
    let changed = false;
    ['factories', 'factoryStaff', 'shipments', 'priceRequests', 'factoryLedger'].forEach((k) => { if (!Array.isArray(d[k])) { d[k] = []; changed = true; } });
    if (!d.settings.shipping) { d.settings.shipping = { provider: 'bosta', mode: 'test', auto: true }; changed = true; }
    if (!d.factories.length) {
      const now = Date.now();
      d.factories.push({ id: 'fc-1', name: 'Nile DTF Works', accountId: 'acc-print', city: 'Obour City', address: 'Industrial Zone B, plot 41, Obour City', phone: '+20 2 4610 2200',
        techniques: ['DTF'], capacity: 150, slaHours: 48, intake: true, intakeNote: '', status: 'active', pausedProducts: [], strikes: [], createdAt: now - 120 * DAY, updatedAt: now });
      STAFF_SEED.forEach(([id, name, role, pin, avatar]) => d.factoryStaff.push({ id, factoryId: 'fc-1', name, role, pin, avatar, active: true, createdAt: now - 100 * DAY, updatedAt: now, lastSeen: 0 }));
      const acc = d.accounts.find((a) => a.id === 'acc-print');
      if (acc) Object.assign(acc, { name: 'Hany Mostafa', company: 'Nile DTF Works', email: 'orders@niledtf.example' });
      changed = true;
    }
    // Existing items get a factory stage, a deadline and signed steps (so reports have history)
    const by = (role, i) => { const list = d.factoryStaff.filter((s) => s.role === role); return list[i % list.length]; };
    (d.podOrders || []).forEach((o, i) => {
      if (o.stage) return;
      const sla = 48 * H, p = o.placedAt;
      const step = (stage, at, s) => ({ stage, at, by: s?.id || '', name: s?.name || '' });
      const r = SA.core.utils.seeded(`${o.id}|fstage`);
      let stage = { received: 'new', printing: r < 0.4 ? 'printing' : r < 0.7 ? 'qc' : 'packing', shipped: 'shipped', delivered: 'delivered', refused: 'refused', cancelled: 'cancelled' }[o.status] || 'new';
      const steps = [step('new', p)];
      const prT = p + (6 + r * 14) * H, qcT = prT + (2 + r * 4) * H, pkT = qcT + (1 + r * 3) * H, rdT = pkT + (0.5 + r) * H;
      const reached = (s) => STAGES.indexOf(stage) >= STAGES.indexOf(s) && stage !== 'cancelled';
      if (reached('printing') && prT < Date.now()) steps.push(step('printing', prT, by('printer', i)));
      if (reached('qc') && qcT < Date.now()) steps.push(step('qc', qcT, by('printer', i)));
      if (reached('packing') && pkT < Date.now()) steps.push(step('packing', pkT, by('qc', i)));
      if (reached('ready')) steps.push(step('ready', Math.min(rdT, p + 40 * H), by('packer', i)));
      const qcFails = reached('packing') && r > 0.93 ? 1 : 0;
      Object.assign(o, { factoryId: 'fc-1', stage, steps, dueAt: p + sla, qcFails, reprints: (o.reprints || 0) + qcFails });
      if (['shipped', 'delivered', 'refused'].includes(o.status)) {
        const sh = { id: `SH-${o.id.slice(3)}`, provider: 'bosta', providerName: o.courier || 'Bosta', awb: o.tracking || `BST-${o.id.slice(3)}`, orderId: o.orderId, factoryId: 'fc-1', items: [o.id],
          customer: { ...o.customer }, cod: 0, status: o.status === 'shipped' ? 'picked_up' : o.status, events: [{ status: 'awaiting_pickup', at: p + 40 * H }, { status: 'picked_up', at: p + 44 * H }, ...(o.status !== 'shipped' ? [{ status: o.status, at: o.deliveredAt || p + 4 * DAY }] : [])],
          createdAt: p + 40 * H, updatedAt: p + 44 * H, seed: true };
        d.shipments.push(sh); o.shipmentId = sh.id;
      }
      changed = true;
    });
    // A fuller floor for the demo: recent jobs at every stage (two orders with two items each, a batch, one late job)
    if (!d.settings.factoryDemo) {
      const live = (d.designs || []).filter((x) => x.status === 'live');
      const people = SA.data.matches.people, now = Date.now();
      const plan = [['new', 1], ['new', 1.5], ['new', 2, 0], ['new', 2.2, 0], ['printing', 5], ['printing', 7], ['printing', 50], ['qc', 9], ['qc', 10], ['packing', 12, 'A'], ['packing', 12, 'A'], ['ready', 14, 'B'], ['packing', 14, 'B']];
      plan.forEach(([stage, ago, grp], i) => {
        const ds = live[grp === 0 ? 0 : (i * 3) % live.length]; if (!ds) return;
        const cat = d.podCatalog.find((c) => c.id === ds.catalogId); if (!cat) return;
        const placed = now - ago * H, ci = (i * 5) % people.length;
        const orderId = grp === 'A' ? 'ORD-904410' : grp === 'B' ? 'ORD-904422' : `ORD-${904300 + i * 7}`;
        const steps = [{ stage: 'new', at: placed, by: '', name: '' }];
        const order = ['printing', 'qc', 'packing', 'ready'];
        order.slice(0, order.indexOf(stage) + 1).forEach((st, k) => { const who = by(['printer', 'printer', 'qc', 'packer'][k], i); steps.push({ stage: st, at: placed + (k + 1) * 0.15 * ago * H, by: who.id, name: who.name }); });
        d.podOrders.unshift({ id: `PO-F${String(700 + i)}`, orderId, designId: ds.id, sellerId: ds.sellerId, catalogId: ds.catalogId, title: ds.title, color: grp === 0 ? ds.colors[0] : ds.colors[i % ds.colors.length],
          size: cat.sizes[(i + 1) % Math.min(4, cat.sizes.length)], qty: i % 4 === 3 ? 2 : 1, price: ds.price, base: cat.base, profit: ds.price - cat.base, partnerCost: cat.partnerCost,
          customer: { id: `c-fac-${i}`, name: people[ci], city: ['Cairo', 'Giza', 'Alexandria', 'Mansoura', 'Tanta'][i % 5], phone: `+20 10${String(10000000 + i * 7919123).slice(0, 8)}`, address: `${12 + i} El Tahrir St., apt ${3 + i}` },
          payment: i % 3 ? 'cod' : 'card', status: stage === 'new' ? 'received' : 'printing', history: [{ status: 'received', at: placed }, ...(stage !== 'new' ? [{ status: 'printing', at: steps[1].at }] : [])],
          placedAt: placed, deliveredAt: 0, createdAt: placed, updatedAt: placed, factoryId: 'fc-1', stage, steps, dueAt: placed + 48 * H, qcFails: i === 8 ? 1 : 0, reprints: i === 8 ? 1 : 0,
          assignedTo: stage === 'printing' ? by('printer', i).id : '', ...(i === 6 ? { issue: { type: 'Out of this colour or size', note: 'Waiting for the supplier — tomorrow morning.', by: 'Reem Adel', at: now - 3 * H, resolved: false } } : {}) });
      });
      d.settings.factoryDemo = 1; changed = true;
    }
    if (changed) db().touch('podOrders');
  };

  /* ---------- Session (staff sign-in) ---------- */
  const factoryMode = () => /^#\/factory(?:[/?]|$)/.test(location.hash);
  const staffAll = (factoryId) => db().all('factoryStaff').filter((s) => !factoryId || s.factoryId === factoryId);
  const auth = {
    current() {
      let id = ''; try { id = localStorage.getItem(SESSION) || ''; } catch { /* private mode */ }
      const s = id && db().find('factoryStaff', id);
      if (!s || !s.active) return null;
      const f = db().find('factories', s.factoryId);
      return f && f.status !== 'suspended' ? { ...s, factory: f } : null;
    },
    signIn(staffId, pin) {
      const s = db().find('factoryStaff', staffId);
      if (!s || !s.active) throw new ApiError('This account is not active.', 'forbidden');
      if (String(pin) !== String(s.pin)) throw new ApiError('Wrong PIN. Try again.', 'invalid');
      const f = db().find('factories', s.factoryId);
      if (f?.status === 'suspended') throw new ApiError('This factory is suspended. Contact Super App.', 'forbidden');
      try { localStorage.setItem(SESSION, s.id); } catch { /* ignore */ }
      db().update('factoryStaff', s.id, { lastSeen: Date.now() });
      return auth.current();
    },
    signOut() { try { localStorage.removeItem(SESSION); } catch { /* ignore */ } },
  };
  const me = () => { const s = auth.current(); if (!s) throw new ApiError('Sign in to the factory first.', 'unauthenticated'); return s; };
  const can = (perm, s = auth.current()) => !!s && ROLES[s.role]?.can.includes(perm);
  const need = (perm) => { const s = me(); if (!can(perm, s)) throw new ApiError('Your role can’t do this. Ask your supervisor.', 'forbidden'); return s; };
  const isAdmin = () => { try { return SA.services.dash.auth.current()?.role === 'admin'; } catch { return false; } };
  const needAdmin = () => SA.services.admin.me();
  const flog = (f, action, text, ref) => db().log(f?.accountId || 'acc-print', `factory.${action}`, text, ref);

  /* ---------- Jobs ---------- */
  const isLate = (o) => ACTIVE.includes(o.stage) && Date.now() > o.dueAt;
  const stepAt = (o, stage) => (o.steps || []).filter((x) => x.stage === stage).pop()?.at || 0;
  const enrich = (o) => ({ ...o, late: isLate(o), left: o.dueAt - Date.now(), design: P().get?.(o.designId) || null, product: db().find('podCatalog', o.catalogId) || null, assignee: o.assignedTo ? db().find('factoryStaff', o.assignedTo) : null });
  const jobs = ({ factoryId, stage, mine, late, issue, q } = {}) => {
    const s = auth.current();
    const fid = factoryId || s?.factoryId;
    return (db().all('podOrders') || [])
      .filter((o) => o.stage && (!fid || o.factoryId === fid) && (!stage || (Array.isArray(stage) ? stage.includes(o.stage) : o.stage === stage)))
      .filter((o) => (!mine || o.assignedTo === s?.id) && (!late || isLate(o)) && (!issue || (o.issue && !o.issue.resolved)))
      .filter((o) => !q || `${o.id} ${o.orderId} ${o.title} ${o.customer?.name}`.toLowerCase().includes(String(q).toLowerCase()))
      .map(enrich)
      .sort((a, b) => (ACTIVE.includes(a.stage) && ACTIVE.includes(b.stage) ? a.dueAt - b.dueAt : (b.updatedAt || b.placedAt) - (a.updatedAt || a.placedAt)));
  };
  const job = (id) => { const o = db().find('podOrders', String(id || '').trim().toUpperCase()); if (!o || !o.stage) throw new ApiError('No job with that code.', 'not_found'); return enrich(o); };
  const addStep = (o, stage, s, note = '') => [...(o.steps || []), { stage, at: Date.now(), by: s?.id || 'admin', name: s?.name || 'Super App', ...(note ? { note } : {}) }];

  const factory = {
    ROLES, STAGES, ACTIVE, STAGE_LABEL, NEXT, ISSUES, migrate, factoryMode, auth, me: () => auth.current(), can, isLate, stepAt,
    list: () => db().all('factories'),
    get: (id) => db().find('factories', id),
    staff: (factoryId = auth.current()?.factoryId) => staffAll(factoryId).sort((a, b) => Object.keys(ROLES).indexOf(a.role) - Object.keys(ROLES).indexOf(b.role)),
    jobs, job,
    /** Is there a factory taking orders for this product? (the shop and samples check this) */
    canMake(catalogId) { return db().all('factories').some((f) => f.status === 'active' && f.intake && !(f.pausedProducts || []).includes(catalogId)); },
    accepting: () => db().all('factories').some((f) => f.status === 'active' && f.intake),
    /** Pick the factory for a new item: active, taking orders, makes this product; the least loaded wins */
    route(catalogId) {
      const ok = db().all('factories').filter((f) => f.status === 'active' && f.intake && !(f.pausedProducts || []).includes(catalogId));
      const pool = ok.length ? ok : db().all('factories').filter((f) => f.status !== 'suspended');
      return pool.sort((a, b) => jobs({ factoryId: a.id, stage: ACTIVE }).length / a.capacity - jobs({ factoryId: b.id, stage: ACTIVE }).length / b.capacity)[0] || null;
    },
    /** Called by pod._place for each new item */
    assignNew(o) {
      const f = factory.route(o.catalogId); if (!f) return {};
      return { factoryId: f.id, stage: 'new', steps: [{ stage: 'new', at: Date.now(), by: '', name: '' }], dueAt: Date.now() + f.slaHours * H, qcFails: 0 };
    },
    /** After checkout: one message to the factory per order */
    announce(made) {
      const byF = {};
      made.forEach((o) => o.factoryId && (byF[o.factoryId] ||= []).push(o));
      Object.entries(byF).forEach(([fid, os]) => {
        const f = factory.get(fid); const qty = sum(os, (o) => o.qty);
        N()?.toAccount(f.accountId, { cat: 'orders', tone: 'blaze', title: `New job: ${qty} item${qty > 1 ? 's' : ''} · ${os[0].orderId}`, body: os.map((o) => `${o.qty} × ${db().find('podCatalog', o.catalogId)?.name || ''} ${o.color} ${o.size}`).join(', '), link: '#/factory/jobs' });
      });
    },

    /** Move a job one step forward (role-checked). Packing the last item of an order creates the shipment. */
    move(id, to) {
      const s = me();
      const o = db().find('podOrders', id); if (!o || o.factoryId !== s.factoryId) throw new ApiError('Job not found.', 'not_found');
      const nx = NEXT[o.stage];
      if (!nx || nx.to !== to) throw new ApiError(`This job is at “${STAGE_LABEL[o.stage]}”.`, 'invalid');
      if (!can(nx.perm, s)) throw new ApiError('Your role can’t do this step.', 'forbidden');
      if (o.status === 'cancelled') throw new ApiError('This item was cancelled.', 'invalid');
      const patch = { stage: to, steps: addStep(o, to, s) };
      if (to === 'printing' && !o.assignedTo && s.role === 'printer') patch.assignedTo = s.id;
      db().update('podOrders', id, patch);
      if (to === 'printing' && o.status === 'received') P().advance(id, 'printing');
      flog(s.factory, to, `${s.name}: ${o.id} “${o.title}” → ${STAGE_LABEL[to]}`, id);
      if (to === 'ready') SA.services.shipping.packed(o.orderId, o.factoryId);
      return db().find('podOrders', id);
    },
    /** Start every new job in a batch (same product and colour) */
    startBatch(ids) { need('print'); return ids.map((id) => factory.move(id, 'printing')).length; },
    /** New jobs grouped by product + colour, so the printer can run them together */
    batches(factoryId) {
      const map = new Map();
      jobs({ factoryId, stage: 'new' }).forEach((o) => { const k = `${o.catalogId}|${o.color}`; if (!map.has(k)) map.set(k, { key: k, product: o.product, color: o.color, jobs: [] }); map.get(k).jobs.push(o); });
      return [...map.values()].sort((a, b) => b.jobs.length - a.jobs.length);
    },
    qcFail(id, reason) {
      const s = need('qc');
      const o = db().find('podOrders', id); if (!o || o.factoryId !== s.factoryId || o.stage !== 'qc') throw new ApiError('Only items at the quality check can fail it.', 'invalid');
      if (!reason || reason.trim().length < 3) throw new ApiError('Write what is wrong.', 'invalid');
      db().update('podOrders', id, { stage: 'printing', qcFails: (o.qcFails || 0) + 1, reprints: (o.reprints || 0) + 1, steps: addStep(o, 'qc-fail', s, reason.trim()) });
      flog(s.factory, 'qc_fail', `${s.name}: ${o.id} failed QC — ${reason.trim()}`, id);
      const who = o.assignedTo && db().find('factoryStaff', o.assignedTo);
      if (who) N()?.toAccount(s.factory.accountId, { cat: 'orders', tone: 'danger', title: `Reprint: ${o.id}`, body: `${who.name} — ${reason.trim()}`, link: `#/factory/job/${o.id}` });
    },
    assign(id, staffId) {
      const s = need('assign');
      const o = db().find('podOrders', id); if (!o || o.factoryId !== s.factoryId) throw new ApiError('Job not found.', 'not_found');
      const to = staffId ? db().find('factoryStaff', staffId) : null;
      db().update('podOrders', id, { assignedTo: to?.id || '' });
      flog(s.factory, 'assign', `${s.name} assigned ${o.id} to ${to?.name || 'nobody'}`, id);
    },
    reportIssue(id, { type, note = '' }) {
      const s = me();
      const o = db().find('podOrders', id); if (!o || o.factoryId !== s.factoryId) throw new ApiError('Job not found.', 'not_found');
      if (!ISSUES.includes(type)) throw new ApiError('Choose what the problem is.', 'invalid');
      const issue = { type, note: note.trim(), by: s.name, at: Date.now(), resolved: false };
      db().update('podOrders', id, { issue, steps: addStep(o, 'issue', s, `${type}${note ? ` — ${note.trim()}` : ''}`) });
      flog(s.factory, 'issue', `${s.name} reported a problem on ${o.id}: ${type}`, id);
      N()?.toAdmins({ cat: 'orders', tone: 'danger', title: `Factory problem: ${o.id}`, body: `${s.factory.name} — ${type}${note ? `: ${note.trim()}` : ''}`, link: '#/admin/factories?tab=jobs' });
    },
    resolveIssue(id, note = '') {
      const admin = isAdmin() && !factoryMode();
      const s = admin ? null : need('assign');
      const o = db().find('podOrders', id); if (!o?.issue) throw new ApiError('No open problem.', 'invalid');
      db().update('podOrders', id, { issue: { ...o.issue, resolved: true, resolvedAt: Date.now(), resolvedBy: s?.name || 'Super App', resolution: note.trim() }, steps: addStep(o, 'resolved', s, note.trim()) });
      flog(factory.get(o.factoryId), 'resolved', `${s?.name || 'Super App'} resolved the problem on ${o.id}${note ? `: ${note.trim()}` : ''}`, id);
    },

    /* ---------- Admin actions ---------- */
    /** A customer complaint about quality: the item goes back to the factory to be reprinted at its cost */
    complaintReprint(id, reason, { deduct = 0 } = {}) {
      needAdmin();
      const o = db().find('podOrders', id); if (!o?.factoryId) throw new ApiError('Job not found.', 'not_found');
      if (!reason || reason.trim().length < 3) throw new ApiError('Write the reason.', 'invalid');
      const f = factory.get(o.factoryId);
      db().update('podOrders', id, { stage: 'printing', complaints: (o.complaints || 0) + 1, reprints: (o.reprints || 0) + 1, dueAt: Date.now() + f.slaHours * H, steps: addStep(o, 'complaint', null, reason.trim()) });
      if (+deduct > 0) factory.addLedger(f.id, { type: 'deduction', amount: +deduct, reason: `Quality complaint on ${o.id}: ${reason.trim()}`, jobId: o.id });
      N()?.toAccount(f.accountId, { cat: 'orders', tone: 'danger', title: `Reprint requested: ${o.id}`, body: `Customer complaint — ${reason.trim()}`, link: `#/factory/job/${o.id}` });
      P().reprint?.(id, `Complaint: ${reason.trim()}`);
      SA.services.admin.log?.('factory.reprint', `Sent ${o.id} back to ${f.name} for a reprint: ${reason.trim()}`, id);
    },
    reassign(id, factoryId) {
      needAdmin();
      const o = db().find('podOrders', id); const f = factory.get(factoryId);
      if (!o || !f) throw new ApiError('Not found.', 'not_found');
      if (!ACTIVE.includes(o.stage)) throw new ApiError('Only jobs in production can move.', 'invalid');
      db().update('podOrders', id, { factoryId, stage: 'new', assignedTo: '', dueAt: Date.now() + f.slaHours * H, steps: addStep(o, 'moved', null, f.name) });
      N()?.toAccount(f.accountId, { cat: 'orders', tone: 'blaze', title: `Job moved to you: ${o.id}`, body: `${o.qty} × ${o.title}`, link: `#/factory/job/${o.id}` });
    },
    setStatus(factoryId, status, reason = '') {
      needAdmin();
      const f = factory.get(factoryId); if (!f) throw new ApiError('Factory not found.', 'not_found');
      if (!['active', 'paused', 'suspended'].includes(status)) throw new ApiError('Unknown status.', 'invalid');
      db().update('factories', f.id, { status, statusReason: reason.trim() });
      SA.services.admin.log?.('factory.status', `${f.name} is now ${status}${reason ? `: ${reason.trim()}` : ''}`, f.id);
      N()?.toAccount(f.accountId, { cat: 'account', tone: status === 'active' ? 'turf' : 'danger', title: status === 'active' ? 'Your factory is active again' : `Your factory was ${status} by Super App`, body: reason.trim() || 'Contact Super App for details.', link: '#/factory' });
    },
    addStrike(factoryId, reason) {
      needAdmin();
      const f = factory.get(factoryId); if (!f) throw new ApiError('Factory not found.', 'not_found');
      if (!reason || reason.trim().length < 3) throw new ApiError('Write the reason.', 'invalid');
      db().update('factories', f.id, { strikes: [...(f.strikes || []), { reason: reason.trim(), at: Date.now() }] });
      SA.services.admin.log?.('factory.strike', `Warning to ${f.name}: ${reason.trim()}`, f.id);
      N()?.toAccount(f.accountId, { cat: 'account', tone: 'danger', title: 'Warning from Super App', body: reason.trim(), link: '#/factory' });
    },

    /* ---------- Settings (owner) ---------- */
    saveSettings({ capacity, slaHours }) {
      const s = need('settings');
      const cap = Math.round(+capacity), sla = Math.round(+slaHours);
      if (!(cap >= 1 && cap <= 5000)) throw new ApiError('Capacity must be between 1 and 5,000 items a day.', 'invalid', { capacity: 'Between 1 and 5,000' });
      if (!(sla >= 12 && sla <= 120)) throw new ApiError('Ship within 12 to 120 hours.', 'invalid', { slaHours: '12 to 120 hours' });
      db().update('factories', s.factoryId, { capacity: cap, slaHours: sla });
      flog(s.factory, 'settings', `${s.name} set capacity ${cap}/day and ${sla} h to ship`, s.factoryId);
    },
    setIntake(on, note = '') {
      const s = need('intake');
      if (!on && note.trim().length < 3) throw new ApiError('Write why (Super App and the team will see it).', 'invalid');
      db().update('factories', s.factoryId, { intake: !!on, intakeNote: on ? '' : note.trim(), intakeAt: Date.now() });
      flog(s.factory, 'intake', `${s.name} ${on ? 'resumed' : 'paused'} new orders${on ? '' : `: ${note.trim()}`}`, s.factoryId);
      N()?.toAdmins({ cat: 'orders', tone: on ? 'turf' : 'danger', title: `${s.factory.name} ${on ? 'is taking orders again' : 'paused new orders'}`, body: on ? 'Printed products are back in the shop.' : `${note.trim()} — printed products are hidden from the shop until it resumes.`, link: '#/admin/factories' });
    },
    setProductPaused(catalogId, paused) {
      const s = need('settings');
      const f = s.factory;
      const list = new Set(f.pausedProducts || []);
      if (paused) list.add(catalogId); else list.delete(catalogId);
      db().update('factories', f.id, { pausedProducts: [...list] });
      const c = db().find('podCatalog', catalogId);
      flog(f, 'product', `${s.name} ${paused ? 'paused' : 'resumed'} ${c?.name || catalogId}`, catalogId);
      N()?.toAdmins({ cat: 'orders', tone: paused ? 'gold' : 'turf', title: `${f.name} ${paused ? 'can’t make' : 'can make again'}: ${c?.name || catalogId}`, body: paused ? 'Designs on it are hidden from the shop for now.' : 'Designs on it are back in the shop.', link: '#/admin/factories' });
    },

    /* ---------- Prices (what the factory charges Super App per piece) ---------- */
    priceList(factoryId = auth.current()?.factoryId) {
      return P().catalog({ all: true }).map((c) => ({ ...c, pending: db().all('priceRequests').find((r) => r.factoryId === factoryId && r.catalogId === c.id && r.status === 'pending') || null }));
    },
    requestPrice(catalogId, cost, note = '') {
      const s = need('money');
      const c = db().find('podCatalog', catalogId); const v = Math.round(+cost);
      if (!c) throw new ApiError('Product not found.', 'not_found');
      if (!(v > 0 && v < c.base)) throw new ApiError(`Enter a price under the shop base price (${c.base} EGP).`, 'invalid');
      if (v === c.partnerCost) throw new ApiError('That is the current price.', 'invalid');
      db().all('priceRequests').filter((r) => r.factoryId === s.factoryId && r.catalogId === catalogId && r.status === 'pending').forEach((r) => db().update('priceRequests', r.id, { status: 'replaced' }));
      const r = db().insert('priceRequests', { factoryId: s.factoryId, catalogId, from: c.partnerCost, to: v, note: note.trim(), by: s.name, status: 'pending' }, 'pr');
      flog(s.factory, 'price', `${s.name} asked to change ${c.name} from ${c.partnerCost} to ${v} EGP`, r.id);
      N()?.toAdmins({ cat: 'money', tone: 'gold', title: `Price change request: ${c.name}`, body: `${s.factory.name}: ${c.partnerCost} → ${v} EGP${note ? ` — ${note.trim()}` : ''}`, link: '#/admin/factories?tab=prices' });
      return r;
    },
    decidePrice(id, approve, reason = '') {
      needAdmin();
      const r = db().find('priceRequests', id); if (!r || r.status !== 'pending') throw new ApiError('Request not found.', 'not_found');
      const c = db().find('podCatalog', r.catalogId); const f = factory.get(r.factoryId);
      db().update('priceRequests', id, { status: approve ? 'approved' : 'rejected', reason: reason.trim(), decidedAt: Date.now() });
      if (approve) { db().update('podCatalog', c.id, { partnerCost: r.to }); }
      SA.services.admin.log?.('factory.price', `${approve ? 'Approved' : 'Rejected'} ${f.name}'s price for ${c.name}: ${r.from} → ${r.to} EGP`, id);
      N()?.toAccount(f.accountId, { cat: 'money', tone: approve ? 'turf' : 'danger', title: `${approve ? 'Price approved' : 'Price not approved'}: ${c.name}`, body: approve ? `${r.to} EGP per piece from new orders.` : reason.trim() || 'Contact Super App.', link: '#/factory/settings' });
    },
    priceRequests: (status = 'pending') => db().all('priceRequests').filter((r) => !status || r.status === status),

    /* ---------- Team (owner) ---------- */
    saveStaff(d, id = null) {
      const s = need('team');
      const name = String(d.name || '').trim(), pin = String(d.pin || '').trim();
      const err = {};
      if (name.length < 3) err.name = 'Write the full name.';
      if (!ROLES[d.role] || d.role === 'owner') err.role = 'Choose a role.';
      if (!id || pin) { if (!/^\d{4}$/.test(pin)) err.pin = '4 digits.'; else if (staffAll(s.factoryId).some((x) => x.pin === pin && x.id !== id)) err.pin = 'Someone already uses this PIN.'; }
      if (Object.keys(err).length) throw new ApiError('Check the highlighted fields.', 'invalid', err);
      const patch = { name, role: d.role, ...(pin ? { pin } : {}) };
      const out = id ? db().update('factoryStaff', id, patch) : db().insert('factoryStaff', { ...patch, factoryId: s.factoryId, active: true, avatar: Math.floor(Math.random() * 11), lastSeen: 0 }, 'fs');
      flog(s.factory, 'team', `${s.name} ${id ? 'updated' : 'added'} ${name} (${ROLES[d.role].label})`, out.id);
      return out;
    },
    setStaffActive(id, active) {
      const s = need('team');
      const x = db().find('factoryStaff', id); if (!x || x.factoryId !== s.factoryId) throw new ApiError('Not found.', 'not_found');
      if (x.role === 'owner') throw new ApiError('The owner account can’t be turned off.', 'invalid');
      db().update('factoryStaff', id, { active: !!active });
      if (!active) jobs({ factoryId: s.factoryId, stage: ACTIVE }).filter((o) => o.assignedTo === id).forEach((o) => db().update('podOrders', o.id, { assignedTo: '' }));
      flog(s.factory, 'team', `${s.name} ${active ? 'turned on' : 'turned off'} ${x.name}`, id);
    },

    /* ---------- Numbers ---------- */
    /** Production stats for a period: on time, defects, average hours, per person, per day */
    stats(factoryId = auth.current()?.factoryId, days = 30) {
      const since = Date.now() - days * DAY;
      const all = jobs({ factoryId });
      const done = all.filter((o) => stepAt(o, 'ready') >= since);
      const onTime = done.filter((o) => stepAt(o, 'ready') <= o.dueAt).length;
      const qcFails = sum(done, (o) => o.qcFails), complaints = sum(all.filter((o) => (o.updatedAt || 0) >= since), (o) => o.complaints);
      const hours = done.map((o) => (stepAt(o, 'ready') - o.placedAt) / H);
      const people = {};
      all.forEach((o) => (o.steps || []).forEach((st) => { if (!st.by || st.at < since || !['printing', 'qc', 'packing', 'ready', 'qc-fail'].includes(st.stage)) return; const p = (people[st.by] ||= { id: st.by, name: st.name, printed: 0, checked: 0, packed: 0, failed: 0 }); if (st.stage === 'qc') p.printed++; if (st.stage === 'packing') p.checked++; if (st.stage === 'ready') p.packed++; if (st.stage === 'qc-fail') p.failed++; }));
      const daily = Array.from({ length: Math.min(days, 30) }, (_, i) => {
        const d0 = new Date(); d0.setHours(0, 0, 0, 0); const start = d0.getTime() - (Math.min(days, 30) - 1 - i) * DAY;
        return { day: start, items: sum(done.filter((o) => { const t = stepAt(o, 'ready'); return t >= start && t < start + DAY; }), (o) => o.qty) };
      });
      return { items: sum(done, (o) => o.qty), jobs: done.length, onTimePct: done.length ? Math.round((onTime / done.length) * 100) : 100, late: done.length - onTime,
        defectPct: done.length ? Math.round(((qcFails + complaints) / done.length) * 1000) / 10 : 0, qcFails, complaints,
        avgHours: hours.length ? Math.round((sum(hours, (h) => h) / hours.length) * 10) / 10 : 0, people: Object.values(people), daily };
    },
    /** Today: items in, capacity, pipeline, late */
    today(factoryId = auth.current()?.factoryId) {
      const f = factory.get(factoryId); const d0 = new Date(); d0.setHours(0, 0, 0, 0);
      const all = jobs({ factoryId });
      const pipe = Object.fromEntries(STAGES.map((st) => [st, all.filter((o) => o.stage === st).length]));
      return { factory: f, inToday: sum(all.filter((o) => o.placedAt >= d0.getTime()), (o) => o.qty), readyToday: sum(all.filter((o) => stepAt(o, 'ready') >= d0.getTime()), (o) => o.qty),
        open: sum(all.filter((o) => ACTIVE.includes(o.stage)), (o) => o.qty), capacity: f.capacity, pipe, late: all.filter(isLate).length, issues: all.filter((o) => o.issue && !o.issue.resolved).length };
    },
    /** Money between Super App and the factory: per-piece price of everything handed to the courier, minus deductions and payments */
    money(factoryId = auth.current()?.factoryId) {
      const f = factory.get(factoryId);
      // Demo history: older seeded items count as already settled; the last two weeks are still owed
      const done = jobs({ factoryId, stage: ['ready', 'shipped', 'delivered', 'refused'] }).filter((o) => !o.seed || o.placedAt > Date.now() - 14 * DAY);
      const earned = Math.round(sum(done, (o) => o.partnerCost * (o.qty || 1)));
      const ledger = db().all('factoryLedger').filter((l) => l.factoryId === factoryId);
      const deductions = Math.round(sum(ledger.filter((l) => l.type === 'deduction'), (l) => l.amount));
      const bonuses = Math.round(sum(ledger.filter((l) => l.type === 'bonus'), (l) => l.amount));
      const payouts = db().all('payouts').filter((p) => p.accountId === f?.accountId && p.status !== 'rejected');
      const paid = Math.round(sum(payouts, (p) => p.amount));
      return { factory: f, account: db().find('accounts', f?.accountId), items: done.length, earned, deductions, bonuses, paid, owed: Math.max(0, earned + bonuses - deductions - paid), orders: done, ledger, payouts };
    },
    addLedger(factoryId, { type, amount, reason, jobId = '' }) {
      needAdmin();
      const amt = Math.round(+amount);
      if (!['deduction', 'bonus'].includes(type)) throw new ApiError('Choose deduction or bonus.', 'invalid');
      if (!(amt > 0)) throw new ApiError('Enter an amount.', 'invalid');
      if (!reason || reason.trim().length < 3) throw new ApiError('Write the reason.', 'invalid');
      const f = factory.get(factoryId);
      const l = db().insert('factoryLedger', { factoryId, type, amount: amt, reason: reason.trim(), jobId }, 'fl');
      SA.services.admin.log?.(`factory.${type}`, `${type === 'deduction' ? 'Deducted' : 'Added'} ${amt.toLocaleString()} EGP ${type === 'deduction' ? 'from' : 'to'} ${f.name}: ${reason.trim()}`, l.id);
      N()?.toAccount(f.accountId, { cat: 'money', tone: type === 'deduction' ? 'danger' : 'turf', title: `${type === 'deduction' ? 'Deduction' : 'Bonus'}: ${amt.toLocaleString()} EGP`, body: reason.trim(), link: '#/factory/money' });
      return l;
    },
    activity: (factoryId = auth.current()?.factoryId) => { const f = factory.get(factoryId); return db().all('activity').filter((a) => a.action.startsWith('factory.') && a.actor === f?.accountId); },

    /** Admin to-do items about factories (bell + sidebar badge) */
    alerts() {
      const all = jobs({});
      const late = all.filter(isLate), issues = all.filter((o) => o.issue && !o.issue.resolved), prices = factory.priceRequests();
      const stopped = db().all('factories').filter((f) => f.status === 'active' && !f.intake);
      return { late: late.length, issues: issues.length, prices: prices.length, stopped: stopped.length, total: late.length + issues.length + prices.length + stopped.length };
    },
  };

  SA.services.factory = factory;
  try { migrate(); } catch (e) { console.warn('factory migrate', e); }
})(window.SA);
