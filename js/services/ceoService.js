/**
 * CEO system (#/ceo) — the "backend" for the Command Center. It sits above every other system.
 *
 * - Session: the CEO has their own sign-in (superapp.ceo.session). The CEO account is an admin account with the
 *   team role 'ceo', so every admin action works from the CEO system and is logged with the CEO as the actor.
 * - Org: departments and roles come from SA.data.org. AI agents have admin accounts (acc-ag-<role>) and sign in to the
 *   admin console as their role: the navigation shows only their sections, and money above their limit or outside
 *   their permissions goes to the CEO's approvals inbox. The CEO can pause, resume or stop every agent.
 * - Approvals: withdrawals and factory settlements over the payout limit, refunds and credits over the refund limit,
 *   and anything an agent may only prepare. Over the two-person limit the CFO confirms with their PIN too.
 * - Kill switches, limits, goals (OKRs), budgets, the compliance calendar, the morning briefing, the what-if
 *   simulator, department health, reports and "open as" into every system.
 *
 * Future API: /ceo/* (the same methods; agents become real AI agents with these permissions).
 */
(function (SA) {
  const { ApiError } = SA.services.http;
  const { isoDate, addDays, today, parseISO, seeded, fmtDate } = SA.core.utils;
  const db = () => SA.services.db;
  const D = () => SA.services.dash;
  const AD = () => SA.services.admin;
  const N = () => SA.services.notify;
  const ORG = () => SA.data.org;
  const DAY = 864e5;

  const SESSION = 'superapp.ceo.session';
  const ADMIN_SESSION = 'superapp.admin.session';
  const CEO_ID = 'acc-ceo';
  const CEO_PIN = '2026';
  const LIMITS = { payout: 10000, refund: 1000, dual: 50000 };
  const SWITCHES = [
    ['bookings', 'Pitch bookings', 'New bookings and weekly packages', 'pitch'],
    ['shop', 'Shop checkout', 'Buying from stores and the Super App Store', 'bag'],
    ['pod', 'Printed designs', 'Print-on-demand products in the shop', 'shirt'],
    ['payouts', 'Withdrawals', 'Partners and sellers asking for money, and sending it', 'wallet'],
    ['applications', 'New applications', 'New pitch partners and sellers', 'users'],
    ['fantasy', 'Fantasy', 'Saving teams and buying the Fantasy Pass', 'trophy'],
    ['community', 'Community posting', 'New posts and comments', 'message'],
    ['games', 'Joining games', 'Players asking to join free-slot games', 'ball'],
  ];
  const agentAcc = (roleId) => `acc-ag-${roleId}`;

  /* ---------- Seed + migration ---------- */
  const GOALS = [
    { id: 'g-1', objective: 'Grow the business', dept: 'ops', quarter: 'Q4', krs: [
      { text: 'Platform revenue in the last 30 days', metric: 'revenue30', target: 600000 },
      { text: 'Money through Super App in the last 30 days', metric: 'gmv30', target: 3000000 },
      { text: 'Active pitches', metric: 'pitches', target: 40 },
    ] },
    { id: 'g-2', objective: 'Customers love Super App', dept: 'support', quarter: 'Q4', krs: [
      { text: 'Open support tickets (lower is better)', metric: 'ticketsOpen', target: 5, lower: true },
      { text: 'Average pitch rating', metric: 'rating', target: 4.6 },
    ] },
    { id: 'g-3', objective: 'A strong seller marketplace', dept: 'bizdev', quarter: 'Q4', krs: [
      { text: 'Active sellers', metric: 'sellers', target: 60 },
      { text: 'Live designs', metric: 'designs', target: 300 },
    ] },
    { id: 'g-4', objective: 'Run lean with AI agents', dept: 'aiops', quarter: 'Q4', krs: [
      { text: 'AI agents cost per month (EGP, lower is better)', metric: 'agentCost', target: 30000, lower: true },
      { text: 'Agents working (not paused)', metric: 'agentsActive', target: 19 },
    ] },
  ];
  const BUDGETS = { ops: 45000, support: 30000, bizdev: 15000, finance: 12000, legal: 20000, marketing: 80000, tech: 40000, aiops: 8000 };
  // Egyptian obligations and contracts (fictional partners); "every" repeats it when marked done
  const CAL = [
    ['VAT return (Form 10)', 'finance', 'cfo', 'monthly', 'Tax Authority — by the end of the next month', 30],
    ['Withholding tax (Form 41)', 'finance', 'cfo', 'quarterly', 'Tax Authority — within the month after the quarter', 20],
    ['Social insurance payments', 'finance', 'cfo', 'monthly', 'For the few human staff — by the 15th', 12],
    ['Annual corporate tax return', 'finance', 'counsel', 'yearly', 'Due by 30 April', 196],
    ['Commercial register renewal', 'legal', 'counsel', 'yearly', 'Renew before it expires', 75],
    ['Data protection review (Law 151 of 2020)', 'legal', 'compliance', 'yearly', 'Personal data map, consent and retention', 44],
    ['Payment provider security review', 'tech', 'cto', 'yearly', 'Card data stays with the provider (PCI)', 58],
    ['Courier contract — Bosta trial ends', 'legal', 'counsel', 'once', 'Decide: sign the contract or switch courier', 9],
    ['Factory contract renewal — Nile DTF Works', 'legal', 'counsel', 'yearly', 'Prices, delivery times and quality terms', 103],
    ['Seller ID re-checks', 'legal', 'compliance', 'quarterly', 'Sellers with withdrawals over 20,000 EGP', 5],
    ['Monthly close and bank reconciliation', 'finance', 'analyst', 'monthly', 'Every payment matched to the bank statement', 3],
    ['Backups restore test', 'tech', 'qa', 'monthly', 'Restore last night’s backup to a test server', -2],
  ];
  const migrate = () => {
    const d = db().raw();
    let changed = false;
    ['ceoApprovals', 'ceoGoals', 'ceoCalendar', 'ceoExpenses'].forEach((k) => { if (!Array.isArray(d[k])) { d[k] = []; changed = true; } });
    const now = Date.now();
    const since = d.platformSince || now - 200 * DAY;
    const addAcc = (a) => { if (!d.accounts.some((x) => x.id === a.id)) { d.accounts.push({ joinedAt: since, createdAt: since, updatedAt: now, ...a }); changed = true; } };
    addAcc({ id: CEO_ID, role: 'admin', teamRole: 'ceo', name: 'Sharaf', company: 'Super App Egypt', email: 'ceo@superapp.example', phone: '+20 2 2735 0001', avatar: 4 });
    addAcc({ id: 'acc-cfo', role: 'admin', teamRole: 'cfo', name: 'Laila Mansour', company: 'Super App Egypt', email: 'finance@superapp.example', phone: '+20 2 2735 0002', avatar: 9 });
    ORG().ROLES.filter((r) => r.holder === 'ai').forEach((r, i) => addAcc({ id: agentAcc(r.id), role: 'admin', teamRole: r.id, agent: true, name: `${r.name} (AI)`, company: 'Super App Egypt', email: `${r.id}@agents.superapp.example`, avatar: (i * 3) % 12 }));
    const hq = d.accounts.find((a) => a.id === 'acc-admin');
    if (hq && !hq.teamRole) { hq.teamRole = 'hq'; changed = true; }
    if (!d.settings.ceo) {
      d.settings.ceo = { limits: { ...LIMITS }, agents: {}, budgets: { ...BUDGETS }, switches: {}, briefingAt: 0 };
      changed = true;
    }
    if (!d.ceoGoals.length) { GOALS.forEach((g) => d.ceoGoals.push({ ...g, createdAt: now, updatedAt: now })); changed = true; }
    if (!d.ceoCalendar.length) {
      CAL.forEach(([title, dept, owner, every, note, inDays], i) => d.ceoCalendar.push({ id: `cal-${i + 1}`, title, dept, owner, every, note, due: isoDate(addDays(today(), inDays)), done: false, createdAt: now, updatedAt: now }));
      changed = true;
    }
    if (!d.ceoExpenses.length) {
      [['marketing', 'Social media ads (Meta & TikTok)', 18500, 6], ['marketing', 'Influencer match-day videos', 9000, 11], ['legal', 'Law firm retainer', 12000, 2], ['tech', 'Cloud hosting & AI model usage', 14200, 1], ['ops', 'Courier trial (Bosta)', 6400, 4], ['support', 'Customer goodwill credits', 2100, 8]]
        .forEach(([dept, text, amount, ago], i) => d.ceoExpenses.push({ id: `ex-${i + 1}`, dept, text, amount, at: now - ago * DAY, createdAt: now, updatedAt: now, by: CEO_ID }));
      changed = true;
    }
    if (changed) db().touch('accounts');
  };

  /* ---------- Session ---------- */
  const ceoMode = () => /^\/ceo(?:[/?]|$)/.test(SA.core.router.location());
  const auth = {
    current() { try { const id = localStorage.getItem(SESSION); const a = id && db().find('accounts', id); return a && a.teamRole === 'ceo' ? a : null; } catch { return null; } },
    signIn(pin) {
      if (String(pin) !== CEO_PIN) throw new ApiError('Wrong PIN.', 'invalid');
      try { localStorage.setItem(SESSION, CEO_ID); } catch { /* private mode */ }
      db().log(CEO_ID, 'ceo.signin', 'The CEO signed in to the Command Center');
    },
    /** API mode: the real CEO account signed in with its authenticator code opens the Command Center */
    signInWithAccount() {
      if (!SA.services.session?.has('ceo')) throw new ApiError('Sign in with the CEO account first.', 'forbidden');
      try { localStorage.setItem(SESSION, CEO_ID); } catch { /* private mode */ }
      db().log(CEO_ID, 'ceo.signin', 'The CEO signed in to the Command Center (account + authenticator)');
    },
    signOut() { try { localStorage.removeItem(SESSION); } catch { /* ignore */ } },
  };
  const me = () => { const a = auth.current(); if (!a) throw new ApiError('Sign in to the CEO system first.', 'forbidden'); return a; };
  const log = (action, text, ref) => db().log(me().id, `ceo.${action}`, text, ref);
  const cfg = () => db().settings().ceo || { limits: LIMITS, agents: {}, budgets: BUDGETS, switches: {} };
  const saveCfg = (patch) => db().setSettings({ ceo: { ...cfg(), ...patch } });
  const limits = () => ({ ...LIMITS, ...(cfg().limits || {}) });

  /* ---------- Org: roles and agents ---------- */
  const role = (id) => ORG().ROLES.find((r) => r.id === id);
  /** A role with the CEO's changes applied (status, limit, permissions) and its account */
  const view = (r) => {
    const st = cfg().agents?.[r.id] || {};
    const accountId = r.account || (r.holder === 'ai' ? agentAcc(r.id) : r.id === 'ceo' ? CEO_ID : null);
    return { ...r, status: st.status || 'active', pausedReason: st.reason || '', limit: st.limit ?? r.limit, can: st.can || r.can, sections: st.sections || r.sections, accountId, dept: r.dept, deptInfo: ORG().DEPTS.find((x) => x.key === r.dept) };
  };
  const roles = () => ORG().ROLES.map(view);
  const getRole = (id) => { const r = role(id); if (!r) throw new ApiError('Role not found.', 'not_found'); return view(r); };
  /** The role of an admin account (accounts without one are the HQ admin manager) */
  const roleOfAccount = (a) => (a ? getRole(a.teamRole && role(a.teamRole) ? a.teamRole : 'hq') : null);
  const canSee = (a, section) => { const r = roleOfAccount(a); return !r || r.sections.includes('*') || r.sections.includes(section); };
  const may = (a, action) => { const r = roleOfAccount(a); return !r || r.can.includes(action); };

  /** Pause or resume one agent, or change its limit and permissions (logged, the agent is told) */
  const setAgent = (id, patch, reason = '') => {
    me();
    const r = getRole(id);
    if (r.id === 'ceo') throw new ApiError('You can’t change your own role here.', 'invalid');
    const cur = cfg().agents?.[id] || {};
    const next = { ...cur };
    const changes = [];
    if (patch.status && patch.status !== r.status) {
      if (!['active', 'paused'].includes(patch.status)) throw new ApiError('Unknown status.', 'invalid');
      if (patch.status === 'paused' && reason.trim().length < 3) throw new ApiError('Write the reason.', 'invalid');
      next.status = patch.status; next.reason = patch.status === 'paused' ? reason.trim() : ''; next.at = Date.now();
      changes.push(patch.status === 'paused' ? `paused: ${reason.trim()}` : 'resumed');
    }
    if (patch.limit != null) {
      const v = Math.round(+patch.limit);
      if (!(v >= 0 && v <= 1000000)) throw new ApiError('Limit between 0 and 1,000,000 EGP.', 'invalid');
      if (v !== r.limit) { next.limit = v; changes.push(`money limit ${v.toLocaleString()} EGP`); }
    }
    if (patch.can) { next.can = patch.can.filter((k) => ORG().ACTIONS.some(([x]) => x === k)); changes.push(`permissions: ${next.can.length ? next.can.join(', ') : 'none'}`); }
    if (patch.sections) { next.sections = patch.sections; changes.push(`sections: ${patch.sections.length}`); }
    if (!changes.length) return r;
    saveCfg({ agents: { ...(cfg().agents || {}), [id]: next } });
    log(`agent.${patch.status || 'changed'}`, `${r.name} — ${r.title}: ${changes.join('; ')}`, id);
    if (r.accountId) N()?.toAccount(r.accountId, { cat: 'account', tone: patch.status === 'paused' ? 'danger' : 'info', title: patch.status === 'paused' ? 'You were paused by the CEO' : patch.status === 'active' ? 'You can work again' : 'Your role was changed by the CEO', body: changes.join(' · '), link: '#/admin' });
    return getRole(id);
  };
  /** Emergency stop: every AI agent is paused at once (humans keep working) */
  const stopAll = (reason) => {
    me();
    if (!reason || reason.trim().length < 3) throw new ApiError('Write the reason.', 'invalid');
    const agents = { ...(cfg().agents || {}) };
    const list = roles().filter((r) => r.holder === 'ai' && r.status === 'active');
    list.forEach((r) => { agents[r.id] = { ...(agents[r.id] || {}), status: 'paused', reason: `Emergency stop: ${reason.trim()}`, at: Date.now(), stopAll: true }; });
    saveCfg({ agents });
    log('agent.stopall', `Emergency stop — ${list.length} AI agents paused: ${reason.trim()}`);
    N()?.toAdmins({ cat: 'account', tone: 'danger', title: 'Emergency stop: AI agents paused', body: reason.trim(), link: '#/admin' });
    return list.length;
  };
  const resumeAll = () => {
    me();
    const agents = { ...(cfg().agents || {}) };
    let n = 0;
    Object.keys(agents).forEach((k) => { if (agents[k].stopAll) { agents[k] = { ...agents[k], status: 'active', reason: '', stopAll: false }; n++; } });
    saveCfg({ agents });
    log('agent.resumeall', `Emergency stop lifted — ${n} AI agents working again`);
    return n;
  };
  /** Is this admin account allowed in? (paused agents are kept out of the admin console) */
  const blockedAccount = (a) => { if (!a?.teamRole || ['ceo', 'hq'].includes(a.teamRole)) return ''; const r = role(a.teamRole) && getRole(a.teamRole); return r && r.status === 'paused' ? (r.pausedReason || 'Paused by the CEO') : ''; };

  /** What happened in an agent's area. Its own actions are real; the area's actions are shown as demo work */
  const activity = (id, { limit = 60 } = {}) => {
    const r = getRole(id);
    const acts = db().all('activity');
    return acts.filter((x) => x.actor === r.accountId || (r.watch || []).some((w) => x.action?.startsWith(w))).slice(0, limit)
      .map((x) => ({ ...x, own: x.actor === r.accountId, demo: x.actor !== r.accountId, actorName: db().find('accounts', x.actor)?.name || (x.actor === 'customer' ? 'Customer' : 'System') }));
  };
  /** Work numbers for one agent (30 days). Errors, tokens and response time are demo numbers until the backend */
  const agentStats = (id) => {
    const r = getRole(id);
    const since = Date.now() - 30 * DAY;
    const acts = activity(id, { limit: 1000 }).filter((x) => x.at >= since);
    const s = (k) => seeded(`${id}|${k}|${isoDate(today())}`);
    const tasks = acts.length + Math.round(40 + s('t') * 260);
    return { tasks, errors: r.holder === 'ai' ? Math.round(s('e') * 6) : 0, accuracy: r.holder === 'ai' ? +(96 + s('a') * 3.8).toFixed(1) : null, avgMinutes: +(0.4 + s('m') * 3).toFixed(1), cost: r.cost || 0, tokens: r.holder === 'ai' ? Math.round((1.5 + s('k') * 9) * 1e6) : 0, last: acts[0]?.at || 0, escalations: Math.round(s('x') * 9) };
  };

  /* ---------- Approvals (+ the two-person rule) ---------- */
  let exec = false; // true while an approved request runs, so the gate lets it through
  let queuedMsg = '';
  const KIND = { payout: 'Withdrawal', settle: 'Factory settlement', refund: 'Customer refund', credit: 'Customer credit', adjust: 'Balance adjustment' };
  const ACTION_OF = { payout: 'payout.approve', settle: 'settle', refund: 'refund', credit: 'credit', adjust: 'adjust' };
  /**
   * Called by money actions before they move money. Lets it run, or creates a request for the CEO and stops.
   * kind: payout | settle | refund | credit | adjust · fn/args: the admin method to run once approved
   */
  const gate = ({ kind, amount, fn, args, title, detail = '', ref = '' }) => {
    if (exec) return;
    const a = D().me();
    if (!a || a.role !== 'admin') return; // not an admin action (the admin service checks that itself)
    const r = roleOfAccount(a);
    const L = limits();
    const cap = ['refund', 'credit'].includes(kind) ? L.refund : L.payout;
    const action = ACTION_OF[kind];
    const isCeo = r.id === 'ceo';
    const allowed = r.can.includes(action);
    const prepares = kind === 'payout' && r.can.includes('payout.prepare');
    if (!isCeo && !allowed && !prepares) throw new ApiError(`Your role (${r.title}) can’t do this. Ask the admin manager.`, 'forbidden');
    const why = [];
    if (isCeo) { if (amount > L.dual) why.push(`over ${L.dual.toLocaleString()} EGP — the CFO confirms too`); }
    else {
      if (!allowed) why.push(`${r.title} prepares — the CFO or the CEO sends it`);
      if (amount > cap) why.push(`over the ${cap.toLocaleString()} EGP ${['refund', 'credit'].includes(kind) ? 'refund' : 'payout'} limit`);
      else if (amount > r.limit) why.push(`over your ${r.limit.toLocaleString()} EGP limit`);
    }
    if (!why.length) return;
    const open = db().all('ceoApprovals').find((x) => x.status === 'pending' && x.fn === fn && JSON.stringify(x.args) === JSON.stringify(args));
    if (open) { queuedMsg = `Already waiting for the CEO (${open.id}).`; throw new ApiError(queuedMsg, 'needs_approval'); }
    const need = amount > L.dual ? 2 : 1;
    const req = db().insert('ceoApprovals', { kind, amount, fn, args, title, detail, ref, why, need, status: 'pending', approvals: isCeo ? [{ by: a.id, name: a.name, at: Date.now(), role: 'ceo' }] : [], requestedBy: { id: a.id, name: a.name, role: r.id, title: r.title }, at: Date.now() }, 'AP');
    db().log(a.id, 'ceo.approval.requested', `${KIND[kind]} of ${amount.toLocaleString()} EGP sent for approval: ${title} (${why.join('; ')})`, req.id);
    if (!isCeo) N()?.toAccount(CEO_ID, { cat: 'money', tone: 'blaze', title: `Approve: ${KIND[kind]} ${amount.toLocaleString()} EGP`, body: `${title} — asked by ${a.name}`, link: '#/ceo/approvals' });
    // A withdrawal the payables agent prepared (within the CEO's limit) can be sent by the CFO from the admin console
    if (need === 1 && !allowed && prepares && amount <= cap) N()?.toAccount('acc-cfo', { cat: 'money', tone: 'blaze', title: `Ready to send: ${amount.toLocaleString()} EGP`, body: `${title} — prepared by ${a.name}`, link: '#/admin/finance-approvals' });
    if (need === 2) N()?.toAccount('acc-cfo', { cat: 'money', tone: 'blaze', title: `Second approval needed: ${amount.toLocaleString()} EGP`, body: title, link: '#/admin/finance-approvals' });
    queuedMsg = isCeo ? `Over ${L.dual.toLocaleString()} EGP — waiting for the CFO’s confirmation (${req.id}).` : `Sent to the CEO for approval (${req.id}).`;
    throw new ApiError(queuedMsg, 'needs_approval');
  };
  const approvals = ({ status = '' } = {}) => db().all('ceoApprovals').filter((x) => !status || x.status === status).sort((a, b) => (a.status === 'pending') === (b.status === 'pending') ? b.at - a.at : a.status === 'pending' ? -1 : 1);
  const findReq = (id) => { const x = db().find('ceoApprovals', id); if (!x) throw new ApiError('Request not found.', 'not_found'); if (x.status !== 'pending') throw new ApiError('This request was already handled.', 'invalid'); return x; };
  const run = async (x) => {
    exec = true;
    try { await AD()[x.fn](...x.args); }
    finally { exec = false; }
  };
  const tellRequester = (x, title, body) => { if (x.requestedBy?.id && x.requestedBy.id !== CEO_ID) N()?.toAccount(x.requestedBy.id, { cat: 'money', tone: title.startsWith('Approved') ? 'turf' : 'danger', title, body, link: '#/admin' }); };
  /** The CEO approves. Over the two-person limit the CFO must confirm with their PIN as well (cfoPin) */
  const approve = async (id, { cfoPin = '', note = '' } = {}) => {
    const a = me();
    const x = findReq(id);
    const approvals_ = [...x.approvals];
    if (!approvals_.some((p) => p.by === a.id)) approvals_.push({ by: a.id, name: a.name, at: Date.now(), role: 'ceo', note: note.trim() });
    if (x.need === 2 && !approvals_.some((p) => p.role === 'cfo')) {
      const cfo = role('cfo');
      if (!cfoPin) { db().update('ceoApprovals', id, { approvals: approvals_ }); log('approval.first', `First approval for ${x.id} (${x.amount.toLocaleString()} EGP) — waiting for the CFO`, id); return { waiting: true }; }
      if (String(cfoPin) !== cfo.pin) throw new ApiError('The CFO’s PIN is wrong.', 'invalid');
      approvals_.push({ by: 'acc-cfo', name: cfo.name, at: Date.now(), role: 'cfo' });
    }
    try { await run(x); }
    catch (e) { db().update('ceoApprovals', id, { approvals: approvals_, lastError: e.message }); throw e; }
    db().update('ceoApprovals', id, { status: 'approved', approvals: approvals_, doneAt: Date.now(), note: note.trim() });
    log('approval.approved', `Approved ${x.id}: ${KIND[x.kind]} of ${x.amount.toLocaleString()} EGP — ${x.title}${approvals_.length > 1 ? ` (${approvals_.map((p) => p.name).join(' + ')})` : ''}`, id);
    tellRequester(x, `Approved: ${x.amount.toLocaleString()} EGP`, x.title);
    return { done: true };
  };
  /**
   * Admin console (finance): what this admin may do with a request.
   * 'confirm' = second approval over the two-person limit (the CFO) · 'send' = a prepared request within the CEO's
   * limit and the role's own limit · 'ceo' = only the CEO decides · '' = not for this role
   */
  const financeState = (x, a = D().me()) => {
    if (!a || a.role !== 'admin' || x.status !== 'pending') return '';
    const r = roleOfAccount(a);
    if (r.id === 'ceo' || !r.can.includes(ACTION_OF[x.kind])) return '';
    if (x.requestedBy?.id === a.id) return 'own';
    if (x.need === 2) return r.id === 'cfo' ? ((x.approvals || []).some((p) => p.role === 'cfo') ? 'done' : 'confirm') : 'ceo';
    const L = limits(); const cap = ['refund', 'credit'].includes(x.kind) ? L.refund : L.payout;
    return x.amount <= cap && x.amount <= r.limit ? 'send' : 'ceo';
  };
  const financeQueue = () => { const a = D().me(); return approvals({ status: 'pending' }).map((x) => ({ ...x, state: financeState(x, a) })).filter((x) => x.state); };
  const financeApprove = async (id) => {
    const a = D().me();
    const x = findReq(id);
    const st = financeState(x, a);
    if (!['confirm', 'send'].includes(st)) throw new ApiError(st === 'own' ? 'You asked for this — someone else must approve it.' : 'Only the CEO can approve this.', 'forbidden');
    const r = roleOfAccount(a);
    const approvals_ = [...(x.approvals || []), { by: a.id, name: a.name, at: Date.now(), role: r.id }];
    const complete = st === 'send' || approvals_.some((p) => p.role === 'ceo');
    if (!complete) {
      db().update('ceoApprovals', id, { approvals: approvals_ });
      db().log(a.id, 'ceo.approval.second', `${r.title} approved ${x.id} (${x.amount.toLocaleString()} EGP) — waiting for the CEO`, id);
      N()?.toAccount(CEO_ID, { cat: 'money', tone: 'blaze', title: `CFO approved — your turn: ${x.amount.toLocaleString()} EGP`, body: x.title, link: '#/ceo/approvals' });
      return { waiting: true };
    }
    try { await run(x); }
    catch (e) { db().update('ceoApprovals', id, { approvals: approvals_, lastError: e.message }); throw e; }
    db().update('ceoApprovals', id, { status: 'approved', approvals: approvals_, doneAt: Date.now() });
    db().log(a.id, 'ceo.approval.approved', `Approved ${x.id}: ${KIND[x.kind]} of ${x.amount.toLocaleString()} EGP — ${x.title} (${approvals_.map((p) => p.name).join(' + ')})`, id);
    tellRequester(x, `Approved: ${x.amount.toLocaleString()} EGP`, x.title);
    return { done: true };
  };
  const financeReject = (id, reason) => {
    const a = D().me();
    const x = findReq(id);
    if (!['confirm', 'send'].includes(financeState(x, a))) throw new ApiError('Only the CEO can decide this.', 'forbidden');
    if (!reason || reason.trim().length < 3) throw new ApiError('Write the reason.', 'invalid');
    db().update('ceoApprovals', id, { status: 'rejected', reason: reason.trim(), doneAt: Date.now(), rejectedBy: a.name });
    db().log(a.id, 'ceo.approval.rejected', `Rejected ${x.id}: ${KIND[x.kind]} of ${x.amount.toLocaleString()} EGP — ${reason.trim()}`, id);
    tellRequester(x, `Not approved: ${x.amount.toLocaleString()} EGP`, `${x.title} — ${reason.trim()}`);
    N()?.toAccount(CEO_ID, { cat: 'money', title: `${a.name} rejected ${x.id}`, body: reason.trim(), link: '#/ceo/approvals?tab=rejected' });
  };
  const reject = (id, reason) => {
    me();
    const x = findReq(id);
    if (!reason || reason.trim().length < 3) throw new ApiError('Write the reason.', 'invalid');
    db().update('ceoApprovals', id, { status: 'rejected', reason: reason.trim(), doneAt: Date.now() });
    log('approval.rejected', `Rejected ${x.id}: ${KIND[x.kind]} of ${x.amount.toLocaleString()} EGP — ${reason.trim()}`, id);
    tellRequester(x, `Not approved: ${x.amount.toLocaleString()} EGP`, `${x.title} — ${reason.trim()}`);
  };
  const setLimits = ({ payout, refund, dual }) => {
    me();
    const v = { payout: Math.round(+payout), refund: Math.round(+refund), dual: Math.round(+dual) };
    if (!(v.payout >= 0 && v.refund >= 0 && v.dual >= v.payout && v.dual >= v.refund)) throw new ApiError('The two-person limit must be at least as high as the other two.', 'invalid');
    saveCfg({ limits: v });
    log('limits', `Approval limits: payouts over ${v.payout.toLocaleString()}, refunds and credits over ${v.refund.toLocaleString()}, two approvals over ${v.dual.toLocaleString()} EGP`);
  };

  /* ---------- Kill switches ---------- */
  const switches = () => {
    const m = db().settings().maintenance || {};
    const s = cfg().switches || {};
    return SWITCHES.map(([key, label, text, ic]) => ({ key, label, text, icon: ic, on: key === 'bookings' ? !m.bookings : key === 'shop' ? !m.shop : s[key]?.on !== false, reason: key === 'bookings' || key === 'shop' ? m.message || '' : s[key]?.reason || '', at: s[key]?.at || m.at || 0 }));
  };
  const isOn = (key) => { try { if (key === 'bookings' || key === 'shop') return !(db().settings().maintenance || {})[key]; return cfg().switches?.[key]?.on !== false; } catch { return true; } };
  const OFF_MSG = { pod: 'Printed designs are paused right now.', payouts: 'Withdrawals are paused right now. Your balance is safe — try again later.', applications: 'New applications are paused right now. Please try again soon.', fantasy: 'Fantasy is paused for a short while. Please try again soon.', community: 'Posting is paused for a short while.', games: 'Joining games is paused right now.' };
  const assertOn = (key) => { if (!isOn(key)) throw new ApiError(OFF_MSG[key] || 'This is paused right now.', 'paused'); };
  const setSwitch = (key, on, reason = '') => {
    me();
    if (!SWITCHES.some(([k]) => k === key)) throw new ApiError('Unknown switch.', 'invalid');
    if (!on && reason.trim().length < 3) throw new ApiError('Write the reason (customers see it for bookings and the shop).', 'invalid');
    if (key === 'bookings' || key === 'shop') {
      const m = db().settings().maintenance || {};
      AD().setMaintenance({ bookings: key === 'bookings' ? !on : !!m.bookings, shop: key === 'shop' ? !on : !!m.shop, message: on ? m.message || '' : reason.trim() });
    } else saveCfg({ switches: { ...(cfg().switches || {}), [key]: { on: !!on, reason: on ? '' : reason.trim(), at: Date.now() } } });
    const label = SWITCHES.find(([k]) => k === key)[1];
    log(`switch.${on ? 'on' : 'off'}`, `${label} ${on ? 'turned back on' : `turned off: ${reason.trim()}`}`, key);
    if (!on) N()?.toAdmins({ cat: 'account', tone: 'danger', title: `${label} turned off by the CEO`, body: reason.trim(), link: '#/admin' });
    SA.ui.shell?.render?.();
  };

  /* ---------- Numbers ---------- */
  const range30 = () => ({ from: isoDate(addDays(today(), -29)), to: isoDate(today()) });
  const rev = (r) => { try { return AD().revenue(r); } catch { return null; } };
  const kpis = () => {
    const r = rev(range30()), p = rev({ from: isoDate(addDays(today(), -59)), to: isoDate(addDays(today(), -30)) });
    const t = r?.total || {}, pt = p?.total || {};
    const s = r?.summary || {};
    const bookings = (s.ledger || []).filter((b) => b.status !== 'cancelled').length;
    const pBookings = (p?.summary?.ledger || []).filter((b) => b.status !== 'cancelled').length;
    const pod = (db().all('podOrders') || []).filter((o) => !o.sample && o.placedAt >= Date.now() - 30 * DAY).length;
    const customers = (() => { try { return AD().customers().length; } catch { return 0; } })();
    const roleList = roles();
    return {
      revenue: t.net || 0, revenuePrev: pt.net || 0, gmv: t.gmv || 0, gmvPrev: pt.gmv || 0, income: t.income || 0, cost: t.cost || 0,
      bookings, bookingsPrev: pBookings, orders: (s.sales || []).length + pod, customers,
      partners: db().all('accounts').filter((a) => a.role === 'owner' && a.status !== 'suspended').length,
      pitches: db().all('pitches').filter((x) => !x.deleted && x.status === 'active').length,
      sellers: db().all('sellers').filter((x) => x.status === 'active').length,
      designs: (db().all('designs') || []).filter((x) => x.status === 'live').length,
      ticketsOpen: (db().all('tickets') || []).filter((x) => x.status !== 'resolved').length,
      approvals: approvals({ status: 'pending' }).length,
      payoutsWaiting: (() => { try { return AD().pendingPayouts().length; } catch { return 0; } })(),
      agents: roleList.filter((x) => x.holder === 'ai').length, agentsActive: roleList.filter((x) => x.holder === 'ai' && x.status === 'active').length,
      agentCost: roleList.reduce((a, x) => a + (x.cost || 0), 0),
      rating: (() => { const rs = db().all('reviews') || []; return rs.length ? +(rs.reduce((a, x) => a + (x.rating || 0), 0) / rs.length).toFixed(2) : 0; })(),
      daily: r?.daily || [],
    };
  };
  const METRICS = { revenue30: ['Platform revenue (30 days)', 'money'], gmv30: ['Money through Super App (30 days)', 'money'], bookings30: ['Bookings (30 days)', 'n'], pitches: ['Active pitches', 'n'], partners: ['Pitch partners', 'n'], sellers: ['Active sellers', 'n'], designs: ['Live designs', 'n'], customers: ['Customers', 'n'], ticketsOpen: ['Open tickets', 'n'], rating: ['Average rating', 'n'], agentCost: ['AI agents cost / month', 'money'], agentsActive: ['Agents working', 'n'], manual: ['Entered by hand', 'n'] };
  const metric = (k, K = kpis()) => ({ revenue30: K.revenue, gmv30: K.gmv, bookings30: K.bookings, pitches: K.pitches, partners: K.partners, sellers: K.sellers, designs: K.designs, customers: K.customers, ticketsOpen: K.ticketsOpen, rating: K.rating, agentCost: K.agentCost, agentsActive: K.agentsActive }[k] ?? 0);

  /** Department health: a score out of 100 and the reasons */
  const health = (K = kpis()) => {
    const al = (() => { try { return AD().alerts(); } catch { return []; } })();
    const cnt = (key) => al.find((x) => x.key === key)?.count || 0;
    const fa = SA.services.factory?.alerts() || {};
    const cal = calendar();
    const late = (dept) => cal.filter((c) => c.dept === dept && c.state === 'overdue').length;
    const agentsOf = (dept) => roles().filter((r) => r.dept === dept && r.holder === 'ai');
    const paused = (dept) => agentsOf(dept).filter((r) => r.status === 'paused').length;
    const B = budgets();
    const over = (dept) => { const b = B.find((x) => x.dept === dept); return b && b.spent > b.budget ? 1 : 0; };
    const issues = {
      ops: [[fa.late, `${fa.late} late factory job(s)`, 12], [fa.issues, `${fa.issues} production problem(s)`, 10], [cnt('stock'), 'Store products low on stock', 4]],
      support: [[K.ticketsOpen, `${K.ticketsOpen} open ticket(s)`, 4], [cnt('reviews'), 'Reported reviews waiting', 4], [cnt('community'), 'Community reports waiting', 4]],
      bizdev: [[cnt('approvals'), `${cnt('approvals')} application(s) waiting`, 5], [cnt('kyc'), 'Seller ID checks waiting', 5]],
      finance: [[K.payoutsWaiting, `${K.payoutsWaiting} withdrawal(s) waiting`, 4], [K.approvals, `${K.approvals} approval(s) waiting for you`, 6], [K.cost > K.income * 0.2 ? 1 : 0, 'Costs over 20% of income', 15]],
      legal: [[late('legal'), 'Overdue legal deadline(s)', 20], [cnt('designs'), 'Copyright reviews waiting', 3]],
      marketing: [[K.revenue < K.revenuePrev ? 1 : 0, 'Revenue below the 30 days before', 15]],
      tech: [[late('tech'), 'Overdue technology task(s)', 15], [switches().filter((s) => !s.on).length, 'Something is switched off', 10]],
      aiops: [[roles().filter((r) => r.holder === 'ai' && r.status === 'paused').length, 'Paused agent(s)', 8]],
    };
    return ORG().DEPTS.map((d) => {
      const list = [...(issues[d.key] || []), [late(d.key) && d.key !== 'legal' && d.key !== 'tech' ? late(d.key) : 0, 'Overdue deadline(s)', 12], [paused(d.key) && d.key !== 'aiops' ? paused(d.key) : 0, 'Agent(s) paused', 10], [over(d.key), 'Over budget', 10]].filter(([n]) => n > 0);
      const score = Math.max(5, 100 - list.reduce((t, [n, , w]) => t + Math.min(30, n * w), 0));
      return { ...d, score, tone: score >= 80 ? 'turf' : score >= 55 ? 'gold' : 'danger', issues: list.map(([n, text]) => ({ n, text })), people: roles().filter((r) => r.dept === d.key), headRole: roles().find((r) => r.id === d.head) };
    });
  };

  /* ---------- Goals (OKRs) ---------- */
  const goals = () => { const K = kpis(); return db().all('ceoGoals').map((g) => {
    const krs = g.krs.map((kr) => { const cur = kr.metric === 'manual' ? +kr.current || 0 : metric(kr.metric, K); const pct = kr.lower ? (cur <= kr.target ? 100 : Math.max(0, Math.round((kr.target / Math.max(cur, 1)) * 100))) : Math.min(100, Math.round((cur / Math.max(kr.target, 1)) * 100)); return { ...kr, current: cur, pct }; });
    return { ...g, krs, pct: krs.length ? Math.round(krs.reduce((t, k) => t + k.pct, 0) / krs.length) : 0 };
  }); };
  const saveGoal = (d, id = null) => {
    me();
    const objective = String(d.objective || '').trim();
    if (objective.length < 3) throw new ApiError('Write the goal.', 'invalid', { objective: 'Write the goal.' });
    const krs = (d.krs || []).filter((k) => String(k.text || '').trim()).map((k) => ({ text: String(k.text).trim().slice(0, 120), metric: METRICS[k.metric] ? k.metric : 'manual', target: +k.target || 0, current: +k.current || 0, lower: !!k.lower }));
    if (!krs.length) throw new ApiError('Add at least one key result.', 'invalid');
    const rec = { objective: objective.slice(0, 120), dept: d.dept || 'ops', quarter: d.quarter || 'Q4', krs };
    const g = id ? db().update('ceoGoals', id, rec) : db().insert('ceoGoals', rec, 'g');
    log('goal.saved', `${id ? 'Updated' : 'New'} goal: ${rec.objective}`, g.id);
    return g;
  };
  const removeGoal = (id) => { me(); const g = db().find('ceoGoals', id); db().remove('ceoGoals', id); log('goal.removed', `Removed goal: ${g?.objective}`, id); };

  /* ---------- Budgets ---------- */
  const monthStart = () => { const d = today(); return new Date(d.getFullYear(), d.getMonth(), 1).getTime(); };
  const budgets = () => {
    const B = { ...BUDGETS, ...(cfg().budgets || {}) };
    const from = isoDate(new Date(monthStart())), to = isoDate(today());
    const r = rev({ from, to })?.total || {};
    const auto = { marketing: r.promos || 0, support: r.refunds || 0, ops: r.refusals || 0 };
    const ex = db().all('ceoExpenses').filter((x) => x.at >= monthStart());
    const dayOfMonth = today().getDate(), daysInMonth = new Date(today().getFullYear(), today().getMonth() + 1, 0).getDate();
    return ORG().DEPTS.map((d) => {
      const agents = roles().filter((x) => x.dept === d.key && x.holder === 'ai').reduce((t, x) => t + (x.cost || 0), 0);
      const agentsSoFar = Math.round((agents * dayOfMonth) / daysInMonth);
      const spentEx = ex.filter((x) => x.dept === d.key).reduce((t, x) => t + x.amount, 0);
      const spent = Math.round(agentsSoFar + spentEx + (auto[d.key] || 0));
      const forecast = Math.round(agents + spentEx * (daysInMonth / Math.max(1, dayOfMonth)) + (auto[d.key] || 0) * (daysInMonth / Math.max(1, dayOfMonth)));
      return { dept: d.key, name: d.name, icon: d.icon, budget: B[d.key] || 0, spent, forecast, parts: { agents: agentsSoFar, expenses: spentEx, auto: Math.round(auto[d.key] || 0) }, pct: B[d.key] ? Math.round((spent / B[d.key]) * 100) : 0 };
    });
  };
  const setBudget = (dept, amount) => {
    me();
    const v = Math.round(+amount);
    if (!(v >= 0 && v <= 10000000)) throw new ApiError('Budget between 0 and 10,000,000 EGP.', 'invalid');
    saveCfg({ budgets: { ...(cfg().budgets || BUDGETS), [dept]: v } });
    log('budget', `${ORG().DEPTS.find((d) => d.key === dept)?.name} budget set to ${v.toLocaleString()} EGP a month`, dept);
  };
  const addExpense = ({ dept, text, amount }) => {
    me();
    const v = Math.round(+amount);
    if (!ORG().DEPTS.some((d) => d.key === dept)) throw new ApiError('Choose the department.', 'invalid', { dept: 'Choose the department.' });
    if (String(text || '').trim().length < 3) throw new ApiError('Write what it was for.', 'invalid', { text: 'Write what it was for.' });
    if (!(v > 0 && v <= 10000000)) throw new ApiError('Enter the amount.', 'invalid', { amount: 'Enter the amount.' });
    const x = db().insert('ceoExpenses', { dept, text: String(text).trim().slice(0, 120), amount: v, at: Date.now(), by: me().id }, 'ex');
    log('expense', `Expense ${v.toLocaleString()} EGP for ${ORG().DEPTS.find((d) => d.key === dept).name}: ${x.text}`, x.id);
    return x;
  };

  /* ---------- Compliance calendar ---------- */
  const NEXT = { monthly: 1, quarterly: 3, yearly: 12 };
  const calendar = () => db().all('ceoCalendar').map((c) => {
    const left = Math.round((parseISO(c.due).getTime() - today().getTime()) / DAY);
    return { ...c, left, state: c.done ? 'done' : left < 0 ? 'overdue' : left <= 7 ? 'soon' : 'later', ownerRole: role(c.owner) };
  }).sort((a, b) => (a.done === b.done ? a.due.localeCompare(b.due) : a.done ? 1 : -1));
  const doneItem = (id) => {
    me();
    const c = db().find('ceoCalendar', id); if (!c) throw new ApiError('Not found.', 'not_found');
    db().update('ceoCalendar', id, { done: true, doneAt: Date.now() });
    if (NEXT[c.every]) { const d = parseISO(c.due); d.setMonth(d.getMonth() + NEXT[c.every]); db().insert('ceoCalendar', { title: c.title, dept: c.dept, owner: c.owner, every: c.every, note: c.note, due: isoDate(d), done: false }, 'cal'); }
    log('calendar.done', `Done: ${c.title} (due ${fmtDate(c.due)})`, id);
  };
  const addItem = ({ title, dept, owner, every, due, note = '' }) => {
    me();
    const errs = {};
    if (String(title || '').trim().length < 3) errs.title = 'Write what is due.';
    if (!/^\d{4}-\d{2}-\d{2}$/.test(due || '')) errs.due = 'Choose the date.';
    if (Object.keys(errs).length) throw new ApiError('Check the form.', 'invalid', errs);
    const x = db().insert('ceoCalendar', { title: String(title).trim().slice(0, 100), dept: dept || 'legal', owner: owner || 'compliance', every: every || 'once', note: String(note).trim().slice(0, 140), due, done: false }, 'cal');
    log('calendar.added', `Added to the compliance calendar: ${x.title} (${fmtDate(due)})`, x.id);
    return x;
  };

  /* ---------- Morning briefing ---------- */
  const briefing = () => {
    const y = isoDate(addDays(today(), -1));
    const r = rev({ from: y, to: y });
    const wk = rev({ from: isoDate(addDays(today(), -8)), to: isoDate(addDays(today(), -2)) });
    const avg = wk ? wk.total.net / 7 : 0;
    const yt = r?.total || {};
    const K = kpis();
    const since = parseISO(y).getTime(), until = since + DAY;
    const acts = db().all('activity').filter((x) => x.at >= since && x.at < until);
    const cal = calendar().filter((c) => !c.done && c.left <= 7);
    const H = health(K);
    const lines = [];
    const pct = avg ? Math.round(((yt.net - avg) / avg) * 100) : 0;
    lines.push({ tone: pct >= 0 ? 'turf' : 'danger', icon: 'percent', text: `Yesterday’s platform revenue was ${Math.round(yt.net || 0).toLocaleString()} EGP — ${pct >= 0 ? `${pct}% above` : `${Math.abs(pct)}% below`} the 7-day average.` });
    lines.push({ tone: 'info', icon: 'trend', text: `${Math.round(yt.gmv || 0).toLocaleString()} EGP went through Super App, with ${(r?.summary?.ledger || []).filter((b) => b.status !== 'cancelled').length} bookings and ${(r?.summary?.sales || []).length} store orders.` });
    if (K.approvals) lines.push({ tone: 'blaze', icon: 'check', text: `${K.approvals} request(s) wait for your approval.`, href: '#/ceo/approvals' });
    if (K.payoutsWaiting) lines.push({ tone: 'gold', icon: 'wallet', text: `${K.payoutsWaiting} withdrawal(s) are waiting in the admin console.` });
    const weak = H.filter((d) => d.score < 80);
    if (weak.length) lines.push({ tone: 'gold', icon: 'alert', text: `Needs attention: ${weak.map((d) => `${d.name} (${d.score})`).join(', ')}.`, href: '#/ceo/departments' });
    if (cal.length) lines.push({ tone: cal.some((c) => c.state === 'overdue') ? 'danger' : 'gold', icon: 'calendar', text: `${cal.length} deadline(s) this week: ${cal.slice(0, 3).map((c) => c.title).join(', ')}.`, href: '#/ceo/calendar' });
    const off = switches().filter((s) => !s.on);
    if (off.length) lines.push({ tone: 'danger', icon: 'ban', text: `Still switched off: ${off.map((s) => s.label).join(', ')}.`, href: '#/ceo/control' });
    const paused = roles().filter((x) => x.holder === 'ai' && x.status === 'paused');
    if (paused.length) lines.push({ tone: 'gold', icon: 'bolt', text: `${paused.length} AI agent(s) paused: ${paused.map((x) => x.name).join(', ')}.`, href: '#/ceo/agents' });
    lines.push({ tone: 'info', icon: 'clock', text: `${acts.length} action(s) were logged yesterday across all systems.`, href: '#/ceo/audit' });
    const top = (r?.daily?.[0]) ? AD().STREAMS.map(([k, l]) => [l, yt[k] || 0]).sort((a, b) => b[1] - a[1])[0] : null;
    if (top && top[1] > 0) lines.push({ tone: 'turf', icon: 'star', text: `Best stream yesterday: ${top[0]} (${Math.round(top[1]).toLocaleString()} EGP).` });
    return { date: y, lines, revenue: yt, avg, kpis: K, deadlines: cal, health: H };
  };

  /* ---------- What-if simulator ---------- */
  /** Next 30 days if the CEO changes prices, based on the last 30 days. Demand changes are the CEO's guess */
  const whatIf = ({ bookingRate, storeRate, serviceFee, demand = 0, podMargin = 0, marketing = 0, agents = 0 }) => {
    const r = rev(range30())?.total || {};
    const s = db().settings();
    const curB = s.commission?.bookings ?? 0.1, curS = s.commission?.store ?? 0.12, curF = s.serviceFee ?? 0.05;
    const dem = 1 + (+demand || 0) / 100;
    const ratio = (n, c) => (c ? n / c : 1);
    const base = { bookings: r.bookings || 0, fees: r.fees || 0, store: r.store || 0, official: r.official || 0, pod: r.pod || 0, passes: r.passes || 0, cost: r.cost || 0 };
    const next = {
      bookings: base.bookings * ratio(+bookingRate / 100, curB) * dem, fees: base.fees * ratio(+serviceFee / 100, curF) * dem, store: base.store * ratio(+storeRate / 100, curS) * dem,
      official: base.official * dem, pod: base.pod * (1 + (+podMargin || 0) / 100) * dem, passes: base.passes * dem, cost: base.cost * dem,
    };
    const agentCost = roles().reduce((t, x) => t + (x.cost || 0), 0);
    const income = (o) => o.bookings + o.fees + o.store + o.official + o.pod + o.passes;
    const now = { income: income(base), net: income(base) - base.cost - agentCost };
    const extra = agentCost * (1 + (+agents || 0) / 100) + (+marketing || 0);
    const then = { income: income(next), net: income(next) - next.cost - extra };
    return { base, next, now, then, extra, change: Math.round(then.net - now.net), pct: now.net ? Math.round(((then.net - now.net) / Math.abs(now.net)) * 100) : 0, current: { bookingRate: +(curB * 100).toFixed(1), storeRate: +(curS * 100).toFixed(1), serviceFee: +(curF * 100).toFixed(1) }, agentCost };
  };
  const applyRates = ({ bookingRate, storeRate, serviceFee }) => {
    me();
    const b = +bookingRate / 100, s = +storeRate / 100, f = +serviceFee / 100;
    if (![b, s].every((x) => x >= 0 && x <= 0.5) || !(f >= 0 && f <= 0.15)) throw new ApiError('Commission 0–50%, service fee 0–15%.', 'invalid');
    const st = db().settings();
    db().setSettings({ commission: { ...(st.commission || {}), bookings: b, store: s }, serviceFee: f });
    log('rates', `Platform rates: bookings ${+(b * 100).toFixed(1)}%, store ${+(s * 100).toFixed(1)}%, service fee ${+(f * 100).toFixed(1)}%`);
    N()?.toAdmins({ cat: 'money', title: 'The CEO changed the platform rates', body: `Bookings ${+(b * 100).toFixed(1)}% · store ${+(s * 100).toFixed(1)}% · service fee ${+(f * 100).toFixed(1)}%`, link: '#/admin/settings' });
  };

  /* ---------- Open as (every system) ---------- */
  const SYSTEMS = { site: 'Super App (the site)', partner: 'Partner dashboard', seller: 'Seller dashboard', factory: 'Factory system', admin: 'Admin console' };
  const openAs = (kind, id = '') => {
    me();
    let url = '/', who = '';
    if (kind === 'partner') { const a = db().find('accounts', id); if (!a) throw new ApiError('Account not found.', 'not_found'); localStorage.setItem('superapp.dash.session', id); url = '/dash'; who = a.company || a.name; }
    else if (kind === 'seller') { const s = db().find('sellers', id); if (!s) throw new ApiError('Seller not found.', 'not_found'); localStorage.setItem('superapp.seller.session', s.accountId); url = '/seller'; who = s.name; }
    else if (kind === 'factory') { const x = db().find('factoryStaff', id); if (!x) throw new ApiError('Not found.', 'not_found'); localStorage.setItem('superapp.factory.session', x.id); url = '/factory'; who = x.name; }
    else if (kind === 'admin') { const a = db().find('accounts', id || CEO_ID); if (!a || a.role !== 'admin') throw new ApiError('Account not found.', 'not_found'); localStorage.setItem(ADMIN_SESSION, a.id); url = '/admin'; who = a.name; }
    log('open', `Opened the ${SYSTEMS[kind] || 'site'}${who ? ` as ${who}` : ''}`, id);
    window.open(SA.core.router.url(url), '_blank', 'noopener');
  };

  /* ---------- Reports ---------- */
  const REPORTS = [
    ['finance', 'Finance', 'Revenue, costs and net by day and stream', 'wallet'],
    ['operations', 'Operations', 'Bookings, store and printed orders', 'package'],
    ['customers', 'Customers', 'Everyone who booked or ordered', 'user'],
    ['partners', 'Pitch partners', 'Pitches, money and status', 'pitch'],
    ['sellers', 'Sellers', 'Designs, sales and levels', 'users'],
    ['marketing', 'Marketing', 'Promo codes, broadcasts and costs', 'megaphone'],
    ['support', 'Support', 'Tickets, refunds and response', 'message'],
    ['returns', 'Returns & exchanges', 'Every return, why, and the money', 'refresh'],
    ['agents', 'AI agents', 'Work, errors and cost of every agent', 'bolt'],
    ['approvals', 'Approvals', 'Everything that needed the CEO', 'check'],
  ];
  const inR = (t, from, to) => { const d = isoDate(new Date(t)); return d >= from && d <= to; };
  const report = (kind, { from, to }) => {
    me();
    const r = (cols, rows, extra = {}) => ({ columns: cols, rows, ...extra });
    if (kind === 'finance') {
      const R = AD().revenue({ from, to });
      const cols = [['date', 'Date', 'date'], ...AD().STREAMS.map(([k, l]) => [k, l, 'money']), ['income', 'Income', 'money'], ['refusals', 'Refused deliveries', 'money'], ['refunds', 'Refunds', 'money'], ['promos', 'Promo codes', 'money'], ['cost', 'Costs', 'money'], ['net', 'Net', 'money'], ['gmv', 'Money through Super App', 'money']];
      return r(cols, R.daily.map((d) => ({ ...d, date: d.date })), { kpis: [['Income', R.total.income, 'money'], ['Costs', R.total.cost, 'money'], ['Net', R.total.net, 'money'], ['Money through Super App', R.total.gmv, 'money']], sumCols: ['income', 'cost', 'net', 'gmv'], chart: 'net' });
    }
    if (kind === 'operations') {
      const s = D().summary({ from, to });
      const rows = [
        ...s.ledger.map((b) => ({ date: b.date, type: 'Booking', ref: b.id, who: b.customer?.name || '', where: db().find('pitches', b.pitchId)?.name || '', status: b.status, amount: b.money?.gross || b.amount || 0 })),
        ...s.sales.map((x) => ({ date: isoDate(new Date(x.createdAt)), type: 'Store order', ref: x.id, who: x.customer?.name || '', where: db().find('stores', x.storeId)?.name || '', status: x.status || 'paid', amount: x.money?.gross || 0 })),
        ...(db().all('podOrders') || []).filter((o) => !o.sample && inR(o.placedAt, from, to)).map((o) => ({ date: isoDate(new Date(o.placedAt)), type: 'Printed item', ref: o.id, who: o.customer?.name || '', where: o.stage || '', status: o.status, amount: o.price * (o.qty || 1) })),
      ].sort((a, b) => b.date.localeCompare(a.date));
      return r([['date', 'Date', 'date'], ['type', 'Type'], ['ref', 'Reference'], ['who', 'Customer'], ['where', 'Pitch, store or stage'], ['status', 'Status'], ['amount', 'Amount', 'money']], rows, { kpis: [['Lines', rows.length, 'n'], ['Total', rows.reduce((t, x) => t + x.amount, 0), 'money']], group: 'type' });
    }
    if (kind === 'customers') {
      const rows = AD().customers().map((c) => ({ name: c.name, city: c.city, phone: c.phone, bookings: c.bookings, orders: c.orders, items: c.items, spent: Math.round(c.spent), refused: c.refused, blocked: c.blocked ? 'Yes' : 'No', last: c.last ? isoDate(new Date(c.last)) : '' }));
      return r([['name', 'Name'], ['city', 'City'], ['phone', 'Phone'], ['bookings', 'Bookings', 'n'], ['orders', 'Orders', 'n'], ['items', 'Printed items', 'n'], ['spent', 'Spent', 'money'], ['refused', 'Refused', 'n'], ['blocked', 'Blocked'], ['last', 'Last seen', 'date']], rows, { kpis: [['Customers', rows.length, 'n'], ['Spent', rows.reduce((t, x) => t + x.spent, 0), 'money']], group: 'city' });
    }
    if (kind === 'partners') {
      const rows = AD().partners().map((p) => ({ company: p.company, name: p.name, pitches: p.pitches ?? db().all('pitches').filter((x) => x.ownerId === p.id && !x.deleted).length, balance: Math.round(p.balance || 0), status: p.status, phone: p.phone || '' }));
      return r([['company', 'Company'], ['name', 'Owner'], ['pitches', 'Pitches', 'n'], ['balance', 'Balance', 'money'], ['status', 'Status'], ['phone', 'Phone']], rows, { kpis: [['Partners', rows.length, 'n'], ['Owed to partners', rows.reduce((t, x) => t + x.balance, 0), 'money']], group: 'status' });
    }
    if (kind === 'sellers') {
      const rows = db().all('sellers').map((s) => { let e = {}; try { e = SA.services.pod.earnings(s.id); } catch { /* store-only */ } return { name: s.name, handle: s.handle, mode: s.mode, status: s.status, level: (() => { try { return SA.services.sellers.levelOf(s).name; } catch { return ''; } })(), designs: (db().all('designs') || []).filter((d) => d.sellerId === s.id && d.status === 'live').length, lifetime: Math.round(e.lifetime || 0), available: Math.round(e.available || 0), kyc: s.kyc || '' }; });
      return r([['name', 'Seller'], ['handle', 'Handle'], ['mode', 'Type'], ['status', 'Status'], ['level', 'Level'], ['designs', 'Live designs', 'n'], ['lifetime', 'Earned', 'money'], ['available', 'Available', 'money'], ['kyc', 'ID check']], rows, { kpis: [['Sellers', rows.length, 'n'], ['Earned', rows.reduce((t, x) => t + x.lifetime, 0), 'money']], group: 'level' });
    }
    if (kind === 'marketing') {
      const promos = AD().promos().map((c) => ({ kind: 'Promo code', name: c.code, detail: c.type === 'percent' ? `${c.value}%` : `${c.value} EGP`, uses: c.uses || 0, date: isoDate(new Date(c.createdAt)), cost: Math.round((db().all('promoUses') || []).filter((u) => u.code === c.code && inR(u.at, from, to)).reduce((t, u) => t + u.amount, 0)) }));
      const bcs = (db().all('broadcasts') || []).filter((b) => inR(b.createdAt, from, to)).map((b) => ({ kind: 'Broadcast', name: b.title, detail: b.audience, uses: b.reach || 0, date: isoDate(new Date(b.createdAt)), cost: 0 }));
      const ex = db().all('ceoExpenses').filter((x) => x.dept === 'marketing' && inR(x.at, from, to)).map((x) => ({ kind: 'Expense', name: x.text, detail: '', uses: 0, date: isoDate(new Date(x.at)), cost: x.amount }));
      const rows = [...promos, ...bcs, ...ex];
      return r([['kind', 'Type'], ['name', 'Name'], ['detail', 'Detail'], ['uses', 'Uses or reach', 'n'], ['date', 'Date', 'date'], ['cost', 'Cost', 'money']], rows, { kpis: [['Lines', rows.length, 'n'], ['Cost', rows.reduce((t, x) => t + x.cost, 0), 'money']], group: 'kind' });
    }
    if (kind === 'support') {
      const rows = (db().all('tickets') || []).filter((t) => inR(t.createdAt || t.at || Date.now(), from, to)).map((t) => ({ id: t.id, date: isoDate(new Date(t.createdAt || Date.now())), from: t.from?.name || '', kind: t.from?.kind || '', topic: t.topic || t.category || '', subject: t.subject, status: t.status, refund: t.refund?.amount || 0, replies: (t.messages || []).length }));
      return r([['id', 'Ticket'], ['date', 'Date', 'date'], ['from', 'From'], ['kind', 'Type'], ['topic', 'Topic'], ['subject', 'Subject'], ['status', 'Status'], ['refund', 'Refund', 'money'], ['replies', 'Messages', 'n']], rows, { kpis: [['Tickets', rows.length, 'n'], ['Refunded', rows.reduce((t, x) => t + x.refund, 0), 'money']], group: 'status' });
    }
    if (kind === 'returns') {
      const rows = (db().all('returns') || []).filter((x) => inR(x.createdAt, from, to)).map((x) => ({ id: x.id, date: isoDate(new Date(x.createdAt)), order: x.orderId, seller: x.storeName, customer: x.customer?.name || '', items: x.items.map((i) => `${i.qty}× ${i.name}`).join(', '), action: [...new Set(x.items.map((i) => i.action))].join(' + '), reason: x.reasonLabel, status: x.status, amount: x.amount || 0 }));
      return r([['id', 'Return'], ['date', 'Date', 'date'], ['order', 'Order'], ['seller', 'Seller'], ['customer', 'Customer'], ['items', 'Items'], ['action', 'Asked for'], ['reason', 'Reason'], ['status', 'Status'], ['amount', 'Refunded', 'money']], rows, { kpis: [['Returns', rows.length, 'n'], ['Refunded', rows.reduce((t, x) => t + x.amount, 0), 'money']], group: 'reason' });
    }
    if (kind === 'agents') {
      const rows = roles().filter((x) => x.id !== 'ceo').map((x) => { const s = agentStats(x.id); return { name: x.name, title: x.title, dept: x.deptInfo?.name || '', holder: x.holder === 'ai' ? 'AI agent' : 'Human', status: x.status, tasks: s.tasks, errors: s.errors, accuracy: s.accuracy ?? '', limit: x.limit === Infinity ? '' : x.limit, cost: s.cost }; });
      return r([['name', 'Name'], ['title', 'Role'], ['dept', 'Department'], ['holder', 'Held by'], ['status', 'Status'], ['tasks', 'Tasks (30 days, demo)', 'n'], ['errors', 'Errors (demo)', 'n'], ['accuracy', 'Accuracy % (demo)'], ['limit', 'Money limit', 'money'], ['cost', 'Cost / month', 'money']], rows, { kpis: [['Roles', rows.length, 'n'], ['Cost / month', rows.reduce((t, x) => t + x.cost, 0), 'money']], group: 'dept' });
    }
    if (kind === 'approvals') {
      const rows = approvals().filter((x) => inR(x.at, from, to)).map((x) => ({ id: x.id, date: isoDate(new Date(x.at)), kind: KIND[x.kind], title: x.title, amount: x.amount, by: x.requestedBy?.name || '', status: x.status, approvers: (x.approvals || []).map((p) => p.name).join(' + '), reason: x.reason || (x.why || []).join('; ') }));
      return r([['id', 'Request'], ['date', 'Date', 'date'], ['kind', 'Type'], ['title', 'What'], ['amount', 'Amount', 'money'], ['by', 'Asked by'], ['status', 'Status'], ['approvers', 'Approved by'], ['reason', 'Why']], rows, { kpis: [['Requests', rows.length, 'n'], ['Amount', rows.reduce((t, x) => t + x.amount, 0), 'money']], group: 'status' });
    }
    throw new ApiError('Unknown report.', 'not_found');
  };

  const ceo = {
    SESSION, CEO_ID, CEO_PIN, SWITCHES, SYSTEMS, REPORTS, METRICS, KIND, LIMITS,
    auth, me, ceoMode, log, cfg, limits, setLimits,
    roles, getRole, roleOfAccount, canSee, may, setAgent, stopAll, resumeAll, blockedAccount, activity, agentStats, agentAcc,
    gate, approvals, approve, reject, financeState, financeQueue, financeApprove, financeReject, isQueued: (msg) => !!queuedMsg && msg === queuedMsg,
    switches, isOn, assertOn, setSwitch,
    kpis, health, metric, goals, saveGoal, removeGoal, budgets, setBudget, addExpense, expenses: () => db().all('ceoExpenses').slice().sort((a, b) => b.at - a.at),
    calendar, doneItem, addItem, briefing, whatIf, applyRates, openAs, report,
  };
  SA.services.ceo = ceo;
  migrate();
})(window.SA);
