/**
 * Returns & exchanges (Refunds & returns policy, js/data/legal.js).
 * - Within 14 days of delivery. Store products: return (refund) or exchange (another size/colour) for any reason;
 *   "changed my mind" pays the return delivery. Printed designs (made to order): only a fault, damage, wrong size sent
 *   or a wrong print — reprinted by the factory or refunded; never for a change of mind.
 * - One request per seller in the order. Who decides: the store owner (their own store), Super App (the official store
 *   and printed designs). Steps: requested → approved (courier pickup booked) → received → refunded / exchanged;
 *   printed designs skip the pickup: approved → reprinting (factory) or refunded. Or rejected, with a reason.
 * - Refunds go to the customer's wallet with a receipt; the seller's store wallet is charged its share and Super App
 *   returns its commission (recorded as a platform refund).
 *
 * Future API: POST /returns, PATCH /returns/:id (approve, reject, received)
 */
(function (SA) {
  const { ApiError } = SA.services.http;
  const db = () => SA.services.db;
  const N = () => SA.services.notify;
  const DAY = 864e5;
  const WINDOW = 14;
  const REASONS = {
    wrong_size: { label: 'Wrong size', free: true, pod: true },
    faulty: { label: 'Faulty or damaged', free: true, pod: true },
    wrong_item: { label: 'Wrong item or wrong print', free: true, pod: true },
    not_as_described: { label: 'Not as described', free: true, pod: false },
    changed_mind: { label: 'Changed my mind', free: false, pod: false },
  };
  const STATUS = { requested: 'Waiting for the seller', approved: 'Courier pickup booked', received: 'Received by the seller', refunded: 'Refunded', exchanged: 'Replacement sent', reprinting: 'Being reprinted', rejected: 'Not accepted' };
  const OPEN = ['requested', 'approved', 'received', 'reprinting'];

  const ensure = () => { const d = db().raw(); if (!Array.isArray(d.returns)) { d.returns = []; db().touch('returns'); } };
  const all = () => db().all('returns') || [];
  const user = () => SA.core.store.get().user || {};
  const fee = () => SA.data.products.shipping?.standard ?? 60;

  /** Which lines of an order can still be returned, and why not */
  const eligible = (o) => {
    const groups = o.sellers || [];
    const taken = {};
    all().filter((r) => r.orderId === o.id && r.status !== 'rejected').forEach((r) => r.items.forEach((it) => (taken[it.index] = (taken[it.index] || 0) + it.qty)));
    return groups.flatMap((g) => g.lines.map((l) => {
      const deliveredAt = g.at?.delivered || (g.status === 'delivered' ? new Date(`${o.eta}T18:00`).getTime() : 0);
      const left = deliveredAt ? WINDOW - Math.floor((Date.now() - deliveredAt) / DAY) : null;
      const qty = l.qty - (taken[l.index] || 0);
      const why = g.status !== 'delivered' ? 'Not delivered yet' : left < 0 ? `The ${WINDOW}-day window closed` : qty <= 0 ? 'Already returned' : '';
      return { index: l.index, line: l, group: g.pod ? 'pod' : g.storeId, storeName: g.storeName, pod: !!g.pod, official: !!g.official, deliveredAt, daysLeft: left, qty, ok: !why, why };
    }));
  };

  const owns = (r) => {
    const a = SA.services.dash?.me?.();
    if (!a) return false;
    if (a.role === 'admin') return true;
    return r.ownerId !== 'admin' && (r.ownerId === a.id || r.ownerId === a.baseId || r.ownerId === (a.baseRole ? a.id : ''));
  };
  const actor = () => SA.services.dash?.me?.() || { id: 'system', name: 'Super App' };
  const step = (r, status, note = '') => [...r.history, { status, at: Date.now(), by: actor().name, note }];
  const tellCustomer = (r, title, body) => { const u = user(); if (u.id && r.customer?.id === u.id) SA.services.user.notify({ kind: 'shop', title, body, link: `/order/${r.orderId}` }); };
  const tellOwner = (r, n) => (r.ownerId === 'admin' ? N()?.toAdmins(n) : N()?.toAccount(r.ownerId, n));

  const returns = {
    REASONS, STATUS, OPEN, WINDOW,
    eligible,
    forOrder: (orderId) => all().filter((r) => r.orderId === orderId),
    list: ({ ownerId = '', status = '' } = {}) => all().filter((r) => (!ownerId || r.ownerId === ownerId) && (!status || (status === 'open' ? OPEN.includes(r.status) : r.status === status))).sort((a, b) => b.createdAt - a.createdAt),
    get: (id) => { const r = db().find('returns', id); if (!r) throw new ApiError('Return not found.', 'not_found'); return r; },

    /** Customer: ask to return or exchange items. items: [{ index, qty, action: refund|exchange|reprint, size }] */
    async request(orderId, { items = [], reason, note = '' }) {
      ensure();
      const o = await SA.services.shop.order(orderId);
      const R = REASONS[reason]; if (!R) throw new ApiError('Choose the reason.', 'invalid', { reason: 'Choose the reason.' });
      const can = eligible(o);
      const picked = items.filter((x) => +x.qty > 0);
      if (!picked.length) throw new ApiError('Choose at least one item.', 'invalid', { items: 'Choose at least one item.' });
      const groups = {};
      picked.forEach((x) => {
        const e = can.find((c) => c.index === +x.index);
        if (!e || !e.ok) throw new ApiError(`“${e?.line.name || 'This item'}” can’t be returned: ${(e?.why || 'not found').toLowerCase()}.`, 'invalid');
        if (+x.qty > e.qty) throw new ApiError(`Only ${e.qty} of “${e.line.name}” can be returned.`, 'invalid');
        if (e.pod && !R.pod) throw new ApiError('Printed designs are made for you, so they can only be returned for a fault, damage, a wrong size or a wrong print.', 'invalid', { reason: 'Not for printed designs.' });
        const action = e.pod ? (x.action === 'refund' ? 'refund' : 'reprint') : x.action === 'exchange' ? 'exchange' : 'refund';
        if (action === 'exchange') {
          const p = db().find('products', e.line.productId);
          const size = x.size || e.line.variant.size;
          if (!p) throw new ApiError(`“${e.line.name}” isn’t sold any more — choose a refund.`, 'invalid');
          if (size === e.line.variant.size && reason === 'wrong_size') throw new ApiError('Choose the size you want instead.', 'invalid', { size: 'Choose another size.' });
          if ((p.stock ?? 0) < +x.qty) throw new ApiError(`“${e.line.name}” is out of stock — choose a refund.`, 'invalid');
        }
        (groups[e.group] ||= []).push({ index: e.index, name: e.line.name, image: e.line.image, productId: e.line.productId, designId: e.line.designId || '', qty: +x.qty, price: e.line.price, variant: e.line.variant, action, size: action === 'exchange' ? (x.size || e.line.variant.size) : '', pod: e.pod, storeName: e.storeName, official: e.official });
      });
      const u = user();
      const out = Object.entries(groups).map(([g, list]) => {
        const pod = g === 'pod';
        const st = pod ? null : db().find('stores', g);
        const ownerId = pod || !st || st.official ? 'admin' : st.ownerId;
        const value = list.filter((i) => i.action === 'refund').reduce((t, i) => t + i.price * i.qty, 0);
        const charge = R.free || pod ? 0 : fee();
        const r = db().insert('returns', {
          id: `RT-${Date.now().toString(36).toUpperCase().slice(-6)}${Math.floor(Math.random() * 9)}`, orderId, kind: pod ? 'pod' : 'store', storeId: pod ? '' : g, storeName: list[0].storeName, ownerId,
          customer: { id: u.id || 'u-001', name: u.name || 'Customer', phone: o.address?.phone || u.phone || '', city: o.address?.city || '' }, address: o.address, items: list,
          reason, reasonLabel: R.label, note: String(note).trim().slice(0, 400), fee: value ? Math.min(charge, value) : charge, amount: Math.max(0, value - (value ? Math.min(charge, value) : 0)),
          paidBy: o.payment?.method || 'card', status: 'requested', history: [{ status: 'requested', at: Date.now(), by: u.name || 'Customer', note: R.label }],
        }, 'RT');
        db().log('customer', 'return.requested', `Return ${r.id} asked for ${orderId}: ${list.map((i) => `${i.qty}× ${i.name} (${i.action})`).join(', ')} — ${R.label}`, r.id);
        tellOwner(r, { cat: 'orders', tone: 'gold', title: `Return request ${r.id}`, body: `${r.customer.name} · ${list.map((i) => `${i.qty}× ${i.name}`).join(', ')} · ${R.label}`, link: ownerId === 'admin' ? '#/admin/returns' : '#/seller/returns' });
        return r;
      });
      SA.services.user.notify({ kind: 'shop', title: 'Return request sent', body: `${out.map((r) => r.id).join(', ')} — the seller answers within 2 working days.`, link: `/order/${orderId}` });
      return out;
    },

    /** Seller (own store) or Super App: accept or refuse a request */
    async decide(id, approve, note = '') {
      const r = returns.get(id);
      if (!owns(r)) throw new ApiError('This return belongs to another seller.', 'forbidden');
      if (r.status !== 'requested') throw new ApiError('This return was already handled.', 'invalid');
      if (!approve) {
        if (String(note).trim().length < 3) throw new ApiError('Write the reason — the customer sees it.', 'invalid');
        db().update('returns', id, { status: 'rejected', reason2: note.trim(), history: step(r, 'rejected', note.trim()) });
        db().log(actor().id, 'return.rejected', `Return ${id} refused: ${note.trim()}`, id);
        tellCustomer(r, `Return ${id} not accepted`, `${note.trim()} If you disagree, open “Get help” on the order.`);
        return;
      }
      if (r.kind === 'pod') {
        // Printed designs: no pickup — reprint at the factory's cost, or refund straight away
        const reprint = r.items.filter((i) => i.action === 'reprint');
        if (reprint.length) {
          const jobs = (db().all('podOrders') || []).filter((o) => o.orderId === r.orderId && reprint.some((i) => i.designId === o.designId && i.variant.size === o.size && i.variant.color === o.color));
          jobs.forEach((j) => { try { SA.services.factory.complaintReprint(j.id, `Return ${id}: ${r.reasonLabel}${r.note ? ` — ${r.note}` : ''}`); } catch (e) { console.warn(e); } });
          db().update('returns', id, { status: r.amount ? 'refunded' : 'reprinting', history: step(r, 'reprinting', `${jobs.length} item(s) back to the factory`) });
          tellCustomer(r, 'We’re reprinting your item', 'The factory prints it again at no cost to you. You’ll get the tracking number when it ships.');
        }
        if (r.amount) await returns._refund(returns.get(id), 'platform');
        db().log(actor().id, 'return.approved', `Return ${id} accepted (printed design)`, id);
        return;
      }
      const sh = SA.services.shipping?.current() || { name: 'Courier', prefix: 'RTN' };
      const awb = `${sh.prefix}-R${Math.floor(1e6 + Math.random() * 8e6)}`;
      db().update('returns', id, { status: 'approved', courier: sh.name, awb, history: step(r, 'approved', `${sh.name} pickup ${awb}${note ? ` — ${note}` : ''}`) });
      db().log(actor().id, 'return.approved', `Return ${id} accepted — ${sh.name} pickup ${awb}`, id);
      tellCustomer(r, `Return ${id} accepted`, `${sh.name} will pick it up within 2 working days (tracking ${awb}). Keep the item in its original condition${r.fee ? `; ${r.fee} EGP return delivery is taken from the refund` : ''}.`);
    },
    /** The returned item arrived: refund or send the replacement */
    async receive(id, { restock = true } = {}) {
      const r = returns.get(id);
      if (!owns(r)) throw new ApiError('This return belongs to another seller.', 'forbidden');
      if (r.status !== 'approved') throw new ApiError('Only accepted returns can be marked as received.', 'invalid');
      db().update('returns', id, { status: 'received', history: step(r, 'received') });
      if (restock && r.reason !== 'faulty') r.items.forEach((i) => { const p = db().find('products', i.productId); if (p) db().update('products', p.id, { stock: (p.stock || 0) + i.qty }); });
      const ex = r.items.filter((i) => i.action === 'exchange');
      ex.forEach((i) => { const p = db().find('products', i.productId); if (p) db().update('products', p.id, { stock: Math.max(0, (p.stock || 0) - i.qty) }); });
      if (r.amount) await returns._refund(returns.get(id), 'seller');
      if (ex.length) {
        db().update('returns', id, { status: r.amount ? 'refunded' : 'exchanged', history: step(returns.get(id), 'exchanged', ex.map((i) => `${i.qty}× ${i.name} size ${i.size}`).join(', ')) });
        tellCustomer(r, 'Your replacement is on the way', ex.map((i) => `${i.qty}× ${i.name} (${i.size})`).join(', '));
      }
      db().log(actor().id, 'return.received', `Return ${id} received${r.amount ? ` — ${r.amount.toLocaleString()} EGP refunded` : ''}${ex.length ? ' — replacement sent' : ''}`, id);
    },
    /** Money back: the customer's wallet (receipt), the seller's store wallet pays its share, Super App its commission */
    async _refund(r, from) {
      const u = user();
      if (r.customer?.id === u.id && r.amount > 0) await SA.services.payment.refund(r.amount, `Refund — return ${r.id} (${r.orderId})`);
      if (from === 'seller' && r.storeId) {
        const st = db().find('stores', r.storeId);
        const rate = SA.services.dash?.storeRate ? SA.services.dash.storeRate(st) : 0.12;
        const commission = Math.round(r.amount * rate);
        if (st && !st.official) db().insert('adjustments', { accountId: st.ownerId, amount: -(r.amount - commission), reason: `Return ${r.id} for ${r.orderId}: ${r.reasonLabel}`, kind: 'return', ref: r.id, wallet: 'store', at: Date.now(), by: actor().id }, 'adj');
        if (commission) db().insert('refunds', { customerId: r.customer.id, amount: st?.official ? r.amount : commission, bearer: 'platform', kind: 'return', reason: `Commission on return ${r.id}`, ref: r.id, at: Date.now() }, 'rf');
        if (st && !st.official) N()?.toAccount(st.ownerId, { cat: 'money', tone: 'gold', title: `−${(r.amount - commission).toLocaleString()} EGP · return ${r.id}`, body: `Refund to ${r.customer.name}; Super App returned its ${commission.toLocaleString()} EGP commission.`, link: '#/seller/returns' });
      } else db().insert('refunds', { customerId: r.customer.id, amount: r.amount, bearer: 'platform', kind: 'return', reason: `Return ${r.id}`, ref: r.id, at: Date.now() }, 'rf');
      db().update('returns', r.id, { status: 'refunded', refundedAt: Date.now(), history: step(db().find('returns', r.id), 'refunded', `${r.amount.toLocaleString()} EGP to the wallet`) });
      tellCustomer(r, `${r.amount.toLocaleString()} EGP refunded`, `Return ${r.id} — it’s in your Super App wallet. The receipt is in your profile.`);
    },
  };

  SA.services.returns = returns;
})(window.SA);
