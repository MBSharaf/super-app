/**
 * Returns from the real API (owner's decisions, 2026-10-27): only an item with a problem (damaged, wrong item, not as
 * described), within 24 hours of delivery; Super App's courier collects it, the refund goes to the customer's wallet and
 * the store pays for it. Loaded after returnService and liveShop; demo mode (GitHub Pages) is unchanged.
 *
 * The returns UI reads `SA.services.returns` synchronously, so the order page fills a small cache first: what can still
 * be returned (`/orders/{code}/returnable`) and the customer's returns (`/returns/mine`).
 */
(function (SA) {
  const api = SA.services.api;
  const RT = SA.services.returns;
  const S = SA.services.shop;
  const { ApiError } = SA.services.http;
  const demo = { ...RT };
  const live = () => api.isLive();

  const REASONS = {
    faulty: { label: 'Faulty or damaged', free: true, pod: true, server: 'FaultyOrDamaged' },
    wrong_item: { label: 'Wrong item or wrong print', free: true, pod: true, server: 'WrongItemOrPrint' },
    not_as_described: { label: 'Not as described', free: true, pod: false, server: 'NotAsDescribed' },
  };
  const LABEL = Object.fromEntries(Object.values(REASONS).map((r) => [r.server, r.label]));
  const STATUS = { Requested: 'requested', Approved: 'approved', Received: 'received', Refunded: 'refunded', Exchanged: 'exchanged', Reprinting: 'reprinting', Rejected: 'rejected' };

  const returnable = new Map();   // order code → server rows
  let mine = [];                  // the customer's returns (site shape)

  /** Server return → the site's shape (as returns.js reads it) */
  const toReturn = (r, storeName = '') => ({
    id: r.code, orderId: r.orderCode, kind: 'store', storeName: storeName || r.decidedBy, status: STATUS[r.status] || 'requested', live: true,
    items: r.lines.map((l) => ({ index: l.orderItemId, name: l.title, qty: l.qty, action: l.action.toLowerCase(), size: l.newSize || '', variant: { size: l.size, color: l.color }, image: l.image?.url ? `${location.origin}${l.image.url}` : l.image?.key || '' })),
    reason: r.reason, reasonLabel: LABEL[r.reason] || r.reason, note: r.detail || '', amount: +r.refundAmount, fee: 0, reason2: r.rejectReason || '',
    courier: r.pickupTracking ? 'Super App courier' : '', awb: r.pickupTracking || '', createdAt: Date.parse(r.createdAt), history: [], customer: { name: '' },
  });

  /** Loads what the order page needs: the returnable items of this order and the customer's returns */
  const prefetch = async (o) => {
    if (!o?.live) return;
    const [rows, list] = await Promise.all([api.get(`/orders/${encodeURIComponent(o.id)}/returnable`).catch(() => []), api.get('/returns/mine').catch(() => [])]);
    returnable.set(o.id, rows);
    mine = (list.items || list).map((r) => toReturn(r));
  };

  /** Lines of an order that can still be returned (24 hours after delivery), in the shape returns.js reads */
  const eligible = (o) => {
    const rows = returnable.get(o.id) || [];
    return (o.sellers || []).flatMap((g) => g.lines.map((l) => {
      const row = rows.find((x) => x.orderItemId === l.id);
      const until = row ? Date.parse(row.until) : 0;
      const why = !row ? (l.status === 'Delivered' ? 'The 24-hour window closed or it is already being returned' : 'Not delivered yet') : '';
      return { index: l.id, line: l, group: g.pod ? 'pod' : g.storeId, storeName: g.storeName, pod: !!g.pod, official: !!g.official, deliveredAt: l.deliveredAt, until,
        hoursLeft: row ? Math.max(1, Math.ceil((until - Date.now()) / 36e5)) : 0, qty: row?.qtyLeft || 0, exchange: !!row?.actions.includes('Exchange'), ok: !why, why };
    }));
  };

  /** Sends one request per store (a return is decided by one side) */
  const request = async (orderId, { items = [], reason, note = '' }) => {
    const R = REASONS[reason];
    if (!R) throw new ApiError('Choose what is wrong with the item.', 'invalid', { reason: 'Choose what is wrong with the item.' });
    const o = await S.order(orderId);
    const can = eligible(o);
    const picked = items.filter((x) => +x.qty > 0);
    if (!picked.length) throw new ApiError('Choose at least one item.', 'invalid', { items: 'Choose at least one item.' });
    const groups = {};
    picked.forEach((x) => {
      const e = can.find((c) => c.index === +x.index);
      if (!e?.ok) throw new ApiError(`“${e?.line.name || 'This item'}” can’t be returned: ${(e?.why || 'not found').toLowerCase()}.`, 'invalid');
      const action = e.pod ? (x.action === 'refund' ? 'Refund' : 'Reprint') : x.action === 'exchange' ? 'Exchange' : 'Refund';
      (groups[e.group] ||= []).push({ orderItemId: e.index, qty: +x.qty, action, newSize: action === 'Exchange' ? x.size || null : null, newColor: null });
    });
    const out = [];
    for (const list of Object.values(groups)) {
      out.push(toReturn(await api.post('/returns', { orderCode: orderId, items: list, reason: R.server, detail: String(note).trim() || null })));
    }
    await prefetch(o);
    return out;
  };

  // Read when the form opens (the mode is known by then)
  Object.defineProperty(RT, 'REASONS', { get: () => (live() ? REASONS : demo.REASONS), configurable: true });
  Object.assign(RT, {
    windowText: () => (live() ? '24 hours' : `${demo.WINDOW} days`),
    leftText: (can) => (live() ? `${Math.min(...can.map((x) => x.hoursLeft))} hour(s) left` : `${Math.min(...can.map((x) => x.daysLeft))} day(s) left`),
    eligible: (o) => (live() && o?.live ? eligible(o) : demo.eligible(o)),
    forOrder: (orderId) => (live() ? mine.filter((r) => r.orderId === orderId) : demo.forOrder(orderId)),
    request: (orderId, args) => (live() ? request(orderId, args) : demo.request(orderId, args)),
  });

  // The order page loads the order first: fill the returns cache with it
  const order = S.order;
  S.order = async (id) => { const o = await order(id); if (live()) await prefetch(o); return o; };

  SA.services.liveReturns = { prefetch, toReturn };
})(window.SA);
