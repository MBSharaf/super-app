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

  const admin = {
    me, log, alerts, revenue, passSales, audit, actorKind, STREAMS, COSTS,
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
