/**
 * Shop, cart, checkout, orders and wishlist from the real API (backend Phase 4). Loaded after shopService and podService.
 *
 * In API mode the shop list, product and design pages, the cart, checkout, "My orders" and the wishlist read and write the
 * server; the pages keep their design and call the same service methods as before. A small cache answers the methods the
 * pages call synchronously (getSync, totals, wishlist); the async methods fill it first. Demo mode (GitHub Pages) is unchanged.
 *
 * Ids: store products keep their slug as id (as in the demo data); printed designs are `pod-<design id>`. Live designs are
 * mirrored in memory into the local "designs" table (not saved on the device) so the garment mockups keep working.
 * Promo codes are not stored on the server: the code is kept on the device and sent with the cart and at checkout.
 */
(function (SA) {
  const api = SA.services.api;
  const S = SA.services.shop;
  const { ApiError } = SA.services.http;
  const store = SA.core.store;
  const POD = 'pod-';

  const demo = { ...S };
  const live = () => api.isLive();
  const ready = (fn) => async (...args) => { await api.ready; return fn(...args); };
  const products = new Map();   // site id → product in the site's shape
  let cart = null;              // last server cart
  let wishes = [];              // server wishlist rows

  const img = (ph) => (!ph ? 'photo-1579952363873-27f3bade9f55' : ph.url ? `${location.origin}${ph.url}` : ph.key);
  const categoryOf = (key) => SA.data.products.categories.find((c) => c.id === key) || { id: key, name: key, icon: 'bag' };
  const sellerRef = (st) => {
    const sl = SA.services.sellers?.byHandle(st.handle);
    return { id: sl?.id || st.handle, name: st.name, handle: st.handle, official: st.type === 'Official', pod: st.type === 'Seller',
      href: st.type === 'Seller' ? `#/s/${st.handle}` : `#/shop?store=${st.handle}`, tags: sl?.tags || [] };
  };

  /* ---------------- Designs mirrored for the mockups ---------------- */

  const mirrorDesign = (d) => {
    const raw = SA.services.db.raw();
    raw.designs = Array.isArray(raw.designs) ? raw.designs : [];
    const id = String(d.card.designId);
    const art = (a) => (!a ? '' : a.url ? `${location.origin}${a.url}` : `seed:${String(a.key).replace(/^seed:/, '')}`);
    const rec = {
      id, live: true, sellerId: SA.services.sellers?.byHandle(d.card.store.handle)?.id || null, catalogId: d.catalogKey, title: d.card.name.split(' — ')[0],
      desc: d.description || '', price: +d.card.price, art: art(d.frontArt || d.backArt), artBack: d.frontArt && d.backArt ? art(d.backArt) : '', artW: 0, artH: 0,
      placement: { front: d.front ? { ...d.front } : null, back: d.back ? { ...d.back } : null }, colors: d.colors.map((c) => c.nameEn),
      category: d.card.designCategoryKey, tags: d.card.tags, status: 'live', views: 0, sales: 0, createdAt: Date.parse(d.card.createdAt), updatedAt: Date.parse(d.card.createdAt),
    };
    raw.designs = [...raw.designs.filter((x) => x.id !== id), rec];
    return rec;
  };

  /* ---------------- Products in the site's shape ---------------- */

  const fromCard = (c) => {
    const prev = products.get(c.kind === 'design' ? POD + c.designId : c.id) || {};
    const p = {
      ...prev, id: c.kind === 'design' ? POD + c.designId : c.id, live: true, pod: c.kind === 'design', designId: c.designId ? String(c.designId) : undefined,
      storeId: `api:${c.store.handle}`, cat: c.kind === 'design' ? 'designs' : c.categoryKey, designCat: c.designCategoryKey, brand: c.brand || c.store.name, name: c.name,
      title: c.name.split(' — ')[0], productName: c.name.split(' — ')[1] || '', price: +c.price, was: +(c.compareAtPrice ?? c.price), rating: +c.rating || 4.6,
      reviews: prev.reviews || 0, images: prev.images || [c.kind === 'design' && prev.designId ? `pod:${c.designId}:${c.colors[0]}` : img(c.image)],
      colors: prev.colors || c.colors.map((n) => ({ name: n, hex: '#8A8F8C' })), sizes: c.sizes.length ? c.sizes : ['One size'], stock: c.soldOut ? 0 : prev.stock ?? 99,
      badges: c.kind === 'design' ? ['Made to order'] : prev.badges || [], desc: prev.desc || '', tags: c.tags, rank: c.rank, status: 'active', createdAt: Date.parse(c.createdAt),
      seller: sellerRef(c.store),
    };
    products.set(p.id, p);
    return p;
  };

  const decorate = (p) => ({ ...p, discount: p.was > p.price ? Math.round((1 - p.price / p.was) * 100) : 0, wished: wishes.some((w) => w.card && (w.kind === 'design' ? POD + w.itemId : w.itemId) === p.id),
    category: p.pod ? { id: 'designs', name: 'Designs', icon: 'shirt' } : categoryOf(p.cat), soldOut: (p.stock ?? 1) <= 0 });

  const fromProduct = (d) => {
    const p = fromCard(d.card);
    Object.assign(p, {
      desc: d.description || '', images: d.images.length ? d.images.map(img) : p.images, specs: d.specs.map((s) => [s.label, s.value]),
      colors: [...new Map(d.variants.map((v) => [v.colorName, { name: v.colorName, hex: v.colorHex || '#8A8F8C' }])).values()],
      sizes: [...new Set(d.variants.map((v) => v.size))], stock: d.variants.reduce((t, v) => t + v.available, 0), variants: d.variants,
    });
    return p;
  };

  const fromDesign = (d) => {
    const p = fromCard(d.card);
    mirrorDesign(d);
    const colors = d.colors.map((c) => ({ name: c.nameEn, hex: c.hex }));
    const big = d.sizes.filter((s) => s.isBigSize).map((s) => s.size);
    Object.assign(p, {
      colors, sizes: d.sizes.map((s) => s.size), bigSizes: big, bigSizeExtra: +d.bigSizeExtra, kind: d.garmentKind.toLowerCase(), desc: d.description || '',
      images: colors.map((c) => `pod:${p.designId}:${c.name}`), designCat: d.card.designCategoryKey,
      specs: [['Product', d.productNameEn], ...d.details.map((x) => [x.labelEn, x.valueEn]), ['Printed on', d.front && d.back ? 'Front and back' : d.back ? 'Back' : 'Front'], ['Made to order', 'Printed after you order · ships in 3–5 days']],
      sizeChart: { cols: ['Chest', 'Length', 'Sleeve'], rows: Object.fromEntries(d.sizes.map((s) => [s.size, [+s.chestCm, +s.lengthCm, +s.sleeveCm]])) },
    });
    return p;
  };

  const search = async (f = {}) => {
    const qs = new URLSearchParams({ pageSize: '60' });
    if (f.q) qs.set('q', f.q);
    if (f.cat) qs.set('category', f.cat);
    if (f.dcat) qs.set('designCategory', f.dcat);
    if (f.tag) qs.set('tag', String(f.tag).toLowerCase());
    if (f.max) qs.set('maxPrice', f.max);
    if (f.store && String(f.store).startsWith('api:')) qs.set('store', String(f.store).slice(4));
    const handle = f.seller && (SA.services.sellers?.get(f.seller)?.handle || f.seller);
    if (handle) qs.set('store', handle);
    const sort = { 'price-asc': 'price', 'price-desc': '-price', rating: 'rating', newest: 'newest' }[f.sort];
    if (sort) qs.set('sort', sort);
    const res = await api.get(`/shop/products?${qs}`);
    let out = res.items.map(fromCard).map(decorate);
    if (f.brand) { const set = String(f.brand).split(','); out = out.filter((p) => set.includes(p.brand)); }
    if (f.sale) out = out.filter((p) => p.discount > 0);
    if (f.size) out = out.filter((p) => p.sizes.includes(f.size));
    if (f.sort === 'discount') out.sort((a, b) => b.discount - a.discount);
    return out;
  };

  const getProduct = async (id) => {
    try {
      const p = String(id).startsWith(POD) ? fromDesign(await api.get(`/shop/designs/${String(id).slice(POD.length)}`)) : fromProduct(await api.get(`/shop/products/${encodeURIComponent(id)}`));
      store.set((s) => ({ recentlyViewed: [p.id, ...s.recentlyViewed.filter((x) => x !== p.id)].slice(0, 8) }));
      return decorate(p);
    } catch (err) {
      if (err.status === 404) throw new ApiError('This product isn’t available any more.', 'not_found');
      throw err;
    }
  };

  Object.assign(S, {
    list: ready((f) => (live() ? search(f) : demo.list(f))),
    get: ready((id) => (live() ? getProduct(id) : demo.get(id))),
    getSync: (id) => (live() && products.has(id) ? decorate(products.get(id)) : demo.getSync(id)),
    related: ready(async (id, n = 4) => {
      if (!live()) return demo.related(id, n);
      const p = products.get(id);
      return (await search(p?.pod ? { cat: 'designs' } : { cat: p?.cat })).filter((x) => x.id !== id).slice(0, n);
    }),
    recommended: ready(async (n = 4, exclude = []) => (live() ? (await search({})).filter((p) => !exclude.includes(p.id)).slice(0, n) : demo.recommended(n, exclude))),
    reviews: (id) => (live() && products.has(id) ? Promise.resolve([]) : demo.reviews(id)),
  });

  /* ---------------- Cart (on the server) ---------------- */

  const signedIn = () => !!SA.services.session?.me();
  const needSignIn = () => {
    SA.core.router.go(`/auth/sign-in?next=${encodeURIComponent(location.hash.slice(1) || '/shop')}`);
    return new ApiError('Sign in to use your cart.', 'sign_in');
  };
  const lineProductId = (l) => (l.kind === 'design' ? POD + l.itemId : l.itemId);

  /** The server cart → the site's cart lines (kept in the store for the badge) */
  const toLines = (c) => c.lines.map((l) => ({ key: String(l.id), productId: lineProductId(l), variant: { size: l.size, color: l.color }, qty: l.qty, live: true }));

  const loadCart = async () => {
    if (!signedIn()) { cart = null; return null; }
    const code = store.get().coupon;
    cart = await api.get(`/cart${code ? `?promo=${encodeURIComponent(code)}` : ''}`);
    store.set({ cart: toLines(cart) }, 'cart');
    return cart;
  };

  const totalsFrom = (c) => {
    if (!c) return { lines: [], count: 0, subtotal: 0, savings: 0, couponOff: 0, coupon: null, partnerCode: null, shipping: 0, total: 0, freeShipGap: 0 };
    const lines = c.lines.filter((l) => l.canBuy).map((l) => {
      const id = lineProductId(l);
      const known = products.get(id);
      const image = l.kind === 'design' && SA.services.db.find('designs', String(l.designId)) ? `pod:${l.designId}:${l.color}` : img(l.image);
      return { key: String(l.id), productId: id, variant: { size: l.size, color: l.color }, qty: l.qty,
        product: { ...(known || {}), id, live: true, pod: l.kind === 'design', designId: l.designId ? String(l.designId) : undefined, name: l.title, brand: l.store.name,
          images: [image], price: +l.unitPrice, was: Math.max(+(l.compareAtPrice ?? l.unitPrice), +l.unitPrice), stock: l.available ?? 99, storeId: `api:${l.store.handle}` } };
    });
    const promo = c.promo ? { code: c.promo.code, label: c.promo.label || c.promo.code, platform: c.promo.funding === 'SuperApp', partner: c.promo.funding === 'Store' } : null;
    return { lines, count: c.count, subtotal: +c.subtotal, savings: +c.savings, couponOff: +c.discount, coupon: promo, partnerCode: null,
      shipping: +c.standardFee, express: +c.expressFee, total: +c.subtotal - +c.discount + +c.standardFee, freeShipGap: +c.freeDeliveryGap };
  };

  /** Runs a cart change on the server, then reloads the cart and redraws the page */
  const change = (fn) => fn().then(loadCart).then(() => SA.app.rerender?.()).catch((err) => { SA.ui.overlay?.toast(err.message, { type: 'error' }); loadCart().then(() => SA.app.rerender?.()); });

  const variantFor = (p, v) => p.variants?.find((x) => (!v.size || x.size === v.size) && (!v.color || x.colorName === v.color)) || p.variants?.[0];

  const addToCart = async (productId, variant = {}, qty = 1) => {
    if (!signedIn()) throw needSignIn();
    const p = products.get(productId)?.variants || String(productId).startsWith(POD) ? products.get(productId) : await getProduct(productId);
    if (!p) throw new ApiError('This product isn’t available any more.', 'not_found');
    if (p.sizes.length > 1 && !variant.size) throw new ApiError('Choose a size first.', 'size_required');
    const v = { size: variant.size || p.sizes[0], color: variant.color || p.colors[0]?.name || 'Standard' };
    const body = p.pod ? { designId: +p.designId, size: v.size, color: v.color, qty } : { productVariantId: variantFor(products.get(productId), v)?.id, qty };
    await api.post('/cart/items', body);
    await loadCart();
    return { product: p, variant: v, limited: false, added: qty };
  };

  Object.assign(S, {
    addToCart: (productId, variant, qty) => (live() ? addToCart(productId, variant, qty) : demo.addToCart(productId, variant, qty)),
    updateQty(key, qty) {
      if (!live() || !cart) return demo.updateQty(key, qty);
      const line = cart.lines.find((l) => String(l.id) === key);
      if (line) line.qty = Math.max(1, Math.min(20, qty));
      change(() => (qty <= 0 ? api.del(`/cart/items/${key}`) : api.patch(`/cart/items/${key}`, { qty: Math.max(1, Math.min(20, qty)) })));
      return null;
    },
    removeLine(key) {
      if (!live() || !cart) return demo.removeLine(key);
      cart.lines = cart.lines.filter((l) => String(l.id) !== key);
      change(() => api.del(`/cart/items/${key}`));
    },
    moveToWishlist(key) {
      if (!live() || !cart) return demo.moveToWishlist(key);
      const line = cart.lines.find((l) => String(l.id) === key);
      if (line) change(() => api.post('/wishlist', line.kind === 'design' ? { designId: line.designId } : { productSlug: line.itemId }).then(loadWishes));
      S.removeLine(key);
    },
    totals: () => (live() && signedIn() ? totalsFrom(cart) : demo.totals()),
    applyCoupon: ready(async (code) => {
      if (!live()) return demo.applyCoupon(code);
      const c = await api.post('/cart/promo', { code: code.trim() });
      store.set({ coupon: c.promo.code }, 'cart');
      cart = c;
      return { label: c.promo.label || c.promo.code };
    }),
    removeCoupon() { store.set({ coupon: null }, 'cart'); if (live()) loadCart().then(() => SA.app.rerender?.()); },
  });

  /* ---------------- Checkout and orders ---------------- */

  const ITEM = { Received: 'confirmed', Printing: 'packed', Packed: 'packed', Shipped: 'shipped', Delivered: 'delivered', Refused: 'cancelled', Cancelled: 'cancelled', Returned: 'delivered' };
  const PARCEL = { New: 'confirmed', Packed: 'packed', Shipped: 'shipped', Delivered: 'delivered', Cancelled: 'cancelled' };
  const STAGES = ['confirmed', 'packed', 'shipped', 'delivered'];
  const PAY = { Online: ['card', 'Card (test payment)'], Wallet: ['wallet', 'Wallet (test payment)'], CashOnDelivery: ['cod', 'Cash on delivery'] };

  const toLine = (i) => ({ id: i.id, productId: i.kind === 'design' ? POD + i.itemId : i.itemId, name: i.title, brand: '', price: +i.unitPrice, qty: i.qty,
    variant: { size: i.size, color: i.color }, image: i.kind === 'design' && SA.services.db.find('designs', i.itemId) ? `pod:${i.itemId}:${i.color}` : img(i.image),
    pod: i.kind === 'design', designId: i.kind === 'design' ? i.itemId : undefined, status: i.status, deliveredAt: i.deliveredAt ? Date.parse(i.deliveredAt) : null });

  /** Server order → the site's order shape (one group per parcel, as the order page reads it) */
  const toOrder = (o) => {
    const address = JSON.parse(o.addressJson || '{}');
    const sellers = o.groups.map((g) => ({ storeId: g.printed ? 'pod' : `api:${g.store.handle}`, storeName: g.printed ? 'Printed for you by Super App' : g.store.name, official: g.store?.type === 'Official',
      pod: g.printed, status: g.printed ? ITEM[g.status] || 'confirmed' : PARCEL[g.status] || 'confirmed', courier: g.courier === 'courier.simulated' ? 'Super App courier (test)' : g.courier || '',
      tracking: g.trackingNumber || '', at: {}, reason: '', lines: g.items.map(toLine) }));
    const open = sellers.filter((g) => g.status !== 'cancelled');
    const placed = Date.parse(o.placedAt);
    const [method, label] = PAY[o.paymentMethod] || PAY.Online;
    return {
      id: o.code, live: true, createdAt: placed, status: open.length ? STAGES[Math.min(...open.map((g) => STAGES.indexOf(g.status)))] : 'cancelled',
      lines: sellers.flatMap((g) => g.lines), subtotal: +o.subtotal, couponOff: +o.discount, coupon: o.promoCode, shipping: +o.deliveryFee, total: +o.total, vat: +o.vatAmount,
      address: { label: address.label || '', line1: address.line1 || '', line2: address.line2 || '', city: address.city || '', phone: o.phone }, delivery: o.deliverySpeed === 'Express' ? 'express' : 'standard',
      eta: new Date(placed + (o.deliverySpeed === 'Express' ? 1 : 3) * 864e5).toISOString().slice(0, 10), payment: { method, label, ref: o.code }, sellers,
      cancelledValue: sellers.filter((g) => g.status === 'cancelled').reduce((t, g) => t + g.lines.reduce((a, l) => a + l.price * l.qty, 0), 0), canCancel: o.canCancel,
    };
  };

  const placeOrder = async ({ address, delivery, payment }) => {
    if (!signedIn()) throw needSignIn();
    const method = payment?.method === 'cod' ? 'cod' : payment?.method === 'wallet' ? 'wallet' : 'online';
    const key = crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`;
    const r = await api.post('/orders/checkout', {
      address: { label: address?.label, line1: address?.line1 || '', line2: address?.line2 || null, city: address?.city || '' }, phone: address?.phone || SA.services.session.me()?.phone || '',
      deliverySpeed: delivery === 'express' ? 'express' : 'standard', paymentMethod: method, promoCode: store.get().coupon || null,
    }, { headers: { 'Idempotency-Key': key } });
    store.set({ coupon: null }, 'cart');
    await loadCart();
    return toOrder(r.order);
  };

  const myOrders = async () => {
    if (!signedIn()) return [];
    const page = await api.get('/orders/mine?pageSize=20');
    return Promise.all(page.items.map(async (o) => toOrder(await api.get(`/orders/${encodeURIComponent(o.code)}`))));
  };

  Object.assign(S, {
    placeOrder: (args) => (live() ? placeOrder(args) : demo.placeOrder(args)),
    orders: ready(() => (live() ? myOrders() : demo.orders())),
    order: ready(async (id) => {
      if (!live()) return demo.order(id);
      try { return toOrder(await api.get(`/orders/${encodeURIComponent(id)}`)); } catch (err) {
        if (err.status === 404) throw new ApiError('Order not found.', 'not_found');
        throw err;
      }
    }),
    trackingSync: (o) => (o?.live ? { status: o.status, sellers: o.sellers, cancelledValue: o.cancelledValue } : demo.trackingSync(o)),
  });

  /* ---------------- Wishlist (on the server) ---------------- */

  const loadWishes = async () => {
    if (!signedIn()) { wishes = []; return wishes; }
    wishes = await api.get('/wishlist');
    wishes.filter((w) => w.card).forEach((w) => fromCard(w.card));
    store.set({ wishlist: wishes.filter((w) => w.card).map((w) => (w.kind === 'design' ? POD + w.itemId : w.itemId)) }, 'wishlist');
    return wishes;
  };

  Object.assign(S, {
    toggleWish(id) {
      if (!live() || !signedIn()) return demo.toggleWish(id);
      const row = wishes.find((w) => (w.kind === 'design' ? POD + w.itemId : w.itemId) === id);
      const req = row ? api.del(`/wishlist/${row.id}`) : api.post('/wishlist', String(id).startsWith(POD) ? { designId: +String(id).slice(POD.length) } : { productSlug: id });
      store.set((s) => ({ wishlist: row ? s.wishlist.filter((x) => x !== id) : [id, ...s.wishlist] }), 'wishlist');
      req.then(loadWishes).catch((err) => SA.ui.overlay?.toast(err.message, { type: 'error' }));
      return !row;
    },
    wishlist: () => (live() && signedIn() ? wishes.filter((w) => w.card).map((w) => decorate(fromCard(w.card))) : demo.wishlist()),
  });

  /* ---------------- Start ---------------- */

  const start = async () => {
    await api.ready;
    if (!live()) return;
    await SA.services.session?.load?.();
    await Promise.all([loadCart().catch(() => null), loadWishes().catch(() => null)]);
    SA.services.session?.subscribe?.(() => { loadCart().catch(() => null); loadWishes().catch(() => null); });
  };
  start();
})(window.SA);