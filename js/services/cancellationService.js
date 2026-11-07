/**
 * Cancellation requests (owner's rule, 2026-11-07). A customer never cancels a paid service straight away: pressing
 * "Cancel" on a pitch booking, a weekly package, a shop order, a request to join a game or a joined game creates a
 * cancellation request with its own number (CX-…). The request goes to the admin console (#/admin/cancellations), the
 * chatbot tells the customer it was sent and that the team replies within 2 hours, and the n8n automation gets
 * `cancellation.requested` (the trigger of the AI agent's workflow). The admin (or the AI agent) approves it — the
 * service is then cancelled with the usual refund rules — or refuses it with a reason. Either way the customer is told.
 * Demo data: db.cancelRequests. On the real API (js/services/liveCancellations.js) the server does all of it: POST /cancellations,
 * GET /cancellations/mine, GET /admin/cancellations, POST /admin/cancellations/{code}/approve|reject.
 */
(function (SA) {
  const { ApiError } = SA.services.http;
  const S = SA.services;
  const db = () => S.db;
  const store = SA.core.store;
  const REPLY_HOURS = 2;
  const KINDS = { booking: 'Pitch booking', package: 'Weekly package', order: 'Shop order', 'game-request': 'Request to join a game', game: 'Joined game' };
  const OPEN = ['pending'];
  const me = () => store.get().user || SA.data.user.profile;
  const all = () => db().all('cancelRequests') || [];
  const admin = () => { const a = S.dash?.me(); if (!a || a.role !== 'admin') throw new ApiError('Sign in to the admin console first.', 'forbidden'); return a; };
  const startsAt = (b) => { const d = SA.core.utils.parseISO(b.date); d.setHours(0, 0, 0, 0); return d.getTime() + b.hour * 36e5; };

  /** Code: CX- + 6 letters/digits, unique */
  const newCode = () => {
    const A = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
    let c;
    do { c = `CX-${Array.from({ length: 6 }, () => A[Math.floor(Math.random() * A.length)]).join('')}`; } while (all().some((x) => x.id === c));
    return c;
  };

  /** What is being cancelled: its label, link, what was paid and the refund the rules would give today */
  const subject = (kind, refId) => {
    if (kind === 'booking') {
      const b = S.bookings.getSync(refId);
      if (!b) throw new ApiError('Booking not found.', 'not_found');
      if (b.status === 'cancelled') throw new ApiError('This booking is already cancelled.', 'invalid');
      if (startsAt(b) < Date.now() || b.checkedInAt) throw new ApiError('This booking has started, so it can’t be cancelled.', 'invalid');
      const pol = S.bookings.cancellationPolicy(b);
      return { label: `${b.pitchName} · ${b.date} ${SA.core.utils.fmtHour(b.hour)}`, link: `/booking/${b.id}`, paid: S.bookings.paidOnline(b), refund: pol.refund, policy: pol.policy?.name || null, pitchId: b.pitchId };
    }
    if (kind === 'package') {
      const list = S.bookings.seriesOf(refId).filter((b) => b.status === 'confirmed' && startsAt(b) > Date.now());
      if (!list.length) throw new ApiError('This package has no games left to cancel.', 'invalid');
      return { label: `${list[0].pitchName} · ${list.length} remaining weekly games`, link: `/booking/${list[0].id}`, paid: list.reduce((t, b) => t + S.bookings.paidOnline(b), 0),
        refund: list.reduce((t, b) => t + S.bookings.cancellationPolicy(b).refund, 0), games: list.length, pitchId: list[0].pitchId };
    }
    if (kind === 'order') {
      const o = store.get().orders.find((x) => x.id === refId);
      if (!o) throw new ApiError('Order not found.', 'not_found');
      const t = S.shop.trackingSync(o);
      if (t.status === 'cancelled') throw new ApiError('This order is already cancelled.', 'invalid');
      if (['shipped', 'delivered'].includes(t.status)) throw new ApiError('This order has shipped. Ask for a return after delivery instead.', 'invalid');
      const paid = o.payment?.method === 'cod' ? 0 : Math.max(0, o.total - (o.refunded || 0));
      return { label: `Order ${o.id} · ${o.lines.length} item${o.lines.length > 1 ? 's' : ''}`, link: `/order/${o.id}`, paid, refund: paid };
    }
    if (kind === 'game-request') {
      const r = db().find('gameRequests', refId);
      if (!r || r.player?.id !== me().id) throw new ApiError('Request not found.', 'not_found');
      if (!['pending', 'waitlist', 'accepted'].includes(r.status)) throw new ApiError('This request is already closed.', 'invalid');
      const m = S.matches.allSync().find((x) => x.id === r.matchId);
      const late = r.status === 'accepted' && m && m.startsAt - Date.now() < 6 * 36e5;
      return { label: `Game at ${r.pitchName}`, link: `/players/${r.matchId}`, paid: r.paid ? r.amount : 0, refund: r.paid && !late ? r.amount : 0, matchId: r.matchId };
    }
    if (kind === 'game') {
      const m = S.matches.allSync().find((x) => x.id === refId);
      if (!m || !m.joined) throw new ApiError('You’re not in this game.', 'not_found');
      const req = store.get().matchRequests[refId];
      const paid = req && req.payment !== 'Pay at the pitch' ? m.pricePerPlayer : 0;
      return { label: `Game at ${m.pitch.name}`, link: `/players/${m.id}`, paid, refund: paid, matchId: m.id };
    }
    throw new ApiError('This can’t be cancelled.', 'invalid');
  };

  const cancellations = {
    REPLY_HOURS, KINDS,
    all: () => all().slice().sort((a, b) => b.createdAt - a.createdAt),
    mine: () => cancellations.all().filter((x) => x.customer?.id === me().id),
    /** The open request for something, if there is one (pages show it instead of the Cancel button) */
    openFor: (kind, refId) => all().find((x) => x.kind === kind && x.refId === refId && OPEN.includes(x.status)) || null,
    lastFor: (kind, refId) => cancellations.all().find((x) => x.kind === kind && x.refId === refId) || null,
    pending: () => cancellations.all().filter((x) => OPEN.includes(x.status)),

    /** The customer pressed "Cancel": create the request (nothing is cancelled yet) */
    request({ kind, refId, reason = '' }) {
      S.user?.assertActive?.();
      if (cancellations.openFor(kind, refId)) throw new ApiError('You already asked to cancel this. We’ll reply within 2 hours.', 'already');
      const sub = subject(kind, refId);
      const u = me();
      const now = Date.now();
      const rec = db().insert('cancelRequests', {
        id: newCode(), kind, kindLabel: KINDS[kind], refId, ...sub, reason: String(reason || '').trim().slice(0, 300), status: 'pending',
        customer: { id: u.id || null, name: u.name || 'Customer', email: u.email || null, phone: u.phone || null }, dueAt: now + REPLY_HOURS * 36e5, channel: 'chatbot',
      });
      db().log('customer', 'cancellation.requested', `${rec.customer.name} asked to cancel ${rec.kindLabel.toLowerCase()} — ${rec.label} (${rec.id})`, rec.id);
      S.notify?.toAdmins({ cat: 'account', tone: 'blaze', title: `Cancellation request ${rec.id}`, body: `${rec.customer.name} · ${rec.kindLabel}: ${rec.label}. Reply within ${REPLY_HOURS} hours.`, link: `#/admin/cancellations?id=${rec.id}` });
      S.user?.notify({ kind: 'shop', title: `Cancellation request ${rec.id} sent`, body: `We’ll reply within ${REPLY_HOURS} hours. Your ${rec.kindLabel.toLowerCase()} stays active until then.`, link: rec.link });
      SA.ui.chatbot?.cancellation(rec, 'sent');
      return rec;
    },

    /** Admin: cancel the service with the usual refund rules, then tell the customer */
    async approve(id, note = '') {
      admin();
      const r = db().find('cancelRequests', id);
      if (!r || r.status !== 'pending') throw new ApiError('This request is already answered.', 'invalid');
      let refund = 0;
      if (r.kind === 'booking') { const b = S.bookings.getSync(r.refId); if (b?.publishedMatchId) await S.matches.unpublish(b.publishedMatchId); refund = (await S.bookings.cancel(r.refId)).refund || 0; }
      else if (r.kind === 'package') refund = (await S.bookings.cancelSeries(r.refId)).refund || 0;
      else if (r.kind === 'order') refund = await cancelOrder(r.refId);
      else if (r.kind === 'game-request') { const x = db().find('gameRequests', r.refId); refund = (await S.games.withdraw(r.refId)).refunded ? x.amount : 0; }
      else if (r.kind === 'game') { await S.matches.leave(r.refId); refund = r.refund; }
      const out = db().update('cancelRequests', id, { status: 'approved', refunded: refund, note: String(note || '').trim().slice(0, 300), decidedAt: Date.now(), decidedBy: S.dash?.me()?.name || 'Super App' });
      db().log(S.dash?.me()?.id || 'admin', 'admin.cancellation.approved', `Approved cancellation ${id} (${r.label})${refund ? ` — ${refund} EGP refunded` : ''}`, id);
      S.user?.notify({ kind: 'shop', title: `Cancellation ${id} approved`, body: `${r.kindLabel} cancelled.${refund ? ` ${refund.toLocaleString()} EGP is back in your wallet.` : ''}${out.note ? ` ${out.note}` : ''}`, link: r.link });
      SA.ui.chatbot?.cancellation(out, 'approved');
      return out;
    },
    reject(id, reason) {
      admin();
      const r = db().find('cancelRequests', id);
      if (!r || r.status !== 'pending') throw new ApiError('This request is already answered.', 'invalid');
      const why = String(reason || '').trim();
      if (why.length < 5) throw new ApiError('Write the reason for the customer (5+ characters).', 'invalid', { reason: 'Write the reason.' });
      const out = db().update('cancelRequests', id, { status: 'rejected', note: why.slice(0, 300), decidedAt: Date.now(), decidedBy: S.dash?.me()?.name || 'Super App' });
      db().log(S.dash?.me()?.id || 'admin', 'admin.cancellation.rejected', `Refused cancellation ${id} (${r.label}): ${why}`, id);
      S.user?.notify({ kind: 'shop', title: `Cancellation ${id} not approved`, body: `${why} Your ${r.kindLabel.toLowerCase()} stays active.`, link: r.link });
      SA.ui.chatbot?.cancellation(out, 'rejected');
      return out;
    },
  };

  /** Cancel every not-yet-shipped part of a shop order on this device: stock back, the rest of the money refunded */
  const cancelOrder = async (orderId) => {
    const o = store.get().orders.find((x) => x.id === orderId);
    if (!o) throw new ApiError('Order not found.', 'not_found');
    const now = Date.now();
    o.lines.forEach((l, i) => {
      const sale = db().find('sales', `${o.id}-${i + 1}`);
      if (sale) db().update('sales', sale.id, { fulfil: { ...(sale.fulfil || {}), status: 'cancelled', at: { ...(sale.fulfil?.at || {}), cancelled: now }, reason: 'Cancelled at the customer’s request' } });
      const pr = l.productId && db().find('products', l.productId);
      if (pr && !l.pod) db().update('products', pr.id, { stock: (pr.stock || 0) + l.qty });
    });
    (db().all('podOrders') || []).filter((x) => x.orderId === o.id).forEach((x) => db().update('podOrders', x.id, { status: 'cancelled', history: [...(x.history || []), { status: 'cancelled', at: now }] }));
    const left = o.payment?.method === 'cod' ? 0 : Math.max(0, o.total - (o.refunded || 0));
    if (left > 0) await S.payment.refund(left, `Refund — ${o.id} (cancelled)`);
    store.set((st) => ({ orders: st.orders.map((x) => (x.id === o.id ? { ...x, refunded: (x.refunded || 0) + left, cancelledAt: now } : x)) }));
    return left;
  };

  SA.services.cancellations = cancellations;
  if (!db().all('cancelRequests')) { db().raw().cancelRequests = []; try { db().touch('cancelRequests'); } catch { /* storage full */ } }
})(window.SA);
