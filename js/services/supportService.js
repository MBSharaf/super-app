/**
 * Support tickets and disputes. Customers open them from the site (a booking, an order or a printed item can be attached);
 * pitch owners, sellers and the print partner open them from their dashboard. The Super App team answers in the admin console,
 * and can refund a dispute — choosing who pays (Super App, the partner or the seller).
 * Future API: POST /support/tickets, GET /me/tickets, POST /support/tickets/:id/messages, /admin/support/*
 */
(function (SA) {
  const { ApiError } = SA.services.http;
  const db = () => SA.services.db;
  const N = () => SA.services.notify;

  const CATS = {
    booking: { label: 'A booking', icon: 'ticket', dispute: true },
    order: { label: 'A shop order', icon: 'package', dispute: true },
    print: { label: 'A printed item', icon: 'shirt', dispute: true },
    payment: { label: 'Payments & wallet', icon: 'wallet' },
    account: { label: 'My account', icon: 'user' },
    other: { label: 'Something else', icon: 'message' },
  };
  const STATUS = { open: 'Waiting for Super App', pending: 'Waiting for your reply', resolved: 'Resolved' };

  const me = () => SA.core.store.get().user || SA.data.user.profile;
  const nextId = () => `TK-${2042 + (db().all('tickets') || []).length + Math.floor(Math.random() * 9)}`;

  /** Who a booking / order / printed item involves (the partner or seller a dispute is "against") */
  const resolveRef = (ref) => {
    if (!ref?.id) return { label: '', against: null };
    if (ref.type === 'booking') {
      const b = SA.services.dash.findBooking(ref.id) || db().find('bookings', ref.id);
      const p = b && db().find('pitches', b.pitchId);
      return { label: b ? `Booking ${b.id} · ${p?.name || ''} · ${b.date}` : `Booking ${ref.id}`, against: p?.ownerId || null, amount: b ? (b.amount || 0) + (b.fee || 0) : 0 };
    }
    if (ref.type === 'order') {
      const o = (SA.core.store.get().orders || []).find((x) => x.id === ref.id);
      const st = o?.lines.find((l) => !l.pod && l.storeId) && db().find('stores', o.lines.find((l) => !l.pod && l.storeId).storeId);
      const pod = o?.lines.find((l) => l.pod);
      const against = st && !st.official ? st.ownerId : pod ? SA.services.sellers?.get(pod.sellerId)?.accountId : null;
      return { label: o ? `Order ${o.id} · ${o.lines.length} item${o.lines.length > 1 ? 's' : ''}` : `Order ${ref.id}`, against: against || null, amount: o?.total || 0 };
    }
    if (ref.type === 'print') {
      const o = db().find('podOrders', ref.id);
      return { label: o ? `${o.title} · ${o.color} · ${o.size} (${o.id})` : ref.id, against: o ? SA.services.sellers?.get(o.sellerId)?.accountId : null, amount: o ? o.price * (o.qty || 1) : 0 };
    }
    return { label: ref.id, against: null };
  };

  const support = {
    CATS, STATUS, resolveRef,
    all: () => (db().all('tickets') || []).slice().sort((a, b) => b.updatedAt - a.updatedAt),
    get: (id) => db().find('tickets', id),
    /** Tickets opened by the shopper on this device */
    mine: () => support.all().filter((t) => t.from?.kind === 'customer' && t.from.id === me().id),
    /** Tickets opened by a dashboard account */
    forAccount: (accountId) => support.all().filter((t) => t.from?.id === accountId),

    /** Open a ticket. from = { kind: customer|partner|seller|printer, id, name } (defaults to this device's shopper) */
    create({ category, subject, text, ref = null, from = null }) {
      if (!CATS[category]) throw new ApiError('Choose what it’s about.', 'invalid', { category: 'Required' });
      const s = String(subject || '').trim(), t = String(text || '').trim();
      const e = {};
      if (s.length < 4) e.subject = 'Write a short subject.';
      if (t.length < 10) e.text = 'Tell us a bit more (at least 10 characters).';
      if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e);
      const u = me();
      const who = from || { kind: 'customer', id: u.id, name: u.name, phone: u.phone || '' };
      const r = resolveRef(ref);
      const now = Date.now();
      const tk = db().insert('tickets', {
        id: nextId(), category, subject: s.slice(0, 120), from: who, ref: ref?.id ? { ...ref, label: r.label } : null, against: r.against, amount: r.amount || 0,
        status: 'open', priority: CATS[category].dispute ? 'high' : 'normal', refund: null,
        messages: [{ by: who.kind, name: who.name, text: t.slice(0, 2000), at: now }], createdAt: now, updatedAt: now,
      });
      db().log(who.kind === 'customer' ? 'customer' : who.id, 'support.opened', `${who.name} opened ${tk.id}: ${tk.subject}`, tk.id);
      N()?.toAdmins({ cat: 'account', tone: CATS[category].dispute ? 'danger' : '', title: `${CATS[category].dispute ? 'Dispute' : 'Support'}: ${tk.subject}`, body: `${who.name} · ${tk.id}`, link: `#/admin/support/${tk.id}` });
      return tk;
    },
    /** The customer, partner or seller answers */
    reply(id, text, from = null) {
      const tk = support.get(id); if (!tk) throw new ApiError('Request not found.', 'not_found');
      const t = String(text || '').trim();
      if (t.length < 2) throw new ApiError('Write a message first.', 'invalid', { text: 'Write a message' });
      const who = from || { kind: 'customer', name: me().name };
      db().update('tickets', id, { messages: [...tk.messages, { by: who.kind, name: who.name, text: t.slice(0, 2000), at: Date.now() }], status: 'open' });
      N()?.toAdmins({ cat: 'account', title: `New reply on ${tk.id}`, body: `${who.name}: ${t.slice(0, 90)}`, link: `#/admin/support/${tk.id}` });
    },
    /** Seed requests so the console has history (demo) */
    seed(now = Date.now()) {
      const H = 36e5;
      const pod = (db().all('podOrders') || []).find((o) => o.status === 'delivered' && o.sellerId === 'sl-koora');
      const msg = (by, name, text, ago) => ({ by, name, text, at: now - ago * H });
      return [
        { id: 'TK-2041', category: 'print', subject: 'The print on my hoodie is cracked after one wash', from: { kind: 'customer', id: pod?.customer.id || 'c-pod-0-0', name: pod?.customer.name || 'Hazem Tarek' },
          ref: pod ? { type: 'print', id: pod.id, label: `${pod.title} · ${pod.color} · ${pod.size} (${pod.id})` } : null, against: 'acc-s1', amount: pod?.price || 520, status: 'open', priority: 'high', refund: null,
          messages: [msg('customer', pod?.customer.name || 'Hazem Tarek', 'I washed it once on cold like the label says and the print already has cracks across the logo. Can you send a new one?', 5)], createdAt: now - 5 * H, updatedAt: now - 5 * H },
        { id: 'TK-2040', category: 'booking', subject: 'Floodlights were off for the first 20 minutes', from: { kind: 'customer', id: 'c-disp-1', name: 'Mostafa Gamal' },
          ref: { type: 'booking', id: 'BK-DEMO-2040', label: 'Booking at Gezira Fives · last Friday 9 PM' }, against: 'acc-o2', amount: 960, status: 'open', priority: 'high', refund: null,
          messages: [msg('customer', 'Mostafa Gamal', 'We paid for 9–10 PM but the lights only came on at 9:20. Staff said it was a power cut. We’d like part of the money back.', 20)], createdAt: now - 20 * H, updatedAt: now - 20 * H },
        { id: 'TK-2038', category: 'payment', subject: 'When will my withdrawal arrive?', from: { kind: 'partner', id: 'acc-o3', name: 'Mariam Fathy' }, ref: null, against: null, amount: 0, status: 'pending', priority: 'normal', refund: null,
          messages: [msg('partner', 'Mariam Fathy', 'I requested 2,750 EGP yesterday. Is there anything missing on my side?', 22), msg('admin', 'Super App HQ', 'Hi Mariam — it’s in today’s batch. You’ll get a notification as soon as it’s sent.', 2)], createdAt: now - 22 * H, updatedAt: now - 2 * H },
        { id: 'TK-2035', category: 'account', subject: 'Appeal: “Press Start Hoodie” was rejected', from: { kind: 'seller', id: 'acc-s3', name: 'Nour Khaled' }, ref: null, against: null, amount: 0, status: 'resolved', priority: 'normal', refund: null,
          messages: [msg('seller', 'Nour Khaled', 'The controller in my design is drawn by me — it isn’t a console brand’s logo.', 4 * 24), msg('admin', 'Super App HQ', 'Thanks for explaining. We checked it again: send it once more without the brand-style buttons and it will go live.', 3 * 24)], createdAt: now - 4 * 24 * H, updatedAt: now - 3 * 24 * H },
      ];
    },
  };
  SA.services.support = support;
  // First run on this device: a few requests so the admin console has history
  if (!db().all('tickets')) { db().raw().tickets = support.seed(); try { db().touch('tickets'); } catch { /* storage full */ } }
})(window.SA);
