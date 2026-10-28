/**
 * Store owners on the real API (owner's decisions, 2026-10-27): products, orders and returns of
 *   - the signed-in seller's own store (`/seller/store/...`, `/seller/returns`) in the seller dashboard, with the seller
 *     wallet, withdrawals and store discount codes;
 *   - Super App's official store (`/admin/store/...`) and the returns Super App decides (`/admin/returns`) in the admin console.
 * Loaded after liveShop and liveReturns; demo mode (GitHub Pages) is unchanged.
 *
 * The dashboard pages read `SA.services.dash` synchronously from the local data, so each store, its products and orders
 * are mirrored there (records marked `live`) when its dashboard opens. Changes show at once and go to the server; the
 * mirror is reloaded with the server's answer, and a refused change is undone with the server's message.
 *
 * Products keep their shop slug as id (so "View on the site" opens the product page). The site keeps one stock number
 * per product with its sizes and colours; the server keeps stock per size and colour (variant): a new product's stock is
 * shared out over its variants and a restock goes to the variant with the least. A seller's product is sent for review
 * when published (Super App approves each one): "Waiting for review" or "Not approved" with the reason.
 */
(function (SA) {
  const api = SA.services.api;
  const D = SA.services.dash;
  const RT = SA.services.returns;
  const db = SA.services.db;
  const { ApiError } = SA.services.http;

  const fail = (err) => { SA.ui.overlay.toast(err.message, { type: 'error' }); SA.app?.rerender?.(); };
  const photo = (ph) => (!ph ? '' : ph.url ? `${location.origin}${ph.url}` : ph.key);
  const STATUS_IN = { Published: 'active', Draft: 'draft', Unpublished: 'draft', WaitingForReview: 'review', Rejected: 'rejected' };
  const OSTATUS = { New: 'confirmed', Packed: 'packed', Shipped: 'shipped', Delivered: 'delivered', Cancelled: 'cancelled' };
  const PAY = { Online: 'Card', Wallet: 'Super App wallet', CashOnDelivery: 'Cash on delivery' };

  /** Replaces the records `drop` picks with the given list (a mirror of the server) */
  const put = (col, list, drop) => {
    const raw = db.raw();
    raw[col] = [...(raw[col] || []).filter((x) => !drop(x)), ...list];
    db.touch?.(col);
  };

  /** Sizes × colours with the stock shared out (the site has one stock number); existing variants keep their id */
  const variantsOf = (d, old = []) => {
    const sizes = d.sizes?.length ? d.sizes : ['One size'];
    const colors = d.colors?.length ? d.colors : [{ name: 'Standard', hex: '#0D1411' }];
    const pairs = sizes.flatMap((size) => colors.map((c) => ({ size, c })));
    const total = Math.max(0, Math.round(+d.stock || 0));
    const oldTotal = old.reduce((t, v) => t + v.stock, 0);
    return pairs.map(({ size, c }, i) => {
      const prev = old.find((v) => v.size === size && v.colorName === c.name);
      const share = Math.floor(total / pairs.length) + (i < total % pairs.length ? 1 : 0);
      const stock = prev && total === oldTotal && old.length === pairs.length ? prev.stock : share;
      return { id: prev?.id || null, size, colorName: c.name, colorHex: /^#[0-9a-f]{6}$/i.test(c.hex || '') ? c.hex : null, sku: prev?.sku || null, stock, priceOverride: null };
    });
  };
  const requestOf = (d, status, old) => ({
    name: d.name.trim(), categoryKey: d.cat, brand: (d.brand || '').trim() || null, description: (d.desc || '').trim(), price: Math.round(+d.price),
    compareAtPrice: d.was && +d.was > +d.price ? Math.round(+d.was) : null, lowStockAt: d.lowStockAt === undefined || d.lowStockAt === '' ? 5 : +d.lowStockAt,
    status, specs: (d.specs || []).filter(([k, v]) => k && v).map(([label, value]) => ({ label, value })), variants: variantsOf(d, old?.variants),
  });

  /**
   * One store on the server, mirrored for the dashboard. cfg: area ('/seller' | '/admin'), store (local store id),
   * owner() (the dashboard account that owns it), active() (this dashboard is open), official, returnsFor(opts) (the
   * returns list this store answers), base (the local store record to start from).
   */
  const mountStore = (cfg) => {
    const { area, store } = cfg;
    const demo = { ...D };
    const demoRt = { list: RT.list, get: RT.get, decide: RT.decide, receive: RT.receive };
    const on = () => api.isLive() && cfg.active() && !!db.find('stores', store)?.live;
    const mineProduct = (id) => api.isLive() && db.find('products', id)?.live && db.find('products', id).storeId === store;
    let orders = [];
    let returns = [];

    /* ---------- Mirror ---------- */

    const toProduct = (p) => ({
      id: p.slug || `new-${p.id}`, apiId: p.id, live: true, slug: p.slug, storeId: store, name: p.name, brand: p.brand || 'Unbranded', cat: p.categoryKey, price: +p.price,
      was: p.compareAtPrice ? +p.compareAtPrice : +p.price, stock: p.stock, lowStockAt: p.lowStockAt,
      sizes: [...new Set(p.variants.map((v) => v.size))], colors: [...new Map(p.variants.map((v) => [v.colorName, { name: v.colorName, hex: v.colorHex || '#0D1411' }])).values()],
      images: p.images.map((i) => photo(i.photo)), imageIds: p.images.map((i) => ({ id: i.id, src: photo(i.photo) })), variants: p.variants,
      desc: p.description || '', specs: p.specs.map((s) => [s.label, s.value]), badges: [], status: STATUS_IN[p.status] || 'draft', reviewNote: p.reviewNote || '',
      hiddenByAdmin: p.hiddenByAdmin, hiddenReason: p.hiddenReason || '', rating: 0, reviews: 0, createdAt: Date.parse(p.createdAt), updatedAt: Date.parse(p.createdAt), deleted: false,
    });
    const loadProducts = async () => {
      const list = await api.get(`${area}/store/products`);
      put('products', list.map(toProduct), (x) => x.storeId === store || (x.live && list.some((p) => p.slug === x.id)));
    };

    const toOrder = (o) => {
      const a = JSON.parse(o.addressJson || '{}');
      const gross = o.items.reduce((t, i) => t + i.unitPrice * i.qty, 0);
      const status = OSTATUS[o.status] || 'confirmed';
      return {
        key: `${o.orderCode}|${store}`, live: true, orderId: o.orderCode, storeId: store, storeName: db.find('stores', store)?.name || '', createdAt: Date.parse(o.placedAt), status, at: {},
        auto: status === 'shipped' || status === 'delivered', courier: o.trackingNumber ? 'Super App courier' : '', tracking: o.trackingNumber || '', payment: PAY[o.paymentMethod] || o.paymentMethod,
        customer: { name: o.customerName, city: a.city || '', phone: o.phone }, address: { line1: a.line1 || '', line2: a.line2 || '', city: a.city || '', phone: o.phone },
        delivery: o.deliverySpeed === 'Express' ? 'express' : 'standard', lines: o.items.map((i) => ({ id: i.id, name: i.title, qty: i.qty, price: +i.unitPrice, total: i.unitPrice * i.qty,
          image: photo(i.image) || 'photo-1579952363873-27f3bade9f55', variant: { size: i.size, color: i.color } })),
        gross, commission: o.items.reduce((t, i) => t + +i.commission, 0), net: +o.earnings, units: o.items.reduce((t, i) => t + i.qty, 0),
      };
    };
    const loadOrders = async () => { const r = await api.get(`${area}/store/orders?status=all&pageSize=100`); orders = (r.items || r).map(toOrder); };

    const toReturn = (r) => ({ ...SA.services.liveReturns.toReturn(r, r.decidedBy), ownerId: cfg.official ? 'admin' : cfg.owner(), storeId: store, kind: 'store',
      customer: { name: r.customerName || '—', city: r.city || '' }, items: r.lines.map((l) => ({ index: l.orderItemId, name: l.title, qty: l.qty, action: l.action.toLowerCase(), size: l.newSize || '',
        variant: { size: l.size, color: l.color }, image: photo(l.image), price: orders.flatMap((o) => o.lines).find((x) => x.id === l.orderItemId)?.price || 0 })),
      history: [{ status: 'requested', at: Date.parse(r.createdAt), by: r.customerName || 'Customer', note: '' }] });
    const loadReturns = async () => { const list = await api.get(`${area}/returns`); returns = (list.items || list).map(toReturn); };

    /** The store record the dashboard reads (the official store keeps its demo look: cover, logo) */
    const loadStore = async () => {
      const st = await api.get(`${area}/store`);
      const base = db.find('stores', store) || {};
      put('stores', [{
        ...base, id: store, live: true, ownerId: cfg.owner(), name: st.name, slug: st.handle, handle: st.handle, tagline: st.tagline || base.tagline || '', description: st.about || base.description || '',
        logo: st.logo ? photo(st.logo) : base.logo || null, cover: st.cover ? photo(st.cover) : base.cover || 'photo-1579952363873-27f3bade9f55', phone: base.phone || '',
        status: st.status === 'Suspended' ? 'suspended' : 'active', official: !!cfg.official,
      }], (x) => x.id === store);
      await Promise.all([loadProducts(), loadOrders(), ...(cfg.extra ? [cfg.extra()] : [])]);
      await loadReturns();   // after the orders: the lines take their prices from them
    };
    let loading = null;
    const ready = async () => {
      loading ||= loadStore().catch((err) => { console.error(err); }).finally(() => setTimeout(() => { loading = null; }, 15000));
      await loading;
    };
    const reload = () => { loading = null; return ready().then(() => SA.app?.rerender?.()); };

    /* ---------- Products ---------- */

    /** Photos: new pictures (data: URLs) are uploaded, removed ones deleted, then the order is saved */
    const syncPhotos = async (apiId, images, old = []) => {
      const keep = old.filter((o) => images.includes(o.src));
      for (const o of old.filter((x) => !images.includes(x.src))) await api.del(`${area}/store/products/${apiId}/images/${o.id}`);
      const fresh = images.filter((src) => String(src).startsWith('data:'));
      if (!fresh.length) return;
      const form = new FormData();
      fresh.forEach((src, i) => form.append('photos', api.blob(src), `photo-${i + 1}.jpg`));
      const saved = await api.form(`${area}/store/products/${apiId}/images`, form);
      if (keep.length) {
        // Same order as on the page: kept photos where they were, new ones after them
        const ids = images.map((src) => keep.find((k) => k.src === src)?.id).filter(Boolean);
        const added = saved.images.filter((i) => !keep.some((k) => k.id === i.id)).map((i) => i.id);
        await api.put(`${area}/store/products/${apiId}/images/order`, { imageIds: [...ids, ...added] });
      }
    };
    /** Saved first as a draft with its photos, then published (so the reviewers see the photos) */
    const save = async (old, d) => {
      const publish = d.status !== 'draft';
      let p = old ? await api.put(`${area}/store/products/${old.apiId}`, requestOf(d, old.status === 'draft' || !publish ? 'Draft' : 'Published', old))
        : await api.post(`${area}/store/products`, requestOf(d, 'Draft'));
      await syncPhotos(p.id, d.images || [], old?.imageIds || []);
      if (publish && (!old || old.status === 'draft')) p = await api.put(`${area}/store/products/${p.id}`, requestOf(d, 'Published', toProduct(await api.get(`${area}/store/products/${p.id}`))));
      return p;
    };
    const validate = (d) => { const e = D.validateProduct(d); if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e); };
    const local = (id, patch) => db.update('products', id, patch);
    const after = (req) => req.then(reload, (err) => { fail(err); reload(); });

    Object.assign(D, {
      // Owner's decision: 5% on every purchase (the official store pays none)
      commission: () => (on() ? { ...demo.commission(), store: cfg.official ? 0 : 0.05 } : demo.commission()),
      createProduct(storeId, d) {
        if (!api.isLive() || storeId !== store) return demo.createProduct(storeId, d);
        validate(d);
        const temp = { ...toProduct({ id: Date.now(), slug: '', name: d.name, brand: d.brand, categoryKey: d.cat, price: d.price, compareAtPrice: d.was, stock: +d.stock, lowStockAt: +d.lowStockAt || 5,
          status: d.status === 'draft' ? 'Draft' : cfg.official ? 'Published' : 'WaitingForReview', images: [], variants: [], specs: [], createdAt: new Date().toISOString() }), images: d.images || [], sizes: d.sizes, colors: d.colors };
        db.raw().products.push(temp); db.touch?.('products');
        after(save(null, d));
        return temp;
      },
      updateProduct(id, d) {
        if (!mineProduct(id)) return demo.updateProduct(id, d);
        validate(d);
        const old = db.find('products', id);
        const out = local(id, { name: d.name.trim(), price: Math.round(+d.price), stock: +d.stock, images: d.images, status: d.status === 'draft' ? 'draft' : old.status === 'active' || cfg.official ? 'active' : 'review' });
        after(save(old, d));
        return out;
      },
      deleteProduct(id) {
        if (!mineProduct(id)) return demo.deleteProduct(id);
        const p = db.find('products', id);
        local(id, { deleted: true });
        after(api.del(`${area}/store/products/${p.apiId}`));
      },
      setProductStatus(id, status) {
        if (!mineProduct(id)) return demo.setProductStatus(id, status);
        const p = db.find('products', id);
        local(id, { status: status === 'active' ? (cfg.official ? 'active' : 'review') : 'draft' });   // the server says whether it needs review (reloaded at once)
        after(api.put(`${area}/store/products/${p.apiId}`, requestOf(p, status === 'active' ? 'Published' : 'Unpublished', p)));
      },
      restock(productId, add) {
        if (!mineProduct(productId)) return demo.restock(productId, add);
        const p = db.find('products', productId);
        const n = Math.round(+add);
        if (!(n >= 1 && n <= 10000)) throw new ApiError('Enter how many units arrived (1 or more).', 'invalid', { add: 'Enter a number' });
        const v = [...p.variants].sort((a, b) => a.stock - b.stock)[0];
        const out = local(productId, { stock: p.stock + n });
        after(api.post(`${area}/store/products/${p.apiId}/restock`, { variantId: v.id, add: n }));
        return out;
      },
    });

    /* ---------- Store profile ---------- */

    const upload = async (kind, src) => {
      if (!String(src || '').startsWith('data:')) return;
      const form = new FormData();
      form.append('file', api.blob(src), `${kind}.jpg`);
      await api.form(`${area}/store/${kind}`, form);
    };
    D.updateStore = (id, d) => {
      if (!api.isLive() || id !== store) return demo.updateStore(id, d);
      const e = D.validateStore({ ...d, id }); if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e);
      const st = db.find('stores', store);
      const out = db.update('stores', store, { name: d.name.trim(), tagline: (d.tagline || '').trim(), description: d.description.trim(), logo: d.logo || null, cover: d.cover });
      after(api.put(`${area}/store`, { name: d.name.trim(), handle: st.handle, tagline: (d.tagline || '').trim() || null, about: d.description.trim(), accentColor: null, coverPositionY: 50, mode: cfg.official ? null : 'store' })
        .then(() => Promise.all([upload('logo', d.logo), upload('cover', d.cover)])));
      return out;
    };

    /* ---------- Orders (Super App's courier carries every parcel) ---------- */

    const mineOrder = (key) => api.isLive() && String(key).endsWith(`|${store}`);
    Object.assign(D, {
      orders(opts = {}) {
        if (!on()) return demo.orders(opts);
        const from = opts.from ? Date.parse(`${opts.from}T00:00`) : 0, to = opts.to ? Date.parse(`${opts.to}T23:59:59`) : Infinity;
        return orders.filter((o) => o.createdAt >= from && o.createdAt <= to);
      },
      order(key) {
        if (!mineOrder(key)) return demo.order(key);
        return orders.find((o) => o.key === key) || (() => { throw new ApiError('Order not found.', 'not_found'); })();
      },
      async orderAction(key, action, { reason = '' } = {}) {
        if (!mineOrder(key)) return demo.orderAction(key, action, { reason });
        const o = D.order(key);
        if (action === 'deliver') throw new ApiError('Super App’s courier marks the order delivered when it reaches the customer.', 'courier_delivers');
        await api.post(`${area}/store/orders/${encodeURIComponent(o.orderId)}/${action}`, action === 'cancel' ? { reason: reason || 'Cancelled by the store' } : {});
        await Promise.all([loadOrders(), loadProducts()]);
        return D.order(key);
      },
      /** The store page's numbers (units sold, net) from the server's orders: one line per item, cancelled ones left out */
      salesFor(storeIds, from, to) {
        const rest = demo.salesFor(storeIds.filter((id) => id !== store), from, to);
        if (!on() || !storeIds.includes(store)) return rest;
        const f = Date.parse(`${from}T00:00`), t = Date.parse(`${to}T23:59:59`);
        const byName = new Map(D.products(store).map((p) => [p.name, p.id]));
        return [...rest, ...orders.filter((o) => o.status !== 'cancelled' && o.createdAt >= f && o.createdAt <= t).flatMap((o) => o.lines.map((l) => ({
          orderId: o.orderId, storeId: store, productId: byName.get(l.name) || '', qty: l.qty, total: l.total, createdAt: o.createdAt,
          money: { net: o.gross ? (o.net * l.total) / o.gross : 0, commission: o.gross ? (o.commission * l.total) / o.gross : 0 } })))];
      },
    });

    /* ---------- Returns this store decides ---------- */

    const mineReturn = (id) => api.isLive() && returns.some((r) => r.id === id);
    Object.assign(RT, {
      list(opts = {}) {
        if (!on() || !cfg.returnsFor(opts)) return demoRt.list(opts);
        return returns.filter((r) => !opts.status || (opts.status === 'open' ? RT.OPEN.includes(r.status) : r.status === opts.status));
      },
      get: (id) => (mineReturn(id) ? returns.find((r) => r.id === id) : demoRt.get(id)),
      async decide(id, approve, note = '') {
        if (!mineReturn(id)) return demoRt.decide(id, approve, note);
        await api.post(`${area}/returns/${encodeURIComponent(id)}/${approve ? 'approve' : 'reject'}`, approve ? {} : { reason: String(note).trim() });
        await loadReturns();
      },
      async receive(id) {
        if (!mineReturn(id)) return demoRt.receive(id);
        await api.post(`${area}/returns/${encodeURIComponent(id)}/received`);
        await Promise.all([loadReturns(), loadProducts()]);
      },
    });

    return { ready, reload, orders: () => orders, returns: () => returns };
  };

  /* ================= The seller's own store ================= */

  const SELLER_STORE = 'st-live';
  const ACCOUNT = () => SA.services.liveShop?.SELLER_ACCOUNT || 'acc-live-seller';
  const SELLER = () => SA.services.liveShop.SELLER;
  const P = SA.services.pod;
  const K = SA.services.codes;
  const demoPod = { earnings: P.earnings, withdraw: P.withdraw };
  const demoCodes = { save: K.save, setStatus: K.setStatus, remove: K.remove };
  const sellerOn = () => api.isLive() && !!db.find('stores', SELLER_STORE)?.live && D.me()?.id === ACCOUNT();

  /* ---------- Wallet and withdrawals ---------- */

  const PSTATUS = { Paid: 'paid', Sent: 'paid', Rejected: 'rejected' };
  const MIN_WITHDRAW = 500;   // setting money.payoutMin
  let wallet = null;
  const loadWallet = async () => {
    const [wallets, payouts, kyc] = await Promise.all([api.get('/wallet'), api.get('/wallet/payouts').catch(() => []), api.get('/seller/kyc').catch(() => null)]);
    wallet = wallets.find((w) => w.type === 'Seller') || { pending: 0, available: 0, settled: 0 };
    put('sellerPayouts', (payouts.items || payouts).filter((x) => x.walletType === 'Seller').map((x) => ({ id: x.code, live: true, sellerId: SELLER(), amount: +x.amount, method: x.method, detail: x.destination,
      status: PSTATUS[x.status] || 'processing', createdAt: Date.parse(x.createdAt) })), (x) => x.live);
    if (kyc) db.update('sellers', SELLER(), { kyc: { Verified: 'verified', Pending: 'pending', Rejected: 'rejected' }[kyc.status] || 'none' });
  };
  const liveSeller = (id) => api.isLive() && id === SELLER() && wallet;
  Object.assign(P, {
    earnings(sellerId) {
      if (!liveSeller(sellerId)) return demoPod.earnings(sellerId);
      return { pending: +wallet.pending, available: Math.max(0, +wallet.available), paidOut: +wallet.settled, lifetime: +wallet.pending + +wallet.available + +wallet.settled,
        adjustments: [], adjusted: 0, nextClear: [], minWithdraw: MIN_WITHDRAW };
    },
    withdraw(sellerId, amount) {
      if (!liveSeller(sellerId)) return demoPod.withdraw(sellerId, amount);
      const e = P.earnings(sellerId);
      const n = Math.round(Number(amount));
      if (!(n >= e.minWithdraw)) throw new ApiError(`The minimum withdrawal is ${e.minWithdraw} EGP.`, 'min_not_met');
      if (n > e.available) throw new ApiError(`You can withdraw up to ${e.available} EGP now.`, 'insufficient');
      wallet = { ...wallet, available: wallet.available - n };
      api.post('/wallet/payouts', { walletType: 'Seller', amount: n }).then(() => loadWallet().then(() => SA.app?.rerender?.()), (err) => { fail(err); loadWallet(); });
      return { amount: n };
    },
  });

  /* ---------- Discount codes (paid by the store, only its items) ---------- */

  const toCode = (c) => ({
    id: `lc-${c.id}`, apiId: c.id, live: true, code: c.code, kind: 'store', ownerId: ACCOUNT(), storeId: SELLER_STORE, pitchIds: [], type: c.discountType === 'Fixed' ? 'fixed' : 'percent',
    value: +c.value, min: +c.minSpend, maxUses: c.maxUses || 0, perCustomer: c.perCustomer ? 1 : 0, from: c.startsOn, to: c.endsOn || '', note: c.label || '',
    status: c.status === 'Active' ? 'active' : 'paused', uses: c.usesCount, discountGiven: 0, revenue: 0, usedBy: [], createdAt: Date.parse(`${c.startsOn}T00:00`), deleted: false,
  });
  const loadCodes = async () => put('codes', (await api.get('/seller/codes')).map(toCode), (x) => String(x.id).startsWith('lc-'));
  const codeRequest = (d, active) => ({
    code: String(d.code).trim().toUpperCase(), discountType: d.type === 'fixed' ? 'Fixed' : 'Percent', value: Math.round(+d.value), minSpend: Math.round(+d.min || 0),
    maxUses: +d.maxUses ? Math.round(+d.maxUses) : null, perCustomer: d.perCustomer ? 1 : null, startsOn: d.from, endsOn: d.to || null, label: (d.note || '').trim() || null, active,
  });
  const syncCodes = (req) => req.then(() => loadCodes().then(() => SA.app?.rerender?.()), (err) => { fail(err); loadCodes(); });
  Object.assign(K, {
    save(d, opts = {}) {
      if (!sellerOn() || d.kind !== 'store') return demoCodes.save(d, opts);
      const e = K.validate(d, opts); if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e);
      const old = opts.id ? db.find('codes', opts.id) : null;
      const out = { ...(old || {}), ...toCode({ id: old?.apiId || `new-${Date.now()}`, code: d.code.toUpperCase(), discountType: d.type === 'fixed' ? 'Fixed' : 'Percent', value: d.value, minSpend: d.min || 0,
        maxUses: +d.maxUses || null, perCustomer: d.perCustomer, usesCount: old?.uses || 0, startsOn: d.from, endsOn: d.to, label: d.note, status: d.status === 'paused' ? 'Paused' : 'Active' }) };
      if (!old) { db.raw().codes.push(out); db.touch?.('codes'); }
      syncCodes(old ? api.put(`/seller/codes/${old.apiId}`, codeRequest(d, d.status !== 'paused')) : api.post('/seller/codes', codeRequest(d, d.status !== 'paused')));
      return out;
    },
    setStatus(id, status) {
      const c = db.find('codes', id);
      if (!c?.live) return demoCodes.setStatus(id, status);
      db.update('codes', id, { status });
      syncCodes(api.put(`/seller/codes/${c.apiId}`, codeRequest(c, status === 'active')));
    },
    remove(id) {
      const c = db.find('codes', id);
      if (!c?.live) return demoCodes.remove(id);
      db.update('codes', id, { deleted: true });
      syncCodes(api.del(`/seller/codes/${c.apiId}`));
    },
  });

  const seller = mountStore({
    area: '/seller', store: SELLER_STORE, owner: ACCOUNT, active: () => D.me()?.id === ACCOUNT(),
    returnsFor: (opts) => opts.ownerId === ACCOUNT(), extra: () => Promise.all([loadWallet(), loadCodes()]),
  });
  // The seller dashboard opens the seller from the server: a store-mode seller's store comes with it
  const sellerReady = SA.services.liveShop.sellerReady;
  SA.services.liveShop.sellerReady = async (dashboard) => { const rec = await sellerReady(dashboard); if (rec?.storeId) await seller.ready(); return rec; };

  /* ================= Super App's official store (admin console) ================= */

  const official = mountStore({
    area: '/admin', store: 'st-official', official: true, owner: () => 'acc-admin', active: () => D.adminMode?.(),
    returnsFor: (opts) => !opts.ownerId,
  });
  /** The admin console's store pages load the official store and Super App's returns from the server */
  const ADMIN_STORE = /^\/admin\/(store|store-orders|returns)(\/|$|\?)/;
  const adminReady = () => (api.isLive() && ADMIN_STORE.test(SA.core.router.location()) ? official.ready() : Promise.resolve());

  SA.services.liveStore = { seller, official, adminReady, SELLER_STORE };
})(window.SA);
