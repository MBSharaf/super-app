/**
 * Shipping: Super App's contract with a courier, connected to the factory automatically.
 *
 * - The courier is one adapter in PROVIDERS. Today Bosta is used as a trial; switching courier is one setting in the
 *   admin console (settings.courier.provider) and only affects new shipments. With the backend, each adapter calls
 *   the courier's real API (create shipment → airway bill, pickup request, status webhooks) behind the same methods.
 * - When the factory packs the last item of a customer order, `packed()` creates the shipment: an airway bill number,
 *   cash to collect for cash-on-delivery, and a pickup request from the factory.
 * - Courier updates move the items: picked up → shipped (customer and seller are told, with the tracking number),
 *   out for delivery, delivered or refused (pod.advance handles money, sales and refusal counts).
 * - Demo: there is no real courier, so live shipments move on their own every few seconds (`sweep`), and the factory
 *   and admin can push the next update by hand to test.
 */
(function (SA) {
  const { ApiError } = SA.services.http;
  const db = () => SA.services.db;
  const N = () => SA.services.notify;
  const P = () => SA.services.pod;

  const PROVIDERS = {
    bosta: { key: 'bosta', name: 'Bosta', prefix: 'BST', note: 'Trial — no contract signed yet' },
    aramex: { key: 'aramex', name: 'Aramex', prefix: 'ARX', note: '' },
    mylerz: { key: 'mylerz', name: 'Mylerz', prefix: 'MYZ', note: '' },
    jnt: { key: 'jnt', name: 'J&T Express', prefix: 'JNT', note: '' },
  };
  const FLOW = ['awaiting_pickup', 'picked_up', 'out_for_delivery', 'delivered'];
  const STATUS_LABEL = { awaiting_pickup: 'Waiting for pickup', picked_up: 'Picked up', out_for_delivery: 'Out for delivery', delivered: 'Delivered', refused: 'Refused', cancelled: 'Cancelled' };
  // Demo timing: seconds before the courier's next update on a live shipment
  const DEMO_STEP = { awaiting_pickup: 40, picked_up: 45, out_for_delivery: 45 };

  const cfg = () => db().settings().courier || { provider: 'bosta', mode: 'test', auto: true };
  const current = () => PROVIDERS[cfg().provider] || PROVIDERS.bosta;
  const awb = (p) => `${p.prefix}-${Math.floor(10e6 + Math.random() * 89e6)}`;

  const shipping = {
    PROVIDERS, FLOW, STATUS_LABEL, cfg, current,
    list: ({ factoryId, status } = {}) => (db().all('shipments') || []).filter((s) => (!factoryId || s.factoryId === factoryId) && (!status || (Array.isArray(status) ? status.includes(s.status) : s.status === status)))
      .sort((a, b) => b.createdAt - a.createdAt),
    get: (id) => db().find('shipments', id),
    forOrder: (orderId) => (db().all('shipments') || []).filter((s) => s.orderId === orderId),

    /** The factory packed an item: when every item of this order (at this factory) is packed, book the courier */
    packed(orderId, factoryId) {
      const items = db().all('podOrders').filter((o) => o.orderId === orderId && o.factoryId === factoryId && o.status !== 'cancelled');
      if (items.some((o) => ['new', 'printing', 'qc', 'packing'].includes(o.stage))) return null; // the rest of the order isn't packed yet
      const ready = items.filter((o) => o.stage === 'ready' && !o.shipmentId);
      if (!ready.length) return null;
      return shipping.create({ orderId, factoryId, items: ready });
    },
    create({ orderId, factoryId, items }) {
      const p = current();
      const f = db().find('factories', factoryId);
      const first = items[0];
      // Cash on delivery: the courier collects the price of the printed items in this parcel
      const cod = first.payment === 'cod' ? items.reduce((t, o) => t + o.price * o.qty, 0) : 0;
      const sh = db().insert('shipments', {
        id: `SH-${Date.now().toString(36).toUpperCase().slice(-6)}`, provider: p.key, providerName: p.name, mode: cfg().mode, awb: awb(p), orderId, factoryId,
        items: items.map((o) => o.id), customer: { ...first.customer }, cod, status: 'awaiting_pickup',
        events: [{ status: 'awaiting_pickup', at: Date.now(), text: `Pickup requested from ${f?.name || 'the factory'}` }], live: true, nextAt: Date.now() + DEMO_STEP.awaiting_pickup * 1000,
      }, 'SH');
      items.forEach((o) => db().update('podOrders', o.id, { shipmentId: sh.id }));
      db().log(f?.accountId || 'acc-print', 'factory.shipment', `${p.name} shipment ${sh.awb} booked for ${orderId} (${items.length} item${items.length > 1 ? 's' : ''})`, sh.id);
      N()?.toAccount(f?.accountId, { cat: 'orders', tone: 'turf', title: `Courier booked: ${sh.awb}`, body: `${p.name} will pick up ${orderId}. Print the label and stick it on the parcel.`, link: '#/factory/shipping' });
      const u = SA.core.store.get().user;
      if (u && first.customer?.id === u.id && !first.sample) SA.services.user.notify({ kind: 'shop', title: 'Your parcel is booked with the courier', body: `${p.name} · tracking ${sh.awb}. You’ll get a message when it leaves the factory.`, link: `/order/${orderId}` });
      return sh;
    },
    /** A courier status update (from the courier's webhook with the backend; simulated in the demo) */
    update(id, status, text = '') {
      const sh = db().find('shipments', id); if (!sh) throw new ApiError('Shipment not found.', 'not_found');
      if (['delivered', 'refused', 'cancelled'].includes(sh.status)) throw new ApiError('This shipment is closed.', 'invalid');
      const allowed = { awaiting_pickup: ['picked_up', 'cancelled'], picked_up: ['out_for_delivery'], out_for_delivery: ['delivered', 'refused'] }[sh.status] || [];
      if (!allowed.includes(status)) throw new ApiError(`Can’t go from ${STATUS_LABEL[sh.status]} to ${STATUS_LABEL[status]}.`, 'invalid');
      db().update('shipments', id, { status, events: [...sh.events, { status, at: Date.now(), text }], nextAt: DEMO_STEP[status] ? Date.now() + DEMO_STEP[status] * 1000 : 0 });
      sh.items.forEach((itemId) => {
        const o = db().find('podOrders', itemId); if (!o || o.status === 'cancelled') return;
        // A replacement for a quality complaint: the sale is already counted, only the factory stage moves
        if (o.complaints && ['delivered', 'refused'].includes(o.status)) { db().update('podOrders', itemId, { stage: status === 'picked_up' ? 'shipped' : status === 'out_for_delivery' ? 'shipped' : status }); return; }
        try {
          if (status === 'picked_up') { db().update('podOrders', itemId, { stage: 'shipped' }); P().advance(itemId, 'shipped', { courier: sh.providerName, tracking: sh.awb }); }
          if (status === 'delivered') { db().update('podOrders', itemId, { stage: 'delivered' }); P().advance(itemId, 'delivered'); }
          if (status === 'refused') { db().update('podOrders', itemId, { stage: 'refused' }); P().advance(itemId, 'refused'); N()?.toAdmins({ cat: 'orders', tone: 'danger', title: `Parcel refused: ${sh.awb}`, body: `${sh.customer?.name || 'Customer'} · ${sh.orderId}`, link: '#/admin/factories?tab=shipping' }); }
          if (status === 'out_for_delivery') { const u = SA.core.store.get().user; if (u && o.customer?.id === u.id) SA.services.user.notify({ kind: 'shop', title: 'Out for delivery today', body: `“${o.title}” · ${sh.providerName} ${sh.awb}`, link: `/order/${o.orderId}` }); }
        } catch (e) { console.warn(e); }
      });
      return db().find('shipments', id);
    },
    /** Push the next courier update by hand (testing) */
    next(id, { refuse = false } = {}) {
      const sh = db().find('shipments', id); if (!sh) throw new ApiError('Shipment not found.', 'not_found');
      const to = sh.status === 'out_for_delivery' ? (refuse ? 'refused' : 'delivered') : FLOW[FLOW.indexOf(sh.status) + 1];
      if (!to) throw new ApiError('This shipment is closed.', 'invalid');
      return shipping.update(id, to);
    },
    /** Remove a cancelled item from its parcel; an empty parcel is cancelled with the courier */
    dropItem(itemId) {
      const o = db().find('podOrders', itemId); const sh = o?.shipmentId && db().find('shipments', o.shipmentId);
      if (!sh || sh.status !== 'awaiting_pickup') return;
      const items = sh.items.filter((x) => x !== itemId);
      db().update('shipments', sh.id, items.length ? { items } : { items, status: 'cancelled', events: [...sh.events, { status: 'cancelled', at: Date.now(), text: 'Cancelled with the courier' }] });
    },
    /** Admin: switch courier (new shipments only) */
    setProvider(key, mode = cfg().mode) {
      SA.services.admin.me();
      if (!PROVIDERS[key]) throw new ApiError('Unknown courier.', 'invalid');
      if (!['test', 'live'].includes(mode)) throw new ApiError('Choose test or live.', 'invalid');
      const before = current().name;
      db().setSettings({ courier: { ...cfg(), provider: key, mode } });
      SA.services.admin.log('shipping.provider', `Courier set to ${PROVIDERS[key].name} (${mode})${before !== PROVIDERS[key].name ? ` — was ${before}` : ''}`, key);
      db().all('factories').forEach((f) => N()?.toAccount(f.accountId, { cat: 'orders', title: `New courier: ${PROVIDERS[key].name}`, body: 'New parcels are booked with this courier automatically. Use its labels from now on.', link: '#/factory/shipping' }));
    },
    /** Demo courier: live shipments move along on their own */
    sweep() {
      (db().all('shipments') || []).filter((s) => s.live && s.nextAt && s.nextAt <= Date.now() && !['delivered', 'refused', 'cancelled'].includes(s.status)).forEach((s) => {
        try { shipping.next(s.id, { refuse: s.status === 'out_for_delivery' && SA.core.utils.seeded(`${s.id}|refuse`) < 0.08 }); } catch (e) { console.warn(e); }
      });
    },
  };

  SA.services.shipping = shipping;
  setInterval(() => { try { shipping.sweep(); } catch (e) { console.warn(e); } }, 8000);
})(window.SA);
