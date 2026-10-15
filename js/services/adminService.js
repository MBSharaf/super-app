/**
 * Admin console service — the "backend" for the Super App team (#/admin).
 * Every method requires an admin session (separate from the partner dashboard session) and every change is written
 * to the audit log with the admin as the actor.
 *
 * Future API: /admin/* (payouts, revenue, customers, partners, sellers, orders, tickets, broadcasts, content, fantasy, analytics)
 */
(function (SA) {
  const { ApiError } = SA.services.http;
  const { isoDate, parseISO, addDays, today, seeded } = SA.core.utils;
  const db = () => SA.services.db;
  const D = () => SA.services.dash;
  const DAY = 864e5;

  const me = () => { const a = D().me(); if (!a || a.role !== 'admin') throw new ApiError('Sign in to the admin console first.', 'forbidden'); return a; };
  const log = (action, text, ref) => db().log(me().id, `admin.${action}`, text, ref);
  const sumBy = (arr, f) => arr.reduce((t, x) => t + f(x), 0);
  const inRange = (t, from, to) => { const d = isoDate(new Date(t)); return d >= from && d <= to; };
  const days = (from, to) => { const out = []; for (let d = parseISO(from); d <= parseISO(to); d = addDays(d, 1)) out.push(isoDate(d)); return out; };

  /* ======================= Fantasy Pass sales ======================= */
  // Passes sold before this device existed are modelled deterministically; new purchases are recorded in db.passSales
  const passSynth = new Map();
  const passesOn = (iso) => {
    if (passSynth.has(iso)) return passSynth.get(iso);
    const out = [];
    const since = db().raw().platformSince;
    if (parseISO(iso).getTime() >= since && iso < isoDate(today())) {
      const p = db().settings().fantasySubs || { one: 50, both: 80 };
      const wd = parseISO(iso).getDay();
      const n = 6 + Math.floor(seeded(`pass|${iso}`) * 12) + (wd === 4 || wd === 5 ? 6 : 0); // more before the weekend round
      for (let k = 0; k < n; k++) {
        const r = seeded(`pass|${iso}|${k}`);
        const plan = r < 0.42 ? 'both' : r < 0.8 ? 'egy' : 'epl';
        out.push({ id: `FP-${iso}-${k}`, plan, amount: plan === 'both' ? p.both : p.one, at: parseISO(iso).getTime() + (10 + Math.floor(r * 13)) * 36e5, customer: SA.data.matches.people[Math.floor(seeded(`pass|c|${iso}|${k}`) * SA.data.matches.people.length)], synthetic: true });
      }
    }
    passSynth.set(iso, out);
    return out;
  };
  const passSales = (from, to) => [...days(from, to).flatMap(passesOn), ...(db().all('passSales') || []).filter((x) => inRange(x.at, from, to))].sort((a, b) => b.at - a.at);

  /* ======================= Revenue ======================= */
  const STREAMS = [
    ['bookings', 'Booking commission', 'turf'], ['fees', 'Booking service fees', 'info'], ['store', 'Store commission', 'blaze'],
    ['official', 'Official store sales', 'ink'], ['pod', 'Print-on-demand margin', 'gold'], ['passes', 'Fantasy Pass', 'violet'],
  ];
  const COSTS = [['refusals', 'Refused print deliveries'], ['refunds', 'Refunds paid by Super App'], ['promos', 'Super App promo codes']];
  /**
   * Platform revenue between two ISO dates (inclusive), by stream and by day.
   * Accrual view: bookings by match date, store and print orders by order date (refused print items count as a cost).
   */
  const revenue = ({ from, to }) => {
    me();
    const s = D().summary({ from, to });
    const row = () => ({ bookings: 0, fees: 0, store: 0, official: 0, pod: 0, passes: 0, refusals: 0, refunds: 0, promos: 0, gmv: 0 });
    const daily = new Map(days(from, to).map((d) => [d, { date: d, ...row() }]));
    const at = (d) => daily.get(d) || { ...row() };
    s.ledger.forEach((b) => {
      const r = at(b.date);
      r.bookings += b.money.commission; r.gmv += b.money.gross;
      if (b.source === 'app' && b.status !== 'cancelled') r.fees += b.fee || 0;
    });
    (s.tourLines || []).forEach((x) => { const r = at(x.date); r.bookings += x.commission; r.gmv += x.gross; });
    s.sales.forEach((x) => {
      const r = at(isoDate(new Date(x.createdAt)));
      if (db().find('stores', x.storeId)?.official) r.official += x.money.gross; else r.store += x.money.commission;
      r.gmv += x.money.gross;
    });
    (db().all('podOrders') || []).forEach((o) => {
      if (!inRange(o.placedAt, from, to) || o.sample) return;
      const r = at(isoDate(new Date(o.placedAt)));
      if (o.status === 'refused' || o.status === 'cancelled') { r.refusals += o.loss || 0; return; }
      r.pod += (o.base - o.partnerCost) * (o.qty || 1); r.gmv += o.price * (o.qty || 1);
    });
    passSales(from, to).forEach((x) => { const r = at(isoDate(new Date(x.at))); r.passes += x.amount; r.gmv += x.amount; });
    (db().all('refunds') || []).forEach((x) => { if (x.bearer === 'platform' && inRange(x.at, from, to)) at(isoDate(new Date(x.at))).refunds += x.amount; });
    (db().all('promoUses') || []).forEach((x) => { if (inRange(x.at, from, to)) at(isoDate(new Date(x.at))).promos += x.amount; });
    const rows = [...daily.values()].map((r) => ({ ...r, income: r.bookings + r.fees + r.store + r.official + r.pod + r.passes, cost: r.refusals + r.refunds + r.promos }));
    rows.forEach((r) => (r.net = r.income - r.cost));
    const tot = rows.reduce((t, r) => { Object.keys(t).forEach((k) => (t[k] += r[k] || 0)); return t; }, { ...row(), income: 0, cost: 0, net: 0 });
    Object.keys(tot).forEach((k) => (tot[k] = Math.round(tot[k])));
    return { from, to, daily: rows, total: tot, summary: s, passes: passSales(from, to), STREAMS, COSTS };
  };

  /* ======================= To-do for the bell, sidebar badges and overview ======================= */
  const alerts = () => {
    const out = [];
    const add = (key, icon, tone, count, title, text, href) => count && out.push({ key, icon, tone, count, title, text, href });
    const pay = admin.pendingPayouts ? admin.pendingPayouts() : [];
    add('payouts', 'wallet', 'blaze', pay.length, `${pay.length} withdrawal${pay.length > 1 ? 's' : ''} to approve`, `${Math.round(sumBy(pay, (p) => p.amount)).toLocaleString()} EGP waiting to be sent.`, '#/admin/payouts');
    if (SA.services.pod) {
      const q = SA.services.pod.queue().length, rep = db().all('reports').filter((r) => r.status === 'open').length;
      add('designs', 'image', 'gold', q + rep, `${q + rep} design${q + rep > 1 ? 's' : ''} to review`, `${q} new or changed · ${rep} copyright report${rep === 1 ? '' : 's'}.`, '#/admin/designs');
    }
    const kyc = db().all('sellers').filter((s) => s.kyc === 'pending').length;
    add('kyc', 'shield', 'info', kyc, `${kyc} seller ID check${kyc > 1 ? 's' : ''}`, 'Verify before their first withdrawal.', '#/admin/designs?tab=sellers');
    const rv = db().all('reviews').filter((r) => r.reported && !r.reported.resolved && !r.hidden).length;
    add('reviews', 'star', 'danger', rv, `${rv} reported review${rv > 1 ? 's' : ''}`, 'Partners asked Super App to check these.', '#/admin/reviews?filter=reported');
    const tk = (db().all('tickets') || []).filter((t) => t.status === 'open').length;
    add('support', 'message', 'blaze', tk, `${tk} support ticket${tk > 1 ? 's' : ''} waiting`, 'Customers, partners and sellers waiting for a reply.', '#/admin/support');
    const sm = (db().all('samples') || []).filter((x) => x.status === 'requested').length;
    add('samples', 'shirt', 'info', sm, `${sm} sample request${sm > 1 ? 's' : ''}`, 'Approve to send them to the print partner.', '#/admin/samples');
    const ap = admin.pendingPitches ? admin.pendingPitches().filter((p) => p.review === 'pending').length + admin.pendingSellers().filter((s) => s.status === 'pending').length : 0;
    add('approvals', 'check', 'blaze', ap, `${ap} new pitch${ap > 1 ? 'es and sellers' : ' or seller'} to approve`, 'They go live on Super App once you approve them.', '#/admin/approvals');
    try { const low = D().lowStock(); add('stock', 'alert', 'gold', low.length, `${low.length} Super App Store product${low.length > 1 ? 's' : ''} low on stock`, low.slice(0, 3).map((p) => `${p.name} (${p.stock})`).join(', '), '#/admin/store?low=1'); } catch { /* no official store */ }
    if (admin.levelReviewDue && admin.levelReviewDue()) add('levels', 'trend', 'gold', 1, 'Monthly level review is due', 'Check seller levels for this month.', '#/admin/levels');
    return out;
  };

  /* ======================= Audit log ======================= */
  const actorKind = (x) => {
    if (x.actor === 'customer') return 'customer';
    if (!x.actor || x.actor === 'system') return 'system';
    const a = db().find('accounts', x.actor);
    return a?.role === 'admin' ? 'admin' : a?.role === 'seller' ? 'seller' : a?.role === 'printer' ? 'printer' : a?.role === 'owner' ? 'partner' : 'system';
  };
  const audit = ({ who = '', q = '', from = '', to = '' } = {}) => {
    me();
    const s = q.trim().toLowerCase();
    return db().all('activity').filter((x) => (!who || actorKind(x) === who) && (!from || inRange(x.at, from, to || isoDate(today()))) && (!s || `${x.text} ${x.action} ${x.ref || ''}`.toLowerCase().includes(s)))
      .map((x) => ({ ...x, kind: actorKind(x), actorName: x.actor === 'customer' ? 'Customer' : db().find('accounts', x.actor)?.name || 'System' }));
  };

  /* ======================= Withdrawals ======================= */
  const N = () => SA.services.notify;
  /** Partner withdrawals (payouts) and seller withdrawals (sellerPayouts) in one shape */
  const allPayouts = () => {
    const acc = (id) => db().find('accounts', id);
    const partner = db().all('payouts').map((p) => ({ ...p, kind: acc(p.accountId)?.role === 'printer' ? 'printer' : 'partner', account: acc(p.accountId) }));
    const seller = (db().all('sellerPayouts') || []).map((p) => { const s = db().find('sellers', p.sellerId); return { ...p, kind: 'seller', seller: s, accountId: s?.accountId, account: acc(s?.accountId) }; });
    return [...partner, ...seller].map((p) => ({ ...p, name: p.account?.name || 'Unknown', company: p.seller?.name || p.account?.company || '' })).sort((a, b) => b.createdAt - a.createdAt);
  };
  /** Things to check before sending money */
  const checks = (p) => {
    const out = [];
    if (p.account?.status === 'suspended') out.push(['danger', 'Account suspended']);
    if (p.kind === 'seller') {
      if (p.seller?.kyc !== 'verified') out.push(['danger', 'ID not verified']);
      if (p.seller?.status === 'suspended') out.push(['danger', 'Seller suspended']);
      if ((p.seller?.strikes || []).length) out.push(['gold', `${p.seller.strikes.length} copyright strike${p.seller.strikes.length > 1 ? 's' : ''}`]);
    }
    const open = (db().all('tickets') || []).filter((t) => t.status !== 'resolved' && t.against === p.accountId).length;
    if (open) out.push(['gold', `${open} open dispute${open > 1 ? 's' : ''}`]);
    if (!p.detail || p.detail === '—') out.push(['danger', 'No payout account']);
    return out;
  };
  const setPayout = (p, patch) => (p.kind === 'seller' ? db().update('sellerPayouts', p.id, patch) : db().update('payouts', p.id, patch));
  const findPayout = (id) => { const p = allPayouts().find((x) => x.id === id); if (!p) throw new ApiError('Withdrawal not found.', 'not_found'); return p; };

  /** What Super App owes the print partner: partner cost of every item it printed, minus settlements already paid */
  const printerAccount = () => db().all('accounts').find((a) => a.role === 'printer');
  const printerOwed = () => {
    const pr = printerAccount();
    // Demo history: older seeded items count as already settled; the last two weeks are still owed
    const done = (db().all('podOrders') || []).filter((o) => ['shipped', 'delivered', 'refused'].includes(o.status) && (!o.seed || o.placedAt > Date.now() - 14 * DAY));
    const earned = sumBy(done, (o) => o.partnerCost * (o.qty || 1));
    const paid = sumBy(db().all('payouts').filter((p) => p.accountId === pr?.id && p.status !== 'rejected'), (p) => p.amount);
    return { account: pr, items: done.length, earned: Math.round(earned), paid: Math.round(paid), owed: Math.max(0, Math.round(earned - paid)), orders: done };
  };

  /* ======================= Customers ======================= */
  // Customer records (refusals, block, pay-upfront mode, notes, credits) live in db.podCustomers, keyed by customer id
  const recs = () => db().raw().podCustomers;
  const rec = (id) => recs()[id] || {};
  const saveRec = (id, patch) => { recs()[id] = { ...rec(id), ...patch }; db().touch('podCustomers'); return recs()[id]; };
  const deviceUser = () => SA.core.store.get().user;
  /** Everyone who booked or ordered: this device's shopper, app bookings and print-on-demand customers */
  const customers = () => {
    const map = new Map();
    const get = (id, base) => { if (!map.has(id)) map.set(id, { id, name: base.name || 'Customer', city: base.city || '', phone: base.phone || '', bookings: 0, orders: 0, items: 0, spent: 0, last: 0, first: Infinity }); const c = map.get(id); ['city', 'phone'].forEach((k) => { if (!c[k] && base[k]) c[k] = base[k]; }); return c; };
    const touch = (c, t) => { c.last = Math.max(c.last, t || 0); c.first = Math.min(c.first, t || Date.now()); };
    const u = deviceUser();
    if (u) {
      const c = get(u.id, { name: u.name, city: u.city || 'Cairo', phone: u.phone });
      const st = SA.core.store.get();
      c.device = true; c.email = u.email; c.avatar = u.avatar;
      (st.orders || []).forEach((o) => { c.orders++; c.spent += o.total; touch(c, o.createdAt); });
      c.joined = u.memberSince || null;
    }
    db().all('bookings').forEach((b) => { if (!b.customer?.id || b.source !== 'app') return; const c = get(b.customer.id, b.customer); c.bookings++; if (b.status !== 'cancelled') c.spent += (b.amount || 0) + (b.fee || 0); touch(c, b.createdAt); });
    (db().all('podOrders') || []).forEach((o) => { if (!o.customer?.id || o.sample) return; const c = get(o.customer.id, o.customer); if (!c.device) { c.items += o.qty || 1; c.spent += o.price * (o.qty || 1); } else c.items += o.qty || 1; touch(c, o.placedAt); });
    return [...map.values()].map((c) => { const r = rec(c.id); const limit = SA.services.pod?.cfg().refuseLimit ?? 2; return { ...c, first: c.first === Infinity ? 0 : c.first, refused: r.refused || 0, blocked: !!r.blocked, blockedReason: r.blockedReason || '', prepay: r.prepay || 'auto', needsPrepay: SA.services.pod ? SA.services.pod.needsPrepay(c.id) : false, notes: r.notes || [], credits: r.credits || [], limit }; })
      .sort((a, b) => b.last - a.last);
  };
  const customer = (id) => {
    const c = customers().find((x) => x.id === id);
    if (!c) throw new ApiError('Customer not found.', 'not_found');
    const bookings = db().all('bookings').filter((b) => b.customer?.id === id).map((b) => D().findBooking(b.id) || b).sort((a, b) => b.createdAt - a.createdAt);
    const pod = (db().all('podOrders') || []).filter((o) => o.customer?.id === id && !o.sample).sort((a, b) => b.placedAt - a.placedAt);
    const orders = c.device ? (SA.core.store.get().orders || []) : [];
    const tickets = (db().all('tickets') || []).filter((t) => t.from?.id === id);
    return { ...c, bookingList: bookings, podList: pod, orderList: orders, tickets };
  };

  /* ======================= Partners & sellers ======================= */
  const partners = () => {
    const base = D().partners();
    return base.map((p) => ({ ...p, status: db().find('accounts', p.id)?.status || 'active', suspendedReason: db().find('accounts', p.id)?.suspendedReason || '', balance: (() => { try { return D().wallet({ ownerId: p.id }).balance; } catch { return 0; } })() }));
  };

  const admin = {
    me, log, alerts, revenue, passSales, audit, actorKind, STREAMS, COSTS,
    customers, customer, partners,
    /** Blocked customers can't book, order, join games or buy a Fantasy Pass */
    isBlocked: (id) => !!rec(id).blocked,
    blockCustomer(id, reason) {
      me(); const c = customer(id);
      if (!reason || reason.trim().length < 3) throw new ApiError('Write the reason.', 'invalid');
      saveRec(id, { blocked: true, blockedReason: reason.trim(), blockedAt: Date.now() });
      log('customer.blocked', `Blocked customer ${c.name}: ${reason.trim()}`, id);
      if (c.device) SA.services.user.notify({ kind: 'shop', title: 'Your account is on hold', body: 'You can’t book or order for now. Contact Super App support if you think this is a mistake.', link: '/profile' });
    },
    unblockCustomer(id) {
      me(); const c = customer(id);
      saveRec(id, { blocked: false, blockedReason: '', blockedAt: 0 });
      log('customer.unblocked', `Unblocked customer ${c.name}`, id);
      if (c.device) SA.services.user.notify({ kind: 'shop', title: 'Your account is active again', body: 'You can book pitches and order from the shop again.', link: '/' });
    },
    /** Pay-upfront for printed items: auto (refusal limit), always, never */
    setPrepay(id, mode) {
      me(); const c = customer(id);
      if (!['auto', 'always', 'never'].includes(mode)) throw new ApiError('Unknown mode.', 'invalid');
      saveRec(id, { prepay: mode });
      log('customer.prepay', `Pay upfront for printed items for ${c.name}: ${{ auto: 'automatic', always: 'always required', never: 'never required' }[mode]}`, id);
    },
    resetRefusals(id) { me(); const c = customer(id); saveRec(id, { refused: 0 }); log('customer.refusals-reset', `Reset ${c.name}’s refused deliveries (was ${c.refused})`, id); },
    noteCustomer(id, text) {
      me(); const c = customer(id);
      const t = String(text || '').trim();
      if (t.length < 2) throw new ApiError('Write a note first.', 'invalid');
      saveRec(id, { notes: [{ text: t.slice(0, 400), at: Date.now(), by: me().name }, ...(rec(id).notes || [])] });
      log('customer.note', `Note on ${c.name}: ${t.slice(0, 80)}`, id);
    },
    /** Goodwill credit to the customer's Super App wallet (paid by Super App) */
    async creditCustomer(id, amount, reason) {
      me(); const c = customer(id);
      const amt = Math.round(+amount);
      if (!(amt >= 5 && amt <= 5000)) throw new ApiError('Credit between 5 and 5,000 EGP.', 'invalid');
      if (!reason || reason.trim().length < 3) throw new ApiError('Write the reason.', 'invalid');
      if (c.device) await SA.services.payment.refund(amt, `Credit from Super App — ${reason.trim()}`);
      saveRec(id, { credits: [{ amount: amt, reason: reason.trim(), at: Date.now() }, ...(rec(id).credits || [])] });
      db().insert('refunds', { customerId: id, amount: amt, bearer: 'platform', kind: 'credit', reason: reason.trim(), at: Date.now() }, 'rf');
      log('customer.credit', `Credited ${amt.toLocaleString()} EGP to ${c.name}’s wallet: ${reason.trim()}`, id);
      if (c.device) SA.services.user.notify({ kind: 'shop', title: `${amt.toLocaleString()} EGP added to your wallet`, body: reason.trim(), link: '/wallet' });
    },

    /** Add or remove Super Points (the shopper on this device; others are recorded for the backend) */
    adjustPoints(id, points, reason) {
      me(); const c = customer(id);
      const p = Math.round(+points);
      if (!p || Math.abs(p) > 100000) throw new ApiError('Enter the points (between −100,000 and 100,000, not 0).', 'invalid');
      if (!reason || reason.trim().length < 3) throw new ApiError('Write the reason.', 'invalid');
      if (c.device) SA.services.loyalty.adjust(p, reason.trim());
      saveRec(id, { points: [{ points: p, reason: reason.trim(), at: Date.now() }, ...(rec(id).points || [])] });
      log('customer.points', `${p > 0 ? 'Added' : 'Removed'} ${Math.abs(p).toLocaleString()} Super Points ${p > 0 ? 'to' : 'from'} ${c.name}: ${reason.trim()}`, id);
      if (c.device) SA.services.user.notify({ kind: 'shop', title: `${p > 0 ? '+' : '−'}${Math.abs(p).toLocaleString()} Super Points`, body: reason.trim(), link: '/wallet' });
    },
    /** Suspend or reactivate a pitch owner: no sign-in, pitches and store hidden from the site */
    setPartnerStatus(accountId, status, reason = '') {
      me();
      const a = db().find('accounts', accountId);
      if (!a || a.role !== 'owner') throw new ApiError('Partner not found.', 'not_found');
      if (!['active', 'suspended'].includes(status)) throw new ApiError('Unknown status.', 'invalid');
      if (status === 'suspended' && reason.trim().length < 3) throw new ApiError('Write the reason.', 'invalid');
      db().update('accounts', accountId, { status, suspendedReason: status === 'suspended' ? reason.trim() : '', suspendedAt: status === 'suspended' ? Date.now() : 0 });
      db().all('stores').filter((s) => s.ownerId === accountId && !s.official).forEach((s) => db().update('stores', s.id, { status: status === 'active' ? 'active' : 'suspended' }));
      log(`partner.${status}`, `${status === 'active' ? 'Reactivated' : 'Suspended'} ${a.company} (${a.name})${reason ? `: ${reason.trim()}` : ''}`, accountId);
      N()?.toAccount(accountId, status === 'active'
        ? { cat: 'account', tone: 'turf', title: 'Your account is active again', body: 'Your pitches and store are back on Super App.', link: '#/dash' }
        : { cat: 'account', tone: 'danger', title: 'Your account is suspended', body: `${reason.trim()} Your pitches and store are hidden until this is resolved.`, link: '#/dash' });
    },
    /** Commission deal for one partner (percent values; empty = platform rate) */
    setCommissionDeal(accountId, { bookings, store, note = '' }) {
      me();
      const a = db().find('accounts', accountId); if (!a) throw new ApiError('Partner not found.', 'not_found');
      const val = (v) => (v === '' || v == null ? null : +v / 100);
      const b = val(bookings), s = val(store);
      if ([b, s].some((x) => x != null && !(x >= 0 && x <= 0.5))) throw new ApiError('Commission must be between 0% and 50%.', 'invalid');
      const deal = b == null && s == null ? null : { bookings: b, store: s, note: note.trim(), since: Date.now() };
      db().update('accounts', accountId, { commission: deal });
      const g = db().settings().commission;
      log('partner.commission', deal ? `Commission deal for ${a.company}: bookings ${b == null ? `${g.bookings * 100}% (platform)` : `${+(b * 100).toFixed(1)}%`}, store ${s == null ? 'platform rate' : `${+(s * 100).toFixed(1)}%`}${deal.note ? ` — ${deal.note}` : ''}` : `Removed ${a.company}’s commission deal (platform rates apply)`, accountId);
      N()?.toAccount(accountId, { cat: 'money', title: deal ? 'Your commission was updated' : 'Your commission is back to the standard rate', body: deal ? `Bookings ${b == null ? `${+(g.bookings * 100).toFixed(1)}%` : `${+(b * 100).toFixed(1)}%`} · store ${s == null ? `${+(g.store * 100).toFixed(1)}%` : `${+(s * 100).toFixed(1)}%`} — from now on.` : `Bookings ${+(g.bookings * 100).toFixed(1)}% · store ${+(g.store * 100).toFixed(1)}%.`, link: '#/dash/finance' });
    },
    setFees({ standard, express, freeOver, serviceFee }) {
      me();
      const s = { standard: Math.round(+standard), express: Math.round(+express), freeOver: Math.round(+freeOver) };
      const fee = +serviceFee / 100;
      if (!(s.standard >= 0 && s.express >= s.standard && s.freeOver >= 0)) throw new ApiError('Express can’t cost less than standard delivery, and prices can’t be negative.', 'invalid');
      if (!(fee >= 0 && fee <= 0.15)) throw new ApiError('Service fee must be between 0% and 15%.', 'invalid');
      db().setSettings({ shipping: s, serviceFee: fee });
      log('settings.fees', `Delivery ${s.standard}/${s.express} EGP, free from ${s.freeOver || 'never'}; booking service fee ${+(fee * 100).toFixed(1)}%`);
    },
    setMaintenance({ bookings, shop, message }) {
      me();
      const prev = db().settings().maintenance || {};
      db().setSettings({ maintenance: { bookings: !!bookings, shop: !!shop, message: String(message || '').trim().slice(0, 140), v: (prev.v || 0) + 1, at: Date.now() } });
      log('settings.maintenance', bookings || shop ? `Maintenance on: ${[bookings && 'bookings', shop && 'shop checkout'].filter(Boolean).join(' and ')} paused` : 'Maintenance off');
      SA.ui.shell?.render();
    },
    /** Take one product off the shop (or put it back) — the store keeps everything else */
    setProductHidden(productId, hidden, reason = '') {
      me();
      const p = db().find('products', productId); if (!p) throw new ApiError('Product not found.', 'not_found');
      if (hidden && reason.trim().length < 3) throw new ApiError('Write the reason.', 'invalid');
      db().update('products', productId, { hiddenByAdmin: !!hidden, hiddenReason: hidden ? reason.trim() : '' });
      if (hidden) SA.core.store.set((st) => ({ cart: st.cart.filter((l) => l.productId !== productId) }), 'cart');
      const st = db().find('stores', p.storeId);
      log(hidden ? 'product.hidden' : 'product.restored', `${hidden ? 'Hid' : 'Restored'} “${p.name}” (${st?.name || ''})${hidden ? `: ${reason.trim()}` : ''}`, productId);
      if (st && !st.official) N()?.toAccount(st.ownerId, hidden
        ? { cat: 'store', tone: 'danger', title: `Hidden by Super App: ${p.name}`, body: `${reason.trim()} Fix the listing, then contact Help & support to bring it back.`, link: '#/dash/store' }
        : { cat: 'store', tone: 'turf', title: `Back in the shop: ${p.name}`, body: 'Super App restored this product.', link: '#/dash/store' });
    },
    /** Close or reopen a single pitch from the admin side */
    setPitchStatus(pitchId, status, reason = '') {
      me();
      const p = db().find('pitches', pitchId); if (!p) throw new ApiError('Pitch not found.', 'not_found');
      db().update('pitches', pitchId, { status, closedReason: status === 'closed' ? (reason.trim() || 'Closed by Super App') : '' });
      log(`pitch.${status}`, `${status === 'active' ? 'Reopened' : 'Closed'} ${p.name}${reason ? `: ${reason.trim()}` : ''}`, pitchId);
      N()?.toAccount(p.ownerId, { cat: 'account', tone: status === 'active' ? 'turf' : 'danger', title: `${p.name} ${status === 'active' ? 'is open again' : 'was closed by Super App'}`, body: status === 'active' ? 'Players can book it again.' : reason.trim(), link: '#/dash/pitches' });
    },
    setSellerStatus(sellerId, status, reason = '') {
      me();
      const s = db().find('sellers', sellerId); if (!s) throw new ApiError('Seller not found.', 'not_found');
      if (status === 'suspended' && reason.trim().length < 3) throw new ApiError('Write the reason.', 'invalid');
      SA.services.sellers.setStatus(sellerId, status);
      db().update('sellers', sellerId, { suspendedReason: status === 'suspended' ? reason.trim() : '' });
      log(`seller.${status}`, `${status === 'active' ? 'Reactivated' : 'Suspended'} seller ${s.name}${reason ? `: ${reason.trim()}` : ''}`, sellerId);
    },
    removeStrike(sellerId, index, reason) {
      me();
      const s = db().find('sellers', sellerId); if (!s) throw new ApiError('Seller not found.', 'not_found');
      const st = (s.strikes || []).slice(); const [gone] = st.splice(index, 1);
      if (!gone) throw new ApiError('Strike not found.', 'not_found');
      if (!reason || reason.trim().length < 3) throw new ApiError('Write the reason.', 'invalid');
      const active = st.filter((x) => Date.now() - x.at < 90 * DAY);
      db().update('sellers', sellerId, { strikes: st, frozenUntil: active.length ? Math.max(...active.map((x) => x.at)) + 90 * DAY : 0 });
      log('seller.strike-removed', `Removed a copyright strike from ${s.name}: ${reason.trim()}`, sellerId);
      N()?.toSeller(sellerId, { cat: 'account', tone: 'turf', title: 'A copyright strike was removed', body: reason.trim(), link: '#/dash/seller' });
    },
    verifyKyc(sellerId, ok) { me(); const s = SA.services.sellers.verifyKyc(sellerId, ok); log(`seller.kyc-${ok ? 'verified' : 'rejected'}`, `${ok ? 'Verified' : 'Rejected'} ${s.name}’s ID`, sellerId); return s; },
    /* ---------- Support & disputes ---------- */
    replyTicket(id, text, { resolve = false } = {}) {
      me();
      const tk = db().find('tickets', id); if (!tk) throw new ApiError('Ticket not found.', 'not_found');
      const t = String(text || '').trim();
      if (t.length < 2) throw new ApiError('Write a reply first.', 'invalid');
      db().update('tickets', id, { messages: [...tk.messages, { by: 'admin', name: me().name, text: t.slice(0, 2000), at: Date.now() }], status: resolve ? 'resolved' : 'pending', resolvedAt: resolve ? Date.now() : tk.resolvedAt || 0 });
      log('support.replied', `Replied to ${tk.id}${resolve ? ' and resolved it' : ''}: ${t.slice(0, 80)}`, id);
      admin._tellTicket(tk, 'Super App replied', t.slice(0, 140));
    },
    setTicket(id, patch) {
      me();
      const tk = db().find('tickets', id); if (!tk) throw new ApiError('Ticket not found.', 'not_found');
      db().update('tickets', id, { ...patch, ...(patch.status === 'resolved' ? { resolvedAt: Date.now() } : {}) });
      log('support.updated', `${tk.id}: ${Object.entries(patch).map(([k, v]) => `${k} → ${v}`).join(', ')}`, id);
      if (patch.status === 'resolved') admin._tellTicket(tk, 'Your request was resolved', tk.subject);
    },
    /** Refund a dispute: the customer gets the money; Super App, the partner or the seller pays for it */
    async refundTicket(id, { amount, bearer, note = '' }) {
      me();
      const tk = db().find('tickets', id); if (!tk) throw new ApiError('Ticket not found.', 'not_found');
      if (tk.refund) throw new ApiError('This dispute was already refunded.', 'invalid');
      const amt = Math.round(+amount);
      const max = tk.amount || 20000;
      if (!(amt >= 1 && amt <= max)) throw new ApiError(`Refund between 1 and ${max.toLocaleString()} EGP.`, 'invalid');
      if (!['platform', 'partner'].includes(bearer)) throw new ApiError('Choose who pays for it.', 'invalid');
      if (bearer === 'partner' && !tk.against) throw new ApiError('No partner or seller is linked to this request.', 'invalid');
      const u = deviceUser();
      if (tk.from?.kind === 'customer' && u && tk.from.id === u.id) await SA.services.payment.refund(amt, `Refund — ${tk.id} ${tk.subject}`);
      const rf = db().insert('refunds', { ticketId: id, customerId: tk.from?.id, amount: amt, bearer: bearer === 'partner' ? 'partner' : 'platform', against: tk.against, ref: tk.ref, note: note.trim(), at: Date.now() }, 'rf');
      if (bearer === 'partner') admin.adjust({ accountId: tk.against, amount: -amt, reason: `Refund to a customer for ${tk.id}: ${tk.subject}`, kind: 'refund', ref: id });
      const who = bearer === 'partner' ? (db().find('accounts', tk.against)?.company || 'the partner') : 'Super App';
      db().update('tickets', id, { refund: { amount: amt, bearer, at: Date.now(), id: rf.id }, messages: [...tk.messages, { by: 'system', name: 'Super App', text: `Refund of ${amt.toLocaleString()} EGP sent to the customer${note.trim() ? ` — ${note.trim()}` : ''}.`, at: Date.now() }] });
      log('support.refunded', `Refunded ${amt.toLocaleString()} EGP for ${tk.id} (paid by ${who})`, id);
      admin._tellTicket(tk, `${amt.toLocaleString()} EGP refund sent`, tk.from?.kind === 'customer' ? 'It’s in your Super App wallet.' : tk.subject);
    },
    /** Notify whoever opened the ticket */
    _tellTicket(tk, title, body) {
      const u = deviceUser();
      if (tk.from?.kind === 'customer') { if (u && tk.from.id === u.id) SA.services.user.notify({ kind: 'shop', title: `${title} · ${tk.id}`, body, link: `/support/${tk.id}` }); }
      else N()?.toAccount(tk.from.id, { cat: 'account', title: `${title} · ${tk.id}`, body, link: `#/dash/support/${tk.id}` });
    },

    /** Cancel a printed item before it ships (refund the customer; a started print is a cost for Super App) */
    async cancelPrint(id, reason) {
      me();
      const o = db().find('podOrders', id); if (!o) throw new ApiError('Item not found.', 'not_found');
      if (!['received', 'printing'].includes(o.status)) throw new ApiError('Only items that haven’t shipped can be cancelled.', 'invalid');
      if (!reason || reason.trim().length < 3) throw new ApiError('Write the reason.', 'invalid');
      const loss = o.status === 'printing' ? o.partnerCost * (o.qty || 1) : 0;
      db().update('podOrders', id, { status: 'cancelled', loss, reason: reason.trim(), history: [...(o.history || []), { status: 'cancelled', at: Date.now(), note: reason.trim() }] });
      const u = deviceUser();
      const mine = u && o.customer.id === u.id ? (SA.core.store.get().orders || []).find((x) => x.id === o.orderId) : null;
      if (mine && mine.payment?.method !== 'cod') { await SA.services.payment.refund(o.price * (o.qty || 1), `Refund — ${o.title} (cancelled)`); SA.services.user.notify({ kind: 'shop', title: 'A printed item was cancelled', body: `“${o.title}” — ${reason.trim()} The money is back in your wallet.`, link: `/order/${o.orderId}` }); }
      N()?.toSeller(o.sellerId, { cat: 'orders', tone: 'danger', title: `Cancelled: ${o.title}`, body: `${reason.trim()} No profit is counted for this item.`, link: '#/dash/seller/orders' });
      log('print.cancelled', `Cancelled printed item ${o.id} “${o.title}”: ${reason.trim()}`, id);
    },
    /* ---------- Super App promo codes ---------- */
    promos: () => db().all('codes').filter((c) => c.kind === 'platform' && !c.deleted).sort((a, b) => b.createdAt - a.createdAt),
    partnerCodes: () => db().all('codes').filter((c) => c.kind !== 'platform' && !c.deleted).map((c) => ({ ...c, owner: db().find('accounts', c.ownerId) })).sort((a, b) => b.createdAt - a.createdAt),
    savePromo(d, id = null) {
      me();
      const C = SA.services.codes;
      const e = {};
      const code = C.norm(d.code);
      if (!/^[A-Z0-9-]{4,16}$/.test(code)) e.code = 'Use 4–16 letters or numbers (no spaces).';
      else if (db().all('codes').some((c) => c.code === code && c.id !== id && !c.deleted)) e.code = 'This code is already taken.';
      if (!['shop', 'bookings', 'both'].includes(d.scope)) e.scope = 'Choose where it works.';
      if (!['percent', 'fixed', 'shipping'].includes(d.type)) e.type = 'Choose the discount type.';
      if (d.type === 'shipping' && d.scope !== 'shop') e.type = 'Free delivery only works in the shop.';
      const v = +d.value;
      if (d.type === 'percent' && !(v >= 5 && v <= 70)) e.value = 'Between 5% and 70%.';
      if (d.type === 'fixed' && !(v >= 10 && v <= 5000)) e.value = 'Between 10 and 5,000 EGP.';
      if (d.to && d.from && d.to < d.from) e.to = 'End on or after the start date.';
      if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e);
      const rec = { code, kind: 'platform', ownerId: me().id, storeId: null, pitchIds: [], scope: d.scope, type: d.type, value: d.type === 'shipping' ? 0 : Math.round(v), min: Math.round(+d.min || 0), maxUses: Math.round(+d.maxUses || 0),
        perCustomer: d.perCustomer ? 1 : 0, from: d.from || isoDate(today()), to: d.to || '', label: (d.label || '').trim(), note: (d.note || '').trim(), status: d.status === 'paused' ? 'paused' : 'active' };
      const out = id ? db().update('codes', id, rec) : db().insert('codes', { ...rec, uses: 0, discountGiven: 0, revenue: 0, usedBy: [] }, 'cd');
      log(id ? 'promo.updated' : 'promo.created', `${id ? 'Updated' : 'Created'} Super App code ${code} (${SA.services.codes.label(out)}, ${d.scope})`, out.id);
      return out;
    },
    setCodeStatus(id, status) { me(); const c = db().find('codes', id); db().update('codes', id, { status }); log('promo.status', `${status === 'active' ? 'Resumed' : 'Paused'} code ${c.code}${c.kind !== 'platform' ? ` (${db().find('accounts', c.ownerId)?.company || 'partner'})` : ''}`, id); },
    deleteCode(id) { me(); const c = db().find('codes', id); db().update('codes', id, { deleted: true, status: 'paused' }); log('promo.deleted', `Deleted code ${c.code}`, id); },

    /* ---------- Broadcasts ---------- */
    AUDIENCES: { owners: 'All pitch owners', sellers: 'All sellers', printer: 'The print partner', partners: 'Every partner (owners, sellers, print partner)', customers: 'Customers on the site', everyone: 'Everyone' },
    audience(key, level = '') {
      const accs = db().all('accounts');
      const owners = accs.filter((a) => a.role === 'owner' && a.status !== 'suspended');
      const sellers = accs.filter((a) => a.role === 'seller').filter((a) => !level || SA.services.sellers.levelOf(SA.services.sellers.byAccount(a.id) || {}).id === level);
      const printer = accs.filter((a) => a.role === 'printer');
      const ids = { owners, sellers, printer, partners: [...owners, ...sellers, ...printer], customers: [], everyone: [...owners, ...sellers, ...printer] }[key] || [];
      const customers = ['customers', 'everyone'].includes(key) ? admin.customers().length : 0;
      return { accounts: ids, customers };
    },
    broadcast({ audience, level = '', title, body, link = '', when = '' }) {
      me();
      const e = {};
      if (!admin.AUDIENCES[audience]) e.audience = 'Choose who gets it.';
      if (!title || title.trim().length < 3) e.title = 'Write a title (3+ characters).';
      if (!body || body.trim().length < 5) e.body = 'Write the message.';
      if (link && !/^#\/|^\//.test(link)) e.link = 'Use a link inside Super App, like #/shop or #/fantasy.';
      const at = when ? new Date(when).getTime() : 0;
      if (when && !(at > Date.now())) e.when = 'Pick a time in the future, or leave it empty to send now.';
      if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e);
      const b = db().insert('broadcasts', { audience, level, title: title.trim(), body: body.trim(), link: link.trim(), status: at ? 'scheduled' : 'sending', sendAt: at || Date.now(), by: me().name, reach: 0 }, 'bc');
      log(at ? 'broadcast.scheduled' : 'broadcast.sent', `${at ? 'Scheduled' : 'Sent'} “${b.title}” to ${admin.AUDIENCES[audience].toLowerCase()}${level ? ` (${level})` : ''}`, b.id);
      if (!at) admin._deliver(b);
      return b;
    },
    _deliver(b) {
      const { accounts, customers } = admin.audience(b.audience, b.level);
      const link = b.link ? (b.link.startsWith('#') ? b.link : `#${b.link}`) : '';
      accounts.forEach((a) => N()?.toAccount(a.id, { cat: 'account', tone: 'info', icon: 'megaphone', title: b.title, body: b.body, link: link.startsWith('#/dash') ? link : '' }));
      if (customers && deviceUser() && !admin.isBlocked(deviceUser().id)) SA.services.user.notify({ kind: 'shop', title: b.title, body: b.body, link: link ? link.slice(1) : '' });
      db().update('broadcasts', b.id, { status: 'sent', sentAt: Date.now(), reach: accounts.length + customers });
    },
    cancelBroadcast(id) { me(); const b = db().find('broadcasts', id); if (b?.status !== 'scheduled') throw new ApiError('Only scheduled messages can be cancelled.', 'invalid'); db().update('broadcasts', id, { status: 'cancelled' }); log('broadcast.cancelled', `Cancelled scheduled message “${b.title}”`, id); },
    /** Send scheduled broadcasts that are due (runs on start-up and every minute; a server job with the backend) */
    sweepBroadcasts() { (db().all('broadcasts') || []).filter((b) => b.status === 'scheduled' && b.sendAt <= Date.now()).forEach((b) => admin._deliver(b)); },

    /* ---------- Site content ---------- */
    saveBanner(d, id = null) {
      me();
      const e = {};
      if (!d.title || d.title.trim().length < 5) e.title = 'Write a headline (5+ characters).';
      if (!d.link || !/^#\//.test(d.link.trim())) e.link = 'Use a link inside Super App, like #/shop?cat=boots.';
      if (!d.image) e.image = 'Choose a photo.';
      if (d.to && d.from && d.to < d.from) e.to = 'End on or after the start date.';
      if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e);
      const rec = { title: d.title.trim(), badge: (d.badge || '').trim(), cta: (d.cta || '').trim(), link: d.link.trim(), image: d.image, from: d.from || '', to: d.to || '', active: d.active !== false && d.active !== 'off' };
      const list = db().all('banners');
      const out = id ? db().update('banners', id, rec) : db().insert('banners', { ...rec, order: (list.reduce((m, b) => Math.max(m, b.order), 0) || 0) + 1 }, 'bn');
      log(id ? 'banner.updated' : 'banner.created', `${id ? 'Updated' : 'Added'} home banner “${out.title}”`, out.id);
      return out;
    },
    moveBanner(id, dir) {
      me();
      const list = db().all('banners').slice().sort((a, b) => a.order - b.order);
      const i = list.findIndex((b) => b.id === id), j = i + dir;
      if (i < 0 || j < 0 || j >= list.length) return;
      [list[i], list[j]] = [list[j], list[i]];
      list.forEach((b, k) => { b.order = k + 1; });
      db().touch('banners');
      log('banner.moved', `Moved home banner “${list[j].title}” ${dir < 0 ? 'up' : 'down'}`, id);
    },
    toggleBanner(id) { me(); const b = db().find('banners', id); db().update('banners', id, { active: !b.active }); log('banner.toggled', `${b.active ? 'Hid' : 'Showed'} home banner “${b.title}”`, id); },
    deleteBanner(id) { me(); const b = db().find('banners', id); db().remove('banners', id); log('banner.deleted', `Deleted home banner “${b.title}”`, id); },
    setAnnouncement(d) {
      me();
      const on = !!d.on && d.on !== 'off';
      if (on && (!d.text || d.text.trim().length < 5)) throw new ApiError('Write the announcement (5+ characters).', 'invalid', { text: 'Write the announcement' });
      if (d.link && !/^#\//.test(d.link.trim())) throw new ApiError('Use a link inside Super App.', 'invalid', { link: 'Use a link like #/fantasy' });
      const prev = db().settings().announcement || {};
      const a = { on, text: (d.text || '').trim().slice(0, 160), link: (d.link || '').trim(), cta: (d.cta || '').trim().slice(0, 24), tone: ['info', 'promo', 'alert'].includes(d.tone) ? d.tone : 'info', until: d.until || '', v: (prev.v || 0) + 1 };
      db().setSettings({ announcement: a });
      log('announcement', on ? `Announcement on: “${a.text}”` : 'Announcement switched off');
      SA.ui.shell?.render();
    },
    saveNews(d, id = null) {
      me();
      const e = {};
      if (!d.title || d.title.trim().length < 10) e.title = 'Write a headline (10+ characters).';
      if (!SA.data.news.categories.some((c) => c.id === d.cat)) e.cat = 'Choose a category.';
      if (!d.excerpt || d.excerpt.trim().length < 20) e.excerpt = 'Write a short summary (20+ characters).';
      const body = String(d.body || '').split(/\n{2,}|\r\n\r\n/).map((x) => x.trim()).filter(Boolean);
      if (!body.length || body.join(' ').length < 60) e.body = 'Write the story (60+ characters; empty lines start a new paragraph).';
      if (!d.img) e.img = 'Choose a photo.';
      if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e);
      const rec = { cat: d.cat, clubs: [d.cat], type: 'Super App', title: d.title.trim(), excerpt: d.excerpt.trim(), body, img: d.img, breaking: !!d.breaking && d.breaking !== 'off', read: Math.max(1, Math.round(body.join(' ').split(/\s+/).length / 200)) };
      const out = id ? db().update('newsPosts', id, rec) : db().insert('newsPosts', { ...rec, id: `n-sa-${Date.now().toString(36)}`, at: Date.now(), hoursAgo: 0 }, 'n');
      log(id ? 'news.updated' : 'news.published', `${id ? 'Updated' : 'Published'} news “${out.title}”`, out.id);
      return out;
    },
    deleteNews(id) { me(); const p = db().find('newsPosts', id); db().update('newsPosts', id, { deleted: true }); log('news.deleted', `Deleted news “${p.title}”`, id); },
    flagNews(id, patch) {
      me();
      const edits = { ...(db().settings().newsEdits || {}) };
      edits[id] = { ...(edits[id] || {}), ...patch };
      db().setSettings({ newsEdits: edits });
      const s = SA.services.content.allStories().find((x) => x.id === id);
      log('news.flag', `${Object.entries(patch).map(([k, v]) => `${v ? '' : 'un-'}${k === 'hidden' ? 'hid' : 'marked breaking'}`).join(', ')}: “${s?.title || id}”`, id);
    },
    hideMatch(id, reason = '') {
      me();
      const h = { ...(db().settings().hiddenMatches || {}) };
      if (h[id]) delete h[id]; else h[id] = { reason: reason.trim(), at: Date.now() };
      db().setSettings({ hiddenMatches: h });
      log(h[id] ? 'match.hidden' : 'match.restored', `${h[id] ? 'Hid' : 'Restored'} pickup game ${id}${reason ? `: ${reason.trim()}` : ''}`, id);
    },
    /* ---------- Fantasy: sponsors, leagues, winners ---------- */
    saveSponsor(d, id = null) {
      me();
      const e = {};
      if (!d.name || d.name.trim().length < 2) e.name = 'Enter the sponsor’s name.';
      if (d.value !== '' && d.value != null && !(+d.value >= 0)) e.value = 'Enter 0 or more.';
      if (d.to && d.from && d.to < d.from) e.to = 'End on or after the start date.';
      if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e);
      const rec = { name: d.name.trim(), tier: ['gold', 'silver', 'partner'].includes(d.tier) ? d.tier : 'partner', contact: (d.contact || '').trim(), value: Math.round(+d.value || 0), from: d.from || '', to: d.to || '', color: d.color || '#0B6E4F', note: (d.note || '').trim() };
      const out = id ? db().update('sponsors', id, rec) : db().insert('sponsors', rec, 'sp');
      log(id ? 'sponsor.updated' : 'sponsor.added', `${id ? 'Updated' : 'Added'} fantasy sponsor ${out.name}`, out.id);
      return out;
    },
    deleteSponsor(id) {
      me();
      const s = db().find('sponsors', id);
      const fl = { ...(db().settings().fantasyLeagues || {}) };
      Object.keys(fl).forEach((k) => { if (fl[k].sponsorId === id) fl[k] = { ...fl[k], sponsorId: '' }; });
      db().setSettings({ fantasyLeagues: fl });
      db().remove('sponsors', id);
      log('sponsor.removed', `Removed fantasy sponsor ${s.name}`, id);
    },
    /** Edit a prize league: blurb, cap, sponsor, prizes ("1st | Signed kit" per line), hidden */
    saveLeague(id, d) {
      me();
      const prizes = String(d.prizes || '').split('\n').map((l) => l.split('|').map((x) => x.trim())).filter(([a, b]) => a && b);
      if (!prizes.length) throw new ApiError('Add at least one prize, like “1st | Signed kit”.', 'invalid', { prizes: 'Add prizes' });
      if (d.cap !== '' && d.cap != null && !(+d.cap >= 0)) throw new ApiError('Enter 0 (no cap) or more.', 'invalid', { cap: 'Invalid' });
      const fl = { ...(db().settings().fantasyLeagues || {}) };
      fl[id] = { ...(fl[id] || {}), blurb: (d.blurb || '').trim(), cap: d.cap === '' || +d.cap === 0 ? undefined : Math.round(+d.cap), sponsorId: d.sponsorId || '', prizes, prizeValue: Math.round(+d.prizeValue || 0) || undefined, hidden: !!d.hidden && d.hidden !== 'off' };
      if (!fl[id].blurb) delete fl[id].blurb;
      db().setSettings({ fantasyLeagues: fl });
      log('fantasy.league', `Updated prize league ${id}: ${prizes.length} prize line${prizes.length > 1 ? 's' : ''}${fl[id].hidden ? ' (hidden)' : ''}`, id);
    },
    /** Prize for a rank from labels like "1st", "4th – 10th", "Top 100", "11th – 100th" */
    prizeFor(prizes, rank) {
      for (const [label, prize] of prizes) {
        const one = label.match(/^(\d+)(?:st|nd|rd|th)$/i); if (one && +one[1] === rank) return prize;
        const rng = label.match(/^(\d+)(?:st|nd|rd|th)?\s*[–-]\s*(\d+)(?:st|nd|rd|th)?$/i); if (rng && rank >= +rng[1] && rank <= +rng[2]) return prize;
        const top = label.match(/^top\s+(\d+)$/i); if (top && rank <= +top[1]) return prize;
      }
      return null;
    },
    /** Close a round: winners from the league table get their prize; vouchers become single-use Super App codes */
    announceWinners(leagueId, lg) {
      me();
      const F = SA.services.fantasy;
      const prev = F.league();
      if (lg && lg !== prev) F.use(lg);
      try {
        const l = F.allOfficial().find((x) => x.id === leagueId);
        if (!l) throw new ApiError('League not found.', 'not_found');
        const meta = F.meta();
        const table = F.standings(l).filter((r) => r.rank <= 10);
        const rows = table.map((r) => {
          const prize = admin.prizeFor(l.prizes, r.rank);
          if (!prize) return null;
          let code = '';
          const egp = prize.match(/([\d,]+)\s*EGP[^.]*voucher/i), pct = prize.match(/(\d+)%\s*(?:shop\s+)?(?:discount|off)/i);
          if (egp || pct) {
            const booking = /booking|pitch/i.test(prize);
            code = `WIN-${Math.random().toString(36).slice(2, 8).toUpperCase()}`;
            db().insert('codes', { code, kind: 'platform', ownerId: me().id, storeId: null, pitchIds: [], scope: booking ? 'bookings' : 'shop', type: egp ? 'fixed' : 'percent', value: egp ? +egp[1].replace(/,/g, '') : +pct[1], min: 0, maxUses: 1, perCustomer: 1,
              from: isoDate(today()), to: isoDate(addDays(today(), 60)), label: `${l.name} prize`, note: `Fantasy prize — ${r.name}, rank ${r.rank}`, status: 'active', uses: 0, discountGiven: 0, revenue: 0, usedBy: [] }, 'cd');
          }
          return { rank: r.rank, name: r.name, team: r.team, total: r.total, prize, code, delivered: !!code, isMe: !!r.isMe };
        }).filter(Boolean);
        if (!rows.length) throw new ApiError('No prize matches the top of the table.', 'invalid');
        const w = db().insert('fantasyWinners', { leagueId, league: l.name, lg: lg || prev, gw: Math.max(1, meta.gw - 1), at: Date.now(), sponsor: l.sponsor?.name || '', rows, by: me().name }, 'fw');
        const mine = rows.find((r) => r.isMe);
        if (mine) SA.services.user.notify({ kind: 'fantasy', title: `You won in ${l.name}!`, body: `Rank ${mine.rank}: ${mine.prize}.${mine.code ? ` Your code: ${mine.code}` : ' The Super App team will contact you to deliver it.'}`, link: '/fantasy/leagues' });
        log('fantasy.winners', `Announced ${rows.length} winner${rows.length > 1 ? 's' : ''} in ${l.name} (GW${w.gw})`, w.id);
        return w;
      } finally { if (lg && lg !== prev) F.use(prev); }
    },
    markPrizeDelivered(winId, i) {
      me();
      const w = db().find('fantasyWinners', winId); if (!w) throw new ApiError('Not found.', 'not_found');
      const rows = w.rows.map((r, k) => (k === i ? { ...r, delivered: true, deliveredAt: Date.now() } : r));
      db().update('fantasyWinners', winId, { rows });
      log('fantasy.prize-delivered', `Marked prize delivered: ${w.rows[i].name} — ${w.rows[i].prize} (${w.league})`, winId);
    },
    /* ---------- Samples ---------- */
    /** Approve: the seller is charged (unless it's their free Elite sample) and the print partner gets the job */
    approveSample(id) {
      me();
      const x = db().find('samples', id); if (!x || x.status !== 'requested') throw new ApiError('This request was already handled.', 'invalid');
      const s = db().find('sellers', x.sellerId);
      const c = db().find('podCatalog', x.catalogId);
      const now = Date.now();
      const o = db().insert('podOrders', { id: `PO-S${now.toString(36).toUpperCase().slice(-5)}`, orderId: `SMP-${id.slice(-5).toUpperCase()}`, designId: x.designId, sellerId: x.sellerId, catalogId: x.catalogId, title: x.title, color: x.color, size: x.size, qty: 1,
        price: 0, base: 0, profit: 0, partnerCost: c?.partnerCost || 0, sample: true, customer: { id: `seller-${s.id}`, name: `${s.name} (sample)`, city: x.address.split(',').pop().trim() }, payment: 'Seller wallet', status: 'received', history: [{ status: 'received', at: now }], placedAt: now, deliveredAt: 0 });
      if (x.price > 0) admin.adjust({ accountId: s.accountId, amount: -x.price, reason: `Sample: ${x.title} (${x.color}, ${x.size})`, kind: 'sample', ref: id });
      db().update('samples', id, { status: 'approved', orderId: o.id, approvedAt: now });
      log('sample.approved', `Approved a sample of “${x.title}” for ${s.name}${x.price ? ` (${x.price} EGP)` : ' (free)'}`, id);
      N()?.toSeller(s.id, { cat: 'designs', tone: 'turf', title: `Sample approved: ${x.title}`, body: `It’s in the print queue. ${x.price ? `${x.price.toLocaleString()} EGP was taken from your balance.` : 'This one is free with Elite.'}`, link: '#/dash/seller/designs' });
      db().all('accounts').filter((a) => a.role === 'printer').forEach((a) => N()?.toAccount(a.id, { cat: 'orders', tone: 'blaze', title: `Sample to print: ${x.title}`, body: `${x.color} · ${x.size} · ship to the seller`, link: '#/dash/print' }));
    },
    rejectSample(id, reason) {
      me();
      const x = db().find('samples', id); if (!x || x.status !== 'requested') throw new ApiError('This request was already handled.', 'invalid');
      if (!reason || reason.trim().length < 3) throw new ApiError('Write the reason.', 'invalid');
      db().update('samples', id, { status: 'rejected', reason: reason.trim() });
      log('sample.rejected', `Rejected a sample of “${x.title}”: ${reason.trim()}`, id);
      N()?.toSeller(x.sellerId, { cat: 'designs', tone: 'danger', title: `Sample not approved: ${x.title}`, body: reason.trim(), link: '#/dash/seller/designs' });
    },

    /* ---------- Monthly seller level review (grace month before a level goes down) ---------- */
    levelReviewDue() { const last = (db().all('levelReviews') || [])[0]; return !last || last.month < new Date().toISOString().slice(0, 7); },
    previewLevelReview() {
      const S = SA.services.sellers, L = S.levels();
      return S.list().map((s) => {
        const lv = S.levelOf(s);
        const kept = s.levelKept != null ? s.levelKept : lv.earned;
        const action = lv.earned > kept ? 'up' : lv.earned === kept ? 'ok' : s.grace ? 'down' : 'grace';
        return { s, from: L[kept]?.name || lv.name, to: L[action === 'grace' ? kept : lv.earned].name, earned: lv.earned, kept, action, rating: lv.stats.rating, sales: lv.stats.sales };
      });
    },
    runLevelReview() {
      me();
      const month = new Date().toISOString().slice(0, 7);
      const L = SA.services.sellers.levels();
      const results = admin.previewLevelReview().map((r) => {
        const patch = r.action === 'grace' ? { levelKept: r.kept, grace: { since: month } } : { levelKept: r.earned, grace: null };
        db().update('sellers', r.s.id, patch);
        if (r.action === 'grace') N()?.toSeller(r.s.id, { cat: 'level', tone: 'gold', title: `Grace month: keep ${r.from}`, body: `Your rating is ${r.rating.toFixed(1)} — ${L[r.kept].name} needs ${L[r.kept].rating}. Bring it back up before next month’s review to keep your perks.`, link: '#/dash/seller' });
        // A level that goes down is announced by the notification sweep ("Your level is now …")
        return { sellerId: r.s.id, name: r.s.name, from: r.from, to: r.to, action: r.action, rating: r.rating, sales: r.sales };
      });
      const rv = db().insert('levelReviews', { month, results, by: me().name, at: Date.now() }, 'lr');
      log('levels.reviewed', `Ran the ${month} level review: ${results.filter((x) => x.action === 'grace').length} grace, ${results.filter((x) => x.action === 'down').length} down, ${results.filter((x) => x.action === 'up').length} up`, rv.id);
      return rv;
    },
    /* ---------- Approvals: new pitches and new sellers ---------- */
    pendingPitches: () => db().all('pitches').filter((p) => !p.deleted && ['pending', 'rejected'].includes(p.review)).sort((a, b) => (a.review === b.review ? b.submittedAt - a.submittedAt : a.review === 'pending' ? -1 : 1)),
    approvePitch(id) {
      me();
      const p = db().find('pitches', id); if (!p) throw new ApiError('Pitch not found.', 'not_found');
      db().update('pitches', id, { review: 'approved', reviewReason: '', reviewedAt: Date.now() });
      log('pitch.approved', `Approved new pitch ${p.name} (${db().find('accounts', p.ownerId)?.company || ''})`, id);
      N()?.toAccount(p.ownerId, { cat: 'store', tone: 'turf', title: `${p.name} is live on Super App`, body: 'Players can find and book it now.', link: '#/dash/pitches' });
    },
    rejectPitch(id, reason) {
      me();
      const p = db().find('pitches', id); if (!p) throw new ApiError('Pitch not found.', 'not_found');
      if (!reason || reason.trim().length < 3) throw new ApiError('Write the reason.', 'invalid');
      db().update('pitches', id, { review: 'rejected', reviewReason: reason.trim(), reviewedAt: Date.now() });
      log('pitch.rejected', `Didn’t approve pitch ${p.name}: ${reason.trim()}`, id);
      N()?.toAccount(p.ownerId, { cat: 'store', tone: 'danger', title: `${p.name} wasn’t approved`, body: `${reason.trim()} Edit the pitch to send it again.`, link: `#/dash/pitches/${id}` });
    },
    pendingSellers: () => db().all('sellers').filter((s) => ['pending', 'rejected'].includes(s.status)).sort((a, b) => b.createdAt - a.createdAt),
    approveSeller(id) {
      me();
      const s = db().find('sellers', id); if (!s) throw new ApiError('Seller not found.', 'not_found');
      db().update('sellers', id, { status: 'active', reviewReason: '' });
      if (s.storeId) db().update('stores', s.storeId, { status: 'active' });
      log('seller.approved', `Approved new seller ${s.name}`, id);
      N()?.toSeller(id, { cat: 'account', tone: 'turf', title: 'You’re approved — your store is live', body: 'Share your store link and send your designs for review.', link: '#/dash/seller' });
    },
    rejectSeller(id, reason) {
      me();
      const s = db().find('sellers', id); if (!s) throw new ApiError('Seller not found.', 'not_found');
      if (!reason || reason.trim().length < 3) throw new ApiError('Write the reason.', 'invalid');
      db().update('sellers', id, { status: 'rejected', reviewReason: reason.trim() });
      if (s.storeId) db().update('stores', s.storeId, { status: 'suspended' });
      log('seller.rejected', `Didn’t approve seller ${s.name}: ${reason.trim()}`, id);
      N()?.toSeller(id, { cat: 'account', tone: 'danger', title: 'Your store wasn’t approved', body: `${reason.trim()} Contact Help & support to appeal.`, link: '#/dash/seller' });
    },
    setSellerApproval(on) { me(); db().setSettings({ sellerApproval: !!on }); log('settings.seller-approval', `New sellers ${on ? 'need approval' : 'go live straight away'}`); },
    /** Feature a seller on the shop (or remove the badge) */
    featureSeller(sellerId, on) { me(); const s = db().update('sellers', sellerId, { featuredByAdmin: !!on }); log('seller.featured', `${on ? 'Featured' : 'Un-featured'} ${s.name} in the shop`, sellerId); },
    payouts: allPayouts, checks, printerOwed,
    pendingPayouts: () => allPayouts().filter((p) => p.status === 'processing'),
    approvePayout(id, { ref = '' } = {}) {
      me();
      const p = findPayout(id);
      if (p.status !== 'processing') throw new ApiError('This withdrawal was already handled.', 'invalid');
      const hard = checks(p).filter(([t]) => t === 'danger');
      if (hard.length) throw new ApiError(`Can’t send it yet: ${hard.map(([, l]) => l.toLowerCase()).join(', ')}.`, 'invalid');
      setPayout(p, { status: 'paid', paidAt: Date.now(), ref: ref.trim() || `TRX-${Date.now().toString(36).toUpperCase().slice(-6)}`, by: me().id });
      log('payout.approved', `Approved ${p.amount.toLocaleString()} EGP withdrawal for ${p.company || p.name} via ${p.method}`, p.id);
      N()?.toAccount(p.accountId, { cat: 'money', tone: 'turf', title: `${p.amount.toLocaleString()} EGP sent`, body: `To ${p.method} ${p.detail}. It usually arrives within 1–2 working days.`, link: p.kind === 'seller' ? '#/dash/seller/wallet' : '#/dash/wallet' });
    },
    rejectPayout(id, reason) {
      me();
      const p = findPayout(id);
      if (p.status !== 'processing') throw new ApiError('This withdrawal was already handled.', 'invalid');
      if (!reason || reason.trim().length < 3) throw new ApiError('Write the reason — the partner sees it.', 'invalid');
      setPayout(p, { status: 'rejected', reason: reason.trim(), rejectedAt: Date.now(), by: me().id });
      log('payout.rejected', `Rejected ${p.amount.toLocaleString()} EGP withdrawal for ${p.company || p.name}: ${reason.trim()}`, p.id);
      N()?.toAccount(p.accountId, { cat: 'money', tone: 'danger', title: `Withdrawal not sent: ${p.amount.toLocaleString()} EGP`, body: `${reason.trim()} The money is back in your available balance.`, link: p.kind === 'seller' ? '#/dash/seller/wallet' : '#/dash/wallet' });
    },
    /** Pay the print partner what it is owed (one transfer) */
    settlePrinter({ amount, ref = '' } = {}) {
      me();
      const o = printerOwed();
      const amt = Math.round(+amount || o.owed);
      if (!o.account) throw new ApiError('No print partner account.', 'not_found');
      if (!(amt > 0 && amt <= o.owed)) throw new ApiError(`Enter up to ${o.owed.toLocaleString()} EGP.`, 'invalid');
      const p = db().insert('payouts', { accountId: o.account.id, amount: amt, method: o.account.payout?.method || 'Bank transfer', detail: o.account.payout?.detail || 'Print partner account', status: 'paid', paidAt: Date.now(), ref: ref.trim() || `TRX-${Date.now().toString(36).toUpperCase().slice(-6)}`, by: me().id, settlement: true }, 'PO');
      log('printer.settled', `Paid the print partner ${amt.toLocaleString()} EGP for printed items`, p.id);
      N()?.toAccount(o.account.id, { cat: 'account', tone: 'turf', title: `Settlement sent: ${amt.toLocaleString()} EGP`, body: 'Payment for printed and shipped items.', link: '#/dash/print' });
      return p;
    },
    /** Credit (+) or debit (−) a partner's or seller's balance, with a reason they can see */
    adjust({ accountId, amount, reason, kind = 'manual', ref = '' }) {
      me();
      const a = db().find('accounts', accountId);
      const amt = Math.round(+amount);
      if (!a || a.role === 'admin') throw new ApiError('Choose a partner or seller.', 'invalid');
      if (!amt || Math.abs(amt) > 100000) throw new ApiError('Enter an amount between −100,000 and 100,000 EGP (not 0).', 'invalid');
      if (!reason || reason.trim().length < 3) throw new ApiError('Write the reason — they see it in their wallet.', 'invalid');
      // Sellers with print-on-demand: their print wallet; pitch owners and store-only sellers: the partner wallet
      const sl = SA.services.sellers?.byAccount(accountId);
      const wallet = sl && sl.mode !== 'store' ? 'pod' : 'store';
      const x = db().insert('adjustments', { accountId, amount: amt, reason: reason.trim(), kind, ref, wallet, at: Date.now(), by: me().id }, 'adj');
      log('balance.adjusted', `${amt > 0 ? 'Credited' : 'Debited'} ${Math.abs(amt).toLocaleString()} EGP ${amt > 0 ? 'to' : 'from'} ${a.company || a.name}: ${reason.trim()}`, x.id);
      N()?.toAccount(accountId, { cat: 'money', tone: amt > 0 ? 'turf' : 'gold', title: `${amt > 0 ? '+' : '−'}${Math.abs(amt).toLocaleString()} EGP balance ${amt > 0 ? 'credit' : 'adjustment'}`, body: reason.trim(), link: wallet === 'pod' ? '#/dash/seller/wallet' : '#/dash/wallet' });
      return x;
    },
    adjustments: () => (db().all('adjustments') || []).map((x) => ({ ...x, account: db().find('accounts', x.accountId) })).sort((a, b) => b.at - a.at),
    /** Called by the Fantasy Pass checkout on the site */
    recordPass({ plan, amount, customer }) {
      if (!db().all('passSales')) return;
      db().insert('passSales', { plan, amount, customer, at: Date.now() }, 'FP');
    },
    /** Admin opens a partner's or seller's own dashboard in a new tab */
    openDashboard(accountId) {
      const a = db().find('accounts', accountId);
      if (!a || a.role === 'admin') throw new ApiError('Account not found.', 'not_found');
      D().auth.openAs(accountId);
      log('impersonate', `Opened ${a.name}’s dashboard (${a.company})`, accountId);
    },
  };

  SA.services.admin = admin;
  // Scheduled broadcasts go out when they are due
  const tick = () => { try { admin.sweepBroadcasts(); } catch (e) { console.warn(e); } };
  setTimeout(tick, 1500); setInterval(tick, 60000);
})(window.SA);
