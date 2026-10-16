/**
 * Bookings. Future API: POST /bookings, POST /bookings/series, GET /me/bookings,
 * POST /bookings/:id/cancel, POST /bookings/:id/review
 */
(function (SA) {
  const store = SA.core.store;
  const { request, ApiError } = SA.services.http;
  const { isoDate, today, parseISO, addDays, seeded } = SA.core.utils;
  const db = () => SA.services.db;
  const PS = () => SA.services.pitches;

  const bookingId = () => 'SA-' + Math.random().toString(36).slice(2, 6).toUpperCase() + Math.floor(1000 + Math.random() * 9000);
  const startsAt = (b) => { const d = parseISO(b.date); d.setHours(0, 0, 0, 0); return d.getTime() + b.hour * 36e5; };
  const WEEK_OPTIONS = [4, 8, 12];

  /** Check-in code: booking id + a short signature so a typed-up or edited code is rejected */
  const sign = (id) => Math.floor(seeded(`${id}|superapp-checkin|v1`) * 1e5).toString(36).toUpperCase().padStart(4, '0');
  const checkInCode = (id) => `SUPERAPP|BK|${id}|${sign(id)}`;
  /** Weekly package: one code for all its games. It works once per game (the booked number of times), then it expires. */
  const packageCode = (seriesId) => `SUPERAPP|WK|${seriesId}|${sign(`${seriesId}|pkg`)}`;
  const parseCheckInCode = (raw) => {
    const s = String(raw || '').trim();
    const w = s.match(/^SUPERAPP\|WK\|([A-Z0-9-]+)\|([A-Z0-9]+)$/i);
    if (w) return { series: w[1].toUpperCase(), valid: sign(`${w[1].toUpperCase()}|pkg`) === w[2].toUpperCase() };
    if (/^WK-[A-Z0-9]{4,}$/i.test(s)) return { series: s.toUpperCase(), valid: true, manual: true };
    const m = s.match(/^SUPERAPP\|BK\|([A-Z0-9-]+)\|([A-Z0-9]+)$/i);
    if (m) return { id: m[1].toUpperCase(), valid: sign(m[1].toUpperCase()) === m[2].toUpperCase() };
    // Typed booking ID (from the customer's screen) — accepted, but flagged as manual
    if (/^[A-Z]{2}-[A-Z0-9-]{4,}$/i.test(s)) return { id: s.toUpperCase(), valid: true, manual: true };
    return null;
  };

  const sessionsFor = (p, date, hour, duration, weeks) => {
    const out = [];
    for (let k = 0; k < weeks; k++) {
      const d = isoDate(addDays(parseISO(date), k * 7));
      const slots = PS().slotsSync(p.id, d);
      let free = true;
      for (let h = hour; h < hour + duration; h += 1) { const s = slots.find((x) => x.hour === Math.floor(h)); if (!s || s.status !== 'available') free = false; }
      out.push({ date: d, available: free });
    }
    return out;
  };

  const bookingService = {
    WEEK_OPTIONS, checkInCode, parseCheckInCode, packageCode,
    /**
     * A weekly package and each game in it. A game is played (checked in), missed (it ended without a check-in — it
     * still counts as used), excused (the admin's one exception: not counted, a make-up game was added), cancelled or
     * upcoming. The QR code expires when no game is left.
     */
    packageStatus(seriesId) {
      const s = db().find('series', seriesId); if (!s) return null;
      const now = Date.now();
      const sessions = (s.bookingIds || []).map((id) => db().find('bookings', id)).filter(Boolean).map((b) => {
        const start = startsAt(b), end = start + b.duration * 36e5;
        const state = b.status === 'cancelled' ? 'cancelled' : b.excused ? 'excused' : (b.checkedIn || b.checkedInAt || b.status === 'checked-in') ? 'played' : end < now ? 'missed' : 'upcoming';
        return { id: b.id, date: b.date, hour: b.hour, duration: b.duration, makeup: !!b.makeup, checkedInAt: b.checkedInAt || 0, state, start };
      }).sort((a, b) => a.start - b.start);
      const n = (st) => sessions.filter((x) => x.state === st).length;
      const counted = sessions.filter((x) => !['cancelled', 'excused'].includes(x.state));
      counted.forEach((x, i) => (x.no = i + 1));
      const played = n('played'), missed = n('missed'), left = n('upcoming');
      return { id: s.id, series: s, sessions, total: counted.length, played, missed, excused: n('excused'), cancelled: n('cancelled'), left, used: played + missed,
        expired: left === 0, scans: s.scans || [], exception: s.exception || null, next: sessions.find((x) => x.state === 'upcoming') || null };
    },

    /** Price breakdown for a selection (pure — used by the UI before payment). */
    quote({ pitchId, date, hour, duration, discount = 0, code = '' }) {
      const p = PS().getSync(pitchId);
      const hours = [];
      for (let h = hour; h < hour + duration; h += 0.5) hours.push(h);
      const list = Math.round(hours.reduce((sum, h) => sum + PS().priceAt(p, date, Math.floor(h)) / 2, 0));
      const off = Math.round(list * discount);
      // A Super App code (Super App pays: the venue still earns the full price) or the venue's own code (throws a friendly error if it doesn't apply)
      const sa = code ? SA.services.codes.platform(code, { scope: 'bookings', amount: list - off }) : null;
      const promo = sa || (code ? SA.services.codes.forBooking(code, { pitchId, amount: list - off }) : null);
      const pitchTotal = list - off - (promo && !promo.platform ? promo.amount : 0);
      const serviceFee = Math.round(pitchTotal * SA.services.db.settings().serviceFee);
      const total = pitchTotal + serviceFee - (sa ? Math.min(sa.amount, pitchTotal + serviceFee) : 0);
      return { listPrice: list, discountAmount: off, discount, promo, platformOff: sa ? Math.min(sa.amount, pitchTotal + serviceFee) : 0, pitchTotal, serviceFee, total, perPlayer: Math.ceil(total / p.players) };
    },

    /** Weekly booking: every session's availability and price, using the pitch's weekly discount */
    quoteSeries({ pitchId, date, hour, duration, weeks }) {
      const p = PS().getSync(pitchId);
      if (!p.weekly?.enabled) throw new ApiError('This pitch doesn’t take weekly bookings online.', 'not_allowed');
      const discount = p.weekly.discount || 0;
      const sessions = sessionsFor(p, date, hour, duration, weeks).map((s) => ({ ...s, ...(s.available ? bookingService.quote({ pitchId, date: s.date, hour, duration, discount }) : {}) }));
      const ok = sessions.filter((s) => s.available);
      const sum = (k) => ok.reduce((t, s) => t + s[k], 0);
      return { sessions, booked: ok.length, skipped: sessions.length - ok.length, discount, listPrice: sum('listPrice'), discountAmount: sum('discountAmount'), pitchTotal: sum('pitchTotal'), serviceFee: sum('serviceFee'), total: sum('total') };
    },

    /** Checks the slot is still free, takes payment, creates the booking. */
    async create({ pitchId, date, hour, duration, payment, players, code = '', waiver = false }) {
      SA.services.legal?.requireWaiver(waiver, 'booking');
      SA.services.user.assertActive();
      SA.services.content?.assertOpen('bookings');
      const p = PS().getSync(pitchId);
      if (!PS().bookable(p)) throw new ApiError('This pitch isn’t taking bookings right now.', 'closed');
      const slots = PS().slotsSync(pitchId, date);
      for (let h = hour; h < hour + duration; h++) {
        const s = slots.find((x) => x.hour === Math.floor(h));
        if (!s || s.status !== 'available') throw new ApiError('Someone just booked part of this time. Pick another slot.', 'slot_taken');
      }
      const q = bookingService.quote({ pitchId, date, hour, duration, code });
      const paid = await SA.services.payment.charge({ ...payment, amount: q.total, label: `Booking — ${p.name}` });
      return request(() => {
        const booking = bookingService._record(p, { date, hour, duration, players, q, paid });
        if (q.promo) SA.services.codes.use(q.promo.id, { discount: q.promo.amount, revenue: q.pitchTotal });
        if (q.promo?.platform) db().insert('promoUses', { codeId: q.promo.id, code: q.promo.code, amount: q.platformOff, scope: 'bookings', ref: booking.id, at: Date.now() }, 'pu');
        store.set((s) => ({ bookings: [booking, ...s.bookings] }), 'booking:new');
        bookingService._toLedger(booking);
        db().log('customer', 'booking.created', `New app booking ${booking.id} at ${p.name}`, booking.id);
        SA.services.user.notify({ kind: 'booking', title: 'Booking confirmed', body: `${p.name}, ${SA.core.utils.fmtDate(date)} at ${SA.core.utils.fmtHour(hour)}.`, link: `/booking/${booking.id}` });
        // The pitch partner is told too
        SA.services.notify?.toAccount(p.ownerId, { cat: 'bookings', tone: 'turf', title: `New booking at ${p.name}`, body: `${SA.core.store.get().user?.name || 'A customer'} · ${SA.core.utils.fmtDate(date)} at ${SA.core.utils.fmtHour(hour)} · paid in the app`, link: '#/dash/bookings' });
        return booking;
      });
    },

    /** Same slot every week for N weeks, paid once. Weeks where the slot is taken are skipped. */
    async createSeries({ pitchId, date, hour, duration, weeks, payment, players, waiver = false }) {
      SA.services.user.assertActive();
      SA.services.legal?.requireWaiver(waiver, 'weekly package');
      SA.services.content?.assertOpen('bookings');
      const p = PS().getSync(pitchId);
      if (!PS().bookable(p)) throw new ApiError('This pitch isn’t taking bookings right now.', 'closed');
      if (!WEEK_OPTIONS.includes(+weeks)) throw new ApiError('Choose 4, 8 or 12 weeks.', 'invalid');
      const q = bookingService.quoteSeries({ pitchId, date, hour, duration, weeks: +weeks });
      if (!q.sessions[0].available) throw new ApiError('Someone just booked the first session. Pick another slot.', 'slot_taken');
      if (q.booked < 2) throw new ApiError('Fewer than two of those weeks are free. Pick another time.', 'slot_taken');
      const paid = await SA.services.payment.charge({ ...payment, amount: q.total, label: `Weekly booking × ${q.booked} — ${p.name}` });
      return request(() => {
        const u = store.get().user || {};
        const seriesId = 'WK-' + Math.random().toString(36).slice(2, 7).toUpperCase();
        const ok = q.sessions.filter((s) => s.available);
        const list = ok.map((s, i) => bookingService._record(p, { date: s.date, hour, duration, players, q: s, paid, extra: { seriesId, sessionNo: i + 1, seriesSize: ok.length } }));
        store.set((s) => ({ bookings: [...list, ...s.bookings] }), 'booking:new');
        list.forEach((b) => bookingService._toLedger(b));
        db().insert('series', { id: seriesId, kind: 'weekly', pitchId, ownerId: p.ownerId, team: `${(u.name || 'Customer').split(' ')[0]}’s team`, customer: { id: u.id || 'u-001', name: u.name || 'Customer', phone: u.phone || '' },
          weekday: parseISO(date).getDay(), hour, duration, startDate: date, weeks: +weeks, discount: q.discount, payment: paid.method === 'wallet' ? 'Wallet' : 'Card', source: 'app', status: 'active', bookingIds: list.map((b) => b.id), skipped: q.skipped }, 'WK');
        db().log('customer', 'series.created', `New weekly booking ${seriesId} at ${p.name}: ${list.length} sessions`, seriesId);
        SA.services.user.notify({ kind: 'booking', title: 'Weekly booking confirmed', body: `${list.length} games at ${p.name}, every ${SA.core.utils.DAY[parseISO(date).getDay()]} at ${SA.core.utils.fmtHour(hour)}.`, link: `/booking/${list[0].id}` });
        SA.services.notify?.toAccount(p.ownerId, { cat: 'bookings', tone: 'turf', title: `New weekly package at ${p.name}`, body: `${u.name || 'A customer'} · ${list.length} games, every ${SA.core.utils.DAY[parseISO(date).getDay()]} at ${SA.core.utils.fmtHour(hour)} · one QR for the whole package`, link: '#/dash/bookings' });
        return { seriesId, bookings: list };
      });
    },

    _record(p, { date, hour, duration, players, q, paid, extra = {} }) {
      return {
        id: bookingId(), pitchId: p.id, pitchName: p.name, area: p.area, city: p.city, image: p.images[0], format: p.format,
        date, hour, duration, players: players || p.players, listPrice: q.listPrice, discount: q.discount || 0, discountAmount: q.discountAmount || 0, promo: q.promo ? { code: q.promo.code, amount: q.promo.amount } : null, pitchTotal: q.pitchTotal, serviceFee: q.serviceFee, total: q.total, perPlayer: q.perPlayer,
        payment: { method: paid.method, label: paid.label, ref: paid.id }, status: 'confirmed', createdAt: Date.now(), publishedMatchId: null, policy: p.cancelPolicy || 'standard', ...extra,
      };
    },
    _toLedger(b) { const D = db(); D.insert('bookings', D._fromShopperBooking(b)); },

    list: () => request(() => {
      const now = Date.now();
      const all = store.get().bookings.map((b) => ({ ...b, startsAt: startsAt(b), isPast: startsAt(b) + b.duration * 36e5 < now }));
      return {
        upcoming: all.filter((b) => !b.isPast && b.status === 'confirmed').sort((a, b) => a.startsAt - b.startsAt),
        past: all.filter((b) => b.isPast || b.status !== 'confirmed').sort((a, b) => b.startsAt - a.startsAt),
      };
    }),
    get: (id) => request(() => {
      const b = store.get().bookings.find((x) => x.id === id);
      if (!b) throw new ApiError('Booking not found on this device.', 'not_found');
      return { ...b, startsAt: startsAt(b) };
    }),
    getSync: (id) => store.get().bookings.find((x) => x.id === id),
    seriesOf: (seriesId) => store.get().bookings.filter((b) => b.seriesId === seriesId).sort((a, b) => startsAt(a) - startsAt(b)),
    /** What the venue recorded (check-in time, venue cancellation) for a booking made on this device */
    venueRecord: (id) => db().find('bookings', id) || null,
    upcomingSync: () => store.get().bookings.filter((b) => b.status === 'confirmed' && startsAt(b) > Date.now()).sort((a, b) => startsAt(a) - startsAt(b)),

    /** Refund for cancelling now, under the pitch's cancellation policy (as it was when booked) */
    cancellationPolicy(b) {
      const hoursLeft = (startsAt(b) - Date.now()) / 36e5;
      const share = PS().refundShare(b.policy, hoursLeft);
      return { share, refund: Math.round(b.total * share), policy: PS().policyOf(b.policy), hoursLeft, kind: share === 1 ? 'full' : share > 0 ? 'half' : 'none' };
    },
    async cancel(id) {
      const b = store.get().bookings.find((x) => x.id === id);
      if (!b) throw new ApiError('Booking not found.', 'not_found');
      if (b.status === 'cancelled') return { refund: b.refund || 0 };
      const { refund, share } = bookingService.cancellationPolicy(b);
      if (refund > 0) await SA.services.payment.refund(refund, `Refund — ${b.pitchName}`);
      store.set((s) => ({ bookings: s.bookings.map((x) => (x.id === id ? { ...x, status: 'cancelled', refund, cancelledAt: Date.now() } : x)) }));
      if (db().find('bookings', id)) db().update('bookings', id, { status: 'cancelled', refund, refundShare: share, cancelledBy: 'customer', cancelledAt: Date.now() });
      db().log('customer', 'booking.cancelled', `Customer cancelled ${id} at ${b.pitchName} (${Math.round(share * 100)}% refund)`, id);
      // Tell the venue the hour is free again and offer to post it in "Pitches & free slots"
      const p = db().find('pitches', b.pitchId);
      if (p && new Date(`${b.date}T00:00`).getTime() + b.hour * 36e5 > Date.now()) SA.services.notify?.toAccount(p.ownerId, { cat: 'store', tone: 'gold', title: `An hour just freed up at ${p.name}`, body: `${b.date} at ${SA.core.utils.fmtHour(b.hour)} — post it to players in the community in one tap.`, link: `#/dash/slots?pitch=${p.id}&date=${b.date}&hour=${b.hour}` });
      return { refund, share };
    },
    /** Cancel every future session of a weekly booking; each follows the cancellation policy */
    async cancelSeries(seriesId) {
      const list = bookingService.seriesOf(seriesId).filter((b) => b.status === 'confirmed' && startsAt(b) > Date.now());
      let refund = 0;
      for (const b of list) refund += (await bookingService.cancel(b.id)).refund;
      if (db().find('series', seriesId)) db().update('series', seriesId, { status: 'cancelled', cancelledAt: Date.now() });
      return { count: list.length, refund };
    },

    /* ---------- Reviews ---------- */
    canReview(b) { return b && b.status !== 'cancelled' && startsAt(b) + b.duration * 36e5 < Date.now() && !b.reviewId; },
    async review(id, { rating, text }) {
      const b = store.get().bookings.find((x) => x.id === id);
      if (!bookingService.canReview(b)) throw new ApiError('You can review a pitch after you’ve played there.', 'not_allowed');
      const r = Math.round(+rating);
      const errors = {};
      if (!(r >= 1 && r <= 5)) errors.rating = 'Choose 1 to 5 stars.';
      if ((text || '').trim().length < 10) errors.text = 'Tell other players a little more (at least 10 characters).';
      if (Object.keys(errors).length) throw new ApiError('Check your review.', 'invalid', errors);
      return request(() => {
        const u = store.get().user || {};
        const p = db().find('pitches', b.pitchId);
        const rv = db().insert('reviews', { pitchId: b.pitchId, ownerId: p?.ownerId, bookingId: id, customer: { id: u.id || 'u-001', name: u.name || 'Player', avatar: u.avatar ?? 0 }, rating: r, text: text.trim(), reply: null, reported: null, hidden: false }, 'rv');
        if (p) { const n = (p.reviews || 0) + 1; db().update('pitches', p.id, { reviews: n, rating: Math.round((((p.rating || 0) * (n - 1) + r) / n) * 10) / 10 }); }
        store.set((s) => ({ bookings: s.bookings.map((x) => (x.id === id ? { ...x, reviewId: rv.id } : x)) }));
        db().log('customer', 'review.created', `New ${r}★ review for ${b.pitchName}`, rv.id);
        SA.services.loyalty?.bonus(`review-${id}`, 20, 'Review bonus');
        return rv;
      }, { latency: 350 });
    },
    reviewOf: (b) => (b?.reviewId ? db().find('reviews', b.reviewId) : null),

    todayISO: () => isoDate(today()),
  };

  SA.services.bookings = bookingService;
})(window.SA);
