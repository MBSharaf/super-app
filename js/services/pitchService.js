/**
 * Pitches: search, sort, details, availability. Reads the platform DB, so anything a partner
 * changes in the dashboard (details, price, hours, closures, walk-ins) shows up here immediately.
 * Future API: GET /pitches, GET /pitches/:id, GET /pitches/:id/slots?date=
 */
(function (SA) {
  const store = SA.core.store;
  const { request, ApiError } = SA.services.http;
  const { haversineKm, seeded, isoDate, today, parseISO, addDays } = SA.core.utils;
  const db = SA.services.db;

  const all = () => db.all('pitches').filter((p) => !p.deleted);
  // A pitch is listed when it is open and its owner's account isn't suspended by Super App
  const ownerOk = (p) => db.find('accounts', p.ownerId)?.status !== 'suspended';
  const reviewed = (p) => !p.review || p.review === 'approved';
  const live = () => all().filter((p) => p.status === 'active' && ownerOk(p) && reviewed(p));
  const byId = (id) => all().find((x) => x.id === id);

  const origin = () => store.get().location || SA.config.fallbackLocation;
  const withDistance = (p, o = origin()) => ({ ...p, distanceKm: haversineKm(o, p), etaMin: Math.round(haversineKm(o, p) * 2.6 + 4) });

  /** Peak hours (6 PM onward and weekends) use the peak price. */
  const basePrice = (dateISO, hour, snap) => {
    const d = parseISO(dateISO).getDay();
    return hour >= 18 || d === 5 || d === 6 ? snap.peakPrice : snap.price;
  };
  /** Off-peak offer created by the owner in the dashboard (Insights), if one covers this hour */
  const promoAt = (p, dateISO, hour) => {
    const wd = parseISO(dateISO).getDay();
    return db.all('promos').find((x) => x.pitchId === p.id && x.status === 'active' && dateISO >= x.from && dateISO <= x.to && x.days.includes(wd) && hour >= x.hourFrom && hour < x.hourTo) || null;
  };
  /** Live price (with offers). Pass a snapshot to price historical bookings at the old list price. */
  const priceAt = (p, dateISO, hour, snap = p) => {
    const base = basePrice(dateISO, hour, snap);
    if (snap !== p) return base;
    const pr = promoAt(p, dateISO, hour);
    return pr ? Math.round((base * (1 - pr.pct)) / 10) * 10 : base; // offer prices land on whole tens
  };
  const activePromos = (p) => { const t = isoDate(today()); return db.all('promos').filter((x) => x.pitchId === p.id && x.status === 'active' && x.to >= t); };

  /** Cancellation policies a partner can choose per pitch. full/half = hours before kick-off. */
  const POLICIES = {
    flexible: { key: 'flexible', name: 'Flexible', full: 2, half: 0, summary: 'Full refund up to 2 hours before kick-off, then 50% until kick-off.' },
    standard: { key: 'standard', name: 'Standard', full: 6, half: 0, summary: 'Full refund up to 6 hours before kick-off, then 50% until kick-off.' },
    moderate: { key: 'moderate', name: 'Moderate', full: 24, half: 6, summary: 'Full refund up to 24 hours before, 50% up to 6 hours before, no refund after that.' },
    strict: { key: 'strict', name: 'Strict', full: 48, half: 24, summary: 'Full refund up to 48 hours before, 50% up to 24 hours before, no refund after that.' },
  };
  const policyOf = (key) => POLICIES[key] || POLICIES.standard;
  /** Share of the amount refunded when the customer cancels `hoursLeft` hours before kick-off */
  const refundShare = (key, hoursLeft) => {
    const pol = policyOf(key);
    if (hoursLeft >= pol.full) return 1;
    if (hoursLeft >= pol.half) return 0.5;
    return 0;
  };

  /**
   * Bookings made by other customers before this device existed are modelled deterministically:
   * the same predicate generates the dashboard ledger, so both sides always agree.
   */
  const syntheticBooked = (p, dateISO, h, { raw = false } = {}) => {
    if (!p.seed || !p.seedSnapshot) return false;
    const [o, c] = p.seedSnapshot.open;
    if (h < o || h >= c) return false;
    const d = parseISO(dateISO).getTime();
    if (d < db.raw().platformSince) return false;
    // New activity is only modelled up to two weeks ahead
    if (d > addDays(today(), 14).getTime()) return false;
    const r = seeded(`${p.id}|${dateISO}|${h}`);
    let busy = h >= 19 && h <= 23 ? 0.55 : 0.3;
    // People book closer to the date: future days fill up gradually
    const ahead = (d - today().getTime()) / 864e5;
    if (ahead > 0) busy *= Math.max(0.12, 1 - ahead / 9);
    if (!(r < busy)) return false;
    // Hours a tournament reserved were never open to other customers
    if (db.all('closures').some((c) => c.tournamentId && c.pitchId === p.id && dateISO >= c.from && dateISO <= c.to && (!c.hours || c.hours.includes(h)))) return false;
    return raw || db.raw().bookingOverrides[`SB-${p.id}-${dateISO}-${h}`]?.status !== 'cancelled';
  };

  const closureFor = (p, dateISO, h) => db.all('closures').find((c) => c.pitchId === p.id && dateISO >= c.from && dateISO <= c.to && (!c.hours || !c.hours.length || c.hours.includes(h)));

  /** Hour-by-hour status: available | booked | yours | blocked | closed | past */
  const slotsFor = (p, dateISO) => {
    if (!p) return [];
    const now = new Date();
    const isToday = dateISO === isoDate(today());
    const me = store.get().user?.id || 'u-001';
    const mine = store.get().bookings.filter((b) => b.pitchId === p.id && b.date === dateISO && b.status !== 'cancelled');
    const real = db.all('bookings').filter((b) => b.pitchId === p.id && b.date === dateISO && b.status !== 'cancelled');
    const slots = [];
    for (let h = p.open[0]; h < p.open[1]; h++) {
      let status = 'available', booking = null;
      const covering = real.find((b) => h >= b.hour && h < b.hour + b.duration);
      if (p.status !== 'active') status = 'closed';
      else if (covering) { status = covering.customer?.id === me ? 'yours' : 'booked'; booking = covering.id; }
      else if (mine.some((b) => h >= b.hour && h < b.hour + b.duration)) status = 'yours';
      else if (syntheticBooked(p, dateISO, h)) { status = 'booked'; booking = `SB-${p.id}-${dateISO}-${h}`; }
      else if (closureFor(p, dateISO, h)) status = 'blocked';
      if (status === 'available' && isToday && h <= now.getHours()) status = 'past';
      const base = basePrice(dateISO, h, p), price = priceAt(p, dateISO, h);
      slots.push({ hour: h, status, booking, price, base, deal: price < base ? Math.round((1 - price / base) * 100) : 0, peak: base === p.peakPrice && p.peakPrice !== p.price });
    }
    return slots;
  };

  const availableCount = (p, dateISO) => slotsFor(p, dateISO).filter((s) => s.status === 'available').length;
  const nextFree = (p) => {
    for (let i = 0; i < 7; i++) {
      const d = isoDate(addDays(today(), i));
      const s = slotsFor(p, d).find((x) => x.status === 'available');
      if (s) return { date: d, hour: s.hour };
    }
    return null;
  };

  const pitchService = {
    priceAt, basePrice, promoAt, activePromos, syntheticBooked, closureFor, POLICIES, policyOf, refundShare,
    /**
     * @param {{q?:string, format?:string, maxPrice?:number, minRating?:number, indoor?:'indoor'|'outdoor', date?:string, sort?:'distance'|'price'|'rating'|'availability'}} f
     */
    list: (f = {}) => request(() => {
      const date = f.date || isoDate(today());
      let out = live().map((p) => ({ ...withDistance(p), freeSlots: availableCount(p, date), next: nextFree(p) }));
      if (f.q) { const q = f.q.toLowerCase(); out = out.filter((p) => `${p.name} ${p.area} ${p.city} ${p.address}`.toLowerCase().includes(q)); }
      if (f.format) out = out.filter((p) => p.format === f.format);
      if (f.maxPrice) out = out.filter((p) => p.price <= f.maxPrice);
      if (f.minRating) out = out.filter((p) => p.rating >= f.minRating);
      if (f.indoor === 'indoor') out = out.filter((p) => p.indoor);
      if (f.indoor === 'outdoor') out = out.filter((p) => !p.indoor);
      if (f.surface === 'grass') out = out.filter((p) => /grass/i.test(p.surface));
      if (f.onlyAvailable) out = out.filter((p) => p.freeSlots > 0);
      const sorters = {
        distance: (a, b) => a.distanceKm - b.distanceKm,
        price: (a, b) => a.price - b.price,
        rating: (a, b) => b.rating - a.rating || b.reviews - a.reviews,
        availability: (a, b) => b.freeSlots - a.freeSlots,
      };
      out.sort(sorters[f.sort] || sorters.distance);
      return out;
    }),
    get: (id) => request(() => {
      const p = byId(id);
      if (!p) throw new ApiError('We couldn’t find that pitch. It may have been removed.', 'not_found');
      return withDistance(p);
    }),
    getSync: (id) => byId(id),
    bookable: (p) => !!p && p.status === 'active' && ownerOk(p) && reviewed(p),
    slots: (id, dateISO) => request(() => slotsFor(byId(id), dateISO), { latency: 160 }),
    slotsSync: (id, dateISO) => slotsFor(byId(id), dateISO),
    /** Latest reviews (with the owner's reply) — written by players after a played booking */
    reviews: (id, n = 6) => request(() => db.all('reviews').filter((r) => r.pitchId === id && !r.hidden).sort((a, b) => b.createdAt - a.createdAt).slice(0, n)),
    nearest: (n = 4) => request(() => live().map((p) => ({ ...withDistance(p), freeSlots: availableCount(p, isoDate(today())) })).sort((a, b) => a.distanceKm - b.distanceKm).slice(0, n)),
    toggleFavorite(id) { store.set((s) => ({ favoritesPitches: s.favoritesPitches.includes(id) ? s.favoritesPitches.filter((x) => x !== id) : [...s.favoritesPitches, id] })); },
    isFavorite: (id) => store.get().favoritesPitches.includes(id),
    facilities: () => SA.data.pitches.facilities,
  };

  SA.services.pitches = pitchService;
})(window.SA);
