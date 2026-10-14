/**
 * Shop: catalogue, wishlist, cart, coupons, orders. Products come from the platform DB, so items
 * a partner adds in their dashboard store appear here, stock is enforced, and every order line is
 * recorded as a sale for the store that owns the product. Future API: GET /products, /cart, POST /orders
 */
(function (SA) {
  const store = SA.core.store;
  const { request, ApiError } = SA.services.http;
  const { seeded, uid } = SA.core.utils;
  const P = SA.data.products;            // static config: categories, coupons, shipping, review copy
  const db = SA.services.db;

  const storeOf = (p) => db.find('stores', p.storeId);
  const onBreak = (st) => { const sl = st && SA.services.sellers?.byAccount(st.ownerId); return !!sl && SA.services.sellers.onBreak(sl); };
  const isLive = (p) => p && !p.deleted && p.status === 'active' && storeOf(p)?.status === 'active' && !onBreak(storeOf(p));

  /* Print-on-demand designs are sold as products made to order. Their images are "pod:<designId>:<colour>[:back]"
     references that SA.data.media turns into garment mockups. */
  const POD_PREFIX = 'pod-';
  const DESIGNS_CAT = { id: 'designs', name: 'Designs', icon: 'shirt' };
  const podProduct = (d) => {
    const x = d.product;
    const s = SA.services.sellers.get(d.sellerId);
    const h = s?.history || {};
    const colors = x.colors.filter((k) => d.colors.includes(k.name));
    // Search ranking: sales, views and rating, lifted by the seller's level (Rising ×1.25 … Elite ×1.75 + a head start)
    const lvl = s ? SA.services.sellers.levelOf(s) : null, boost = lvl?.boost || 0;
    const rank = ((d.sales || 0) * 6 + (d.views || 0) / 60 + (h.rating || 4) * 8 + (s?.featured === d.id ? 15 : 0) + (s?.featuredByAdmin ? 25 : 0)) * (1 + boost * 0.25) + boost * 30;
    const levelBadge = lvl?.id === 'elite' ? 'Elite creator' : lvl?.id === 'pro' ? 'Pro creator' : '';
    const images = colors.map((k) => `pod:${d.id}:${k.name}`);
    if (d.placement?.back) images.splice(1, 0, `pod:${d.id}:${colors[0].name}:back`);
    return {
      id: POD_PREFIX + d.id, pod: true, designId: d.id, sellerId: d.sellerId, storeId: null, cat: 'designs', designCat: d.category,
      brand: s?.name || 'Seller', name: `${d.title} — ${x.name}`, title: d.title, productName: x.name, kind: x.kind,
      price: d.price, was: d.price, rating: h.rating || 4.6, reviews: Math.max(d.sales || 0, Math.round((h.reviews || 0) / 4)),
      images, colors, sizes: x.sizes, stock: 99, badges: levelBadge ? [levelBadge, 'Made to order'] : ['Made to order'], desc: d.desc || x.desc, tags: d.tags || [],
      rank, levelId: lvl?.id || 'new',
      bigSizes: x.bigSizes, bigSizeExtra: x.bigSizeExtra,
      specs: [['Product', x.name], ['Fabric', x.desc], ['Print', d.placement?.front && d.placement?.back ? 'Front and back' : d.placement?.back ? 'Back' : 'Front'], ['Made to order', 'Printed after you order · ships in 3–5 days'], ['Designed by', s?.name || '']],
      status: 'active', createdAt: d.reviewedAt || d.createdAt,
    };
  };
  const podLive = () => (SA.services.pod ? SA.services.pod.live().map(podProduct) : []);
  /** Live catalogue: active, not deleted, and the selling store is open — plus live designs */
  const catalogue = () => [...db.all('products').filter(isLive), ...podLive()];
  const find = (id) => {
    if (String(id).startsWith(POD_PREFIX)) {
      const d = SA.services.pod?.get(String(id).slice(POD_PREFIX.length));
      const s = d && SA.services.sellers.get(d.sellerId);
      return d && d.status === 'live' && s?.status === 'active' && !SA.services.sellers.onBreak(s) && d.product?.status === 'active' ? podProduct(d) : undefined;
    }
    const p = db.find('products', id); return isLive(p) ? p : undefined;
  };
  /** Unit price of a cart line (big sizes cost more on printed items) */
  const unitPrice = (p, v = {}) => p.price + (p.pod && p.bigSizes?.includes(v.size) ? p.bigSizeExtra : 0);

  const discount = (p) => (p.was > p.price ? Math.round((1 - p.price / p.was) * 100) : 0);
  const decorate = (p) => {
    if (p.pod) {
      const s = SA.services.sellers.get(p.sellerId);
      return { ...p, discount: 0, wished: store.get().wishlist.includes(p.id), category: DESIGNS_CAT, soldOut: false,
        seller: s ? { id: s.id, name: s.name, handle: s.handle, href: `#/s/${s.handle}`, pod: true } : null };
    }
    const st = storeOf(p);
    const sl = st && SA.services.sellers?.byAccount(st.ownerId);
    return { ...p, discount: discount(p), wished: store.get().wishlist.includes(p.id), category: P.categories.find((c) => c.id === p.cat),
      soldOut: (p.stock ?? 1) <= 0, seller: st ? { id: st.id, name: st.name, official: !!st.official, href: sl ? `#/s/${sl.handle}` : `#/shop?store=${st.id}` } : null };
  };
  const cartKey = (productId, v) => `${productId}|${v.size || ''}|${v.color || ''}`;
  const inCart = (productId) => store.get().cart.filter((l) => l.productId === productId).reduce((a, l) => a + l.qty, 0);

  const totals = () => {
    const s = store.get();
    const lines = s.cart.map((l) => { const p = find(l.productId); return { ...l, product: p && { ...p, price: unitPrice(p, l.variant), was: Math.max(p.was, unitPrice(p, l.variant)) } }; }).filter((l) => l.product);
    const subtotal = lines.reduce((a, l) => a + l.product.price * l.qty, 0);
    const savings = lines.reduce((a, l) => a + Math.max(0, l.product.was - l.product.price) * l.qty, 0);
    let c = null;
    let couponOff = 0, freeShip = false, partner = null;
    // Super App code (made in the admin console, paid by Super App)
    let pc = null;
    if (s.coupon && SA.services.codes) { try { pc = SA.services.codes.platform(s.coupon, { scope: 'shop', amount: subtotal }); } catch { pc = null; } }
    if (pc) {
      couponOff = pc.amount; freeShip = pc.freeShip;
      c = { label: pc.label, platform: true, id: pc.id };
    } else if (s.coupon && SA.services.codes) {
      // A store's own code: only that store's items are discounted
      try { partner = SA.services.codes.forCart(s.coupon, lines); } catch { partner = null; }
      if (partner) { couponOff = partner.amount; c = { label: partner.label, partner: true }; }
    }
    const allServices = lines.length && lines.every((l) => l.product.service);
    const shipping = !lines.length || allServices || freeShip || subtotal >= P.shipping.freeOver ? 0 : P.shipping.standard;
    return { lines, count: lines.reduce((a, l) => a + l.qty, 0), subtotal, savings, couponOff, coupon: c ? { code: s.coupon, ...c } : null, partnerCode: partner, shipping, total: Math.max(0, subtotal - couponOff + shipping), freeShipGap: Math.max(0, P.shipping.freeOver - subtotal) };
  };

  const shopService = {
    categories: () => [...P.categories, DESIGNS_CAT].map((c) => ({ ...c, count: catalogue().filter((p) => p.cat === c.id).length })).filter((c) => c.id !== 'designs' || c.count),
    /** Design sections (sellers' print-on-demand categories) with live counts */
    designCategories: () => (SA.services.pod ? SA.services.pod.categories().map((c) => ({ ...c, count: podLive().filter((p) => p.designCat === c.id).length })) : []),
    podProduct, unitPrice, POD_PREFIX,
    brands: () => [...new Set(catalogue().map((p) => p.brand))].sort(),
    stores: () => db.all('stores').filter((s) => s.status === 'active'),
    storeInfo: (id) => db.find('stores', id),
    list: (f = {}) => request(() => {
      let out = catalogue().map(decorate);
      if (f.store) out = out.filter((p) => p.storeId === f.store);
      if (f.seller) out = out.filter((p) => p.sellerId === f.seller || (p.storeId && p.storeId === SA.services.sellers?.get(f.seller)?.storeId));
      if (f.cat) out = out.filter((p) => p.cat === f.cat);
      if (f.dcat) out = out.filter((p) => p.designCat === f.dcat);
      if (f.q) { const q = f.q.toLowerCase(); out = out.filter((p) => `${p.name} ${p.brand} ${p.category?.name} ${p.seller?.name || ''} ${(p.tags || []).join(' ')}`.toLowerCase().includes(q)); }
      if (f.brand) { const set = String(f.brand).split(','); out = out.filter((p) => set.includes(p.brand)); }
      if (f.max) out = out.filter((p) => p.price <= +f.max);
      if (f.sale) out = out.filter((p) => p.discount > 0);
      if (f.size) out = out.filter((p) => p.sizes.includes(f.size));
      // Featured: shop products by reviews × rating; designs by their level-boosted rank (same scale)
      const score = (p) => (p.pod ? p.rank * 8 : p.reviews * p.rating);
      const sorters = { featured: (a, b) => score(b) - score(a), 'price-asc': (a, b) => a.price - b.price, 'price-desc': (a, b) => b.price - a.price, rating: (a, b) => b.rating - a.rating, discount: (a, b) => b.discount - a.discount, newest: (a, b) => b.createdAt - a.createdAt };
      return out.sort(sorters[f.sort] || sorters.featured);
    }),
    get: (id) => request(() => {
      const p = find(id);
      if (!p) throw new ApiError('This product isn’t available any more.', 'not_found');
      store.set((s) => ({ recentlyViewed: [id, ...s.recentlyViewed.filter((x) => x !== id)].slice(0, 8) }));
      return decorate(p);
    }),
    getSync: (id) => { const p = find(id); return p && decorate(p); },
    related: (id, n = 4) => request(() => {
      const p = find(id) || db.find('products', id);
      const cat = catalogue();
      const same = cat.filter((x) => x.id !== id && (x.storeId === p.storeId && !p.storeOfficial) && x.cat === p.cat);
      return [...same, ...cat.filter((x) => x.id !== id && x.cat === p.cat), ...cat.filter((x) => x.id !== id && x.cat !== p.cat && !x.service)]
        .filter((x, i, arr) => arr.indexOf(x) === i).slice(0, n).map(decorate);
    }),
    recommended: (n = 4, exclude = []) => request(() => {
      const seen = new Set(store.get().recentlyViewed);
      return catalogue().filter((p) => !exclude.includes(p.id) && !p.service).sort((a, b) => (seen.has(b.id) - seen.has(a.id)) || seeded(b.id) - seeded(a.id)).slice(0, n).map(decorate);
    }),
    reviews: (id) => request(() => {
      const start = Math.floor(seeded(id) * P.reviewPool.length);
      return Array.from({ length: 4 }, (_, i) => ({ ...P.reviewPool[(start + i) % P.reviewPool.length], daysAgo: 3 + i * 9, verified: i !== 2, avatar: (start + i * 4) % 15 }));
    }),
    ratingBreakdown: (p) => {
      const n = Math.max(0, p.reviews || 0);
      const five = Math.max(0, Math.round(n * (p.rating - 3.6) / 1.5));
      const four = Math.min(n - five, Math.round(n * 0.22));
      const rest = Math.max(0, n - five - four);
      return [five, four, Math.round(rest * 0.6), Math.round(rest * 0.25), rest - Math.round(rest * 0.6) - Math.round(rest * 0.25)];
    },

    // Wishlist
    toggleWish(id) {
      const on = !store.get().wishlist.includes(id);
      store.set((s) => ({ wishlist: on ? [id, ...s.wishlist] : s.wishlist.filter((x) => x !== id) }), 'wishlist');
      return on;
    },
    wishlist: () => store.get().wishlist.map((id) => find(id)).filter(Boolean).map(decorate),

    // Cart
    addToCart(productId, variant = {}, qty = 1) {
      const p = find(productId);
      if (!p) throw new ApiError('This product isn’t available any more.', 'not_found');
      if (p.sizes.length > 1 && !variant.size) throw new ApiError('Choose a size first.', 'size_required');
      const left = (p.stock ?? 99) - inCart(productId);
      if (left <= 0) throw new ApiError(p.stock <= 0 ? 'This item is sold out.' : `Only ${p.stock} in stock — they’re all in your cart.`, 'out_of_stock');
      const add = Math.min(qty, left);
      const v = { size: variant.size || p.sizes[0], color: variant.color || p.colors[0]?.name || 'Standard' };
      const key = cartKey(productId, v);
      store.set((s) => {
        const existing = s.cart.find((l) => l.key === key);
        const cart = existing ? s.cart.map((l) => (l.key === key ? { ...l, qty: Math.min(10, l.qty + add) } : l)) : [...s.cart, { key, productId, variant: v, qty: add }];
        return { cart };
      }, 'cart:add');
      return { product: p, variant: v, limited: add < qty, added: add };
    },
    updateQty(key, qty) {
      const line = store.get().cart.find((l) => l.key === key);
      const p = line && find(line.productId);
      const max = p ? Math.min(10, (p.stock ?? 99) - (inCart(p.id) - line.qty)) : 10;
      store.set((s) => ({ cart: qty <= 0 ? s.cart.filter((l) => l.key !== key) : s.cart.map((l) => (l.key === key ? { ...l, qty: Math.max(1, Math.min(max, qty)) } : l)) }), 'cart');
      return qty > max ? max : null;
    },
    removeLine(key) { store.set((s) => ({ cart: s.cart.filter((l) => l.key !== key) }), 'cart'); },
    moveToWishlist(key) {
      const line = store.get().cart.find((l) => l.key === key);
      if (line && !store.get().wishlist.includes(line.productId)) shopService.toggleWish(line.productId);
      shopService.removeLine(key);
    },
    totals,
    applyCoupon: (code) => request(() => {
      const c = code.trim().toUpperCase();
      const def = SA.services.codes?.platform(c, { scope: 'shop', amount: totals().subtotal });
      if (!def) {
        // Partner store code?
        const pc = SA.services.codes?.forCart(c, totals().lines);
        if (!pc) throw new ApiError('That code isn’t valid. Try KICKOFF10.', 'invalid_coupon');
        store.set({ coupon: c }, 'cart');
        return { label: pc.label };
      }
      store.set({ coupon: c }, 'cart');
      return def;
    }, { latency: 300 }),
    removeCoupon() { store.set({ coupon: null }, 'cart'); },

    // Orders
    async placeOrder({ address, delivery, payment }) {
      SA.services.user.assertActive();
      const t = totals();
      if (!t.lines.length) throw new ApiError('Your cart is empty.', 'empty_cart');
      // Re-check stock right before taking payment
      const short = t.lines.find((l) => (l.product.stock ?? 99) < l.qty);
      if (short) throw new ApiError(`Only ${short.product.stock} of “${short.product.name}” left. Update your cart and try again.`, 'out_of_stock');
      const deliveryFee = delivery === 'express' && t.shipping > 0 ? P.shipping.express : delivery === 'express' ? P.shipping.express - P.shipping.standard : t.shipping;
      const total = t.subtotal - t.couponOff + deliveryFee;
      // Printed-to-order items: customers who refused deliveries before must pay online
      const me = store.get().user || {};
      if (payment.method === 'cod' && t.lines.some((l) => l.product.pod) && SA.services.pod?.needsPrepay(me.id || 'u-001')) {
        throw new ApiError('Printed-to-order items need online payment on this account because of earlier refused deliveries. Pay by card or wallet.', 'prepay_required');
      }
      let paid;
      if (payment.method === 'cod') paid = { method: 'cod', label: 'Cash on delivery', id: uid('COD') };
      else paid = await SA.services.payment.charge({ ...payment, amount: total, label: `Shop order — ${t.count} item${t.count > 1 ? 's' : ''}` });
      return request(() => {
        const eta = new Date(); eta.setDate(eta.getDate() + (delivery === 'express' ? 1 : 3));
        const order = {
          id: 'ORD-' + Math.floor(100000 + Math.random() * 900000), createdAt: Date.now(), status: 'confirmed',
          lines: t.lines.map((l) => ({ productId: l.productId, storeId: l.product.storeId, name: l.product.name, brand: l.product.brand, price: l.product.price, qty: l.qty, variant: l.variant,
            image: l.product.pod ? `pod:${l.product.designId}:${l.variant.color}` : l.product.images[0], ...(l.product.pod ? { pod: true, designId: l.product.designId, sellerId: l.product.sellerId } : {}) })),
          subtotal: t.subtotal, couponOff: t.couponOff, coupon: t.coupon?.code || null, shipping: deliveryFee, total,
          address, delivery, eta: eta.toISOString().slice(0, 10), payment: { method: paid.method, label: paid.label, ref: paid.id },
        };
        store.set((s) => ({ orders: [order, ...s.orders], cart: [], coupon: null }), 'order:new');
        // Platform side: record each line as a sale for its store and reduce stock
        const u = store.get().user || {};
        const podLines = order.lines.filter((l) => l.pod);
        if (podLines.length) {
          SA.services.pod.place({ orderId: order.id, customer: { id: u.id || 'u-001', name: u.name || 'Customer', city: address?.city || '' }, payment: paid.method,
            lines: podLines.map((l) => ({ designId: l.designId, color: l.variant.color, size: l.variant.size, qty: l.qty, title: l.name })) });
        }
        order.lines.forEach((l, i) => {
          if (l.pod) return; // made by the print partner, not a store sale
          const prod = db.find('products', l.productId);
          if (prod) {
            const left = Math.max(0, (prod.stock ?? 0) - l.qty);
            db.update('products', prod.id, { stock: left });
            if (left <= (prod.lowStockAt ?? 5)) {
              db.log('system', 'stock.low', `Low stock: “${prod.name}” has ${left} left`, prod.id);
              const owner = storeOf(prod)?.ownerId;
              if (owner) SA.services.notify?.toAccount(owner, { cat: 'store', tone: left ? 'gold' : 'danger', title: left ? `Low stock: ${prod.name}` : `Sold out: ${prod.name}`, body: left ? `${left} left. Restock so you don’t miss orders.` : 'It’s hidden from the shop until you restock.', link: '#/dash/store' });
            }
          }
          db.insert('sales', { id: `${order.id}-${i + 1}`, orderId: order.id, productId: l.productId, storeId: l.storeId, name: l.name, image: l.image, qty: l.qty, price: l.price, total: l.price * l.qty,
            variant: l.variant, customer: { name: u.name || 'Customer', city: address?.city || '', phone: address?.phone || u.phone || '' }, payment: paid.method === 'cod' ? 'Cash on delivery' : paid.method === 'wallet' ? 'Wallet' : 'Card',
            address: address ? { label: address.label || '', line1: address.line1 || '', line2: address.line2 || '', city: address.city || '', phone: address.phone || u.phone || '' } : null, delivery, fulfil: null });
        });
        // Super App code: Super App pays the discount (recorded for the revenue report)
        if (t.coupon?.platform) {
          const saved = t.couponOff || (t.shipping === 0 && t.subtotal < P.shipping.freeOver ? P.shipping.standard : 0);
          SA.services.codes.use(t.coupon.id, { discount: saved, revenue: t.subtotal - t.couponOff });
          db.insert('promoUses', { codeId: t.coupon.id, code: t.coupon.code, amount: saved, scope: 'shop', ref: order.id, at: Date.now() }, 'pu');
        }
        // Store code: the store funds it, so its sale lines carry the discount
        if (t.partnerCode) {
          const pc = t.partnerCode;
          const mine = order.lines.map((l, i) => ({ l, i })).filter((x) => x.l.storeId === pc.storeId);
          let left = pc.amount;
          mine.forEach(({ l, i }, k) => {
            const share = k === mine.length - 1 ? left : Math.round((l.price * l.qty / pc.base) * pc.amount);
            left -= share;
            db.update('sales', `${order.id}-${i + 1}`, { total: l.price * l.qty - share, discount: share, code: pc.code });
          });
          SA.services.codes.use(pc.id, { discount: pc.amount, revenue: pc.base - pc.amount });
          const code = db.find('codes', pc.id);
          if (code) SA.services.notify?.toAccount(code.ownerId, { cat: 'store', title: `Code ${code.code} was used`, body: `${pc.amount.toLocaleString()} EGP off an order of ${pc.base.toLocaleString()} EGP · used ${code.uses}${code.maxUses ? ` of ${code.maxUses}` : ''} times.`, link: '#/dash/codes' });
        }
        // Each store owner hears about their part of the order
        const byStore = {};
        order.lines.filter((l) => !l.pod && l.storeId).forEach((l) => (byStore[l.storeId] ||= []).push(l));
        Object.entries(byStore).forEach(([sid, ls]) => {
          const st = db.find('stores', sid);
          if (!st || st.official) return;
          const items = ls.reduce((t, l) => t + l.qty, 0), value = ls.reduce((t, l) => t + l.price * l.qty, 0);
          SA.services.notify?.toAccount(st.ownerId, { cat: 'orders', tone: 'turf', title: `New store order ${order.id}`, body: `${items} item${items > 1 ? 's' : ''} · ${value.toLocaleString()} EGP · ${address?.city || ''}. Pack and ship it to get paid.`, link: '#/dash/orders?status=open' });
        });
        db.log('customer', 'order.created', `New shop order ${order.id} (${t.count} item${t.count > 1 ? 's' : ''})`, order.id);
        SA.services.user.notify({ kind: 'shop', title: 'Order confirmed', body: `${order.id} — arriving by ${SA.core.utils.fmtDate(order.eta)}.`, link: `/order/${order.id}` });
        return order;
      });
    },
    orders: () => request(() => store.get().orders.map((o) => ({ ...o, ...tracking(o) }))),
    order: (id) => request(() => {
      const o = store.get().orders.find((x) => x.id === id);
      if (!o) throw new ApiError('Order not found on this device.', 'not_found');
      return { ...o, ...tracking(o) };
    }),
    orderStatus: (createdAt) => orderStatus({ createdAt }),
    trackingSync: (o) => tracking(o),
  };

  /**
   * Live status of an order: each seller fulfils its own items (see dashService.saleState).
   * The order shows the least advanced active stage; fully cancelled orders show "cancelled".
   */
  const STAGES = ['confirmed', 'packed', 'shipped', 'delivered'];
  const tracking = (o) => {
    const groups = new Map();
    o.lines.forEach((l, i) => {
      if (l.pod) {
        // Printed items: one group, status from the print partner's orders
        const po = db.all('podOrders').filter((x) => x.orderId === o.id);
        const PST = { received: 'confirmed', printing: 'packed', shipped: 'shipped', delivered: 'delivered', refused: 'cancelled', cancelled: 'cancelled' };
        const rank = (s) => STAGES.indexOf(PST[s] || 'confirmed');
        const slow = po.filter((x) => x.status !== 'refused').sort((a, b) => rank(a.status) - rank(b.status))[0] || po[0];
        const g = groups.get('pod') || { storeId: 'pod', storeName: 'Printed for you by Super App', official: false, pod: true, status: slow ? PST[slow.status] : 'confirmed',
          courier: slow?.courier || '', tracking: slow?.tracking || '', at: Object.fromEntries((slow?.history || []).map((h) => [PST[h.status] || h.status, h.at])), reason: '', lines: [] };
        g.lines.push({ ...l, index: i });
        groups.set('pod', g);
        return;
      }
      const sale = db.find('sales', `${o.id}-${i + 1}`);
      const f = sale && SA.services.dash ? SA.services.dash.saleState(sale) : { status: orderStatus(o), at: {} };
      const k = l.storeId || 'st-official';
      const g = groups.get(k) || { storeId: k, storeName: storeOf({ storeId: k })?.name || 'Super App Store', official: !!storeOf({ storeId: k })?.official, status: f.status, courier: f.courier || '', tracking: f.tracking || '', at: f.at || {}, reason: (sale?.fulfil || {}).reason || '', lines: [] };
      g.lines.push({ ...l, index: i });
      groups.set(k, g);
    });
    const list = [...groups.values()];
    const live = list.filter((g) => g.status !== 'cancelled');
    const status = live.length ? STAGES[Math.min(...live.map((g) => STAGES.indexOf(g.status)))] : 'cancelled';
    const cancelledValue = list.filter((g) => g.status === 'cancelled').reduce((t, g) => t + g.lines.reduce((a, l) => a + l.price * l.qty, 0), 0);
    return { status, sellers: list, cancelledValue: o.refunded || cancelledValue };
  };

  /** Simulated fulfilment timeline based on order age (official store / legacy orders). */
  const orderStatus = (o) => {
    const h = (Date.now() - o.createdAt) / 36e5;
    return h < 2 ? 'confirmed' : h < 24 ? 'packed' : h < 48 ? 'shipped' : 'delivered';
  };

  SA.services.shop = shopService;
})(window.SA);
