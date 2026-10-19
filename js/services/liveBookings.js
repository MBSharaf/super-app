/**
 * Pitches and bookings from the real API (backend Phase 3). Loaded after pitchService and bookingService.
 *
 * In API mode the pitch list, the pitch page, the hour grid, checkout and "My bookings" read and write the
 * server; the pages keep their design and call the same service methods as before. This file keeps a small
 * cache so the methods the pages call synchronously (getSync, slotsSync, quote) still answer straight away:
 * the async methods (list, get, bookings.get/list) fill it first.
 *
 * Site ids are `p-<slug>` (as in the demo data); the API uses the slug. Booking ids are the booking codes.
 * Not on the server yet (later phases) and still demo-only: weekly packages, promo codes, reviews, waitlist.
 */
(function (SA) {
  const api = SA.services.api;
  const P = SA.services.pitches;
  const B = SA.services.bookings;
  const { ApiError } = SA.services.http;
  const { haversineKm, isoDate, today, parseISO, addDays } = SA.core.utils;
  const store = SA.core.store;

  const demo = { pitches: { ...P }, bookings: { ...B } };
  const pitches = new Map();   // site id → pitch in the site's shape
  const days = new Map();      // `${id}|${date}` → hour slots
  const bookings = new Map();  // booking code → booking in the site's shape

  const live = () => api.isLive();
  /** Async methods decide the mode once it is known (the page may ask before detection finishes) */
  const ready = (fn) => async (...args) => { await api.ready; return fn(...args); };
  const ar = () => SA.i18n?.lang === 'ar';
  const idOf = (slug) => `p-${slug}`;
  const slugOf = (id) => String(id).replace(/^p-/, '');
  const isLivePitch = (id) => live() && pitches.has(id);

  const SURFACE = { ArtificialTurf: 'Artificial turf', NaturalGrass: 'Natural grass', IndoorCourt: 'Indoor court' };
  const photo = (ph) => (!ph ? 'nightMatch' : ph.url ? `${location.origin}${ph.url}` : ph.key);
  const origin = () => store.get().location || SA.config.fallbackLocation;
  const withDistance = (p) => { const km = haversineKm(origin(), p); return { ...p, distanceKm: km, etaMin: Math.round(km * 2.6 + 4) }; };

  /** API card → the site's pitch shape (details are added by fromDetails) */
  const fromCard = (c) => {
    const prev = pitches.get(idOf(c.slug)) || {};
    const p = withDistance({
      ...prev, id: idOf(c.slug), slug: c.slug, live: true, status: 'active', name: c.name, venue: c.venueName,
      area: ar() ? c.areaAr : c.areaEn, city: c.governorateEn, lat: +c.lat, lng: +c.lng,
      format: `${c.format}-a-side`, players: c.format * 2, surface: SURFACE[c.surface] || c.surface, indoor: c.isIndoor,
      price: +c.pricePerHour, peakPrice: +(c.peakPricePerHour ?? c.pricePerHour), rating: +c.rating, reviews: c.reviewsCount,
      images: prev.images || [photo(c.photo)], facilities: c.facilities || [], tags: prev.tags || [],
      weekly: { enabled: false }, open: prev.open || [9, 24],
    });
    pitches.set(p.id, p);
    return p;
  };

  /** Today's opening hours stand for the pitch on the page (the grid shows each day's own hours) */
  const openToday = (hours) => {
    const h = hours.find((x) => x.dayOfWeek === new Date().getDay() && !x.isClosed) || hours.find((x) => !x.isClosed);
    return h ? [h.openHour, h.closeHour] : [9, 24];
  };

  const fromDetails = (d) => {
    const p = fromCard(d.card);
    Object.assign(p, {
      address: d.address, about: (ar() ? d.descriptionAr : d.descriptionEn) || d.descriptionEn || '',
      images: d.photos.length ? d.photos.map(photo) : p.images, cancelPolicy: d.cancellationPolicy.key,
      open: openToday(d.openingHours), allowCash: d.allowCash, company: d.partnerName, ownerId: null,
      // Weekly packages: the sizes the admin approved (each with its own discount)
      weekly: d.packageOptions.length
        ? { enabled: true, discount: Math.max(...d.packageOptions.map((o) => +o.discountPercent)) / 100, sizes: d.packageOptions.map((o) => o.matchCount), options: d.packageOptions }
        : { enabled: false, discount: 0 },
    });
    return p;
  };

  const STATUS = { free: 'available', booked: 'booked', held: 'booked', closed: 'blocked', past: 'past' };
  const mine = (pitchId, date, hour) => [...bookings.values()].some((b) => b.pitchId === pitchId && b.status === 'confirmed' && b.date === date && hour >= b.hour && hour < b.hour + b.duration);

  /** One availability day → the site's slots */
  const toSlots = (id, day) => day.hours.map((h) => {
    const status = h.status === 'booked' && mine(id, h.date, h.calendarHour) ? 'yours' : STATUS[h.status] || 'blocked';
    return { hour: h.hour, status, price: +h.price, base: +h.basePrice, deal: Math.round(+h.offerPercent), peak: h.isPeak, waiting: h.waiting };
  });

  const loadDays = async (id, from = isoDate(today()), n = 14) => {
    const r = await api.get(`/pitches/${slugOf(id)}/availability?from=${from}&days=${n}`);
    r.days.forEach((d) => days.set(`${id}|${d.date}`, toSlots(id, d)));
    return r.days;
  };

  /* ---------------- Pitches ---------------- */

  const SORTERS = {
    distance: (a, b) => a.distanceKm - b.distanceKm,
    price: (a, b) => a.price - b.price,
    rating: (a, b) => b.rating - a.rating || b.reviews - a.reviews,
    availability: (a, b) => (b.freeSlots ?? -1) - (a.freeSlots ?? -1),
  };

  /** The server filters by text, price and format; the rest is applied here exactly as in demo mode */
  const search = async (f = {}) => {
    const date = f.date || isoDate(today());
    const qs = new URLSearchParams({ pageSize: '50' });
    if (f.q) qs.set('q', f.q);
    if (f.maxPrice) qs.set('maxPrice', f.maxPrice);
    if (parseInt(f.format, 10)) qs.set('format', parseInt(f.format, 10));
    const res = await api.get(`/pitches?${qs}`);
    let out = (res.items || res).map(fromCard);
    await Promise.all(out.map((p) => loadDays(p.id, date, 1).then(() => { p.freeSlots = (days.get(`${p.id}|${date}`) || []).filter((s) => s.status === 'available').length; }).catch(() => { p.freeSlots = null; })));
    if (f.minRating) out = out.filter((p) => p.rating >= f.minRating);
    if (f.indoor === 'indoor') out = out.filter((p) => p.indoor);
    if (f.indoor === 'outdoor') out = out.filter((p) => !p.indoor);
    if (f.surface === 'grass') out = out.filter((p) => /grass/i.test(p.surface));
    if (f.onlyAvailable) out = out.filter((p) => p.freeSlots > 0);
    return out.sort(SORTERS[f.sort] || SORTERS.distance);
  };

  const getPitch = async (id) => {
    let d;
    try { d = await api.get(`/pitches/${encodeURIComponent(slugOf(id))}`); } catch (err) {
      if (err.status === 404) throw new ApiError('We couldn’t find that pitch. It may have been removed.', 'not_found');
      throw err;
    }
    const p = fromDetails(d);
    await Promise.all([loadDays(p.id), loadWaits().catch(() => [])]);
    return { ...p };
  };

  Object.assign(P, {
    list: ready((f) => (live() ? search(f) : demo.pitches.list(f))),
    get: ready((id) => (live() ? getPitch(id) : demo.pitches.get(id))),
    getSync: (id) => (isLivePitch(id) ? pitches.get(id) : demo.pitches.getSync(id)),
    slots: ready(async (id, date) => { if (!live()) return demo.pitches.slots(id, date); if (!days.has(`${id}|${date}`)) await loadDays(id, date, 1); return days.get(`${id}|${date}`) || []; }),
    slotsSync: (id, date) => (isLivePitch(id) ? days.get(`${id}|${date}`) || [] : demo.pitches.slotsSync(id, date)),
    nearest: ready(async (n = 4) => (live() ? (await search({ sort: 'distance' })).slice(0, n) : demo.pitches.nearest(n))),
    // Reviews and owner offers arrive with Phase 3.6; offer prices already show on each hour
    reviews: (id, n = 6) => (isLivePitch(id) ? api.get(`/pitches/${slugOf(id)}/reviews?pageSize=${n}`).then((r) => (r.items || r).map(fromReview)).catch(() => []) : demo.pitches.reviews(id, n)),
    activePromos: (p) => (p?.live && live() ? [] : demo.pitches.activePromos(p)),
    bookable: (p) => (p?.live && live() ? p.status === 'active' : demo.pitches.bookable(p)),
  });

  /* ---------------- Bookings ---------------- */

  const startsAt = (b) => parseISO(b.date).getTime() + b.hour * 36e5;

  /** API booking → the site's booking shape */
  const fromBooking = (r) => {
    const id = idOf(r.pitchSlug);
    const p = pitches.get(id) || {};
    const prev = bookings.get(r.code) || {};
    const b = {
      id: r.code, code: r.code, live: true, pitchId: id, pitchName: r.pitchName, address: r.address,
      area: p.area || '', city: p.city || '', image: p.images?.[0] || 'nightMatch', format: p.format || '',
      date: r.date, hour: r.startHour, duration: r.duration, players: prev.players || p.players || 10,
      listPrice: +r.pitchAmount, pitchTotal: +r.pitchAmount, serviceFee: +r.serviceFee, total: +r.total,
      discount: 0, discountAmount: 0, promo: null, publishedMatchId: prev.publishedMatchId || null,
      payment: r.payAtVenue ? { method: 'cash', label: 'Pay at the venue' } : { method: 'card', label: 'Paid in the app' },
      status: r.status.startsWith('Cancelled') ? 'cancelled' : 'confirmed', checkedIn: ['CheckedIn', 'Played'].includes(r.status),
      refund: +r.refundAmount, createdAt: Date.parse(r.createdAt), cancelledAt: r.cancelledAt ? Date.parse(r.cancelledAt) : 0,
      policy: r.policyKey, qrToken: r.qrToken || prev.qrToken || null,
      seriesId: r.packageCode || null, sessionNo: prev.sessionNo || null, seriesSize: prev.seriesSize || null,
    };
    const pk = r.packageCode && packs.get(r.packageCode);
    if (pk) Object.assign(b, { sessionNo: pk.matches.find((m) => m.code === r.code)?.number || null, seriesSize: pk.matchCount, discountAmount: +(pk.matches.find((m) => m.code === r.code)?.discountAmount || 0) });
    b.startsAt = startsAt(b);
    bookings.set(b.id, b);
    return b;
  };

  const hold = async ({ pitchId, date, hour, duration }) => {
    const h = await api.post('/bookings/hold', { pitchSlug: slugOf(pitchId), date, hour, duration });
    return { listPrice: +h.pitchAmount, pitchTotal: +h.pitchAmount, serviceFee: +h.serviceFee, total: +h.total, discount: 0, discountAmount: 0, promo: null, expiresAt: Date.parse(h.expiresAt) };
  };

  /** Price from the hour grid (the server confirms it when the hours are held at checkout) */
  const quote = ({ pitchId, date, hour, duration, code = '' }) => {
    if (code) throw new ApiError('Promo codes aren’t available yet. They arrive with online payments.', 'invalid');
    const p = pitches.get(pitchId);
    const slots = days.get(`${pitchId}|${date}`) || [];
    let list = 0;
    for (let h = hour; h < hour + duration; h++) list += slots.find((s) => s.hour === h)?.price ?? p.price;
    const serviceFee = Math.round(list * SA.services.db.settings().serviceFee);
    const total = list + serviceFee;
    return { listPrice: list, discountAmount: 0, discount: 0, promo: null, platformOff: 0, pitchTotal: list, serviceFee, total, perPlayer: Math.ceil(total / p.players) };
  };

  const create = async ({ pitchId, date, hour, duration, payment, players, waiver = false }) => {
    SA.services.legal?.requireWaiver(waiver, 'booking');
    const r = await api.post('/bookings', { pitchSlug: slugOf(pitchId), date, hour, duration, payAtVenue: payment?.method === 'cash', acceptWaiver: !!waiver });
    [...days.keys()].filter((k) => k.startsWith(`${pitchId}|`)).forEach((k) => days.delete(k));
    const b = fromBooking(r);
    b.players = players || b.players;
    return b;
  };

  const myList = async () => {
    const tab = (t) => api.get(`/bookings/mine?tab=${t}&pageSize=50`).then((r) => r.items || r);
    const raw = await Promise.all([tab('upcoming'), tab('past'), tab('cancelled')]);
    // The rows show each pitch's area and photo
    const missing = [...new Set(raw.flat().map((r) => idOf(r.pitchSlug)))].filter((id) => !pitches.has(id));
    await Promise.all(missing.map((id) => getPitch(id).catch(() => null)));
    const [upcoming, past, cancelled] = raw.map((list) => list.map(fromBooking));
    const now = Date.now();
    const mark = (b) => ({ ...b, isPast: b.startsAt + b.duration * 36e5 < now });
    return { upcoming: upcoming.map(mark), past: [...past, ...cancelled].map(mark).sort((a, b) => b.startsAt - a.startsAt) };
  };

  const getBooking = async (id) => {
    let r;
    try { r = await api.get(`/bookings/${encodeURIComponent(id)}`); } catch (err) {
      if (err.status === 404) throw new ApiError('Booking not found.', 'not_found');
      throw err;
    }
    // The ticket shows the pitch's address and photo, and a package match shows its package
    if (!pitches.has(idOf(r.pitchSlug))) await getPitch(idOf(r.pitchSlug)).catch(() => null);
    if (r.packageCode) await loadPackage(r.packageCode).catch(() => null);
    const mineReview = await api.get(`/bookings/${encodeURIComponent(r.code)}/review`).catch(() => null);
    if (mineReview) myReviews.set(r.code, fromReview(mineReview));
    return { ...fromBooking(r) };
  };

  /* ---------------- Weekly packages ---------------- */

  const packs = new Map();   // package code → GET /packages/{code}
  const loadPackage = async (code) => { const pk = await api.get(`/packages/${encodeURIComponent(code)}`); packs.set(code, pk); return pk; };
  const optionFor = (p, weeks) => p?.weekly?.options?.find((o) => o.matchCount === +weeks);

  /** Estimate for the pitch page (hours not loaded yet count as free; checkout asks the server for the exact quote) */
  const quoteSeries = ({ pitchId, date, hour, duration, weeks }) => {
    const p = pitches.get(pitchId);
    const option = optionFor(p, weeks);
    if (!option) throw new ApiError('This pitch doesn’t offer this package.', 'not_allowed');
    const discount = +option.discountPercent / 100, rate = SA.services.db.settings().serviceFee;
    const sessions = [];
    for (let k = 0; sessions.filter((s) => s.available).length < weeks && k < weeks + 4; k++) {
      const d = isoDate(addDays(parseISO(date), 7 * k));
      const slots = days.get(`${pitchId}|${d}`);
      let list = 0, free = true;
      for (let h = hour; h < hour + duration; h++) {
        const s = slots?.find((x) => x.hour === h);
        if (slots && (!s || s.status !== 'available')) free = false;
        list += s?.price ?? (h >= 18 || [5, 6].includes(parseISO(d).getDay()) ? p.peakPrice : p.price);
      }
      const pitchTotal = Math.round(list * (1 - discount)), serviceFee = Math.round(pitchTotal * rate);
      sessions.push(free ? { date: d, available: true, listPrice: list, discountAmount: list - pitchTotal, pitchTotal, serviceFee, total: pitchTotal + serviceFee } : { date: d, available: false });
    }
    return seriesTotals(sessions, discount);
  };

  const seriesTotals = (sessions, discount) => {
    const ok = sessions.filter((s) => s.available);
    const sum = (k) => ok.reduce((t, s) => t + s[k], 0);
    return { sessions, booked: ok.length, skipped: sessions.length - ok.length, discount, listPrice: sum('listPrice'), discountAmount: sum('discountAmount'), pitchTotal: sum('pitchTotal'), serviceFee: sum('serviceFee'), total: sum('total') };
  };

  /** The server's exact quote (checkout) in the same shape */
  const quoteSeriesExact = async ({ pitchId, date, hour, duration, weeks }) => {
    const option = optionFor(pitches.get(pitchId), weeks);
    if (!option) throw new ApiError('This pitch doesn’t offer this package.', 'not_allowed');
    const q = await api.get(`/packages/quote?pitchSlug=${slugOf(pitchId)}&optionId=${option.id}&startsOn=${date}&hour=${hour}&duration=${duration}`);
    const rate = SA.services.db.settings().serviceFee;
    const sessions = q.weeks.map((w) => (w.available
      ? { date: w.date, available: true, listPrice: +w.listPrice, discountAmount: +w.listPrice - +w.price, pitchTotal: +w.price, serviceFee: Math.round(+w.price * rate), total: +w.price + Math.round(+w.price * rate) }
      : { date: w.date, available: false }));
    return { ...seriesTotals(sessions, +q.discountPercent / 100), listPrice: +q.listPrice, discountAmount: +q.discountAmount, serviceFee: +q.serviceFee, total: +q.total, pitchTotal: +q.listPrice - +q.discountAmount };
  };

  const createSeries = async ({ pitchId, date, hour, duration, weeks, waiver = false }) => {
    SA.services.legal?.requireWaiver(waiver, 'weekly package');
    const option = optionFor(pitches.get(pitchId), weeks);
    const pk = await api.post('/packages', { pitchSlug: slugOf(pitchId), optionId: option.id, startsOn: date, hour, duration, acceptWaiver: !!waiver });
    packs.set(pk.code, pk);
    [...days.keys()].filter((k) => k.startsWith(`${pitchId}|`)).forEach((k) => days.delete(k));
    return { seriesId: pk.code, bookings: pk.matches.map((m) => ({ id: m.code })) };
  };

  const MATCH_STATUS = { CancelledByCustomer: 'cancelled', CancelledByVenue: 'cancelled' };
  /** A package's matches in the site's booking shape */
  const seriesOf = (code) => {
    const pk = packs.get(code);
    if (!pk) return [];
    const p = pitches.get(idOf(pk.pitchSlug)) || {};
    return pk.matches.map((m) => {
      const b = bookings.get(m.code) || {};
      return { ...b, id: m.code, live: true, pitchId: idOf(pk.pitchSlug), pitchName: pk.pitchName, date: m.date, hour: m.startHour, duration: pk.duration,
        total: +m.total, discountAmount: +m.discountAmount, status: MATCH_STATUS[m.status] || 'confirmed', seriesId: code, sessionNo: m.number, seriesSize: pk.matchCount,
        policy: b.policy || p.cancelPolicy || 'standard', checkedIn: !!m.checkedInAt };
    });
  };

  /** The package card: played / missed / left, and whether its QR still works */
  const packageStatus = (code) => {
    const pk = packs.get(code);
    if (!pk) return null;
    const now = Date.now();
    const sessions = pk.matches.map((m) => {
      const start = parseISO(m.date).getTime() + m.startHour * 36e5, end = start + pk.duration * 36e5;
      const state = MATCH_STATUS[m.status] ? 'cancelled' : m.checkedInAt || ['CheckedIn', 'Played'].includes(m.status) ? 'played' : m.status === 'NoShow' || end < now ? 'missed' : 'upcoming';
      return { id: m.code, date: m.date, hour: m.startHour, duration: pk.duration, makeup: false, checkedInAt: m.checkedInAt ? Date.parse(m.checkedInAt) : 0, state, start, no: m.number };
    });
    const n = (st) => sessions.filter((x) => x.state === st).length;
    const counted = sessions.filter((x) => x.state !== 'cancelled');
    const played = n('played'), missed = n('missed'), left = n('upcoming');
    return { id: pk.code, series: { pitchId: idOf(pk.pitchSlug) }, sessions, total: counted.length, played, missed, excused: 0, cancelled: n('cancelled'), left,
      used: played + missed, expired: pk.status !== 'Active' || left === 0, scans: [], exception: null, next: sessions.find((x) => x.state === 'upcoming') || null };
  };

  const cancelSeries = async (code) => {
    const list = seriesOf(code).filter((b) => b.status === 'confirmed' && startsAt(b) > Date.now());
    let refund = 0;
    for (const b of list) refund += (await cancel(b.id)).refund;
    await loadPackage(code);
    return { count: list.length, refund };
  };

  const cancel = async (id) => {
    const r = await api.post(`/bookings/${encodeURIComponent(id)}/cancel`);
    const b = fromBooking(r);
    [...days.keys()].filter((k) => k.startsWith(`${b.pitchId}|`)).forEach((k) => days.delete(k));
    return { refund: b.refund, share: b.total ? b.refund / b.total : 0 };
  };

  const isLiveBooking = (id) => live() && bookings.has(id);

  /* ---------------- Waitlist and reviews (3.6) ---------------- */

  const waits = new Map();   // `${pitchId}|${gridDate}|${gridHour}` → server entry
  const waitKey = (pitchId, date, hour) => `${pitchId}|${date}|${hour}`;
  const toWait = (e) => ({ id: e.id, live: true, pitchId: idOf(e.pitchSlug), pitchName: e.pitchName, date: e.date, hour: e.hour, status: e.status === 'FreeNow' ? 'notified' : 'waiting', createdAt: Date.parse(e.createdAt) });
  const loadWaits = async () => {
    if (!SA.services.session?.me()) return [];
    const list = await api.get('/waitlist/mine').catch(() => []);
    [...waits.keys()].forEach((k) => waits.delete(k));
    list.forEach((e) => waits.set(waitKey(idOf(e.pitchSlug), e.date, e.hour), e));
    return list.map(toWait);
  };
  const W = SA.services.waitlist;
  const demoW = { ...W };
  Object.assign(W, {
    entryFor: (pitchId, date, hour) => {
      if (!isLivePitch(pitchId)) return demoW.entryFor(pitchId, date, hour);
      const e = waits.get(waitKey(pitchId, date, hour));
      return e && e.status === 'Waiting' ? toWait(e) : null;
    },
    countFor: (pitchId, date, hour) => (isLivePitch(pitchId) ? (days.get(`${pitchId}|${date}`) || []).find((s) => s.hour === hour)?.waiting || 0 : demoW.countFor(pitchId, date, hour)),
    join: (args) => {
      if (!isLivePitch(args.pitchId)) return demoW.join(args);
      return api.post('/waitlist', { pitchSlug: slugOf(args.pitchId), date: args.date, hour: args.hour })
        .then((e) => { waits.set(waitKey(args.pitchId, args.date, args.hour), e); return toWait(e); });
    },
    leave: (id) => {
      const hit = [...waits.entries()].find(([, e]) => String(e.id) === String(id));
      if (!live() || !hit) return demoW.leave(id);
      return api.del(`/waitlist/${id}`).then(() => waits.delete(hit[0]));
    },
    mine: () => (live() ? [...waits.values()].map(toWait) : demoW.mine()),
    load: ready(() => (live() ? loadWaits() : demoW.mine())),
  });

  const myReviews = new Map();   // booking code → my review
  const fromReview = (r) => ({ id: r.id, rating: r.stars, text: r.text || '', createdAt: Date.parse(r.createdAt), customer: { name: r.customerName, avatar: 0 },
    reply: r.reply ? { text: r.reply, at: Date.parse(r.repliedAt) } : null });

  const isLivePackage = (code) => live() && packs.has(code);
  Object.assign(B, {
    quoteSeries: (args) => (isLivePitch(args.pitchId) ? quoteSeries(args) : demo.bookings.quoteSeries(args)),
    quoteSeriesExact: ready((args) => (live() ? quoteSeriesExact(args) : demo.bookings.quoteSeries(args))),
    createSeries: ready((args) => (live() ? createSeries(args) : demo.bookings.createSeries(args))),
    seriesOf: (code) => (isLivePackage(code) ? seriesOf(code) : demo.bookings.seriesOf(code)),
    packageStatus: (code) => (isLivePackage(code) ? packageStatus(code) : demo.bookings.packageStatus(code)),
    packageCode: (code) => (isLivePackage(code) ? packs.get(code).qrToken || code : demo.bookings.packageCode(code)),
    cancelSeries: (code) => (isLivePackage(code) ? cancelSeries(code) : demo.bookings.cancelSeries(code)),
    hold: ready((args) => (live() ? hold(args) : null)),
    quote: (args) => (isLivePitch(args.pitchId) ? quote(args) : demo.bookings.quote(args)),
    create: ready((args) => (live() ? create(args) : demo.bookings.create(args))),
    list: ready(() => (live() ? myList() : demo.bookings.list())),
    get: ready((id) => (live() ? getBooking(id) : demo.bookings.get(id))),
    getSync: (id) => (isLiveBooking(id) ? bookings.get(id) : demo.bookings.getSync(id)),
    cancel: (id) => (isLiveBooking(id) ? cancel(id) : demo.bookings.cancel(id)),
    venueRecord: (id) => (isLiveBooking(id) ? (bookings.get(id).checkedIn ? { checkedIn: true } : null) : demo.bookings.venueRecord(id)),
    // The gate scans the server's signed token
    checkInCode: (id) => (isLiveBooking(id) && bookings.get(id).qrToken ? bookings.get(id).qrToken : demo.bookings.checkInCode(id)),
    review: (id, d) => (isLiveBooking(id) ? api.post(`/bookings/${encodeURIComponent(id)}/review`, { stars: Math.round(+d.rating), text: (d.text || '').trim() }).then((r) => { myReviews.set(id, fromReview(r)); return myReviews.get(id); }) : demo.bookings.review(id, d)),
    reviewOf: (b) => (b?.live ? myReviews.get(b.id) || null : demo.bookings.reviewOf(b)),
    upcomingSync: () => (live() ? [...bookings.values()].filter((b) => b.status === 'confirmed' && b.startsAt > Date.now()).sort((a, b) => a.startsAt - b.startsAt) : demo.bookings.upcomingSync()),
  });
})(window.SA);
