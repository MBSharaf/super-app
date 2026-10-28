/**
 * A seller's own store on the real API (owner's decisions, 2026-10-27): products, orders and returns of the signed-in
 * seller, through `/seller/store/...` and `/seller/returns`. Loaded after liveShop and liveReturns; demo mode is unchanged.
 *
 * The seller dashboard pages read `SA.services.dash` synchronously from the local data, so the store, its products and
 * orders are mirrored there (records marked `live`) when the seller dashboard opens. Changes show at once and go to the
 * server; the mirror is reloaded with the server's answer, and a refused change is undone with the server's message.
 *
 * Products: the site keeps one stock number per product with its sizes and colours; the server keeps stock per size and
 * colour (variant). A new product's stock is shared out over its variants; a restock goes to the variant with the least.
 * A product is sent for review when published (Super App approves each one); its status shows "Waiting for review" or
 * "Not approved" with the reason.
 */
(function (SA) {
  const api = SA.services.api;
  const D = SA.services.dash;
  const RT = SA.services.returns;
  const db = SA.services.db;
  const { ApiError } = SA.services.http;
  const demo = { ...D };
  const demoRt = { list: RT.list, get: RT.get, decide: RT.decide, receive: RT.receive };

  const STORE = 'st-live';
  const AREA = '/seller';
  const ACCOUNT = () => SA.services.liveShop?.SELLER_ACCOUNT || 'acc-live-seller';
  const isLive = () => api.isLive() && !!db.find('stores', STORE);
  const liveProduct = (id) => api.isLive() && String(id).startsWith('lp-');
  const fail = (err) => { SA.ui.overlay.toast(err.message, { type: 'error' }); SA.app?.rerender?.(); };
  const photo = (ph) => (!ph ? '' : ph.url ? `${location.origin}${ph.url}` : ph.key);
  const STATUS_IN = { Published: 'active', Draft: 'draft', Unpublished: 'draft', WaitingForReview: 'review', Rejected: 'rejected' };

  /* ---------------- Mirror ---------------- */

  const toProduct = (p) => ({
    id: `lp-${p.id}`, apiId: p.id, live: true, slug: p.slug, storeId: STORE, name: p.name, brand: p.brand || 'Unbranded', cat: p.categoryKey, price: +p.price,
    was: p.compareAtPrice ? +p.compareAtPrice : +p.price, stock: p.stock, lowStockAt: p.lowStockAt,
    sizes: [...new Set(p.variants.map((v) => v.size))], colors: [...new Map(p.variants.map((v) => [v.colorName, { name: v.colorName, hex: v.colorHex || '#0D1411' }])).values()],
    images: p.images.map((i) => photo(i.photo)), imageIds: p.images.map((i) => ({ id: i.id, src: photo(i.photo) })), variants: p.variants,
    desc: p.description || '', specs: p.specs.map((s) => [s.label, s.value]), badges: [], status: STATUS_IN[p.status] || 'draft', reviewNote: p.reviewNote || '',
    hiddenByAdmin: p.hiddenByAdmin, hiddenReason: p.hiddenReason || '', rating: 0, reviews: 0, createdAt: Date.parse(p.createdAt), updatedAt: Date.parse(p.createdAt), deleted: false,
  });

  const put = (col, list, keep = () => false) => {
    const raw = db.raw();
    raw[col] = [...(raw[col] || []).filter((x) => !x.live || keep(x)), ...list];
    db.touch?.(col);
  };

  const loadProducts = async () => {
    const list = await api.get(`${AREA}/store/products`);
    put('products', list.map(toProduct), (x) => !String(x.id).startsWith('lp-'));
  };

  /** The seller's store as the dashboard's local store record */
  const loadStore = async () => {
    const st = await api.get(`${AREA}/store`);
    const rec = {
      id: STORE, live: true, ownerId: ACCOUNT(), name: st.name, slug: st.handle, handle: st.handle, tagline: st.tagline || '', description: st.about || '',
      logo: st.logo ? photo(st.logo) : null, cover: st.cover ? photo(st.cover) : 'photo-1579952363873-27f3bade9f55', phone: '', status: st.status === 'Suspended' ? 'suspended' : 'active', official: false,
    };
    put('stores', [rec], (x) => x.id !== STORE && x.live);
    await Promise.all([loadProducts(), loadOrders(), loadReturns()]);
    return rec;
  };

  let loading = null;
  const ready = async (rec) => {
    if (!rec?.storeId) return;
    loading ||= loadStore().catch((err) => { console.error(err); }).finally(() => setTimeout(() => { loading = null; }, 15000));
    await loading;
  };
  const reload = () => { loading = null; return ready({ storeId: STORE }).then(() => SA.app?.rerender?.()); };

  /* ---------------- Products ---------------- */

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

  /** Photos: new pictures (data: URLs) are uploaded, removed ones deleted, then the order is saved */
  const syncPhotos = async (apiId, images, old = []) => {
    const keep = old.filter((o) => images.includes(o.src));
    for (const o of old.filter((x) => !images.includes(x.src))) await api.del(`${AREA}/store/products/${apiId}/images/${o.id}`);
    const fresh = images.filter((src) => String(src).startsWith('data:'));
    let saved = null;
    if (fresh.length) {
      const form = new FormData();
      fresh.forEach((src, i) => form.append('photos', api.blob(src), `photo-${i + 1}.jpg`));
      saved = await api.form(`${AREA}/store/products/${apiId}/images`, form);
    }
    const all = saved ? saved.images : null;
    if (all && keep.length) {
      // Same order as on the page: kept photos where they were, new ones in their places
      const ids = images.map((src) => keep.find((k) => k.src === src)?.id).filter(Boolean);
      const added = all.filter((i) => !keep.some((k) => k.id === i.id)).map((i) => i.id);
      await api.put(`${AREA}/store/products/${apiId}/images/order`, { imageIds: [...ids, ...added] });
    }
  };

  /** Saved first as a draft with its photos, then published (so the reviewers see the photos) */
  const save = async (old, d) => {
    const publish = d.status !== 'draft';
    let p = old ? await api.put(`${AREA}/store/products/${old.apiId}`, requestOf(d, old.status === 'draft' || !publish ? 'Draft' : 'Published', old))
      : await api.post(`${AREA}/store/products`, requestOf(d, 'Draft'));
    await syncPhotos(p.id, d.images || [], old?.imageIds || []);
    if (publish && (!old || old.status === 'draft')) p = await api.put(`${AREA}/store/products/${p.id}`, requestOf(d, 'Published', toProduct(await api.get(`${AREA}/store/products/${p.id}`))));
    return p;
  };

  const validate = (d) => { const e = D.validateProduct(d); if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e); };
  const local = (id, patch) => db.update('products', id, patch);

  Object.assign(D, {
    commission: () => (isLive() ? { ...demo.commission(), store: 0.05 } : demo.commission()),   // owner's decision: 5% on every purchase
    createProduct(storeId, d) {
      if (!api.isLive() || storeId !== STORE) return demo.createProduct(storeId, d);
      validate(d);
      const temp = { ...toProduct({ id: `new-${Date.now()}`, slug: '', name: d.name, brand: d.brand, categoryKey: d.cat, price: d.price, compareAtPrice: d.was, stock: +d.stock, lowStockAt: +d.lowStockAt || 5,
        status: d.status === 'draft' ? 'Draft' : 'WaitingForReview', images: [], variants: [], specs: [], createdAt: new Date().toISOString() }), images: d.images || [], sizes: d.sizes, colors: d.colors };
      db.raw().products.push(temp); db.touch?.('products');
      save(null, d).then(reload, (err) => { fail(err); reload(); });
      return temp;
    },
    updateProduct(id, d) {
      if (!liveProduct(id)) return demo.updateProduct(id, d);
      validate(d);
      const old = db.find('products', id);
      const out = local(id, { name: d.name.trim(), price: Math.round(+d.price), stock: +d.stock, images: d.images, status: d.status === 'draft' ? 'draft' : old.status === 'active' ? 'active' : 'review' });
      save(old, d).then(reload, (err) => { fail(err); reload(); });
      return out;
    },
    deleteProduct(id) {
      if (!liveProduct(id)) return demo.deleteProduct(id);
      const p = db.find('products', id);
      local(id, { deleted: true });
      api.del(`${AREA}/store/products/${p.apiId}`).then(reload, (err) => { fail(err); reload(); });
    },
    setProductStatus(id, status) {
      if (!liveProduct(id)) return demo.setProductStatus(id, status);
      const p = db.find('products', id);
      const next = status === 'active' ? 'Published' : 'Unpublished';
      local(id, { status: status === 'active' ? 'review' : 'draft' });   // the server says whether it needs review (reloaded at once)
      api.put(`${AREA}/store/products/${p.apiId}`, requestOf(p, next, p)).then(reload, (err) => { fail(err); reload(); });
    },
    restock(productId, add) {
      if (!liveProduct(productId)) return demo.restock(productId, add);
      const p = db.find('products', productId);
      const n = Math.round(+add);
      if (!(n >= 1 && n <= 10000)) throw new ApiError('Enter how many units arrived (1 or more).', 'invalid', { add: 'Enter a number' });
      const v = [...p.variants].sort((a, b) => a.stock - b.stock)[0];
      const out = local(productId, { stock: p.stock + n });
      api.post(`${AREA}/store/products/${p.apiId}/restock`, { variantId: v.id, add: n }).then(reload, (err) => { fail(err); reload(); });
      return out;
    },
  });

  /* ---------------- Store profile ---------------- */

  const upload = async (kind, src) => {
    if (!String(src || '').startsWith('data:')) return;
    const form = new FormData();
    form.append('file', api.blob(src), `${kind}.jpg`);
    await api.form(`${AREA}/store/${kind}`, form);
  };
  D.updateStore = (id, d) => {
    if (!api.isLive() || id !== STORE) return demo.updateStore(id, d);
    const e = D.validateStore({ ...d, id }); if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e);
    const st = db.find('stores', STORE);
    const out = db.update('stores', STORE, { name: d.name.trim(), tagline: (d.tagline || '').trim(), description: d.description.trim(), logo: d.logo || null, cover: d.cover });
    api.put(`${AREA}/store`, { name: d.name.trim(), handle: st.handle, tagline: (d.tagline || '').trim() || null, about: d.description.trim(), accentColor: null, coverPositionY: 50, mode: 'store' })
      .then(() => Promise.all([upload('logo', d.logo), upload('cover', d.cover)])).then(reload, (err) => { fail(err); reload(); });
    return out;
  };

  /* ---------------- Store orders (filled in R6.7d3) ---------------- */
  let orders = [];
  const loadOrders = async () => { orders = []; };

  /* ---------------- Returns (filled in R6.7d3) ---------------- */
  let returns = [];
  const loadReturns = async () => { returns = []; };

  // The seller dashboard opens the seller from the server: the store comes with it
  const sellerReady = SA.services.liveShop.sellerReady;
  SA.services.liveShop.sellerReady = async (dashboard) => { const rec = await sellerReady(dashboard); if (rec) await ready(rec); return rec; };

  SA.services.liveStore = { STORE, reload, orders: () => orders, returns: () => returns, demoRt };
})(window.SA);
