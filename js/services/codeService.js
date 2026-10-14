/**
 * Discount codes created by partners: pitch codes (used at booking checkout) and store codes
 * (used in the shop cart, only on that store's products). The partner pays for the discount —
 * their revenue is the discounted price. Platform codes (kind "platform") are made by the Super App team in the admin console,
 * work in the shop, on bookings or both, and Super App pays for the discount (partners still earn on the full price).
 * Future API: POST /partner/codes, GET /codes/:code/validate
 */
(function (SA) {
  const { ApiError } = SA.services.http;
  const { isoDate, today } = SA.core.utils;
  const db = () => SA.services.db;
  const D = () => SA.services.dash;

  const norm = (c) => String(c || '').trim().toUpperCase().replace(/\s+/g, '');
  const find = (code) => db().all('codes').find((x) => x.code === norm(code) && !x.deleted);
  const label = (c) => (c.type === 'shipping' ? 'Free delivery' : c.type === 'percent' ? `${Math.round(c.value)}% off` : `${c.value.toLocaleString()} EGP off`);
  const me = () => SA.core.store.get().user?.id || 'u-001';

  /** Times this shopper (this device) already used the code */
  const usedByMe = (c) => (c.usedBy || []).filter((u) => u === me()).length;

  /** Common checks; returns the code or throws with a friendly message */
  const check = (code, { kind, amount }) => {
    const c = find(code);
    const t = isoDate(today());
    if (!c || c.kind !== kind) return null;
    if (c.status !== 'active') throw new ApiError(kind === 'platform' ? 'This code is paused.' : 'This code is paused by the venue.', 'invalid_coupon');
    if (c.from && t < c.from) throw new ApiError(`This code starts on ${SA.core.utils.fmtDate(c.from)}.`, 'invalid_coupon');
    if (c.to && t > c.to) throw new ApiError('This code has expired.', 'invalid_coupon');
    if (c.maxUses && (c.uses || 0) >= c.maxUses) throw new ApiError('This code has been fully used.', 'invalid_coupon');
    if (c.perCustomer && usedByMe(c) >= c.perCustomer) throw new ApiError('You’ve already used this code.', 'invalid_coupon');
    if (c.min && amount < c.min) throw new ApiError(`This code needs at least ${c.min.toLocaleString()} EGP.`, 'min_not_met');
    return c;
  };
  const discountOf = (c, amount) => Math.min(amount, Math.round(c.type === 'percent' ? (amount * c.value) / 100 : c.value));

  const codes = {
    norm, find, label,
    /** Booking checkout: code must belong to the pitch's owner and cover this pitch */
    forBooking(code, { pitchId, amount }) {
      const p = db().find('pitches', pitchId);
      const c0 = find(code);
      if (!c0 || c0.kind !== 'pitch' || c0.ownerId !== p?.ownerId || (c0.pitchIds?.length && !c0.pitchIds.includes(pitchId))) throw new ApiError('That code isn’t valid for this pitch.', 'invalid_coupon');
      const c = check(code, { kind: 'pitch', amount });
      return { code: c.code, id: c.id, label: label(c), amount: discountOf(c, amount) };
    },
    /** Cart: a store code discounts only that store's lines */
    forCart(code, lines) {
      const c0 = find(code);
      if (!c0 || c0.kind !== 'store') return null;
      const mine = lines.filter((l) => l.product.storeId === c0.storeId);
      const sub = mine.reduce((t, l) => t + l.product.price * l.qty, 0);
      if (!mine.length) throw new ApiError(`This code only works on products from ${db().find('stores', c0.storeId)?.name || 'one store'}.`, 'invalid_coupon');
      const c = check(code, { kind: 'store', amount: sub });
      return { code: c.code, id: c.id, storeId: c.storeId, storeName: db().find('stores', c.storeId)?.name, label: `${label(c)} ${db().find('stores', c.storeId)?.name}`, amount: discountOf(c, sub), base: sub };
    },
    /** Super App code for the shop or bookings (null when it isn't a platform code) */
    platform(code, { scope, amount }) {
      const c0 = find(code);
      if (!c0 || c0.kind !== 'platform') return null;
      if (!(c0.scope === 'both' || c0.scope === scope)) throw new ApiError(scope === 'shop' ? 'This code works on pitch bookings, not in the shop.' : 'This code works in the shop, not on pitch bookings.', 'invalid_coupon');
      if (scope === 'bookings' && c0.type === 'shipping') throw new ApiError('This code is for free delivery in the shop.', 'invalid_coupon');
      const c = check(code, { kind: 'platform', amount });
      return { code: c.code, id: c.id, label: c.label || label(c), amount: c.type === 'shipping' ? 0 : discountOf(c, amount), freeShip: c.type === 'shipping', platform: true };
    },
    /** Record a use (called after payment succeeds) */
    use(id, { discount, revenue }) {
      const c = db().find('codes', id); if (!c) return;
      db().update('codes', id, { uses: (c.uses || 0) + 1, discountGiven: (c.discountGiven || 0) + discount, revenue: (c.revenue || 0) + revenue, usedBy: [...(c.usedBy || []), me()] });
    },

    /* ---------- Partner management (permission-checked) ---------- */
    list({ ownerId } = {}) {
      const a = D().me(); if (!a) throw new ApiError('Sign in first.', 'unauthenticated');
      const admin = D().isAdmin();
      return db().all('codes').filter((c) => !c.deleted && (admin ? (!ownerId || c.ownerId === ownerId) : c.ownerId === a.id)).sort((x, y) => y.createdAt - x.createdAt);
    },
    validate(d, { id, ownerId } = {}) {
      const e = {};
      const code = norm(d.code);
      if (!/^[A-Z0-9-]{4,16}$/.test(code)) e.code = 'Use 4–16 letters or numbers (no spaces).';
      else if (SA.data.products.coupons[code] || db().all('codes').some((c) => c.code === code && c.id !== id && !c.deleted)) e.code = 'This code is already taken. Try another.';
      if (!['pitch', 'store'].includes(d.kind)) e.kind = 'Choose where the code works.';
      const v = +d.value;
      if (d.type === 'percent' && !(v >= 5 && v <= 70)) e.value = 'Percentage must be between 5% and 70%.';
      if (d.type === 'fixed' && !(v >= 10 && v <= 5000)) e.value = 'Amount must be between 10 and 5,000 EGP.';
      if (!['percent', 'fixed'].includes(d.type)) e.type = 'Choose a discount type.';
      if (d.min !== '' && d.min != null && !(+d.min >= 0)) e.min = 'Enter 0 or more.';
      if (d.maxUses !== '' && d.maxUses != null && !(Number.isInteger(+d.maxUses) && +d.maxUses >= 0)) e.maxUses = 'Enter a whole number (0 = unlimited).';
      if (d.to && d.from && d.to < d.from) e.to = 'End on or after the start date.';
      if (d.kind === 'store' && !D().myStore(ownerId)) e.kind = 'Open your store first to create store codes.';
      if (d.kind === 'pitch' && !D().myPitches({ ownerId }).length) e.kind = 'Add a pitch first to create booking codes.';
      return e;
    },
    save(d, { id, ownerId } = {}) {
      const a = D().me(); if (!a) throw new ApiError('Sign in first.', 'unauthenticated');
      const owner = D().isAdmin() && ownerId ? ownerId : a.id;
      if (id) { const c = db().find('codes', id); if (!c || (c.ownerId !== a.id && !D().isAdmin())) throw new ApiError('This code belongs to another partner.', 'forbidden'); }
      const e = codes.validate(d, { id, ownerId: owner });
      // Sellers: active codes are limited by their level (2 / 5 / 15 / unlimited)
      const sl = SA.services.sellers?.byAccount(owner);
      if (!id && sl) {
        const lim = SA.services.sellers.levelOf(sl).codes;
        const active = db().all('codes').filter((c) => c.ownerId === owner && !c.deleted && c.status === 'active').length;
        if (lim != null && active >= lim) e.code = `Your level allows ${lim} active code${lim === 1 ? '' : 's'}. Pause one or level up to add more.`;
      }
      if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e);
      const rec = {
        code: norm(d.code), kind: d.kind, ownerId: owner, storeId: d.kind === 'store' ? D().myStore(owner)?.id : null,
        pitchIds: d.kind === 'pitch' ? (d.pitchIds || []).filter(Boolean) : [], type: d.type, value: Math.round(+d.value), min: Math.round(+d.min || 0),
        maxUses: Math.round(+d.maxUses || 0), perCustomer: d.perCustomer ? 1 : 0, from: d.from || isoDate(today()), to: d.to || '', note: (d.note || '').trim(), status: d.status === 'paused' ? 'paused' : 'active',
      };
      const out = id ? db().update('codes', id, rec) : db().insert('codes', { ...rec, uses: 0, discountGiven: 0, revenue: 0, usedBy: [] }, 'cd');
      db().log(a.id, id ? 'code.updated' : 'code.created', `${id ? 'Updated' : 'Created'} discount code ${out.code} (${label(out)})`, out.id);
      return out;
    },
    setStatus(id, status) { const a = D().me(); const c = db().find('codes', id); if (!c || (c.ownerId !== a?.id && !D().isAdmin())) throw new ApiError('Not your code.', 'forbidden'); db().update('codes', id, { status }); db().log(a.id, 'code.status', `${status === 'active' ? 'Resumed' : 'Paused'} code ${c.code}`, id); },
    remove(id) { const a = D().me(); const c = db().find('codes', id); if (!c || (c.ownerId !== a?.id && !D().isAdmin())) throw new ApiError('Not your code.', 'forbidden'); db().update('codes', id, { deleted: true, status: 'paused' }); db().log(a.id, 'code.deleted', `Deleted code ${c.code}`, id); },
  };

  SA.services.codes = codes;
})(window.SA);
