/**
 * The partner dashboard (#/dash) on the real API (backend Phase 3.4). Loaded after dashService.
 *
 * The dashboard pages read the local data (SA.services.db) through SA.services.dash. In API mode this file loads
 * the signed-in partner's venues, pitches, blocked times and bookings from the server into the same local shape
 * (records marked `live`), signs the dashboard into that business, and sends the dashboard's changes to the
 * server first, then loads again. The pages and their design stay as they are.
 *
 * On the server already: pitches (edit, photos, close / reopen, delete), blocked times, bookings (walk-in, no-show,
 * venue cancel), QR check-in. Still demo-only until their phase: packages (3.5b), weekly teams, offers, reviews, store, payouts.
 */
(function (SA) {
  const api = SA.services.api;
  const db = SA.services.db;
  const dash = SA.services.dash;
  const { ApiError } = SA.services.http;
  const { isoDate, today, addDays } = SA.core.utils;

  const ACCOUNT = 'acc-live-partner';
  const demo = { ...dash };
  let scope = null;          // GET /partner/me
  let loadedAt = 0;
  let areas = null;          // /lookups/areas + governorates

  const live = () => api.isLive();
  const isLive = (rec) => live() && !!rec?.live;
  const SURFACE = { ArtificialTurf: 'Artificial turf', NaturalGrass: 'Natural grass', IndoorCourt: 'Indoor court' };
  const surfaceKey = (text) => (/grass/i.test(text) ? 'NaturalGrass' : /indoor|court/i.test(text) ? 'IndoorCourt' : 'ArtificialTurf');
  const photoSrc = (ph) => (ph.url ? `${location.origin}${ph.url}` : ph.key);

  /* ---------------- Areas (name ↔ id, city) ---------------- */

  const loadAreas = async () => {
    if (areas) return areas;
    const [list, govs] = await Promise.all([api.get('/lookups/areas'), api.get('/lookups/governorates')]);
    const gov = Object.fromEntries(govs.map((g) => [g.id, g.nameEn]));
    areas = list.map((a) => ({ id: a.id, name: a.nameEn, city: gov[a.governorateId] || '' }));
    return areas;
  };
  const areaIdOf = (name) => {
    const n = String(name || '').trim().toLowerCase();
    const a = areas.find((x) => x.name.toLowerCase() === n) || areas.find((x) => x.name.toLowerCase().startsWith(n) || n.startsWith(x.name.toLowerCase()));
    if (!a) throw new ApiError('Choose an area from the list.', 'invalid', { area: 'Choose an area from the list.' });
    return a.id;
  };

  /* ---------------- Server → local records ---------------- */

  const STATUS = { Live: 'active', ClosedByOwner: 'closed', ClosedByAdmin: 'closed', WaitingForReview: 'active', Rejected: 'active' };
  const REVIEW = { WaitingForReview: 'pending', Rejected: 'rejected' };

  const toPitch = (d, card, venue, prev = {}) => {
    const today0 = d.openingHours.find((h) => h.dayOfWeek === new Date().getDay() && !h.isClosed) || d.openingHours.find((h) => !h.isClosed);
    const area = areas.find((a) => a.id === venue?.areaId);
    return {
      ...prev, id: `p-${d.slug}`, live: true, apiId: d.id, slug: d.slug, venueId: d.venueId, ownerId: ACCOUNT,
      name: d.name, area: venue?.areaEn || prev.area || '', city: area?.city || prev.city || 'Cairo', address: venue?.address || '', lat: +(venue?.lat || 0), lng: +(venue?.lng || 0),
      format: `${d.format}-a-side`, players: d.format * 2, surface: prev.surface && surfaceKey(prev.surface) === d.surface ? prev.surface : SURFACE[d.surface],
      indoor: d.isIndoor, price: +d.pricePerHour, peakPrice: +(d.peakPricePerHour ?? d.pricePerHour), open: today0 ? [today0.openHour, today0.closeHour] : [9, 24],
      openingHours: d.openingHours, images: d.photos.map(photoSrc), photoIds: Object.fromEntries(d.photos.map((ph) => [photoSrc(ph), ph.id])),
      facilities: d.facilities, about: d.descriptionEn || '', tags: prev.tags || [], cancelPolicy: d.policyKey, allowCash: d.allowCash,
      weekly: prev.weekly || { enabled: false, discount: 0.1 }, status: STATUS[d.status] || 'active', closedReason: d.status === 'ClosedByAdmin' ? 'Closed by Super App' : prev.closedReason || '',
      review: REVIEW[d.status] || 'approved', reviewReason: d.rejectReason || '', rating: +(card?.rating || 0), reviews: card?.reviewsCount || 0,
      rowVersion: d.rowVersion, seed: false, deleted: false,
    };
  };

  const toClosure = (c, pitchId) => ({
    id: `cl-live-${c.id}`, live: true, apiId: c.id, pitchId, from: c.fromDate, to: c.toDate, reason: c.reason, createdAt: Date.parse(c.createdAt),
    hours: c.fromHour == null ? null : Array.from({ length: c.toHour - c.fromHour }, (_, i) => c.fromHour + i),
  });

  const BOOKING_STATUS = { CancelledByCustomer: 'cancelled', CancelledByVenue: 'cancelled', NoShow: 'no-show', CheckedIn: 'checked-in', Played: 'checked-in' };
  const toBooking = (r, pitchId, policy) => ({
    id: r.code, live: true, pitchId, ownerId: ACCOUNT, date: r.date, hour: r.startHour, duration: r.duration, amount: +r.pitchAmount, fee: +r.serviceFee,
    customer: { id: r.source === 'WalkIn' ? 'walk-in' : `c-${r.code}`, name: r.customerName, phone: r.customerPhone || '' },
    source: r.source === 'WalkIn' ? 'walk-in' : 'app', payment: r.payAtVenue ? 'Cash at venue' : 'Card', status: BOOKING_STATUS[r.status] || 'confirmed',
    checkedIn: !!r.checkedInAt, checkedInAt: r.checkedInAt ? Date.parse(r.checkedInAt) : null, refund: +r.refundAmount,
    cancelledBy: r.status === 'CancelledByCustomer' ? 'customer' : r.status === 'CancelledByVenue' ? 'venue' : undefined,
    refundShare: r.status === 'CancelledByCustomer' && +r.total ? +r.refundAmount / +r.total : undefined,
    createdAt: Date.parse(r.createdAt), policy,
  });

  /** Bookings from 90 days back to 60 days ahead, every page */
  const loadBookings = async () => {
    const from = isoDate(addDays(today(), -90)), to = isoDate(addDays(today(), 60));
    const out = [];
    for (let page = 1; page <= 20; page++) {
      const r = await api.get(`/partner/bookings?status=all&from=${from}&to=${to}&pageSize=100&page=${page}`);
      out.push(...r.items);
      if (out.length >= r.meta.total || !r.items.length) break;
    }
    return out;
  };

  /** Replaces the live records in the local data with the server's */
  const load = async () => {
    await loadAreas();
    scope = await api.get('/partner/me');
    const [venues, cards] = await Promise.all([api.get('/partner/venues'), api.get('/partner/pitches')]);
    const details = await Promise.all(cards.map((c) => api.get(`/partner/pitches/${c.id}`)));
    const closures = await Promise.all(details.map((d) => api.get(`/partner/pitches/${d.id}/closures`)));
    const bookings = await loadBookings();
    const data = db.raw();
    const prev = Object.fromEntries(data.pitches.filter((p) => p.live).map((p) => [p.id, p]));
    const pitches = details.map((d, i) => toPitch(d, cards[i], venues.find((v) => v.id === d.venueId), prev[`p-${d.slug}`]));
    const byApiId = Object.fromEntries(pitches.map((p) => [p.apiId, p]));
    const ids = new Set(pitches.map((p) => p.id));
    data.pitches = [...pitches, ...data.pitches.filter((p) => !p.live && !ids.has(p.id))];
    data.closures = [...closures.flatMap((list, i) => list.map((c) => toClosure(c, pitches[i].id))), ...data.closures.filter((c) => !c.live)];
    data.bookings = [...bookings.filter((b) => byApiId[b.pitchId]).map((b) => toBooking(b, byApiId[b.pitchId].id, byApiId[b.pitchId].cancelPolicy)), ...data.bookings.filter((b) => !b.live)];
    upsertAccount(data);
    loadedAt = Date.now();
    ['pitches', 'closures', 'bookings', 'accounts'].forEach((col) => db.touch(col));
  };

  const upsertAccount = (data) => {
    const me = SA.services.session.me() || {};
    const acc = { id: ACCOUNT, live: true, role: 'owner', name: me.fullName || 'Partner', company: scope.businessName, email: me.email || '', phone: me.phone || '',
      avatar: 0, joinedAt: Date.now(), status: scope.status === 'Suspended' ? 'suspended' : 'active', staffRole: scope.role, payout: { method: 'Bank transfer', detail: '—' } };
    data.accounts = [acc, ...data.accounts.filter((a) => a.id !== ACCOUNT)];
  };

  /* ---------------- Entering the dashboard ---------------- */

  /**
   * Called by the dashboard guard in API mode. Returns 'ok', 'signin' (no account signed in) or 'not_partner'.
   * Reloads from the server when the data is older than 20 seconds.
   */
  const enter = async () => {
    if (!SA.services.session.loaded()) await SA.services.session.load();
    if (!SA.services.session.me()) { dash.auth.signOut(); return 'signin'; }
    try {
      if (Date.now() - loadedAt > 20000) await load();
    } catch (err) {
      if (err.code === 'not_partner' || err.status === 403) { dash.auth.signOut(); return 'not_partner'; }
      throw err;
    }
    if (dash.auth.current()?.id !== ACCOUNT) dash.auth.openAs(ACCOUNT);
    return 'ok';
  };

  const reload = async () => { loadedAt = 0; await load(); };

  /* ---------------- Changes → server ---------------- */

  const SEVEN = (open) => Array.from({ length: 7 }, (_, day) => ({ dayOfWeek: day, openHour: open[0], closeHour: open[1], isClosed: false }));
  const pitchBody = (d, venueId, prev) => ({
    venueId, name: d.name.trim(), format: parseInt(d.format, 10), surface: surfaceKey(d.surface), isIndoor: !!d.indoor, pricePerHour: Math.round(+d.price),
    peakPricePerHour: +d.peakPrice > +d.price ? Math.round(+d.peakPrice) : null, peakFromHour: +d.peakPrice > +d.price ? 18 : null,
    descriptionEn: (d.about || '').trim(), descriptionAr: null, policyKey: d.cancelPolicy || 'standard', allowCash: prev?.allowCash ?? true,
    facilities: d.facilities || [], openingHours: prev?.openingHours && prev.open[0] === +d.open[0] && prev.open[1] === +d.open[1] ? prev.openingHours : SEVEN([+d.open[0], +d.open[1]]),
  });
  const venueBody = (d) => ({ name: d.name.trim(), areaId: areaIdOf(d.area), address: d.address.trim(), lat: +(+d.lat).toFixed(6), lng: +(+d.lng).toFixed(6), phone: null });

  /** Server field names → the editor's */
  const FIELDS = { pricePerHour: 'price', peakPricePerHour: 'peakPrice', openingHours: 'open', descriptionEn: 'about', areaId: 'area', lat: 'location', lng: 'location' };
  const formError = (err) => {
    if (err.details) err.details = Object.fromEntries(Object.entries(err.details).map(([k, v]) => [FIELDS[k.split(/[.[]/)[0]] || k, v]));
    return err;
  };

  /** Photos: uploads new ones (photos picked from the library are fetched first), deletes removed ones, saves the order */
  const syncPhotos = async (pitch, images) => {
    const ids = pitch.photoIds || {};
    const removed = Object.keys(ids).filter((src) => !images.includes(src));
    const added = images.filter((src) => !ids[src]);
    if (added.length) {
      const form = new FormData();
      for (const [i, src] of added.entries()) form.append('photos', await blobOf(src), `photo-${i + 1}.jpg`);
      await api.form(`/partner/pitches/${pitch.apiId}/photos`, form);
    }
    for (const src of removed) await api.del(`/partner/pitches/${pitch.apiId}/photos/${ids[src]}`);
    const now = await api.get(`/partner/pitches/${pitch.apiId}`);
    const bySrc = Object.fromEntries(now.photos.map((ph) => [photoSrc(ph), ph.id]));
    const order = [...images.map((src) => bySrc[src]).filter(Boolean), ...now.photos.map((ph) => ph.id)];
    const unique = [...new Set(order)];
    if (unique.length > 1) await api.put(`/partner/pitches/${pitch.apiId}/photos/order`, { photoIds: unique });
  };

  const blobOf = async (src) => {
    if (String(src).startsWith('data:')) return api.blob(src);
    const res = await fetch(SA.data.media.photo(src, 1600, 1100));
    if (!res.ok) throw new ApiError('A photo could not be loaded. Try another one.', 'photo');
    return res.blob();
  };

  const createPitch = async (d) => {
    const e = dash.validatePitch(d); if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e);
    await loadAreas();
    try {
      const venue = await api.post('/partner/venues', venueBody(d));
      const created = await api.post('/partner/pitches', pitchBody(d, venue.id, null));
      await syncPhotos({ apiId: created.id, photoIds: {} }, d.images);
      await reload();
      return dash.pitch(`p-${created.slug}`);
    } catch (err) { throw formError(err); }
  };

  const updatePitch = async (id, d) => {
    const p = dash.pitch(id);
    const e = dash.validatePitch(d); if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e);
    await loadAreas();
    try {
      if (d.area !== p.area || d.address !== p.address || +d.lat !== p.lat || +d.lng !== p.lng) await api.put(`/partner/venues/${p.venueId}`, venueBody(d));
      await api.put(`/partner/pitches/${p.apiId}`, { ...pitchBody(d, p.venueId, p), rowVersion: p.rowVersion });
      await syncPhotos(p, d.images);
    } catch (err) { throw formError(err); }
    // Highlights and weekly settings are kept on this device until their phase
    db.update('pitches', id, { tags: (d.tags || []).slice(0, 3), weekly: { enabled: false, discount: 0.1 } });
    await reload();
    return dash.pitch(id);
  };

  const setPitchStatus = async (id, status) => {
    const p = dash.pitch(id);
    await api.post(`/partner/pitches/${p.apiId}/${status === 'closed' ? 'close' : 'reopen'}`);
    await reload();
    return dash.pitch(id);
  };

  const deletePitch = async (id) => {
    const p = dash.pitch(id);
    const r = await api.del(`/partner/pitches/${p.apiId}`);
    await reload();
    return { cancelled: r.cancelledBookings };
  };

  /** Blocked hours may be any set: each run of hours in a row is one blocked time on the server */
  const runs = (hours) => {
    const sorted = [...hours].sort((a, b) => a - b), out = [];
    sorted.forEach((h) => { const last = out[out.length - 1]; if (last && h === last[1]) last[1] = h + 1; else out.push([h, h + 1]); });
    return out;
  };
  const addClosure = async ({ pitchId, from, to, hours, reason }) => {
    const p = dash.pitch(pitchId);
    if (!from || !to || to < from) throw new ApiError('Choose a valid date range.', 'invalid');
    const text = (reason || '').trim() || 'Unavailable';
    const ranges = hours?.length ? runs(hours.filter((h) => h < 24)) : [[null, null]];
    for (const [fromHour, toHour] of ranges) await api.post(`/partner/pitches/${p.apiId}/closures`, { fromDate: from, toDate: to, fromHour, toHour, reason: text });
    await reload();
    return dash.closures(pitchId)[0];
  };

  const removeClosure = async (id) => {
    const c = db.find('closures', id);
    const p = dash.pitch(c.pitchId);
    await api.del(`/partner/pitches/${p.apiId}/closures/${c.apiId}`);
    await reload();
  };

  const walkIn = async ({ pitchId, date, hour, duration, name, phone }) => {
    const p = dash.pitch(pitchId);
    if (!name || name.trim().length < 2) throw new ApiError('Enter the customer’s name.', 'invalid', { name: 'Required' });
    const r = await api.post('/partner/bookings/walk-in', { pitchId: p.apiId, date, hour, duration, name: name.trim(), phone: (phone || '').trim() || null });
    await reload();
    return dash.findBooking(r.code);
  };

  /* ---------------- QR check-in ---------------- */

  /** Server reason → the check-in page's states (it already has a card for each) */
  const SCAN_STATE = { already_used: 'already', wrong_day: 'wrong-day', cancelled: 'cancelled', other_venue: 'wrong-venue', bad_signature: 'invalid', package_finished: 'expired', account_blocked: 'invalid' };
  const key = () => (crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`).replace(/-/g, '');

  /** Sends a scanned token or a typed booking ID; the server checks it in straight away when it's valid */
  const scan = async (raw) => {
    const text = String(raw || '').trim();
    const typed = /^SA-[A-Z0-9]{4,}$/i.test(text);
    const r = await api.post('/check-in/scan', { token: typed ? null : text, code: typed ? text.toUpperCase() : null, pitchId: null, device: 'Partner dashboard' }, { headers: { 'Idempotency-Key': key() } });
    if (r.code) await reload();
    const booking = r.code ? dash.findBooking(r.code) : null;
    const late = r.reason === 'wrong_hour' && booking && booking.startsAt < Date.now();
    const state = r.result === 'accepted' ? 'checked' : r.reason === 'wrong_hour' ? (late ? 'ended' : 'early') : SCAN_STATE[r.reason] || 'invalid';
    return { state, message: r.message, booking, manual: typed, live: true };
  };

  const bookingAction = async (id, action, reason = 'Cancelled by the venue') => {
    if (action === 'check-in') {
      const r = await scan(id);
      if (r.state !== 'checked') throw new ApiError(r.message, r.state);
      return r.booking;
    }
    if (action === 'cancel') await api.post(`/partner/bookings/${encodeURIComponent(id)}/cancel`, { reason });
    else if (action === 'no-show') await api.post(`/partner/bookings/${encodeURIComponent(id)}/no-show`);
    await reload();
    return dash.findBooking(id);
  };

  const livePitch = (id) => isLive(db.find('pitches', id));
  Object.assign(dash, {
    createPitch: (d, ownerId) => (live() && dash.auth.current()?.id === ACCOUNT ? createPitch(d) : demo.createPitch(d, ownerId)),
    updatePitch: (id, d) => (livePitch(id) ? updatePitch(id, d) : demo.updatePitch(id, d)),
    setPitchStatus: (id, status, reason) => (livePitch(id) ? setPitchStatus(id, status) : demo.setPitchStatus(id, status, reason)),
    deletePitch: (id) => (livePitch(id) ? deletePitch(id) : demo.deletePitch(id)),
    addClosure: (c) => (livePitch(c.pitchId) ? addClosure(c) : demo.addClosure(c)),
    removeClosure: (id) => (isLive(db.find('closures', id)) ? removeClosure(id) : demo.removeClosure(id)),
    walkIn: (w) => (livePitch(w.pitchId) ? walkIn(w) : demo.walkIn(w)),
    bookingAction: (id, action) => (isLive(db.find('bookings', id)) ? bookingAction(id, action) : demo.bookingAction(id, action)),
    // A token from the app's QR can't be read here: in API mode every scan goes to the server
    checkIn: (raw) => (live() && dash.auth.current()?.id === ACCOUNT ? scan(raw) : demo.checkIn(raw)),
  });

  SA.services.livePartner = { enter, reload, scope: () => scope, ACCOUNT };
})(window.SA);
