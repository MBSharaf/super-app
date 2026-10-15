/**
 * Super Points — loyalty for players. Points are earned when a booked game is played, when a
 * shop order is delivered and on tournament entries; bonuses for reviews. Tiers raise the earn
 * rate. Points convert to wallet cash. Rates come from Platform settings (admin).
 * Earned points are derived from the player's history, so they can never drift out of sync.
 * Future API: GET /me/points, POST /me/points/redeem
 */
(function (SA) {
  const { ApiError } = SA.services.http;
  const store = SA.core.store;
  const db = () => SA.services.db;
  const { parseISO, uid } = SA.core.utils;

  const TIERS = [
    { key: 'bronze', name: 'Bronze', min: 0, mult: 1, color: '#B87333' },
    { key: 'silver', name: 'Silver', min: 1500, mult: 1.25, color: '#9AA5AE' },
    { key: 'gold', name: 'Gold', min: 4000, mult: 1.5, color: '#C9920F' },
  ];
  const cfg = () => db().settings().loyalty || { earnPer: 10, pointValue: 0.2, minRedeem: 500 };
  const tierFor = (lifetime) => [...TIERS].reverse().find((t) => lifetime >= t.min);
  const stored = () => store.get().loyalty?.entries || [];

  /** Earning events from the player's real activity */
  const activity = () => {
    const now = Date.now();
    const out = [];
    store.get().bookings.forEach((b) => {
      const end = parseISO(b.date).getTime() + (b.hour + b.duration) * 36e5;
      if (b.status !== 'cancelled' && end < now) out.push({ id: `bk-${b.id}`, at: end, base: b.pitchTotal, label: `Played at ${b.pitchName}`, kind: 'earn' });
    });
    store.get().orders.forEach((o) => {
      let tr; try { tr = SA.services.shop.trackingSync(o); } catch { tr = null; }
      (tr?.sellers || []).forEach((g) => {
        if (g.status !== 'delivered') return;
        const value = g.lines.reduce((t, l) => t + l.price * l.qty, 0);
        out.push({ id: `or-${o.id}-${g.storeId}`, at: g.at?.delivered || o.createdAt + 48 * 36e5, base: value, label: `Order ${o.id} delivered (${g.storeName})`, kind: 'earn' });
      });
    });
    (db().all('tournaments') || []).forEach((t) => t.teams?.forEach((tm) => {
      if (tm.userId === (store.get().user?.id || 'u-001') && tm.paid && t.status !== 'cancelled') out.push({ id: `tr-${t.id}-${tm.id}`, at: tm.createdAt, base: t.fee, label: `Tournament entry — ${t.name}`, kind: 'earn' });
    }));
    return out;
  };

  const compute = () => {
    const c = cfg();
    const events = [...stored(), ...activity()].sort((a, b) => a.at - b.at);
    let lifetime = 0, balance = 0;
    const history = events.map((e) => {
      let points = e.points;
      if (e.kind === 'earn' && points == null) points = Math.floor((e.base / c.earnPer) * tierFor(lifetime).mult);
      if (points > 0) lifetime += points;
      balance += points;
      return { ...e, points, tier: tierFor(lifetime).key };
    });
    const tier = tierFor(lifetime);
    const next = TIERS[TIERS.indexOf(tier) + 1] || null;
    return { balance, lifetime, tier, next, progress: next ? (lifetime - tier.min) / (next.min - tier.min) : 1, history: history.reverse(), value: balance * c.pointValue, cfg: c };
  };

  const add = (entry) => store.set((s) => ({ loyalty: { entries: [...(s.loyalty?.entries || []), { id: uid('pt'), at: Date.now(), ...entry }] } }), 'loyalty');

  const loyalty = {
    TIERS, summary: compute,
    /** Points a payment of `amount` will earn at the current tier (shown at checkout) */
    estimate(amount) { const s = compute(); return Math.floor((amount / s.cfg.earnPer) * s.tier.mult); },
    /** One-off bonus (idempotent by key) */
    bonus(key, points, label) {
      if (stored().some((e) => e.key === key)) return;
      add({ key, points, label, kind: 'bonus' });
      SA.ui?.overlay?.toast(`+${points} Super Points — ${label}`, { type: 'success' });
    },
    /** Correction from the Super App team (+ or −), shown in the points history */
    adjust(points, label) {
      const p = Math.round(+points);
      if (!p) throw new ApiError('Enter the number of points.', 'invalid');
      if (p < 0 && compute().balance + p < 0) throw new ApiError(`They only have ${compute().balance.toLocaleString()} points.`, 'invalid');
      add({ points: p, label: `Super App: ${label}`, kind: p > 0 ? 'bonus' : 'adjust' });
    },
    redeem(points) {
      const s = compute();
      const p = Math.floor(+points / 100) * 100;
      if (!(p >= s.cfg.minRedeem)) throw new ApiError(`Redeem at least ${s.cfg.minRedeem} points.`, 'invalid');
      if (p > s.balance) throw new ApiError(`You have ${s.balance.toLocaleString()} points.`, 'invalid');
      const amount = Math.round(p * s.cfg.pointValue);
      add({ points: -p, label: `Converted to ${amount} EGP wallet cash`, kind: 'redeem' });
      return SA.services.payment.refund(amount, `Super Points cashback (${p.toLocaleString()} points)`).then(() => ({ points: p, amount }));
    },
  };

  SA.services.loyalty = loyalty;
})(window.SA);
