/**
 * Partner Dashboard service — the "backend" for admins and pitch owners.
 * Every method is permission-checked against the signed-in account.
 *
 * Future API (examples):
 *   POST /auth/partner/login · GET /partner/bookings?from&to · PATCH /partner/pitches/:id
 *   GET /partner/finance?from&to · GET /partner/wallet · POST /partner/payouts · /partner/store/*
 */
(function (SA) {
  const { ApiError } = SA.services.http;
  const { isoDate, parseISO, addDays, today, seeded, fmtHour } = SA.core.utils;
  const db = SA.services.db;
  const PS = SA.services.pitches;
  const DAY = 864e5;
  const SESSION = 'superapp.dash.session';
  // The admin console (#/admin) is a separate system with its own session; admins can't sign in to the partner dashboard
  const ADMIN_SESSION = 'superapp.admin.session';
  const adminMode = () => /^\/admin(?:[/?]|$)/.test(SA.core.router.location());
  // Sellers have their own dashboard (/seller) with its own session; pitch partners use /dash
  const SELLER_SESSION = 'superapp.seller.session';
  const sellerMode = () => /^\/seller(?:[/?]|$)/.test(SA.core.router.location());
  /** A seller account, or a pitch partner who also sells (has a seller profile) */
  const sellsOn = (a) => !!a && (a.role === 'seller' || db.all('sellers').some((s) => s.accountId === a.id && s.status !== 'rejected'));

  /* ======================= Auth & permissions ======================= */
  const auth = {
    accounts: () => db.all('accounts'),
    current: () => {
      try {
        // The CEO system: the CEO's account acts as an admin there (every admin action works and is logged as the CEO)
        if (SA.services.ceo?.ceoMode()) return SA.services.ceo.auth.current();
        const admin = adminMode(), seller = !admin && sellerMode();
        const id = localStorage.getItem(admin ? ADMIN_SESSION : seller ? SELLER_SESSION : SESSION);
        const a = id ? db.find('accounts', id) : null;
        if (!a) return null;
        if (admin) return a.role === 'admin' && !SA.services.ceo?.blockedAccount(a) ? a : null;
        if (a.role === 'admin' || a.role === 'printer' || a.status === 'suspended') return null;
        // In the seller dashboard a pitch partner who also sells acts as a seller
        if (seller) return sellsOn(a) ? (a.role === 'seller' ? a : { ...a, role: 'seller', baseRole: a.role }) : null;
        return a.role === 'owner' ? a : null;
      } catch { return null; }
    },
    signIn(id, { as = '' } = {}) {
      const a = db.find('accounts', id);
      if (!a) throw new ApiError('Account not found.', 'not_found');
      if (a.status === 'suspended') throw new ApiError('This account is suspended. Contact the Super App team.', 'suspended');
      const paused = SA.services.ceo?.blockedAccount(a); if (paused) throw new ApiError(`This agent is paused by the CEO: ${paused}`, 'suspended');
      const key = a.role === 'admin' ? ADMIN_SESSION : (as === 'seller' || a.role === 'seller' || (sellerMode() && sellsOn(a))) ? SELLER_SESSION : SESSION;
      localStorage.setItem(key, id);
      db.log(id, 'auth.signin', `${a.name} signed in${a.role === 'admin' ? ' to the admin console' : key === SELLER_SESSION ? ' to the seller dashboard' : ''}`);
    },
    /** The account signed in to the seller dashboard (whatever page is open) — the studio and the store page use it */
    sellerAccount: () => { try { const a = db.find('accounts', localStorage.getItem(SELLER_SESSION)); return a && sellsOn(a) && a.status !== 'suspended' ? a : null; } catch { return null; } },
    signOut() { localStorage.removeItem(adminMode() ? ADMIN_SESSION : sellerMode() ? SELLER_SESSION : SESSION); },
    /** Admin opens a partner's or seller's dashboard (logged) */
    openAs(id) { const a = db.find('accounts', id); localStorage.setItem(a?.role === 'seller' ? SELLER_SESSION : SESSION, id); },
    /** A pitch partner who also sells switches between the two dashboards with the same account */
    switchTo(kind) { const a = auth.current(); if (!a) return; localStorage.setItem(kind === 'seller' ? SELLER_SESSION : SESSION, a.id); },
  };
  const me = () => { const a = auth.current(); if (!a) throw new ApiError('Sign in to the partner dashboard first.', 'unauthenticated'); return a; };
  const isAdmin = (a = auth.current()) => a?.role === 'admin';
  const canPitch = (p, a = me()) => p && (isAdmin(a) || p.ownerId === a.id);
  const canStore = (s, a = me()) => s && (isAdmin(a) || s.ownerId === a.id);
  const assert = (ok, msg = 'You don’t have access to this.') => { if (!ok) throw new ApiError(msg, 'forbidden'); };

  /** Pitches visible to the account (admin: all, optionally filtered by owner) */
  const myPitches = ({ ownerId, includeDeleted = false } = {}) => {
    const a = me();
    return db.all('pitches').filter((p) => (includeDeleted || !p.deleted) && (isAdmin(a) ? (!ownerId || p.ownerId === ownerId) : p.ownerId === a.id));
  };

  /* ======================= Booking ledger ======================= */
  const PEOPLE = SA.data.matches.people;
  const payOf = (r) => (r < 0.55 ? 'Card' : r < 0.85 ? 'Wallet' : 'Cash at venue');
  const synthCache = new Map();
  db.subscribe(() => synthCache.clear());

  const synthFor = (p, dateISO) => {
    const key = `${p.id}|${dateISO}`;
    if (synthCache.has(key)) return synthCache.get(key);
    const out = [];
    const snap = p.seedSnapshot;
    if (p.seed && snap) {
      for (let h = snap.open[0]; h < snap.open[1]; h++) {
        if (!PS.syntheticBooked(p, dateISO, h, { raw: true })) continue;
        const id = `SB-${p.id}-${dateISO}-${h}`;
        const r = seeded(`${id}|meta`);
        const amount = PS.priceAt(p, dateISO, h, snap);
        const who = PEOPLE[Math.floor(r * PEOPLE.length)];
        out.push({ id, pitchId: p.id, ownerId: p.ownerId, date: dateISO, hour: h, duration: 1, amount, fee: Math.round(amount * 0.03),
          customer: { id: `c-${Math.floor(r * 9000)}`, name: who, phone: `+20 1${Math.floor(r * 3)} •••• ${String(Math.floor(r * 1e4)).padStart(4, '0')}` },
          source: 'app', payment: payOf(seeded(`${id}|pay`)), synthetic: true, noShow: seeded(`${id}|ns`) < 0.025,
          createdAt: parseISO(dateISO).getTime() + h * 36e5 - (1 + Math.floor(r * 6)) * DAY });
      }
    }
    synthCache.set(key, out);
    return out;
  };

  const startOf = (b) => parseISO(b.date).getTime() + b.hour * 36e5;
  /** Normalised display status */
  const statusOf = (b) => {
    const o = b.synthetic ? db.raw().bookingOverrides[b.id] : null;
    const s = o?.status || b.status;
    if (s === 'cancelled') return 'cancelled';
    if (s === 'no-show' || (b.synthetic && b.noShow && !o)) { if (startOf(b) + b.duration * 36e5 < Date.now()) return 'no-show'; }
    const start = startOf(b), end = start + b.duration * 36e5, now = Date.now();
    if (s === 'checked-in' || b.checkedIn || o?.checkedIn) return end < now ? 'completed' : 'checked-in';
    if (end < now) return 'completed';
    if (start <= now) return 'in-progress';
    return 'upcoming';
  };

  const commissionRate = () => db.settings().commission.bookings;
  // A partner can have its own commission deal (set by the Super App team); otherwise the platform rate applies
  const dealOf = (ownerId) => db.find('accounts', ownerId)?.commission || null;
  const bookingRate = (ownerId) => (dealOf(ownerId)?.bookings ?? commissionRate());
  const SOURCES = { app: 'Super App', 'walk-in': 'Walk-in', team: 'Team booking' };
  const sourceLabel = (b) => (b.source === 'app' && b.seriesId ? 'Super App · weekly' : SOURCES[b.source] || 'Super App');
  /** Money for one booking, from the partner's point of view */
  const bookingMoney = (b, status = statusOf(b)) => {
    const rate = b.source === 'app' || !b.source ? bookingRate(b.ownerId) : 0;
    if (status === 'cancelled') {
      // Late customer cancellation: the venue keeps what the policy didn't refund
      const kept = b.cancelledBy === 'customer' && b.refundShare != null ? Math.round(b.amount * (1 - b.refundShare)) : 0;
      if (!kept) return { gross: 0, commission: 0, net: 0, online: 0, cash: 0, refund: b.refund ?? b.amount, wallet: 0 };
      const commission = Math.round(kept * rate);
      return { gross: kept, commission, net: kept - commission, online: kept, cash: 0, refund: b.refund || 0, wallet: kept - commission, retained: true };
    }
    const gross = b.amount, commission = Math.round(gross * rate), net = gross - commission;
    const online = b.payment === 'Card' || b.payment === 'Wallet';
    return { gross, commission, net, online: online ? gross : 0, cash: online ? 0 : gross, refund: 0, wallet: online ? net : -commission };
  };

  const enrich = (b) => {
    const p = db.find('pitches', b.pitchId);
    const status = statusOf(b);
    const o = b.synthetic ? db.raw().bookingOverrides[b.id] : null;
    return { ...b, pitchName: p?.name || 'Removed pitch', area: p?.area || '', status, money: bookingMoney(b, status), startsAt: startOf(b), sourceLabel: sourceLabel(b), checkedInAt: b.checkedInAt || o?.checkedInAt || null, policy: b.policy || p?.cancelPolicy || 'standard' };
  };

  /** All bookings for the given pitches between two ISO dates (inclusive) */
  const ledger = ({ from, to, pitchIds } = {}) => {
    const pitches = myPitches({ includeDeleted: true }).filter((p) => !pitchIds || pitchIds.includes(p.id));
    const ids = new Set(pitches.map((p) => p.id));
    const out = [];
    for (let d = parseISO(from); d <= parseISO(to); d = addDays(d, 1)) {
      const iso = isoDate(d);
      pitches.forEach((p) => out.push(...synthFor(p, iso)));
    }
    db.all('bookings').forEach((b) => { if (ids.has(b.pitchId) && b.date >= from && b.date <= to) out.push(b); });
    return out.map(enrich).sort((a, b) => b.startsAt - a.startsAt);
  };
  const findBooking = (id) => {
    const real = db.find('bookings', id);
    if (real) return enrich(real);
    const m = id.match(/^SB-(.+)-(\d{4}-\d{2}-\d{2})-(\d+)$/);
    if (!m) return null;
    const p = db.find('pitches', m[1]);
    const b = p && synthFor(p, m[2]).find((x) => x.id === id);
    return b ? enrich(b) : null;
  };

  /* ======================= Store sales ======================= */
  // Stores run by sellers pay the commission of their seller level (12% → 8%)
  const storeRate = (st) => {
    if (st?.official) return 0;
    if (st && dealOf(st.ownerId)?.store != null) return dealOf(st.ownerId).store;
    const sl = st && SA.services.sellers?.byAccount(st.ownerId);
    return sl ? SA.services.sellers.levelOf(sl).commission : db.settings().commission.store;
  };
  const SEED_STORE_PRODUCTS = () => ({ 'st-official': SA.data.products.list.filter((p) => !p.service), 'st-turf22': db.all('products').filter((p) => p.storeId === 'st-turf22' && p.seed) });
  const salesCache = new Map();
  db.subscribe(() => salesCache.clear());
  const synthSales = (storeId, iso) => {
    const key = `${storeId}|${iso}`;
    if (salesCache.has(key)) return salesCache.get(key);
    const st = db.find('stores', storeId);
    const list = SEED_STORE_PRODUCTS()[storeId] || [];
    const out = [];
    if (st && list.length && parseISO(iso).getTime() >= st.createdAt - DAY && iso <= isoDate(today())) {
      const n = Math.floor(seeded(`${storeId}|${iso}|n`) * (st.official ? 7 : 2.4));
      for (let k = 0; k < n; k++) {
        const r = seeded(`${storeId}|${iso}|${k}`);
        const p = list[Math.floor(r * list.length)];
        const qty = r > 0.85 ? 2 : 1;
        const at = parseISO(iso).getTime() + (9 + Math.floor(r * 14)) * 36e5;
        if (at > Date.now()) continue;
        out.push({ id: `SS-${storeId}-${iso}-${k}`, orderId: `ORD-${String(Math.floor(r * 900000) + 100000)}`, productId: p.id, storeId, name: p.name, image: p.images[0], qty, price: p.price, total: p.price * qty,
          customer: { name: PEOPLE[Math.floor(seeded(`${storeId}${iso}${k}c`) * PEOPLE.length)], city: ['Cairo', 'Giza', 'Alexandria', 'New Cairo'][k % 4] }, payment: r < 0.6 ? 'Card' : r < 0.8 ? 'Wallet' : 'Cash on delivery', createdAt: at, synthetic: true });
      }
    }
    salesCache.set(key, out);
    return out;
  };
  /**
   * Fulfilment of one order line. Partner stores move their own orders (packed → shipped →
   * delivered); the official store and historical orders follow Super App logistics' timeline.
   */
  const FLOW = ['confirmed', 'packed', 'shipped', 'delivered'];
  const saleState = (s) => {
    const f = s.synthetic ? db.raw().saleOverrides[s.id] : s.fulfil;
    if (f?.status) return { status: f.status, at: f.at || {}, courier: f.courier || '', tracking: f.tracking || '', deliveredAt: f.at?.delivered || null, manual: true };
    const st = db.find('stores', s.storeId);
    const h = (Date.now() - s.createdAt) / 36e5;
    if (st?.official) { const status = SA.services.shop.orderStatus(s.createdAt); return { status, at: {}, auto: true, courier: 'Super App Delivery', tracking: '', deliveredAt: status === 'delivered' ? s.createdAt + 48 * 36e5 : null }; }
    if (s.synthetic) {
      if (h > 72) return { status: 'delivered', at: {}, deliveredAt: s.createdAt + 60 * 36e5 };
      const r = seeded(`${s.id}|ful`);
      return { status: h < 12 ? 'confirmed' : r < 0.5 ? 'packed' : 'shipped', at: {} };
    }
    return { status: 'confirmed', at: {} };
  };
  const saleMoney = (s) => {
    const st = db.find('stores', s.storeId);
    const f = saleState(s);
    if (f.status === 'cancelled') return { gross: 0, commission: 0, net: 0, status: 'cancelled', delivered: false, f };
    const commission = Math.round(s.total * storeRate(st));
    return { gross: s.total, commission, net: s.total - commission, status: f.status, delivered: f.status === 'delivered', deliveredAt: f.deliveredAt, f };
  };
  const salesFor = (storeIds, from, to) => {
    const out = [];
    const fromT = parseISO(from).getTime(), toT = parseISO(to).getTime() + DAY;
    storeIds.forEach((sid) => { for (let d = parseISO(from); d <= parseISO(to); d = addDays(d, 1)) out.push(...synthSales(sid, isoDate(d))); });
    db.all('sales').forEach((s) => { if (storeIds.includes(s.storeId) && s.createdAt >= fromT && s.createdAt < toT) out.push(s); });
    return out.map((s) => ({ ...s, money: saleMoney(s) })).sort((a, b) => b.createdAt - a.createdAt);
  };
  const myStores = ({ ownerId } = {}) => {
    const a = me();
    return db.all('stores').filter((s) => (isAdmin(a) ? (!ownerId || s.ownerId === ownerId) : s.ownerId === a.id));
  };

  /* ======================= Aggregations ======================= */
  const sumBy = (arr, f) => arr.reduce((t, x) => t + f(x), 0);
  const openHoursOf = (p) => Math.max(0, p.open[1] - p.open[0]);

  const summary = ({ from, to, ownerId, pitchIds } = {}) => {
    const pitches = myPitches({ ownerId }).filter((p) => !pitchIds || pitchIds.includes(p.id));
    const L = ledger({ from, to, pitchIds: pitches.map((p) => p.id) });
    const active = L.filter((b) => b.status !== 'cancelled');
    const days = Math.round((parseISO(to) - parseISO(from)) / DAY) + 1;
    const capacity = sumBy(pitches, (p) => openHoursOf(p) * days);
    const hours = sumBy(active, (b) => b.duration);
    const S = salesFor(myStores({ ownerId }).map((s) => s.id), from, to);
    // Tournament entry fees count as pitch income (they pay for pitch time)
    const T = SA.services.tournaments ? SA.services.tournaments.income(myPitches({ ownerId, includeDeleted: true }).filter((p) => !pitchIds || pitchIds.includes(p.id)).map((p) => p.id), from, to) : [];
    return {
      from, to, pitches, ledger: L, sales: S, tourLines: T,
      bookings: active.length, cancelled: L.length - active.length, hours, capacity, occupancy: capacity ? hours / capacity : 0,
      gross: sumBy(L, (b) => b.money.gross) + sumBy(T, (x) => x.gross), commission: sumBy(L, (b) => b.money.commission) + sumBy(T, (x) => x.commission), net: sumBy(L, (b) => b.money.net) + sumBy(T, (x) => x.net),
      tourGross: sumBy(T, (x) => x.gross), tourNet: sumBy(T, (x) => x.net), tourTeams: T.length,
      online: sumBy(L, (b) => b.money.online) + sumBy(T, (x) => x.online), cash: sumBy(L, (b) => b.money.cash) + sumBy(T, (x) => x.cash), refunds: sumBy(L, (b) => b.money.refund), retained: sumBy(L, (b) => (b.money.retained ? b.money.gross : 0)),
      walkIns: active.filter((b) => b.source === 'walk-in').length, teams: active.filter((b) => b.source === 'team' || b.seriesId).length, avgValue: active.length ? sumBy(active, (b) => b.money.gross) / active.length : 0,
      storeGross: sumBy(S, (s) => s.money.gross), storeCommission: sumBy(S, (s) => s.money.commission), storeNet: sumBy(S, (s) => s.money.net), storeUnits: sumBy(S, (s) => s.qty), storeOrders: new Set(S.map((s) => s.orderId)).size,
    };
  };

  const series = (sum, from, to) => {
    const map = new Map();
    for (let d = parseISO(from); d <= parseISO(to); d = addDays(d, 1)) map.set(isoDate(d), { date: isoDate(d), bookingsNet: 0, storeNet: 0, bookings: 0, hours: 0 });
    sum.ledger.forEach((b) => { const r = map.get(b.date); if (!r) return; r.bookingsNet += b.money.net; if (b.status !== 'cancelled') { r.bookings++; r.hours += b.duration; } });
    sum.sales.forEach((s) => { const r = map.get(isoDate(new Date(s.createdAt))); if (r) r.storeNet += s.money.net; });
    (sum.tourLines || []).forEach((x) => { const r = map.get(x.date); if (r) r.bookingsNet += x.net; });
    return [...map.values()];
  };

  const heatmap = (sum) => {
    const grid = Array.from({ length: 7 }, () => Array(27).fill(0));
    const cap = Array.from({ length: 7 }, () => Array(27).fill(0));
    const days = new Map();
    sum.ledger.forEach((b) => { if (b.status === 'cancelled') return; const wd = parseISO(b.date).getDay(); for (let h = b.hour; h < b.hour + b.duration; h++) grid[wd][Math.floor(h)]++; });
    // capacity: number of (pitch, day) pairs per weekday where the hour is open
    sum.ledger.forEach((b) => days.set(b.date, parseISO(b.date).getDay()));
    const from = sum.from, to = sum.to;
    for (let d = parseISO(from); d <= parseISO(to); d = addDays(d, 1)) sum.pitches.forEach((p) => { for (let h = p.open[0]; h < p.open[1]; h++) cap[d.getDay()][h]++; });
    let minH = 26, maxH = 0;
    sum.pitches.forEach((p) => { minH = Math.min(minH, p.open[0]); maxH = Math.max(maxH, p.open[1]); });
    return { grid, cap, minH: Math.min(minH, maxH), maxH };
  };

  /* ======================= Wallet ======================= */
  const lastSettlement = () => { const d = today(); const back = (d.getDay() - db.settings().settlementDay + 7) % 7; return addDays(d, -back).getTime(); };
  const wallet = ({ ownerId } = {}) => {
    const a = me();
    const acct = isAdmin(a) && ownerId ? db.find('accounts', ownerId) : a;
    const scopeOwner = isAdmin(a) ? (ownerId || a.id) : a.id;
    const from = isoDate(new Date(db.raw().platformSince)), to = isoDate(addDays(today(), 21));
    const pitches = myPitches({ ownerId: scopeOwner || undefined, includeDeleted: true });
    const L = ledger({ from, to, pitchIds: pitches.map((p) => p.id) });
    const S = salesFor(myStores({ ownerId: scopeOwner || undefined }).map((s) => s.id), from, isoDate(today()));
    const cut = lastSettlement(), now = Date.now();
    const b = { available: 0, pending: 0, settled: 0, cashCommission: 0 };
    const s = { available: 0, pending: 0, settled: 0 };
    const weekly = new Map();
    const weekKey = (t) => { const d = new Date(t); d.setHours(0, 0, 0, 0); const back = (d.getDay() - db.settings().settlementDay + 7) % 7; return addDays(d, 7 - back).getTime(); };
    L.forEach((x) => {
      if (x.status === 'cancelled' && !x.money.wallet) return;
      const end = x.status === 'cancelled' ? Math.min(x.cancelledAt || x.startsAt, x.startsAt) : x.startsAt + x.duration * 36e5;
      if (end > now) { if (x.money.online) b.pending += x.money.net; return; }
      if (!x.money.online) b.cashCommission += x.money.commission;
      if (end < cut) { b.settled += x.money.wallet; const k = weekKey(end); weekly.set(k, (weekly.get(k) || 0) + x.money.wallet); }
      else b.available += x.money.wallet;
    });
    // Online tournament entry fees: available the day after the first match day
    if (SA.services.tournaments) SA.services.tournaments.income(pitches.map((p) => p.id)).forEach((x) => {
      if (!x.online) return;
      if (x.availableAt > now) { b.pending += x.net; return; }
      if (x.availableAt < cut) { b.settled += x.net; const k = weekKey(x.availableAt); weekly.set(k, (weekly.get(k) || 0) + x.net); }
      else b.available += x.net;
    });
    S.forEach((x) => {
      const deliveredAt = x.money.deliveredAt || x.createdAt + 48 * 36e5;
      if (!x.money.delivered) { s.pending += x.money.net; return; }
      if (deliveredAt < cut) { s.settled += x.money.net; const k = weekKey(deliveredAt); weekly.set(k, (weekly.get(k) || 0) + x.money.net); }
      else s.available += x.money.net;
    });
    // Withdrawals stay "processing" until the Super App team approves them in the admin console
    const requested = db.all('payouts').filter((p) => (scopeOwner ? p.accountId === scopeOwner : isAdmin(a) ? true : p.accountId === a.id));
    const adjust = (db.all('adjustments') || []).filter((x) => x.accountId === (scopeOwner || a.id) && x.wallet !== 'pod');
    const autoPayouts = [...weekly.entries()].filter(([, amt]) => amt > 0).map(([t, amt]) => ({ id: `PO-AUTO-${t}`, createdAt: t, amount: Math.round(amt), method: acct?.payout?.method || 'Bank transfer', detail: acct?.payout?.detail || '—', status: 'paid', auto: true }));
    const withdrawn = sumBy(requested.filter((p) => p.status !== 'rejected'), (p) => p.amount);
    const adjusted = sumBy(adjust, (x) => x.amount);
    const balance = Math.round(b.available + s.available - withdrawn + adjusted);
    return { account: acct, bookings: b, store: s, payouts: [...requested, ...autoPayouts].sort((x, y) => y.createdAt - x.createdAt), adjustments: adjust, adjusted, withdrawn, balance: Math.max(0, balance), lastSettlement: cut, ledger: L, sales: S };
  };

  /* ======================= Mutations ======================= */
  const F = SA.data.pitches.facilities;
  const validatePitch = (d) => {
    const e = {};
    if (!d.name || d.name.trim().length < 3) e.name = 'Enter a name of at least 3 characters.';
    if (!d.area?.trim()) e.area = 'Enter the area or district.';
    if (!d.address?.trim()) e.address = 'Enter the street address.';
    if (!(d.lat > 22 && d.lat < 31.8 && d.lng > 24.6 && d.lng < 37)) e.location = 'Pick the pitch location on the map (inside Egypt).';
    if (!['5-a-side', '7-a-side', '11-a-side'].includes(d.format)) e.format = 'Choose the pitch size.';
    if (!(d.price >= 50 && d.price <= 20000)) e.price = 'Price per hour must be between 50 and 20,000 EGP.';
    if (!(d.peakPrice >= d.price)) e.peakPrice = 'Peak price must be the same as or higher than the standard price.';
    if (!(d.open?.[0] >= 0 && d.open?.[1] <= 28 && d.open[1] - d.open[0] >= 2)) e.open = 'Opening hours must cover at least 2 hours.';
    if (!d.images?.length) e.images = 'Add at least one photo.';
    if ((d.about || '').trim().length < 20) e.about = 'Write at least 20 characters so players know what to expect.';
    if (d.weeklyDiscount !== undefined && d.weeklyDiscount !== '' && !(+d.weeklyDiscount >= 0 && +d.weeklyDiscount <= 30)) e.weeklyDiscount = 'Weekly discount must be between 0% and 30%.';
    return e;
  };
  const cleanPitch = (d) => ({
    name: d.name.trim(), area: d.area.trim(), city: d.city, address: d.address.trim(), lat: +d.lat, lng: +d.lng, format: d.format,
    players: { '5-a-side': 10, '7-a-side': 14, '11-a-side': 22 }[d.format], surface: d.surface, indoor: !!d.indoor,
    price: Math.round(+d.price), peakPrice: Math.round(+d.peakPrice), open: [+d.open[0], +d.open[1]], images: d.images.slice(0, 6),
    facilities: (d.facilities || []).filter((f) => F[f]), about: d.about.trim(), tags: (d.tags || []).map((t) => t.trim()).filter(Boolean).slice(0, 3),
    cancelPolicy: PS.POLICIES[d.cancelPolicy] ? d.cancelPolicy : 'standard',
    weekly: { enabled: d.weeklyEnabled !== false && d.weeklyEnabled !== 'off', discount: Math.min(0.3, Math.max(0, Math.round(+(d.weeklyDiscount ?? 10)) / 100)) },
  });

  const cancelBooking = async (id, { reason = 'Cancelled by the venue', refundShopper = true } = {}) => {
    const b = findBooking(id);
    assert(b && canPitch(db.find('pitches', b.pitchId)), 'You can only manage bookings at your own pitches.');
    if (b.status === 'cancelled') return b;
    if (b.synthetic) db.override(id, { status: 'cancelled', reason, by: me().id });
    else {
      db.update('bookings', id, { status: 'cancelled', refund: b.amount + (b.fee || 0), cancelledBy: 'venue', reason });
      // If the booking belongs to the shopper on this device, refund them in full and tell them
      const mine = SA.core.store.get().bookings.find((x) => x.id === id);
      if (mine && refundShopper && mine.status !== 'cancelled') {
        await SA.services.payment.refund(mine.total, `Refund — ${mine.pitchName} (cancelled by venue)`);
        SA.core.store.set((s) => ({ bookings: s.bookings.map((x) => (x.id === id ? { ...x, status: 'cancelled', refund: mine.total } : x)) }));
        SA.services.user.notify({ kind: 'booking', title: 'Your booking was cancelled by the venue', body: `${mine.pitchName} on ${mine.date}. ${mine.total} EGP is back in your wallet.`, link: `/booking/${id}` });
      }
    }
    db.log(me().id, 'booking.cancelled', `Cancelled booking ${id} at ${b.pitchName}`, id);
    return findBooking(id);
  };

  const validateProduct = (d) => {
    const e = {};
    if (!d.name || d.name.trim().length < 3) e.name = 'Enter a product name of at least 3 characters.';
    if (!SA.data.products.categories.some((c) => c.id === d.cat)) e.cat = 'Choose a category.';
    if (!(d.price > 0 && d.price <= 500000)) e.price = 'Enter a price greater than 0.';
    if (d.was && +d.was < +d.price) e.was = 'The original price must be higher than the selling price (or leave it empty).';
    if (!(Number.isInteger(+d.stock) && +d.stock >= 0)) e.stock = 'Stock must be a whole number (0 or more).';
    if (d.lowStockAt !== undefined && d.lowStockAt !== '' && !(Number.isInteger(+d.lowStockAt) && +d.lowStockAt >= 0 && +d.lowStockAt <= 999)) e.lowStockAt = 'Enter a whole number from 0 to 999.';
    if (!d.images?.length) e.images = 'Add at least one product photo.';
    if ((d.desc || '').trim().length < 10) e.desc = 'Write a short description (at least 10 characters).';
    return e;
  };
  const cleanProduct = (d) => ({
    name: d.name.trim(), brand: (d.brand || '').trim() || 'Unbranded', cat: d.cat, price: Math.round(+d.price), was: d.was ? Math.round(+d.was) : Math.round(+d.price),
    stock: +d.stock, lowStockAt: d.lowStockAt === undefined || d.lowStockAt === '' ? 5 : +d.lowStockAt, sizes: d.sizes?.length ? d.sizes : ['One size'], colors: d.colors?.length ? d.colors : [{ name: 'Standard', hex: '#0D1411' }],
    images: d.images.slice(0, 4), desc: d.desc.trim(), specs: (d.specs || []).filter(([k, v]) => k && v), badges: d.badges || [], status: d.status === 'draft' ? 'draft' : 'active',
  });


  /* ======================= Store orders (fulfilment) ======================= */
  const orderKey = (x) => `${x.orderId}|${x.storeId}`;
  /** Group sale lines into one order per store */
  const groupOrders = (lines) => {
    const m = new Map();
    lines.forEach((x) => {
      const k = orderKey(x);
      const o = m.get(k) || { key: k, orderId: x.orderId, storeId: x.storeId, lines: [], createdAt: x.createdAt, customer: x.customer, address: x.address || null, delivery: x.delivery || 'standard', payment: x.payment, synthetic: !!x.synthetic, gross: 0, net: 0, commission: 0, units: 0 };
      o.lines.push(x); o.gross += x.total; o.net += x.money.net; o.commission += x.money.commission; o.units += x.qty;
      m.set(k, o);
    });
    return [...m.values()].map((o) => {
      const f = o.lines[0].money.f;
      const st = db.find('stores', o.storeId);
      return { ...o, status: f.status, at: f.at || {}, courier: f.courier || '', tracking: f.tracking || '', auto: !!f.auto, storeName: st?.name || 'Store', official: !!st?.official };
    }).sort((a, b) => b.createdAt - a.createdAt);
  };

  /* ======================= Insights: forecast + quiet hours ======================= */
  const HIST_DAYS = 56;
  /** Booked share of each weekday × hour over the last 8 weeks, per pitch */
  const occupancyGrid = (p) => {
    const from = isoDate(addDays(today(), -HIST_DAYS)), to = isoDate(addDays(today(), -1));
    const grid = Array.from({ length: 7 }, () => Array(28).fill(0));
    const days = Array(7).fill(0);
    const start = Math.max(parseISO(from).getTime(), p.createdAt || 0);
    for (let d = new Date(start); d <= parseISO(to); d = addDays(d, 1)) days[d.getDay()]++;
    ledger({ from, to, pitchIds: [p.id] }).forEach((b) => {
      if (b.status === 'cancelled') return;
      const wd = parseISO(b.date).getDay();
      for (let h = b.hour; h < b.hour + b.duration; h++) grid[wd][Math.floor(h)]++;
    });
    const weeks = Math.min(...days);
    // New pitches have little history: fall back to a typical Cairo pitch (busy evenings)
    const prior = (h) => (h >= 19 && h <= 23 ? 0.5 : 0.25);
    const rate = (wd, h) => {
      const n = days[wd];
      if (n < 3) return prior(h);
      const r = Math.min(1, grid[wd][h] / n);
      return n < 6 ? (r * n + prior(h) * (6 - n)) / 6 : r;
    };
    return { rate, weeks, lowConfidence: weeks < 3 };
  };
  const netRate = () => 1 - commissionRate();
  const forecast = ({ pitchIds, days = 30 } = {}) => {
    const pitches = myPitches().filter((p) => (!pitchIds || pitchIds.includes(p.id)));
    const grids = new Map(pitches.map((p) => [p.id, occupancyGrid(p)]));
    const from = isoDate(today()), to = isoDate(addDays(today(), days - 1));
    const L = ledger({ from, to, pitchIds: pitches.map((p) => p.id) });
    const out = [];
    const nowH = new Date().getHours();
    for (let i = 0; i < days; i++) {
      const date = isoDate(addDays(today(), i)), wd = parseISO(date).getDay();
      let confirmed = 0, expected = 0, bookedH = 0, capH = 0;
      L.forEach((b) => { if (b.date === date && b.status !== 'cancelled') { confirmed += b.money.net; } });
      pitches.forEach((p) => {
        if (p.status !== 'active') return;
        const g = grids.get(p.id);
        const slots = PS.slotsSync(p.id, date);
        let exp = 0, booked = 0, free = 0, freeValue = 0;
        slots.forEach((sl) => {
          if (i === 0 && sl.hour <= nowH) return;
          exp += g.rate(wd, sl.hour);
          if (['booked', 'yours'].includes(sl.status)) booked++;
          else if (sl.status === 'available') { free++; freeValue += sl.price; }
        });
        capH += slots.length; bookedH += booked;
        const extraHours = Math.min(free, Math.max(0, exp - booked));
        if (free) expected += extraHours * (freeValue / free) * netRate();
      });
      out.push({ date, confirmed: Math.round(confirmed), expected: Math.round(expected), low: Math.round(confirmed + expected * 0.7), high: Math.round(confirmed + expected * 1.25), bookedH, capH });
    }
    return { days: out, pitches, lowConfidence: pitches.some((p) => grids.get(p.id).lowConfidence) };
  };
  /** Weekday blocks (Sun–Thu / Fri–Sat) that are usually quiet, with a suggested discount */
  const quietHours = ({ pitchIds } = {}) => {
    const pitches = myPitches().filter((p) => p.status === 'active' && (!pitchIds || pitchIds.includes(p.id)));
    const promos = db.all('promos').filter((x) => x.status === 'active' && x.to >= isoDate(today()));
    const out = [];
    const GROUPS = [{ key: 'weekdays', label: 'Sun–Thu', days: [0, 1, 2, 3, 4] }, { key: 'weekend', label: 'Fri–Sat', days: [5, 6] }];
    pitches.forEach((p) => {
      const g = occupancyGrid(p);
      const covered = (days, h) => promos.some((x) => x.pitchId === p.id && h >= x.hourFrom && h < x.hourTo && days.some((d) => x.days.includes(d)));
      GROUPS.forEach((G) => {
        let run = [];
        const flush = () => {
          if (run.length >= 2) {
            for (let k = 0; k < run.length; k += 4) {
              const hrs = run.slice(k, k + 4); if (hrs.length < 2) continue;
              const r = hrs.reduce((t, x) => t + x.r, 0) / hrs.length;
              const disc = r < 0.12 ? 0.3 : r < 0.2 ? 0.2 : 0.15;
              const lift = Math.min(0.3, disc * 1.2);
              const slots = G.days.length * hrs.length * 4.3;
              const price = hrs.reduce((t, x) => t + x.price, 0) / hrs.length;
              const newRate = Math.min(0.9, r + lift);
              const uplift = Math.round((newRate * (1 - disc) - r) * slots * price * netRate());
              if (uplift > 0) out.push({ id: `${p.id}|${G.key}|${hrs[0].h}`, pitchId: p.id, pitchName: p.name, group: G.key, label: G.label, days: G.days, hourFrom: hrs[0].h, hourTo: hrs[hrs.length - 1].h + 1, rate: r, discount: disc, lift, extraHours: Math.round(lift * slots / 4.3 * 10) / 10, uplift, price: Math.round(price), lowConfidence: g.lowConfidence });
            }
          }
          run = [];
        };
        for (let h = p.open[0]; h < p.open[1]; h++) {
          const r = G.days.reduce((t, d) => t + g.rate(d, h), 0) / G.days.length;
          const price = PS.basePrice(isoDate(addDays(today(), (G.days[0] - today().getDay() + 7) % 7)), h, p);
          if (r < 0.3 && !covered(G.days, h)) run.push({ h, r, price }); else flush();
        }
        flush();
      });
    });
    return out.sort((a, b) => b.uplift - a.uplift);
  };

  /* ======================= Teams: weekly bookings & memberships ======================= */
  const seriesSessions = (sr) => db.all('bookings').filter((b) => b.seriesId === sr.id).map(enrich).sort((a, b) => a.startsAt - b.startsAt);
  const freeFor = (pitchId, date, hour, duration) => {
    const slots = PS.slotsSync(pitchId, date);
    for (let h = hour; h < hour + duration; h++) {
      const s = slots.find((x) => x.hour === Math.floor(h));
      if (!s) return { ok: false, reason: 'Outside opening hours' };
      if (s.status !== 'available') return { ok: false, reason: { booked: 'Already booked', yours: 'Already booked', blocked: 'Blocked', closed: 'Pitch closed', past: 'In the past' }[s.status] || 'Not free' };
    }
    return { ok: true };
  };
  const sessionPrice = (p, date, hour, duration, discount) => {
    let list = 0;
    for (let h = hour; h < hour + duration; h += 0.5) list += PS.basePrice(date, Math.floor(h), p) / 2;
    return Math.round(list * (1 - discount));
  };
  const addSessions = (sr, p, dates, startNo) => {
    let n = startNo;
    const ids = [];
    dates.forEach((date) => {
      if (!freeFor(p.id, date, sr.hour, sr.duration).ok) return;
      const id = `TM-${sr.id.slice(3, 8)}-${String(n).padStart(2, '0')}`;
      db.insert('bookings', { id, pitchId: p.id, ownerId: p.ownerId, date, hour: sr.hour, duration: sr.duration, amount: sessionPrice(p, date, sr.hour, sr.duration, sr.discount), fee: 0,
        customer: { id: `team-${sr.id}`, name: sr.team, phone: sr.phone || '' }, source: 'team', payment: sr.payment, status: 'confirmed', seriesId: sr.id, sessionNo: n, policy: p.cancelPolicy });
      ids.push(id); n++;
    });
    return ids;
  };
  /** Memberships renew every 4 weeks while active (runs on load — no sign-in needed) */
  const renewMemberships = () => {
    let renewed = 0;
    db.all('series').filter((x) => x.kind === 'membership' && x.status === 'active' && x.source === 'venue').forEach((sr) => {
      const p = db.find('pitches', sr.pitchId);
      if (!p || p.deleted) return;
      for (let guard = 0; guard < 6; guard++) {
        const sessions = db.all('bookings').filter((b) => b.seriesId === sr.id);
        const last = sessions.reduce((m, b) => (b.date > m ? b.date : m), sr.startDate);
        if ((parseISO(last) - today()) / DAY > 7) break;
        const dates = [1, 2, 3, 4].map((k) => isoDate(addDays(parseISO(last), k * 7)));
        const ids = addSessions(sr, p, dates, sessions.length + 1);
        db.update('series', sr.id, { renewals: (sr.renewals || 0) + 1, lastRenewedAt: Date.now() });
        db.log(sr.ownerId, 'series.renewed', `Membership “${sr.team}” renewed at ${p.name}: ${ids.length} more session${ids.length === 1 ? '' : 's'}`, sr.id);
        renewed++;
        if (!ids.length) break;
      }
    });
    return renewed;
  };

  /* ======================= Monthly statement (PDF) ======================= */
  const vatPart = (gross) => gross - gross / (1 + (db.settings().vat || 0));

  const dash = {
    adminMode, sellerMode, sellsOn, bookingRate, storeRate, dealOf,
    auth, isAdmin, me: () => auth.current(),
    myPitches, myStores, ledger, findBooking, summary, daily: series, heatmap, wallet, salesFor, statusOf, bookingMoney, lastSettlement, sourceLabel, saleState, FLOW, renewMemberships,
    commission: () => db.settings().commission,

    // ---- Pitches ----
    pitch(id) { const p = db.find('pitches', id); assert(canPitch(p), 'You can only manage your own pitches.'); return p; },
    validatePitch,
    createPitch(d, ownerId) {
      const a = me(); const e = validatePitch(d); if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e);
      const owner = isAdmin(a) && ownerId ? ownerId : a.id;
      // New pitches go live after the Super App team checks them (admins' own pitches are live straight away)
      const review = isAdmin(a) ? 'approved' : 'pending';
      const p = db.insert('pitches', { id: `p-${d.name.trim().toLowerCase().replace(/[^a-z0-9]+/g, '-').slice(0, 24)}-${Math.random().toString(36).slice(2, 5)}`, ...cleanPitch(d), ownerId: owner, status: d.status === 'closed' ? 'closed' : 'active', closedReason: '', rating: 0, reviews: 0, seed: false, review, submittedAt: Date.now() });
      db.log(a.id, 'pitch.created', `Added pitch “${p.name}”${review === 'pending' ? ' — waiting for Super App review' : ''}`, p.id);
      if (review === 'pending') SA.services.notify?.toAdmins({ cat: 'store', title: `New pitch to approve: ${p.name}`, body: `${a.company} · ${p.area}, ${p.city}`, link: '#/admin/approvals' });
      return p;
    },
    updatePitch(id, d) {
      const p = dash.pitch(id); const e = validatePitch(d); if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e);
      // A rejected pitch goes back to review when the owner fixes it
      const out = db.update('pitches', id, { ...cleanPitch(d), ...(p.review === 'rejected' ? { review: 'pending', submittedAt: Date.now() } : {}) });
      if (p.review === 'rejected') SA.services.notify?.toAdmins({ cat: 'store', title: `Pitch sent again: ${out.name}`, body: me().company, link: '#/admin/approvals' });
      db.log(me().id, 'pitch.updated', `Updated pitch “${out.name}”`, id);
      return out;
    },
    setPitchStatus(id, status, reason = '') {
      const p = dash.pitch(id);
      const out = db.update('pitches', id, { status, closedReason: status === 'closed' ? reason : '' });
      db.log(me().id, status === 'closed' ? 'pitch.closed' : 'pitch.opened', status === 'closed' ? `Closed “${p.name}” for bookings${reason ? ` — ${reason}` : ''}` : `Reopened “${p.name}” for bookings`, id);
      return out;
    },
    upcomingFor(pitchId) { return ledger({ from: isoDate(today()), to: isoDate(addDays(today(), 21)), pitchIds: [pitchId] }).filter((b) => b.status === 'upcoming'); },
    async deletePitch(id) {
      const p = dash.pitch(id);
      const up = dash.upcomingFor(id);
      for (const b of up) await cancelBooking(b.id, { reason: 'Pitch removed from Super App' });
      db.update('pitches', id, { deleted: true, status: 'closed', deletedAt: Date.now() });
      db.log(me().id, 'pitch.deleted', `Removed pitch “${p.name}”${up.length ? ` and cancelled ${up.length} upcoming booking${up.length > 1 ? 's' : ''}` : ''}`, id);
      return { cancelled: up.length };
    },

    // ---- Availability ----
    slots(pitchId, date) { dash.pitch(pitchId); return PS.slotsSync(pitchId, date); },
    closures(pitchId) { return db.all('closures').filter((c) => (pitchId ? c.pitchId === pitchId : myPitches().some((p) => p.id === c.pitchId))).sort((a, b) => a.from.localeCompare(b.from)); },
    addClosure({ pitchId, from, to, hours, reason }) {
      const p = dash.pitch(pitchId);
      if (!from || !to || to < from) throw new ApiError('Choose a valid date range.', 'invalid');
      const c = db.insert('closures', { pitchId, from, to, hours: hours?.length ? hours : null, reason: (reason || '').trim() || 'Unavailable' }, 'cl');
      db.log(me().id, 'closure.added', `Blocked ${p.name} ${from === to ? from : `${from} → ${to}`}${hours?.length ? ` (${hours.map(fmtHour).join(', ')})` : ''} — ${c.reason}`, c.id);
      return c;
    },
    removeClosure(id) {
      const c = db.find('closures', id); assert(c && canPitch(db.find('pitches', c.pitchId)));
      if (c.tournamentId) throw new ApiError('These hours belong to a tournament. Change the tournament instead.', 'invalid');
      db.remove('closures', id);
      db.log(me().id, 'closure.removed', `Unblocked ${db.find('pitches', c.pitchId)?.name} ${c.from}`, id);
    },
    walkIn({ pitchId, date, hour, duration, name, phone, amount, payment }) {
      const p = dash.pitch(pitchId);
      if (!name || name.trim().length < 2) throw new ApiError('Enter the customer’s name.', 'invalid', { name: 'Required' });
      const slots = PS.slotsSync(pitchId, date);
      for (let h = hour; h < hour + duration; h++) {
        const s = slots.find((x) => x.hour === h);
        if (!s || !['available', 'past'].includes(s.status)) throw new ApiError(`${fmtHour(h)} isn’t free. Pick another time.`, 'slot_taken');
      }
      const id = 'WI-' + Math.random().toString(36).slice(2, 6).toUpperCase() + Math.floor(100 + Math.random() * 900);
      const b = db.insert('bookings', { id, pitchId, ownerId: p.ownerId, date, hour, duration, amount: Math.round(+amount), fee: 0, customer: { id: 'walk-in', name: name.trim(), phone: (phone || '').trim() }, source: 'walk-in', payment: payment || 'Cash at venue', status: 'confirmed' });
      db.log(me().id, 'booking.walkin', `Added walk-in booking ${id} at ${p.name} (${date} ${fmtHour(hour)})`, id);
      return b;
    },
    bookingAction(id, action) {
      const b = findBooking(id);
      assert(b && canPitch(db.find('pitches', b.pitchId)), 'You can only manage bookings at your own pitches.');
      if (action === 'cancel') return cancelBooking(id);
      if (action === 'check-in' && ['cancelled', 'completed', 'no-show'].includes(b.status)) throw new ApiError(`This booking is ${b.status === 'no-show' ? 'marked as a no-show' : b.status}.`, 'invalid');
      const patch = action === 'check-in' ? { status: 'checked-in', checkedIn: true, checkedInAt: Date.now(), checkedInBy: me().id } : action === 'no-show' ? { status: 'no-show' } : null;
      if (!patch) return b;
      if (b.synthetic) db.override(id, patch); else db.update('bookings', id, patch);
      if (action === 'check-in' && b.seriesId) { const sr = db.find('series', b.seriesId); if (sr) db.update('series', sr.id, { scans: [...(sr.scans || []), { at: Date.now(), bookingId: id, by: me().id }] }); }
      db.log(me().id, `booking.${action}`, `${action === 'check-in' ? 'Checked in' : 'Marked no-show'} ${id}`, id);
      return findBooking(id);
    },

    // ---- Store ----
    myStore(ownerId) { const a = me(); return db.all('stores').find((s) => s.ownerId === (isAdmin(a) && ownerId ? ownerId : a.id)) || null; },
    validateStore(d) {
      const e = {};
      if (!d.name || d.name.trim().length < 3) e.name = 'Your store name needs at least 3 characters.';
      if (db.all('stores').some((s) => s.name.toLowerCase() === d.name?.trim().toLowerCase() && s.id !== d.id)) e.name = 'Another store already uses this name.';
      if ((d.description || '').trim().length < 20) e.description = 'Describe your store in at least 20 characters.';
      if (!d.cover) e.cover = 'Add a cover image for your storefront.';
      return e;
    },
    createStore(d) {
      const a = me();
      if (db.all('stores').some((s) => s.ownerId === a.id)) throw new ApiError('You already have a store. Each account can open one store.', 'one_store');
      const e = dash.validateStore(d); if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e);
      const slug = d.name.trim().toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
      const s = db.insert('stores', { id: `st-${slug.slice(0, 20)}-${Math.random().toString(36).slice(2, 5)}`, ownerId: a.id, name: d.name.trim(), slug, tagline: (d.tagline || '').trim(), description: d.description.trim(), logo: d.logo || null, cover: d.cover, phone: (d.phone || '').trim(), status: 'active' }, 'st');
      db.log(a.id, 'store.created', `Opened store “${s.name}”`, s.id);
      return s;
    },
    updateStore(id, d) {
      const s = db.find('stores', id); assert(canStore(s));
      const e = dash.validateStore({ ...d, id }); if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e);
      const out = db.update('stores', id, { name: d.name.trim(), tagline: (d.tagline || '').trim(), description: d.description.trim(), logo: d.logo || null, cover: d.cover, phone: (d.phone || '').trim() });
      db.log(me().id, 'store.updated', `Updated store “${out.name}”`, id);
      return out;
    },
    setStoreStatus(id, status) {
      assert(isAdmin(), 'Only Super App admins can suspend stores.');
      const s = db.update('stores', id, { status });
      db.log(me().id, `store.${status}`, `${status === 'active' ? 'Reactivated' : 'Suspended'} store “${s.name}”`, id);
      return s;
    },
    products(storeId) { const s = db.find('stores', storeId); assert(canStore(s)); return db.all('products').filter((p) => p.storeId === storeId && !p.deleted); },
    product(id) { const p = db.find('products', id); assert(p && !p.deleted && canStore(db.find('stores', p.storeId)), 'You can only manage products in your own store.'); return p; },
    validateProduct,
    createProduct(storeId, d) {
      const s = db.find('stores', storeId); assert(canStore(s));
      const e = validateProduct(d); if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e);
      const p = db.insert('products', { ...cleanProduct(d), storeId, rating: 0, reviews: 0, seed: false }, 'pr');
      db.log(me().id, 'product.created', `Added product “${p.name}” to ${s.name}`, p.id);
      return p;
    },
    updateProduct(id, d) {
      dash.product(id);
      const e = validateProduct(d); if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e);
      const p = db.update('products', id, cleanProduct(d));
      db.log(me().id, 'product.updated', `Updated product “${p.name}”`, id);
      return p;
    },
    deleteProduct(id) {
      const p = dash.product(id);
      db.update('products', id, { deleted: true, status: 'draft' });
      // Remove it from the shopper's cart on this device
      SA.core.store.set((st) => ({ cart: st.cart.filter((l) => l.productId !== id) }), 'cart');
      db.log(me().id, 'product.deleted', `Deleted product “${p.name}”`, id);
    },
    setProductStatus(id, status) { const p = dash.product(id); db.update('products', id, { status }); db.log(me().id, 'product.status', `${status === 'active' ? 'Published' : 'Unpublished'} “${p.name}”`, id); },

    // ---- Wallet ----
    requestPayout({ amount, method, detail }, ownerId) {
      const a = me();
      SA.services.ceo?.assertOn('payouts');
      const w = wallet({ ownerId });
      const amt = Math.round(+amount);
      if (!(amt >= 100)) throw new ApiError('The minimum withdrawal is 100 EGP.', 'invalid', { amount: 'Minimum 100 EGP' });
      if (amt > w.balance) throw new ApiError(`You can withdraw up to ${w.balance.toLocaleString()} EGP.`, 'invalid', { amount: 'More than your available balance' });
      if (!method) throw new ApiError('Choose where to send the money.', 'invalid', { method: 'Required' });
      if (!detail || detail.trim().length < 4) throw new ApiError('Enter the account details.', 'invalid', { detail: 'Required' });
      const po = db.insert('payouts', { accountId: isAdmin(a) && ownerId ? ownerId : a.id, amount: amt, method, detail: detail.trim(), status: 'processing' }, 'PO');
      db.log(a.id, 'payout.requested', `Requested a ${amt.toLocaleString()} EGP payout via ${method}`, po.id);
      return po;
    },


    // ---- Store orders ----
    orders({ ownerId, from, to } = {}) {
      const stores = myStores({ ownerId });
      const f = from || isoDate(addDays(today(), -29)), t = to || isoDate(today());
      return groupOrders(salesFor(stores.map((x) => x.id), f, t));
    },
    order(key) {
      const [orderId, storeId] = key.split('|');
      const st = db.find('stores', storeId); assert(canStore(st), 'This order belongs to another store.');
      const real = db.all('sales').filter((x) => x.orderId === orderId && x.storeId === storeId);
      let lines = real.map((x) => ({ ...x, money: saleMoney(x) }));
      if (!lines.length) {
        const days = []; for (let i = 0; i < 40; i++) days.push(isoDate(addDays(today(), -i)));
        days.some((d) => { const hit = synthSales(storeId, d).filter((x) => x.orderId === orderId); if (hit.length) { lines = hit.map((x) => ({ ...x, money: saleMoney(x) })); return true; } return false; });
      }
      if (!lines.length) throw new ApiError('Order not found.', 'not_found');
      return groupOrders(lines)[0];
    },
    async orderAction(key, action, { courier = '', tracking = '', reason = '' } = {}) {
      const o = dash.order(key);
      const next = { pack: 'packed', ship: 'shipped', deliver: 'delivered', cancel: 'cancelled' }[action];
      if (!next) throw new ApiError('Unknown action.', 'invalid');
      const idx = FLOW.indexOf(o.status);
      if (o.status === 'cancelled' || o.status === 'delivered') throw new ApiError(`This order is already ${o.status}.`, 'invalid');
      if (action === 'cancel' && idx >= 2) throw new ApiError('Shipped orders can’t be cancelled — wait for delivery and handle it as a return.', 'invalid');
      if (action !== 'cancel' && FLOW.indexOf(next) !== idx + 1) throw new ApiError(`Move the order to “${FLOW[idx + 1]}” first.`, 'invalid');
      if (action === 'ship') {
        const e = {};
        if (!courier) e.courier = 'Choose who delivers it.';
        if (courier !== 'Own delivery' && tracking.trim().length < 4) e.tracking = 'Enter the courier’s tracking number.';
        if (Object.keys(e).length) throw new ApiError('Add the delivery details.', 'invalid', e);
      }
      const at = { ...o.at, [next]: Date.now() };
      const patch = { status: next, at, courier: action === 'ship' ? courier : o.courier, tracking: action === 'ship' ? tracking.trim() : o.tracking, reason: action === 'cancel' ? (reason || 'Cancelled by the store') : undefined };
      o.lines.forEach((x) => { if (x.synthetic) db.overrideSale(x.id, patch); else db.update('sales', x.id, { fulfil: patch }); });
      // Cancelled: put the items back in stock and refund the shopper on this device
      if (action === 'cancel') o.lines.forEach((x) => { if (!x.synthetic) { const pr = db.find('products', x.productId); if (pr) db.update('products', pr.id, { stock: (pr.stock || 0) + x.qty }); } });
      const mine = SA.core.store.get().orders.find((x) => x.id === o.orderId);
      if (mine) {
        const label = { packed: 'is being packed', shipped: `is on the way with ${courier}${tracking ? ` (tracking ${tracking.trim()})` : ''}`, delivered: 'was delivered', cancelled: 'was cancelled by the seller' }[next];
        let body = `${o.units} item${o.units > 1 ? 's' : ''} from ${o.storeName} ${label}.`;
        if (next === 'cancelled' && mine.payment.method !== 'cod') {
          // Refund what this seller's items cost; when every item of the order is cancelled, refund the rest too (delivery)
          const allCancelled = mine.lines.every((l, i) => { const sale = db.find('sales', `${mine.id}-${i + 1}`); return sale && saleState(sale).status === 'cancelled'; });
          const left = Math.max(0, mine.total - (mine.refunded || 0));
          const amount = allCancelled ? left : Math.min(o.gross, left);
          if (amount > 0) {
            await SA.services.payment.refund(amount, `Refund — ${o.orderId} (${o.storeName})`);
            SA.core.store.set((st) => ({ orders: st.orders.map((x) => (x.id === mine.id ? { ...x, refunded: (x.refunded || 0) + amount } : x)) }));
            body += ` ${amount.toLocaleString()} EGP is back in your wallet.`;
          }
        }
        SA.services.user.notify({ kind: 'shop', title: `Order ${o.orderId} update`, body, link: `/order/${o.orderId}` });
      }
      db.log(me().id, `order.${next}`, `${o.orderId} (${o.storeName}) marked ${next}${action === 'ship' ? ` — ${courier}${tracking ? ` ${tracking.trim()}` : ''}` : ''}`, o.orderId);
      return dash.order(key);
    },
    restock(productId, add) {
      const p = dash.product(productId);
      const n = Math.round(+add);
      if (!(n >= 1 && n <= 10000)) throw new ApiError('Enter how many units arrived (1 or more).', 'invalid', { add: 'Enter a number' });
      const out = db.update('products', p.id, { stock: (p.stock || 0) + n });
      db.log(me().id, 'product.restocked', `Restocked “${p.name}”: +${n} (now ${out.stock})`, p.id);
      return out;
    },
    lowStock(ownerId) {
      const st = dash.myStore(ownerId);
      return st ? dash.products(st.id).filter((p) => p.status === 'active' && p.stock <= (p.lowStockAt ?? 5)) : [];
    },

    // ---- Reviews ----
    reviews({ ownerId, pitchId } = {}) {
      const ids = new Set(myPitches({ ownerId, includeDeleted: true }).map((p) => p.id));
      return db.all('reviews').filter((r) => ids.has(r.pitchId) && (!pitchId || r.pitchId === pitchId)).map((r) => ({ ...r, pitchName: db.find('pitches', r.pitchId)?.name || 'Removed pitch' })).sort((a, b) => b.createdAt - a.createdAt);
    },
    review(id) { const r = db.find('reviews', id); assert(r && canPitch(db.find('pitches', r.pitchId)), 'You can only manage reviews of your own pitches.'); return r; },
    replyReview(id, text) {
      const r = dash.review(id);
      const t = (text || '').trim();
      if (t.length < 2) throw new ApiError('Write a reply first.', 'invalid', { reply: 'Write a reply' });
      if (t.length > 600) throw new ApiError('Keep replies under 600 characters.', 'invalid', { reply: 'Too long' });
      const out = db.update('reviews', id, { reply: { text: t, at: Date.now(), by: me().id, edited: !!r.reply } });
      const u = SA.core.store.get().user;
      if (u && r.customer?.id === u.id && !r.reply) SA.services.user.notify({ kind: 'booking', title: 'The venue replied to your review', body: `${db.find('pitches', r.pitchId)?.name}: “${t.slice(0, 80)}${t.length > 80 ? '…' : ''}”`, link: `/pitches/${r.pitchId}` });
      db.log(me().id, 'review.replied', `${r.reply ? 'Edited reply to' : 'Replied to'} ${r.customer.name}’s ${r.rating}★ review`, id);
      return out;
    },
    deleteReply(id) { const r = dash.review(id); db.update('reviews', id, { reply: null }); db.log(me().id, 'review.reply-removed', `Removed reply to ${r.customer.name}’s review`, id); },
    reportReview(id, reason) {
      const r = dash.review(id);
      if (!reason) throw new ApiError('Choose a reason.', 'invalid', { reason: 'Required' });
      db.update('reviews', id, { reported: { reason, at: Date.now(), by: me().id } });
      db.log(me().id, 'review.reported', `Reported ${r.customer.name}’s review to Super App — ${reason}`, id);
      SA.services.notify?.toAdmins({ cat: 'store', tone: 'danger', title: `Review reported: ${db.find('pitches', r.pitchId)?.name || ''}`, body: `${me().company || me().name}: ${reason}`, link: '#/admin/reviews?filter=reported' });
      SA.services.notify?.toAccount(me().id, { cat: 'store', title: 'Report sent to Super App', body: `We’ll check ${r.customer.name}’s review and tell you what we decided.`, link: '#/dash/reviews?filter=reported' });
    },
    moderateReview(id, action) {
      assert(isAdmin(), 'Only Super App admins can moderate reviews.');
      const r = db.find('reviews', id);
      const patch = action === 'hide' ? { hidden: true, reported: r.reported ? { ...r.reported, resolved: 'hidden' } : null } : action === 'show' ? { hidden: false } : { reported: null };
      db.update('reviews', id, patch);
      db.log(me().id, `review.${action}`, `${action === 'hide' ? 'Hid' : action === 'show' ? 'Restored' : 'Kept'} ${r.customer.name}’s review`, id);
      // The pitch partner and the player who wrote it hear what Super App decided
      const p = db.find('pitches', r.pitchId);
      if (p) SA.services.notify?.toAccount(p.ownerId, { cat: 'store', tone: action === 'hide' ? 'turf' : '', title: action === 'hide' ? 'A review was hidden' : action === 'show' ? 'A review is visible again' : 'Super App kept a review', body: `${r.customer.name}’s ${r.rating}★ review on ${p.name}${action === 'keep' ? ' follows the review rules, so it stays. You can reply to it publicly.' : '.'}`, link: '#/dash/reviews' });
      const u = SA.core.store.get().user;
      if (u && r.customer?.id === u.id && action !== 'keep') SA.services.user.notify({ kind: 'booking', title: action === 'hide' ? 'Your review was hidden' : 'Your review is visible again', body: action === 'hide' ? `Your review of ${p?.name || 'the pitch'} doesn’t follow the review rules (no insults or personal details).` : `${p?.name || ''}`, link: `/pitches/${r.pitchId}` });
    },

    // ---- Teams (weekly bookings & memberships) ----
    series({ ownerId } = {}) {
      renewMemberships();
      const ids = new Set(myPitches({ ownerId, includeDeleted: true }).map((p) => p.id));
      return db.all('series').filter((x) => ids.has(x.pitchId)).map((sr) => {
        const sessions = seriesSessions(sr);
        const now = Date.now();
        const live = sessions.filter((b) => b.status !== 'cancelled');
        return { ...sr, pitchName: db.find('pitches', sr.pitchId)?.name || 'Removed pitch', sessions, played: live.filter((b) => b.startsAt + b.duration * 36e5 < now).length,
          upcoming: live.filter((b) => b.startsAt > now).length, cancelled: sessions.length - live.length, next: live.find((b) => b.startsAt > now) || null,
          revenue: sessions.reduce((t, b) => t + b.money.net, 0) };
      }).sort((a, b) => (a.status === 'active' ? 0 : 1) - (b.status === 'active' ? 0 : 1) || b.createdAt - a.createdAt);
    },
    previewSeries({ pitchId, startDate, hour, duration, weeks, discount = 0 }) {
      const p = dash.pitch(pitchId);
      const n = Math.max(1, Math.min(52, +weeks || 4));
      return Array.from({ length: n }, (_, k) => {
        const date = isoDate(addDays(parseISO(startDate), k * 7));
        const fr = freeFor(pitchId, date, +hour, +duration);
        return { date, ok: fr.ok, reason: fr.reason || '', price: sessionPrice(p, date, +hour, +duration, discount) };
      });
    },
    createSeries(d) {
      const p = dash.pitch(d.pitchId);
      const e = {};
      if (!d.team || d.team.trim().length < 2) e.team = 'Enter the team name.';
      if (!d.captain || d.captain.trim().length < 2) e.captain = 'Enter the captain’s name.';
      if (!/^\+?[0-9 ]{8,16}$/.test((d.phone || '').trim())) e.phone = 'Enter a valid phone number.';
      if (!d.startDate || d.startDate < isoDate(today())) e.startDate = 'Choose today or a later date.';
      if (!(+d.hour >= p.open[0] && +d.hour + +d.duration <= p.open[1])) e.hour = 'Pick a time inside the opening hours.';
      const disc = +(d.discount ?? 0);
      if (!(disc >= 0 && disc <= 50)) e.discount = 'Discount must be between 0% and 50%.';
      const weeks = d.kind === 'membership' ? 4 : +d.weeks;
      if (d.kind !== 'membership' && !(weeks >= 2 && weeks <= 52)) e.weeks = 'Choose 2 to 52 weeks.';
      if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e);
      const preview = dash.previewSeries({ pitchId: p.id, startDate: d.startDate, hour: +d.hour, duration: +d.duration, weeks, discount: disc / 100 });
      if (!preview.some((x) => x.ok)) throw new ApiError('None of those weeks are free at that time.', 'invalid', { hour: 'Every week is taken at this time' });
      const sr = db.insert('series', { id: `TS-${Math.random().toString(36).slice(2, 7).toUpperCase()}`, kind: d.kind === 'membership' ? 'membership' : 'fixed', pitchId: p.id, ownerId: p.ownerId,
        team: d.team.trim(), captain: d.captain.trim(), phone: d.phone.trim(), weekday: parseISO(d.startDate).getDay(), hour: +d.hour, duration: +d.duration, startDate: d.startDate, weeks,
        discount: disc / 100, payment: d.payment || 'Cash at venue', source: 'venue', status: 'active', renewals: 0, notes: (d.notes || '').trim() });
      const ids = addSessions(sr, p, preview.filter((x) => x.ok).map((x) => x.date), 1);
      db.log(me().id, 'series.created', `New ${sr.kind === 'membership' ? 'monthly membership' : `${weeks}-week booking`} for “${sr.team}” at ${p.name}: ${ids.length} session${ids.length === 1 ? '' : 's'}`, sr.id);
      return { series: sr, created: ids.length, skipped: preview.length - ids.length };
    },
    async cancelSeries(id) {
      const sr = db.find('series', id); assert(sr && canPitch(db.find('pitches', sr.pitchId)), 'You can only manage your own teams.');
      const up = seriesSessions(sr).filter((b) => b.status === 'upcoming');
      for (const b of up) {
        if (b.source === 'app') await cancelBooking(b.id, { reason: 'Weekly booking ended by the venue' });
        else db.update('bookings', b.id, { status: 'cancelled', refund: 0, cancelledBy: 'venue', reason: 'Team booking ended' });
      }
      db.update('series', id, { status: 'cancelled', cancelledAt: Date.now() });
      db.log(me().id, 'series.cancelled', `Ended ${sr.kind === 'membership' ? 'membership' : 'weekly booking'} “${sr.team}” — ${up.length} upcoming session${up.length === 1 ? '' : 's'} cancelled`, id);
      return { cancelled: up.length };
    },

    // ---- Check-in ----
    checkIn(raw) {
      const parsed = SA.services.bookings.parseCheckInCode(raw);
      if (!parsed) return { state: 'invalid', message: 'This isn’t a Super App booking code.' };
      if (!parsed.valid) return { state: 'invalid', message: 'This code has been changed. Ask the customer to open the booking in the Super App.' };
      if (parsed.series) return dash.checkPackage(parsed);
      const b = findBooking(parsed.id);
      if (!b) return { state: 'not-found', message: `No booking with the ID ${parsed.id}.` };
      return dash.checkOne(b, parsed.manual);
    },
    checkOne(b, manual = false) {
      if (!canPitch(db.find('pitches', b.pitchId))) return { state: 'wrong-venue', message: `This booking is for ${b.pitchName}, which isn’t one of your pitches.` };
      const out = { booking: b, manual: !!manual };
      const now = Date.now(), start = b.startsAt, end = start + b.duration * 36e5;
      if (b.status === 'cancelled') return { ...out, state: 'cancelled', message: 'This booking was cancelled.' };
      if (b.status === 'no-show') return { ...out, state: 'no-show', message: 'This booking was marked as a no-show.' };
      if (b.checkedIn || b.checkedInAt || b.status === 'checked-in') return { ...out, state: 'already', message: `Already checked in${b.checkedInAt ? ` at ${new Date(b.checkedInAt).toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' })}` : ''}.` };
      if (b.date !== isoDate(today()) && start - now > 6 * 36e5) return { ...out, state: 'wrong-day', message: `This booking is for ${SA.core.utils.fmtDate(b.date, 'long')} at ${fmtHour(b.hour)}.` };
      if (now > end) return { ...out, state: 'ended', message: 'This game has already finished.' };
      if (start - now > 60 * 6e4) return { ...out, state: 'early', message: `Kick-off is at ${fmtHour(b.hour)} — more than an hour from now.` };
      return { ...out, state: 'ok', message: start > now ? `Kick-off in ${Math.max(1, Math.round((start - now) / 6e4))} min.` : 'The game has started.' };
    },
    /** Weekly package QR: finds today's game in the package; once every game is played or missed, the code has expired */
    checkPackage(parsed) {
      const pk = SA.services.bookings.packageStatus(parsed.series);
      if (!pk) return { state: 'not-found', message: `No weekly package with the ID ${parsed.series}.` };
      if (!canPitch(db.find('pitches', pk.series.pitchId))) return { state: 'wrong-venue', message: 'This package is for a pitch that isn’t one of yours.' };
      const info = { package: pk, manual: !!parsed.manual };
      const first = findBooking(pk.sessions[0]?.id);
      if (pk.expired) return { ...info, booking: first, state: 'expired', message: `This package is used up: ${pk.used} of ${pk.total} games (${pk.played} played, ${pk.missed} missed). The QR code has expired.` };
      const t = isoDate(today());
      const todays = pk.sessions.find((x) => x.date === t && ['upcoming', 'played'].includes(x.state));
      if (!todays) { const nx = pk.next; return { ...info, booking: findBooking(nx.id), state: 'wrong-day', message: `No game in this package today. Next: game ${nx.no} of ${pk.total} on ${SA.core.utils.fmtDate(nx.date, 'long')} at ${fmtHour(nx.hour)}.` }; }
      const r = dash.checkOne(findBooking(todays.id), parsed.manual);
      return { ...r, ...info, message: `${r.message} Game ${todays.no} of ${pk.total} · ${pk.used} used so far.` };
    },
    arrivals(ownerId) {
      const t = isoDate(today());
      return ledger({ from: t, to: t, pitchIds: myPitches({ ownerId }).map((p) => p.id) }).filter((b) => b.status !== 'cancelled').sort((a, b) => a.startsAt - b.startsAt);
    },

    // ---- Offers & insights ----
    forecast, quietHours, occupancyGrid,
    promos({ ownerId } = {}) {
      const ids = new Set(myPitches({ ownerId, includeDeleted: true }).map((p) => p.id));
      return db.all('promos').filter((x) => ids.has(x.pitchId)).map((x) => {
        const hits = db.all('bookings').filter((b) => b.pitchId === x.pitchId && b.status !== 'cancelled' && b.source === 'app' && b.createdAt >= x.createdAt && b.date >= x.from && b.date <= x.to && x.days.includes(parseISO(b.date).getDay()) && b.hour >= x.hourFrom && b.hour < x.hourTo);
        const ended = x.status !== 'active' || x.to < isoDate(today());
        return { ...x, pitchName: db.find('pitches', x.pitchId)?.name || 'Removed pitch', bookings: hits.length, revenue: hits.reduce((t, b) => t + b.amount, 0), ended };
      }).sort((a, b) => a.ended - b.ended || b.createdAt - a.createdAt);
    },
    createPromo(d) {
      const p = dash.pitch(d.pitchId);
      const e = {};
      const days = (d.days || []).map(Number).filter((x) => x >= 0 && x <= 6);
      const pct = +d.pct, hf = +d.hourFrom, ht = +d.hourTo;
      if (!days.length) e.days = 'Pick at least one day.';
      if (!(hf >= p.open[0] && ht <= p.open[1] && ht > hf)) e.hourFrom = `Choose hours between ${fmtHour(p.open[0])} and ${fmtHour(p.open[1])}.`;
      if (!(pct >= 5 && pct <= 50)) e.pct = 'Discount must be between 5% and 50%.';
      if (!d.from || d.from < isoDate(today())) e.from = 'Start today or later.';
      if (!d.to || d.to < d.from) e.to = 'End on or after the start date.';
      else if ((parseISO(d.to) - parseISO(d.from)) / DAY > 92) e.to = 'Offers can run for up to 3 months.';
      if (!Object.keys(e).length) {
        const clash = db.all('promos').find((x) => x.pitchId === p.id && x.status === 'active' && x.to >= d.from && x.from <= d.to && x.days.some((w) => days.includes(w)) && x.hourFrom < ht && hf < x.hourTo);
        if (clash) e.hourFrom = `Overlaps your ${Math.round(clash.pct * 100)}% offer (${fmtHour(clash.hourFrom)}–${fmtHour(clash.hourTo)}). End that one first.`;
      }
      if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e);
      const x = db.insert('promos', { pitchId: p.id, ownerId: p.ownerId, days: [...new Set(days)].sort(), hourFrom: hf, hourTo: ht, pct: pct / 100, from: d.from, to: d.to, label: (d.label || '').trim() || 'Off-peak deal', status: 'active' }, 'pm');
      db.log(me().id, 'promo.created', `Started ${pct}% off at ${p.name} (${fmtHour(hf)}–${fmtHour(ht)})`, x.id);
      return x;
    },
    endPromo(id) {
      const x = db.find('promos', id); assert(x && canPitch(db.find('pitches', x.pitchId)), 'You can only manage your own offers.');
      db.update('promos', id, { status: 'ended', endedAt: Date.now() });
      db.log(me().id, 'promo.ended', `Ended ${Math.round(x.pct * 100)}% offer at ${db.find('pitches', x.pitchId)?.name}`, id);
    },

    // ---- Statement & VAT ----
    vat: () => db.settings().vat || 0,
    vatPart,
    statement({ ownerId, month }) {
      const [y, m] = month.split('-').map(Number);
      const from = isoDate(new Date(y, m - 1, 1));
      const last = new Date(y, m, 0);
      const to = isoDate(last > today() ? today() : last);
      const a = me();
      const acct = isAdmin(a) && ownerId ? db.find('accounts', ownerId) : a;
      const s = summary({ from, to, ownerId: isAdmin(a) ? (ownerId || a.id) : undefined });
      const w = wallet({ ownerId: isAdmin(a) ? (ownerId || a.id) : undefined });
      const payouts = w.payouts.filter((p) => isoDate(new Date(p.createdAt)) >= from && isoDate(new Date(p.createdAt)) <= isoDate(last));
      return { account: acct, from, to, month, monthEnd: isoDate(last), s, daily: series(s, from, to), payouts, vat: dash.vat(), partial: last > today() };
    },

    // ---- Alerts (bell + sidebar badges) ----
    alerts({ ownerId } = {}) {
      const out = [];
      if (auth.current()?.role === 'printer') { // not used: the factory has its own system (#/factory)
        const todo = 0;
        return todo ? [{ key: 'print', icon: 'package', tone: 'blaze', count: todo, title: `${todo} item${todo > 1 ? 's' : ''} to print`, text: 'New and in-progress print orders.', href: '#/dash/print' }] : [];
      }
      const st = dash.myStore(ownerId);
      if (st && !st.official) {
        const open = dash.orders({ ownerId, from: isoDate(addDays(today(), -14)) }).filter((o) => o.storeId === st.id && ['confirmed', 'packed'].includes(o.status));
        if (open.length) out.push({ key: 'orders', icon: 'package', tone: 'blaze', count: open.length, title: `${open.length} order${open.length > 1 ? 's' : ''} to send`, text: 'Pack and ship them so the money reaches your store wallet.', href: '#/dash/orders?status=open' });
      }
      try { const me_ = ownerId || auth.current()?.id; const rt = (SA.services.returns?.list({ ownerId: me_ }) || []).filter((r) => ['requested', 'approved'].includes(r.status)); if (rt.length) out.push({ key: 'returns', icon: 'refresh', tone: 'gold', count: rt.length, title: `${rt.length} return${rt.length > 1 ? 's' : ''} to handle`, text: 'Answer within 2 working days.', href: '#/seller/returns' }); } catch { /* returns not loaded */ }
      const low = dash.lowStock(ownerId);
      if (low.length) out.push({ key: 'stock', icon: 'alert', tone: 'gold', count: low.length, title: `${low.length} product${low.length > 1 ? 's' : ''} low on stock`, text: low.slice(0, 3).map((p) => `${p.name} (${p.stock})`).join(', '), href: '#/dash/store?low=1' });
      const since = Date.now() - 30 * DAY;
      const unreplied = dash.reviews({ ownerId }).filter((r) => !r.reply && !r.hidden && r.createdAt >= since);
      if (unreplied.length) out.push({ key: 'reviews', icon: 'star', tone: 'info', count: unreplied.length, title: `${unreplied.length} review${unreplied.length > 1 ? 's' : ''} waiting for a reply`, text: 'Replies show under the review on your pitch page.', href: '#/dash/reviews?filter=unreplied' });
      if (isAdmin()) {
        const rep = db.all('reviews').filter((r) => r.reported && !r.reported.resolved && !r.hidden);
        if (rep.length) out.push({ key: 'reported', icon: 'shield', tone: 'danger', count: rep.length, title: `${rep.length} reported review${rep.length > 1 ? 's' : ''}`, text: 'Partners asked Super App to check these.', href: '#/dash/reviews?filter=reported' });
        if (SA.services.pod) {
          const q = SA.services.pod.queue().length + db.all('reports').filter((r) => r.status === 'open').length;
          if (q) out.push({ key: 'pod-review', icon: 'image', tone: 'gold', count: q, title: `${q} design${q > 1 ? 's' : ''} to review`, text: 'New designs, spot checks and copyright reports.', href: '#/dash/pod-review' });
          const kyc = db.all('sellers').filter((s) => s.kyc === 'pending').length;
          if (kyc) out.push({ key: 'kyc', icon: 'shield', tone: 'info', count: kyc, title: `${kyc} seller ID check${kyc > 1 ? 's' : ''}`, text: 'Verify before their first withdrawal.', href: '#/dash/pod-review?tab=sellers' });
        }
      }
      if (SA.services.tournaments) {
        const todo = SA.services.tournaments.mine({ ownerId }).filter((t) => (['open', 'full'].includes(t.status) && isoDate(today()) > t.regDeadline && (t.format === 'knockout' ? [4, 8, 16].includes(t.teams.length) : t.teams.length >= 3)) || (t.status === 'running' && t.fixtures.some((f) => f.status !== 'played' && f.date <= isoDate(today()) && parseISO(f.date).getTime() + (f.hour + 1) * 36e5 < Date.now())));
        if (todo.length) out.push({ key: 'tournaments', icon: 'trophy', tone: 'blaze', count: todo.length, title: `${todo.length} tournament${todo.length > 1 ? 's' : ''} need${todo.length > 1 ? '' : 's'} you`, text: 'Draw fixtures or enter today’s results.', href: todo.length === 1 ? `#/dash/tournaments/${todo[0].id}` : '#/dash/tournaments' });
      }
      const arr = dash.arrivals(ownerId).filter((b) => ['upcoming', 'in-progress'].includes(b.status) && b.startsAt - Date.now() < 2 * 36e5);
      if (arr.length) out.push({ key: 'arrivals', icon: 'qr', tone: 'turf', count: arr.length, title: `${arr.length} arrival${arr.length > 1 ? 's' : ''} in the next 2 hours`, text: 'Scan their booking QR at reception.', href: '#/dash/checkin' });
      return out;
    },

    // ---- Admin ----
    setCommission({ bookings, store }) {
      assert(isAdmin(), 'Only Super App admins can change commission rates.');
      const b = +bookings / 100, s = +store / 100;
      if (!(b >= 0 && b <= 0.5 && s >= 0 && s <= 0.5)) throw new ApiError('Commission must be between 0% and 50%.', 'invalid');
      db.setSettings({ commission: { bookings: b, store: s } });
      db.log(me().id, 'settings.commission', `Set commission to ${+bookings}% on bookings and ${+store}% on store sales`);
    },
    setVat(v) {
      assert(isAdmin(), 'Only Super App admins can change the VAT rate.');
      const r = +v / 100;
      if (!(v !== '' && r >= 0 && r <= 0.3)) throw new ApiError('VAT must be between 0% and 30%.', 'invalid');
      db.setSettings({ vat: r });
      db.log(me().id, 'settings.vat', `Set VAT to ${+v}%`);
    },
    partners() {
      assert(isAdmin());
      const from = isoDate(addDays(today(), -29)), to = isoDate(today());
      return db.all('accounts').filter((a) => a.role === 'owner').map((acc) => {
        const s = summary({ from, to, ownerId: acc.id });
        return { ...acc, pitches: s.pitches.length, closed: s.pitches.filter((p) => p.status === 'closed').length, store: db.all('stores').find((x) => x.ownerId === acc.id) || null, net30: s.net, gross30: s.gross, commission30: s.commission + s.storeCommission, storeNet30: s.storeNet, occupancy: s.occupancy };
      });
    },
    activity(limit = 40) {
      const a = me();
      const mineIds = new Set([...myPitches({ includeDeleted: true }).map((p) => p.id), ...myStores().map((s) => s.id)]);
      const owns = (ref) => {
        if (!ref) return false;
        if (mineIds.has(ref)) return true;
        const rec = db.find('products', ref) || db.find('reviews', ref) || db.find('series', ref) || db.find('promos', ref);
        if (rec) return mineIds.has(rec.storeId || rec.pitchId);
        if (/^ORD-/.test(ref)) return db.all('sales').some((x) => x.orderId === ref && mineIds.has(x.storeId));
        const b = findBooking(ref); return !!(b && mineIds.has(b.pitchId));
      };
      return db.all('activity').filter((x) => isAdmin(a) || x.actor === a.id || owns(x.ref)).slice(0, limit);
    },
  };

  SA.services.dash = dash;
  // Keep monthly memberships rolling (would be a nightly job on the backend)
  try { renewMemberships(); } catch (e) { console.warn('Membership renewal skipped', e); }
})(window.SA);
