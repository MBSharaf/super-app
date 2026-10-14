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

  const admin = {
    me, log, alerts, revenue, passSales, audit, actorKind, STREAMS, COSTS,
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
})(window.SA);
