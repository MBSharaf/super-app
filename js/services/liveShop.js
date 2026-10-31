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
    SA.core.router.go(`/auth/sign-in?next=${encodeURIComponent(SA.core.router.location() || '/shop')}`);
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
      // Online payment only: Placed = waiting for the payment (called off after 15 minutes); a cancelled order that was never paid charged nothing
      awaitingPayment: o.status === 'Placed', paid: !!o.paid,
    };
  };

  const placeOrder = async ({ address, delivery, payment }) => {
    if (!signedIn()) throw needSignIn();
    const method = payment?.method === 'wallet' ? 'wallet' : 'online';   // card or wallet only (no cash on delivery)
    const key = crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`;
    const r = await api.post('/orders/checkout', {
      address: { label: address?.label, line1: address?.line1 || '', line2: address?.line2 || null, city: address?.city || '' }, phone: address?.phone || SA.services.session.me()?.phone || '',
      deliverySpeed: delivery === 'express' ? 'express' : 'standard', paymentMethod: method, promoCode: store.get().coupon || null,
    }, { headers: { 'Idempotency-Key': key } });
    store.set({ coupon: null }, 'cart');
    await loadCart();
    SA.services.liveMoney?.sync();
    // A live gateway (Paymob) returns its payment page; the order is Paid when the gateway confirms
    if (r.paymentUrl) window.location.assign(r.paymentUrl);
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

  /* ---------------- Seller studio (on the server) ---------------- */

  const SELLER = 'sl-live';
  const SELLER_ACCOUNT = 'acc-live-seller';
  const P = SA.services.pod;
  const SL = SA.services.sellers;
  const demoPod = { ...P };
  const demoSellers = { ...SL };
  let sellerLoad = null;
  let catalogue = [];           // GET /seller/catalogue (ids ↔ keys, colour ids)
  const isLiveSeller = (id) => live() && id === SELLER;
  const DSTATUS = { Draft: 'draft', InReview: 'review', Live: 'live', Rejected: 'rejected', Paused: 'paused', Removed: 'removed' };
  const MODE = { pod: 'pod', store: 'store', both: 'both' };

  const artOf = (a) => (!a ? '' : a.url ? `${location.origin}${a.url}` : `seed:${String(a.key).replace(/^seed:/, '')}`);
  const toDesign = (d) => ({
    id: String(d.id), live: true, apiId: d.id, sellerId: SELLER, catalogId: d.catalogKey, title: d.title, desc: d.description || '', tags: d.tags, category: d.categoryKey || '',
    colors: d.colors, price: +d.price, art: artOf(d.frontArt || d.backArt), artW: 0, artH: 0, artBack: d.frontArt && d.backArt ? artOf(d.backArt) : '', artBackW: 0, artBackH: 0,
    placement: { front: d.front ? { ...d.front } : null, back: d.back ? { ...d.back } : null }, status: DSTATUS[d.status] || 'draft', reason: d.rejectReason || '',
    spotCheck: d.spotCheck, views: d.views, sales: d.sales, rowVersion: d.rowVersion, submittedAt: d.submittedAt ? Date.parse(d.submittedAt) : 0,
    reviewedAt: d.reviewedAt ? Date.parse(d.reviewedAt) : 0, createdAt: Date.parse(d.createdAt), updatedAt: Date.now(), deleted: false,
  });
  const putDesign = (d) => {
    const raw = SA.services.db.raw();
    const rec = toDesign(d);
    raw.designs = [...(raw.designs || []).filter((x) => x.id !== rec.id), rec];
    return rec;
  };

  /** The signed-in seller as a local seller record (same shape as the demo sellers) and its dashboard account */
  const loadSeller = async () => {
    // Print on demand off (own products only): no designs or print catalogue to load
    const pod = SA.config.podEnabled;
    const [overview, profile, designs] = await Promise.all([api.get('/seller/overview'), api.get('/seller/store'), pod ? api.get('/seller/designs') : []]);
    catalogue = pod ? await api.get('/seller/catalogue') : [];
    const raw = SA.services.db.raw();
    const template = raw.sellers.find((s) => !s.live) || {};
    const me = SA.services.session.me() || {};
    const rec = {
      ...template, id: SELLER, live: true, accountId: SELLER_ACCOUNT, userId: store.get().user?.id, handle: profile.handle, name: profile.name, owner: me.fullName || '',
      email: me.email || '', phone: me.phone || '', mode: MODE[profile.mode] || 'pod', bio: profile.about || '', tagline: profile.tagline || '', color: profile.accentColor,
      cover: profile.cover ? img(profile.cover) : template.cover, status: 'active', strikes: [], frozenUntil: overview.level.frozenUntil ? Date.parse(overview.level.frozenUntil) : 0,
      history: { sales: overview.level.lifetimeSales, revenue: 0, profit: 0, rating: +overview.level.rating, reviews: 0 }, followers: overview.kpis.followers,
      featured: profile.featuredDesignId ? String(profile.featuredDesignId) : null, vacation: profile.status === 'OnBreak', vacationUntil: profile.breakUntil || '', storeId: MODE[profile.mode] && profile.mode !== 'pod' ? 'st-live' : null,   // own products: the store mirrored by liveStore
      updatedAt: Date.now(),
    };
    raw.sellers = [rec, ...raw.sellers.filter((s) => s.id !== SELLER)];
    const acc = { ...(raw.accounts.find((a) => a.role === 'seller') || {}), id: SELLER_ACCOUNT, live: true, role: 'seller', name: me.fullName || profile.name, company: profile.name, email: me.email || '', status: 'active' };
    raw.accounts = [acc, ...raw.accounts.filter((a) => a.id !== SELLER_ACCOUNT)];
    raw.designs = [...(raw.designs || []).filter((d) => d.sellerId !== SELLER), ...designs.map(toDesign)];
    ['sellers', 'accounts', 'designs'].forEach((col) => SA.services.db.touch?.(col));
    return rec;
  };

  /** Loads the seller once per page view (dashboard: signs the seller dashboard into it). Not a seller → nothing changes. */
  const sellerReady = async (dashboard = false) => {
    await api.ready;
    if (!live()) return null;
    if (!SA.services.session.loaded()) await SA.services.session.load();
    if (!signedIn()) return null;
    sellerLoad ||= loadSeller().catch((err) => { if (err.status !== 403) console.error(err); return null; }).finally(() => setTimeout(() => { sellerLoad = null; }, 15000));
    const rec = await sellerLoad;
    if (rec && dashboard && SA.services.dash?.auth.sellerAccount?.()?.id !== SELLER_ACCOUNT) SA.services.dash.auth.openAs(SELLER_ACCOUNT);
    return rec;
  };

  const catalogOf = (key) => catalogue.find((c) => c.key === key) || catalogue[0];
  const blob = (src) => (String(src).startsWith('data:') ? api.blob(src) : null);
  const uploadArt = async (id, side, src) => {
    const file = blob(src);
    if (!file) return;
    const form = new FormData();
    form.append('file', file, `art.${file.type.includes('svg') ? 'svg' : 'png'}`);
    await api.form(`/seller/designs/${id}/art/${side}`, form);
  };

  /** Studio draft → server: create when new, upload changed art, then save the listing (rowVersion from the server) */
  const saveLiveDraft = async (draft) => {
    const c = catalogOf(draft.catalogId);
    let current = draft.id && SA.services.db.find('designs', String(draft.id))?.live ? await api.get(`/seller/designs/${draft.id}`) : await api.post('/seller/designs', { catalogProductId: c.id });
    const id = current.id;
    await uploadArt(id, 'front', draft.art);
    if (draft.artBack) await uploadArt(id, 'back', draft.artBack);
    else if (draft.placement?.back && blob(draft.art)) await uploadArt(id, 'back', draft.art);
    if (!draft.placement?.back && !draft.artBack && current.backArt) await api.del(`/seller/designs/${id}/art/back`);
    current = await api.get(`/seller/designs/${id}`);
    const colorIds = (draft.colors || []).map((n) => c.colors.find((x) => x.nameEn === n)?.id).filter(Boolean);
    const saved = await api.put(`/seller/designs/${id}`, {
      catalogProductId: c.id, categoryKey: draft.category || null, title: String(draft.title || '').trim(), description: draft.desc || null,
      price: Math.max(+draft.price || 0, +current.base), colorIds, tags: draft.tags || [], front: draft.placement?.front || null, back: draft.placement?.back || null, rowVersion: current.rowVersion,
    });
    return P.enrich(putDesign(saved));
  };

  Object.assign(P, {
    saveDraft: (sellerId, draft) => (isLiveSeller(sellerId) ? saveLiveDraft(draft) : demoPod.saveDraft(sellerId, draft)),
    submit: (id) => (live() && SA.services.db.find('designs', String(id))?.live ? api.post(`/seller/designs/${id}/submit`).then((d) => P.enrich(putDesign(d))) : demoPod.submit(id)),
    setPaused: (id, paused) => (live() && SA.services.db.find('designs', String(id))?.live
      ? api.post(`/seller/designs/${id}/${paused ? 'pause' : 'resume'}`).then((d) => P.enrich(putDesign(d))) : demoPod.setPaused(id, paused)),
    remove: (id) => (live() && SA.services.db.find('designs', String(id))?.live
      ? api.del(`/seller/designs/${id}`).then(() => { const raw = SA.services.db.raw(); raw.designs = raw.designs.filter((d) => d.id !== String(id)); }) : demoPod.remove(id)),
  });

  Object.assign(SL, {
    current: () => (live() && SA.services.db.find('sellers', SELLER) && signedIn() ? SL.get(SELLER) : demoSellers.current()),
    update(id, patch) {
      if (!isLiveSeller(id)) return demoSellers.update(id, patch);
      const s = { ...SL.get(SELLER), ...patch };
      const body = { name: s.name, handle: s.handle, tagline: s.tagline || null, about: s.bio || null, accentColor: /^#[0-9a-f]{6}$/i.test(s.color || '') ? s.color : null,
        coverPositionY: 50, breakUntil: s.vacation ? s.vacationUntil || null : null, breakNote: null, mode: s.mode === 'store' ? 'store' : s.mode === 'both' ? 'both' : 'pod',
        featuredDesignId: s.featured && /^\d+$/.test(String(s.featured)) ? +s.featured : null };
      const raw = SA.services.db.raw();
      raw.sellers = raw.sellers.map((x) => (x.id === SELLER ? { ...x, ...patch, updatedAt: Date.now() } : x));
      api.put('/seller/store', body).then(() => { sellerLoad = null; }).catch((err) => SA.ui.overlay?.toast(err.message, { type: 'error' }));
      return SL.get(SELLER);
    },
  });
  /* ---------------- Factory (on the server) ---------------- */

  const F = SA.services.factory;
  const demoFactory = { ...F, auth: { ...F.auth } };
  const FACTORY = 'fc-live';
  const DEVICE_KEY = 'superapp.factory.device';
  const SESSION_KEY = 'superapp.factory.apiSession';
  const STAFF_SESSION = 'superapp.factory.session';
  const FSTAGE = { New: 'new', Printing: 'printing', QualityCheck: 'qc', Packing: 'packing', Ready: 'ready', Shipped: 'shipped', Cancelled: 'cancelled' };
  const FROLE = { Owner: 'owner', Supervisor: 'supervisor', Printer: 'printer', QualityCheck: 'qc', Packer: 'packer' };
  const TO_API = { printing: 'Printing', qc: 'QualityCheck', packing: 'Packing', ready: 'Ready' };
  const ISSUE = { 'Out of this colour or size': 'OutOfBlanks', 'Print file problem': 'BadArtwork', 'Machine down': 'MachineDown', 'Damaged blank': 'Damaged' };
  let factoryLoad = null;
  let factoryOn = false;
  const local = (k, v) => { try { if (v === undefined) return localStorage.getItem(k); if (v === null) localStorage.removeItem(k); else localStorage.setItem(k, v); } catch { /* private mode */ } return null; };

  /** Factory calls carry the device token and the staff session (owners use their account cookie) */
  const fcall = async (method, path, body) => {
    const headers = { Accept: 'application/json' };
    if (local(DEVICE_KEY)) headers['X-Factory-Device'] = local(DEVICE_KEY);
    if (local(SESSION_KEY)) headers['X-Factory-Session'] = local(SESSION_KEY);
    if (method === 'GET') {
      const res = await fetch(`${api.BASE}${path}`, { credentials: 'same-origin', headers });
      const json = await res.json().catch(() => null);
      if (!res.ok) { const err = new ApiError(json?.message || 'Something went wrong.', json?.errors?.code?.[0] || 'error'); err.status = res.status; throw err; }
      return json.meta ? json.data : json.data;
    }
    return api.post(path, body ?? {}, { headers });
  };

  const toJob = (j, staff) => ({
    id: j.code, live: true, orderId: j.orderCode, designId: null, sellerId: null, catalogId: (SA.services.db.all('podCatalog').find((c) => c.name === j.productName) || {}).id || 'pod-tee',
    title: j.title.replace(/ — .*$/, ''), color: j.color, size: j.size, qty: j.qty, price: 0, base: 0, profit: 0, partnerCost: 0, customer: { id: '', name: '', city: j.city, phone: '', address: '' },
    payment: '', status: j.stage === 'Cancelled' ? 'cancelled' : j.stage === 'Shipped' ? 'shipped' : 'printing', history: [], placedAt: Date.parse(j.dueAt) - 48 * 36e5, deliveredAt: 0,
    createdAt: Date.parse(j.dueAt) - 48 * 36e5, updatedAt: Date.now(), factoryId: FACTORY, stage: FSTAGE[j.stage] || 'new', steps: [], dueAt: Date.parse(j.dueAt),
    qcFails: j.reprintCount, reprints: j.reprintCount, assignedTo: staff.find((s) => s.name === j.assignedTo)?.id || '', sample: j.isSample,
  });

  /** Loads the factory, its staff and jobs into the local shape (records marked live). Owners sign in with the account; this device is registered once. */
  const loadFactory = async () => {
    let me = await fcall('GET', '/factory/me').catch((err) => { if (err.status === 401 && local(SESSION_KEY)) { local(SESSION_KEY, null); return fcall('GET', '/factory/me'); } throw err; });
    if (me.staffId == null && !local(DEVICE_KEY)) {
      const device = await api.post('/factory/devices/register', { name: navigator.userAgent.includes('Mobile') ? 'Phone' : 'Computer' });
      local(DEVICE_KEY, device.token);
      me = await fcall('GET', '/factory/me');
    }
    const [names, jobs, settings] = await Promise.all([fcall('GET', '/factory/staff/list'), fcall('GET', '/factory/jobs?stage=all&pageSize=200'), fcall('GET', '/factory/settings')]);
    const raw = SA.services.db.raw();
    const template = raw.factories.find((x) => !x.live) || {};
    raw.factories = [{ ...template, id: FACTORY, live: true, name: me.factoryName, capacity: settings.capacityPerDay, slaHours: settings.slaHours, intake: !settings.intakePaused, status: 'active', address: settings.address },
      ...raw.factories.filter((x) => x.id !== FACTORY)];
    const staff = [{ id: 'fs-live-owner', live: true, factoryId: FACTORY, name: me.staffId == null ? me.name : 'Owner', role: 'owner', active: me.staffId == null, avatar: 2, pin: null },
      ...names.map((s) => ({ id: `fs-live-${s.id}`, live: true, apiId: s.id, factoryId: FACTORY, name: s.name, role: FROLE[s.role] || 'printer', active: true, avatar: s.id % 15, pin: null }))];
    raw.factoryStaff = [...staff, ...raw.factoryStaff.filter((s) => !s.live)];
    raw.podOrders = [...jobs.map((j) => toJob(j, staff)), ...raw.podOrders.filter((o) => !o.live)];
    local(STAFF_SESSION, me.staffId == null ? 'fs-live-owner' : `fs-live-${me.staffId}`);
    factoryOn = true;
    ['factories', 'factoryStaff', 'podOrders'].forEach((col) => SA.services.db.touch?.(col));
  };

  const factoryReady = async () => {
    await api.ready;
    if (!live()) return;
    if (!SA.services.session.loaded()) await SA.services.session.load();
    factoryLoad ||= loadFactory().catch((err) => { if (![401, 403].includes(err.status)) console.error(err); }).finally(() => setTimeout(() => { factoryLoad = null; }, 10000));
    await factoryLoad;
  };

  const liveJob = (id) => factoryOn && SA.services.db.find('podOrders', String(id))?.live;
  /** Shows the change at once, sends it to the server, then reloads (a refused step is undone with the server's message) */
  const sync = (request) => request.then(() => { factoryLoad = null; return factoryReady(); }).then(() => SA.app.rerender?.())
    .catch((err) => { SA.ui.overlay?.toast(err.message, { type: 'error' }); factoryLoad = null; factoryReady().then(() => SA.app.rerender?.()); });

  Object.assign(F, {
    list: () => (factoryOn && live() ? SA.services.db.all('factories').filter((x) => x.live) : demoFactory.list()),
    move(id, to) {
      if (!liveJob(id)) return demoFactory.move(id, to);
      const o = SA.services.db.find('podOrders', id);
      const s = F.me();
      SA.services.db.update('podOrders', id, { stage: to, steps: [...(o.steps || []), { stage: to, at: Date.now(), by: s?.id || '', name: s?.name || '' }] });
      sync(o.stage === 'qc' && to === 'packing' ? fcall('POST', `/factory/jobs/${id}/qc`, { pass: true }) : fcall('POST', `/factory/jobs/${id}/advance`, { toStage: TO_API[to] }));
      return SA.services.db.find('podOrders', id);
    },
    qcFail(id, reason) {
      if (!liveJob(id)) return demoFactory.qcFail(id, reason);
      SA.services.db.update('podOrders', id, { stage: 'printing' });
      sync(fcall('POST', `/factory/jobs/${id}/qc`, { pass: false, reason }));
      return SA.services.db.find('podOrders', id);
    },
    assign(id, staffId) {
      if (!liveJob(id)) return demoFactory.assign(id, staffId);
      SA.services.db.update('podOrders', id, { assignedTo: staffId || '' });
      sync(fcall('POST', `/factory/jobs/${id}/assign`, { staffId: SA.services.db.find('factoryStaff', staffId)?.apiId ?? null }));
      return SA.services.db.find('podOrders', id);
    },
    reportIssue(id, { type, note = '' }) {
      if (!liveJob(id)) return demoFactory.reportIssue(id, { type, note });
      SA.services.db.update('podOrders', id, { issue: { type, note, by: F.me()?.name || '', at: Date.now(), resolved: false } });
      sync(fcall('POST', `/factory/jobs/${id}/problem`, { kind: ISSUE[type] || 'Other', detail: `${type}${note ? ` — ${note}` : ''}` }));
      return SA.services.db.find('podOrders', id);
    },
    saveSettings(v) {
      if (!factoryOn || !live()) return demoFactory.saveSettings(v);
      const f = SA.services.db.find('factories', FACTORY);
      sync(api.put('/factory/settings', { capacityPerDay: +v.capacity || f.capacity, slaHours: +v.slaHours || f.slaHours, intakePaused: !f.intake }));
      return demoFactory.saveSettings(v);
    },
  });
  F.auth.signIn = (staffId, pin) => {
    const s = SA.services.db.find('factoryStaff', staffId);
    if (!factoryOn || !s?.live) return demoFactory.auth.signIn(staffId, pin);
    return fcall('POST', '/factory/auth/pin', { staffId: s.apiId, pin: String(pin) }).then((session) => {
      local(SESSION_KEY, session.token);
      local(STAFF_SESSION, s.id);
      factoryLoad = null;
      return factoryReady().then(() => F.auth.current());
    });
  };
  F.auth.signOut = () => { local(SESSION_KEY, null); demoFactory.auth.signOut(); };
  /* ---------------- Start ---------------- */

  const start = async () => {
    await api.ready;
    if (!live()) return;
    await SA.services.session?.load?.();
    await Promise.all([loadCart().catch(() => null), loadWishes().catch(() => null)]);
    SA.services.session?.subscribe?.(() => { loadCart().catch(() => null); loadWishes().catch(() => null); });
  };
  // The cart and checkout pages wait for this on a fresh load (otherwise they draw an empty cart)
  const started = start().catch(() => null);
  SA.services.liveShop = { sellerReady, factoryReady, loadCart, started, SELLER, SELLER_ACCOUNT, FACTORY };
})(window.SA);