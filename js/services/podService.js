/**
 * Print on demand: the partner catalogue, seller designs (draft → review → live), copyright reports,
 * print orders (received → printing → shipped → delivered | refused) and seller earnings.
 *
 * Money: the customer pays the seller's price. The base price (which already includes Super App's margin, minus the
 * seller's level discount) stays with the platform; the rest is the seller's profit. Profit is pending until the item is
 * delivered and the hold period (7 days) has passed, then it can be withdrawn (minimum 500 EGP).
 * Future API: /pod/catalog, /pod/designs, /pod/orders, /sellers/me/earnings
 */
(function (SA) {
  const { ApiError } = SA.services.http;
  const db = () => SA.services.db;
  const S = () => SA.services.sellers;
  const N = () => SA.services.notify;
  const DAY = 864e5;
  const STATUSES = ['received', 'printing', 'shipped', 'delivered'];

  const cfg = () => db().settings().pod;
  const catalogItem = (id) => db().find('podCatalog', id);
  const artSrc = (d) => (String(d.art || '').startsWith('seed:') ? SA.data.pod.art[d.art.slice(5)] : d.art);

  /** Platform base for one item: catalogue base + back print + big size − the seller's level discount */
  const baseFor = (catalogId, { size, back = false, sellerId } = {}) => {
    const c = catalogItem(catalogId); if (!c) return 0;
    const s = sellerId && S().get(sellerId);
    const disc = s ? S().levelOf(s).baseDiscount || 0 : 0;
    return c.base + (back ? c.backExtra : 0) + (size && c.bigSizes.includes(size) ? c.bigSizeExtra : 0) - disc;
  };
  /** Customer price for a size: seller price + any extra the catalogue charges for big sizes */
  const priceFor = (d, size) => {
    const c = catalogItem(d.catalogId);
    return d.price + (c && size && c.bigSizes.includes(size) ? c.bigSizeExtra : 0);
  };
  const profitFor = (d, size) => priceFor(d, size) - baseFor(d.catalogId, { size, back: !!d.placement?.back, sellerId: d.sellerId });

  const enrich = (d) => {
    const c = catalogItem(d.catalogId);
    const s = S().get(d.sellerId);
    return { ...d, src: artSrc(d), product: c, seller: s ? { id: s.id, name: s.name, handle: s.handle } : null, profit: profitFor(d), base: baseFor(d.catalogId, { back: !!d.placement?.back, sellerId: d.sellerId }) };
  };

  const validate = (d, { submit } = {}) => {
    const c = catalogItem(d.catalogId);
    if (!c || c.status !== 'active') throw new ApiError('Choose a product from the catalogue.', 'validation');
    if (submit) {
      if (!d.art) throw new ApiError('Upload your design first.', 'validation');
      if (String(d.title || '').trim().length < 3) throw new ApiError('Give the design a title (at least 3 characters).', 'validation');
      if (!d.category) throw new ApiError('Choose a category.', 'validation');
      if (!d.colors?.length) throw new ApiError('Pick at least one colour.', 'validation');
    }
    const base = baseFor(d.catalogId, { back: !!d.placement?.back, sellerId: d.sellerId });
    if (!(d.price >= c.minPrice) || d.price > c.maxPrice) throw new ApiError(`Price must be between ${c.minPrice} and ${c.maxPrice} EGP.`, 'validation');
    if (d.price <= base) throw new ApiError(`Price must be above the base price (${base} EGP) so you make a profit.`, 'validation');
    (d.colors || []).forEach((n) => { if (!c.colors.some((x) => x.name === n)) throw new ApiError(`${n} isn’t available for ${c.name}.`, 'validation'); });
  };

  /** Product photo for a design: the garment mockup as an image (cached per design, colour and side) */
  const mocks = new Map();
  const mockUrl = (ref) => {
    const [, id, color, side = 'front'] = String(ref).split(':');
    const d = db().find('designs', id);
    if (!d) return '';
    const key = `${ref}|${d.updatedAt}`;
    const c = catalogItem(d.catalogId);
    const col = c?.colors.find((k) => k.name === color) || c?.colors[0];
    const sd = d.placement?.[side] ? side : d.placement?.front ? 'front' : 'back';
    // Real product photo with the print, once the photos and the artwork have loaded
    const pm = SA.ui.photoMock;
    if (pm && pm.isReady() && pm.hasSide(c?.kind || 'tee', sd)) {
      const photo = pm.compose({ kind: c?.kind || 'tee', side: sd, hex: col?.hex, art: artSrc(d), place: d.placement?.[sd], artRatio: d.artW ? d.artH / d.artW : 1, key: `${d.id}|${d.updatedAt}|${sd}` });
      if (photo) return photo;
    }
    // Drawn mockup while the photos load (cached separately; the photo above has its own cache)
    if (mocks.has(key)) return mocks.get(key);
    const light = SA.ui.garment.lum(col?.hex || '#fff') > 0.6;
    const svgStr = SA.ui.garment({ kind: c?.kind || 'tee', hex: col?.hex, art: artSrc(d), place: d.placement?.[sd], side: sd, artRatio: d.artW ? d.artH / d.artW : 1, label: d.title, bg: light ? '#E4E7E2' : '#F1F0EB' });
    const url = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svgStr)}`;
    mocks.set(key, url);
    if (mocks.size > 200) mocks.delete(mocks.keys().next().value);
    return url;
  };

  const pod = {
    STATUSES, artSrc, baseFor, priceFor, profitFor, enrich, mockUrl,
    cfg,
    /* ---------- Catalogue (admin) ---------- */
    catalog: ({ all = false } = {}) => db().all('podCatalog').filter((c) => all || c.status === 'active'),
    catalogItem,
    saveCatalogItem(item) {
      const n = (v) => Math.round(Number(v));
      const c = { ...item, base: n(item.base), partnerCost: n(item.partnerCost), backExtra: n(item.backExtra || 0), bigSizeExtra: n(item.bigSizeExtra || 0), minPrice: n(item.minPrice), maxPrice: n(item.maxPrice) };
      if (!c.name || c.name.trim().length < 2) throw new ApiError('Name the product.', 'validation');
      if (!(c.base > 0) || !(c.partnerCost > 0)) throw new ApiError('Base price and partner cost are required.', 'validation');
      if (c.base <= c.partnerCost) throw new ApiError('Base price must be above the partner cost — that difference is the platform margin.', 'validation');
      if (!(c.minPrice > c.base - 1) || c.maxPrice <= c.minPrice) throw new ApiError('Check the price limits: minimum ≥ base, maximum > minimum.', 'validation');
      if (!c.colors?.length || !c.sizes?.length) throw new ApiError('Add at least one colour and one size.', 'validation');
      const before = c.id ? catalogItem(c.id) : null;
      const saved = before ? db().update('podCatalog', c.id, c) : db().insert('podCatalog', { ...c, status: c.status || 'active' }, 'pod');
      db().log('acc-admin', 'pod.catalog', `Print catalogue updated: ${saved.name}`, saved.id);
      // Tell sellers whose live designs use this product about changes that affect them
      const affected = [...new Set(db().all('designs').filter((d) => d.catalogId === saved.id && ['live', 'paused'].includes(d.status)).map((d) => d.sellerId))];
      const msg = !before ? null
        : before.status === 'active' && saved.status !== 'active' ? { tone: 'danger', title: `${saved.name} is paused in the catalogue`, body: 'Your designs on it are hidden from the shop until it comes back.' }
        : before.status !== 'active' && saved.status === 'active' ? { tone: 'turf', title: `${saved.name} is back`, body: 'Your designs on it are in the shop again.' }
        : before.base !== saved.base ? { title: `${saved.name}: base price is now ${saved.base} EGP`, body: `It was ${before.base} EGP. Check your prices — your profit per item changes by ${before.base - saved.base} EGP.` }
        : null;
      if (msg) affected.forEach((sid) => N()?.toSeller(sid, { cat: 'store', link: '#/dash/seller/designs', ...msg }));
      // A new product: every print-on-demand seller hears about it
      if (!before) S().list().filter((s) => s.mode !== 'store').forEach((s) => N()?.toSeller(s.id, { cat: 'store', tone: 'turf', title: `New product: ${saved.name}`, body: `Base price ${saved.base} EGP. Add your designs to it in the studio.`, link: '#/sell/studio' }));
      return saved;
    },
    categories: () => db().all('podCategories'),
    category: (id) => db().find('podCategories', id),
    addCategory(name) {
      name = String(name || '').trim();
      if (name.length < 2) throw new ApiError('Name the category.', 'validation');
      const id = S().slug(name) || `cat-${Date.now().toString(36)}`;
      if (db().find('podCategories', id)) throw new ApiError('That category exists.', 'exists');
      return db().insert('podCategories', { id, name });
    },

    /* ---------- Designs ---------- */
    get: (id) => { const d = db().find('designs', id); return d ? enrich(d) : null; },
    list({ sellerId, status, category, q } = {}) {
      const term = String(q || '').trim().toLowerCase();
      return db().all('designs')
        .filter((d) => !d.deleted && (!sellerId || d.sellerId === sellerId) && (!status || (Array.isArray(status) ? status.includes(d.status) : d.status === status)) && (!category || d.category === category))
        .filter((d) => !term || [d.title, d.desc, ...(d.tags || [])].join(' ').toLowerCase().includes(term))
        .map(enrich);
    },
    /** Live designs from active sellers, ranked by sales with the level search boost */
    live({ category, q, sellerId } = {}) {
      return pod.list({ status: 'live', category, q, sellerId })
        .filter((d) => { const s = S().get(d.sellerId); return s?.status === 'active' && !S().onBreak(s) && d.product?.status === 'active' && (!SA.services.factory || SA.services.factory.canMake(d.catalogId)); })
        .map((d) => ({ ...d, rank: (d.sales || 0) + (d.views || 0) / 100 + S().levelOf(S().get(d.sellerId)).boost * 4 }))
        .sort((a, b) => b.rank - a.rank);
    },

    saveDraft(sellerId, draft) {
      const s = S().get(sellerId);
      if (!s || s.status !== 'active') throw new ApiError('Your seller account is not active.', 'forbidden');
      const c = catalogItem(draft.catalogId);
      const d = {
        sellerId, title: String(draft.title || '').trim().slice(0, 60), desc: String(draft.desc || '').trim().slice(0, 500), tags: (draft.tags || []).slice(0, 10),
        category: draft.category || '', catalogId: draft.catalogId, colors: draft.colors || [], price: Math.round(Number(draft.price) || (c ? c.base + 100 : 0)),
        art: draft.art || '', artW: draft.artW || 0, artH: draft.artH || 0, placement: draft.placement || { front: { x: 0.5, y: 0.36, w: 0.42 }, back: null },
      };
      validate({ ...d, price: d.price || c?.minPrice }, { submit: false });
      if (draft.id) {
        const cur = db().find('designs', draft.id);
        if (!cur || cur.sellerId !== sellerId) throw new ApiError('Design not found.', 'not_found');
        // Editing a live design sends it back to review (the old version stays hidden until approved)
        const status = cur.status === 'live' || cur.status === 'rejected' ? 'draft' : cur.status;
        return db().update('designs', draft.id, { ...d, status });
      }
      const lim = S().canAddDesign(s);
      if (!lim.ok) throw new ApiError(`Your level allows ${lim.limit} active designs. Pause or delete one, or level up to add more.`, 'limit');
      return db().insert('designs', { ...d, status: 'draft', reason: '', views: 0, sales: 0 }, 'ds');
    },
    /** Send for review — Elite sellers publish instantly (spot-checked later) */
    submit(id) {
      const d = db().find('designs', id); if (!d) throw new ApiError('Design not found.', 'not_found');
      const s = S().get(d.sellerId);
      validate(d, { submit: true });
      const instant = S().levelOf(s).review === 'instant';
      const out = db().update('designs', id, { status: instant ? 'live' : 'review', reason: '', submittedAt: Date.now(), reviewedAt: instant ? Date.now() : 0, spotCheck: instant });
      db().log(s.accountId, 'design.submitted', `${s.name} ${instant ? 'published' : 'sent for review'} “${d.title}”`, id);
      N()?.toSeller(s.id, instant
        ? { cat: 'designs', tone: 'turf', title: `“${d.title}” is live`, body: 'Elite sellers publish straight away. We may spot-check it later.', link: `#/shop/pod-${id}` }
        : { cat: 'designs', title: `“${d.title}” is in review`, body: S().levelOf(s).review === 'priority' ? 'Pro sellers are reviewed first — usually within a few hours.' : 'We review every design, usually within 24 hours.', link: '#/dash/seller/designs' });
      if (!instant) N()?.toAdmins({ cat: 'designs', title: `New design to review: “${d.title}”`, body: `${s.name} · ${S().levelOf(s).name}${S().levelOf(s).review === 'priority' ? ' · priority' : ''}`, link: '#/admin/designs' });
      return out;
    },
    setPaused(id, paused) {
      const d = db().find('designs', id);
      if (!d || !['live', 'paused'].includes(d.status)) throw new ApiError('Only live designs can be paused.', 'validation');
      return db().update('designs', id, { status: paused ? 'paused' : 'live' });
    },
    remove(id) {
      const d = db().find('designs', id); if (!d) return;
      db().update('designs', id, { deleted: true, status: 'deleted' });
    },
    /** Review queue: priority sellers first, then oldest */
    queue() {
      const order = { priority: 0, instant: 0, normal: 1 };
      return pod.list({ status: 'review' }).map((d) => ({ ...d, lane: S().levelOf(S().get(d.sellerId)).review }))
        .concat(pod.list({ status: 'live' }).filter((d) => d.spotCheck).map((d) => ({ ...d, lane: 'spot' })))
        .sort((a, b) => (order[a.lane] ?? 2) - (order[b.lane] ?? 2) || (a.submittedAt || 0) - (b.submittedAt || 0));
    },
    approve(id) {
      const d = db().update('designs', id, { status: 'live', reason: '', reviewedAt: Date.now(), spotCheck: false });
      N()?.toSeller(d.sellerId, { cat: 'designs', tone: 'turf', title: `Approved: “${d.title}”`, body: 'It’s live in the shop now. Share your store link to get the first sale.', link: `#/shop/pod-${id}` });
      db().log('acc-admin', 'design.approved', `Approved “${d.title}”`, id);
      return d;
    },
    reject(id, reason, { strike = false } = {}) {
      reason = String(reason || '').trim();
      if (reason.length < 4) throw new ApiError('Tell the seller why (at least a few words).', 'validation');
      const d = db().update('designs', id, { status: 'rejected', reason, reviewedAt: Date.now(), spotCheck: false });
      N()?.toSeller(d.sellerId, { cat: 'designs', tone: 'danger', title: `Not approved: “${d.title}”`, body: `Reason: ${reason} Fix it in the studio and send it again.`, link: `#/sell/studio/${id}` });
      if (strike) S().strike(d.sellerId, reason, id);
      db().log('acc-admin', 'design.rejected', `Rejected “${d.title}”: ${reason}`, id);
      return d;
    },

    /* ---------- Copyright / content reports ---------- */
    report(designId, { reason, detail = '', contact = '' }) {
      if (!['copyright', 'trademark', 'offensive', 'other'].includes(reason)) throw new ApiError('Choose a reason.', 'validation');
      const d = db().find('designs', designId); if (!d) throw new ApiError('Design not found.', 'not_found');
      const r = db().insert('reports', { designId, sellerId: d.sellerId, reason, detail: String(detail).slice(0, 600), contact: String(contact).slice(0, 120), status: 'open' }, 'rp');
      N()?.toAdmins({ cat: 'account', tone: 'danger', title: `${reason[0].toUpperCase()}${reason.slice(1)} report: “${d.title}”`, body: String(detail || 'No details given').slice(0, 120), link: '#/admin/designs?tab=reports' });
      return r;
    },
    reports: ({ status } = {}) => db().all('reports').filter((r) => !status || r.status === status).map((r) => ({ ...r, design: pod.get(r.designId) })),
    resolveReport(id, action) {
      const r = db().find('reports', id); if (!r) throw new ApiError('Report not found.', 'not_found');
      if (action === 'takedown') pod.reject(r.designId, `Removed after a ${r.reason} report.`, { strike: r.reason === 'copyright' || r.reason === 'trademark' });
      if (action !== 'takedown') { const d = db().find('designs', r.designId); if (d) N()?.toSeller(d.sellerId, { cat: 'designs', tone: 'turf', title: `Report checked: “${d.title}” stays live`, body: `Someone reported it (${r.reason}). The Super App team checked it and found no problem.`, link: '#/dash/seller/designs' }); }
      return db().update('reports', id, { status: action === 'takedown' ? 'removed' : 'dismissed', resolvedAt: Date.now() });
    },

    /* ---------- Orders ---------- */
    refusals: (customerId) => db().raw().podCustomers[customerId]?.refused || 0,
    /** Customers who refused delivery too often must pay online for print-on-demand items */
    needsPrepay: (customerId) => {
      // The admin can force it on or off for a customer; otherwise it follows the refusal limit
      const mode = db().raw().podCustomers[customerId]?.prepay;
      return mode === 'always' ? true : mode === 'never' ? false : pod.refusals(customerId) >= cfg().refuseLimit;
    },

    /** Called by checkout for each print-on-demand cart line */
    place({ orderId, customer, lines, payment }) {
      if (payment === 'cod' && pod.needsPrepay(customer.id)) throw new ApiError('Printed-to-order items need online payment on this account.', 'prepay_required');
      const made = pod._place({ orderId, customer, lines, payment });
      // One notification per seller for this order
      const bySeller = {};
      made.forEach((o) => (bySeller[o.sellerId] ||= []).push(o));
      Object.entries(bySeller).forEach(([sid, os]) => {
        const items = os.reduce((t, o) => t + o.qty, 0), profit = os.reduce((t, o) => t + o.profit, 0);
        const first = os[0], prod = catalogItem(first.catalogId)?.name || '';
        N()?.toSeller(sid, { cat: 'orders', tone: 'turf', title: os.length === 1 ? `New order: ${first.title}` : `New order: ${items} items`,
          body: `${os.length === 1 ? `${prod}, ` : ''}${os.map((o) => `${o.qty} × ${o.color} · ${o.size}`).join(', ')} · you earn ${profit.toLocaleString()} EGP after delivery.`, link: '#/dash/seller/orders' });
      });
      // The factory gets the job (SA.services.factory routes it and tells the factory)
      SA.services.factory?.announce(made);
      return made;
    },
    _place({ orderId, customer, lines, payment }) {
      return lines.map((l) => {
        const d = db().find('designs', l.designId);
        if (!d || d.status !== 'live') throw new ApiError(`“${l.title || 'A design'}” is no longer available.`, 'unavailable');
        if (SA.services.factory && !SA.services.factory.canMake(d.catalogId)) throw new ApiError(`“${d.title}” can’t be printed right now. Try again later.`, 'unavailable');
        const c = catalogItem(d.catalogId);
        const base = baseFor(d.catalogId, { size: l.size, back: !!d.placement?.back, sellerId: d.sellerId });
        const price = priceFor(d, l.size);
        const now = Date.now();
        return db().insert('podOrders', {
          id: `PO-${now.toString(36).toUpperCase().slice(-5)}${Math.random().toString(36).slice(2, 4).toUpperCase()}`, orderId, designId: d.id, sellerId: d.sellerId, catalogId: d.catalogId,
          title: d.title, color: l.color, size: l.size, qty: l.qty, price, base, profit: (price - base) * l.qty, partnerCost: c.partnerCost + (d.placement?.back ? Math.round(c.backExtra * 0.6) : 0),
          customer: { id: customer.id, name: customer.name, city: customer.city || '', phone: customer.phone || '', address: customer.address || '' }, payment, status: 'received', history: [{ status: 'received', at: now }], placedAt: now, deliveredAt: 0,
          ...(SA.services.factory?.assignNew({ catalogId: d.catalogId }) || {}),
        });
      });
    },
    orders: ({ sellerId, status } = {}) => db().all('podOrders').filter((o) => (!sellerId || o.sellerId === sellerId) && (!status || (Array.isArray(status) ? status.includes(o.status) : o.status === status))),
    /** Print partner moves an order along; delivery counts the sale, refusal is absorbed by the platform */
    advance(id, status, { courier, tracking } = {}) {
      const o = db().find('podOrders', id); if (!o) throw new ApiError('Order not found.', 'not_found');
      const allowed = { received: ['printing'], printing: ['shipped'], shipped: ['delivered', 'refused'] };
      if (!allowed[o.status]?.includes(status)) throw new ApiError(`Can’t move from ${o.status} to ${status}.`, 'validation');
      if (status === 'shipped' && !tracking) throw new ApiError('Add the courier tracking number.', 'validation');
      const now = Date.now();
      const patch = { status, history: [...(o.history || []), { status, at: now }] };
      if (status === 'shipped') Object.assign(patch, { courier: courier || 'Courier', tracking });
      if (status === 'delivered') {
        patch.deliveredAt = now;
        const d = db().find('designs', o.designId); if (d && !o.sample) db().update('designs', d.id, { sales: (d.sales || 0) + o.qty });
        if (o.sample) { const sm = (db().all('samples') || []).find((x) => x.orderId === o.id); if (sm) db().update('samples', sm.id, { status: 'delivered' }); }
      }
      if (status === 'refused') {
        const pc = db().raw().podCustomers;
        pc[o.customer.id] = { refused: (pc[o.customer.id]?.refused || 0) + 1, at: now };
        patch.loss = o.partnerCost; // the platform absorbs the print + shipping cost
      }
      const out = db().update('podOrders', id, patch);
      db().log('acc-print', `pod.${status}`, `${o.id} “${o.title}” ${status}`, id);
      const hold = cfg().holdDays;
      const sellerMsg = {
        printing: { title: `Printing: ${o.title}`, body: `${o.qty} × ${o.color} · ${o.size} — the print partner started on it.` },
        shipped: { title: `Shipped: ${o.title}`, body: `${courier || 'Courier'} · tracking ${tracking}. Usually delivered in 1–3 days.` },
        delivered: { tone: 'turf', title: `Delivered: ${o.title}`, body: `${o.profit.toLocaleString()} EGP profit is released in ${hold} days.` },
        refused: { tone: 'danger', title: `Refused on delivery: ${o.title}`, body: 'The customer didn’t accept it. Super App covers the cost — nothing is taken from your wallet.' },
      }[status];
      if (sellerMsg) N()?.toSeller(o.sellerId, { cat: 'orders', link: '#/dash/seller/orders', ...sellerMsg });
      // The customer on this device hears about shipping and delivery
      const u = SA.core.store.get().user;
      if (u && o.customer.id === u.id && ['shipped', 'delivered'].includes(status)) {
        SA.services.user.notify({ kind: 'shop', title: status === 'shipped' ? 'Your printed item is on the way' : 'Your printed item was delivered', body: `“${o.title}” (${o.color}, ${o.size})${status === 'shipped' ? ` — tracking ${tracking}` : ''}.`, link: `/order/${o.orderId}` });
      }
      return out;
    },
    /** A print fault: the partner reprints at its own cost, nothing changes for the seller */
    reprint(id, note) {
      const o = db().find('podOrders', id); if (!o) throw new ApiError('Order not found.', 'not_found');
      // Logged on the order only: the status, the sale and the seller's profit don't change
      N()?.toSeller(o.sellerId, { cat: 'orders', title: `Reprinted: ${o.title}`, body: 'The print partner fixed a print fault at their own cost. Your profit doesn’t change.', link: '#/dash/seller/orders' });
      return db().update('podOrders', id, { reprints: (o.reprints || 0) + 1, history: [...o.history, { status: 'reprint', at: Date.now(), note }] });
    },

    /* ---------- Samples (a seller orders their own design to check the print) ---------- */
    /** Price of a sample for this seller: base price, Pro 30% off, Elite one free a month */
    samplePrice(designId, size) {
      const d = db().find('designs', designId); if (!d) throw new ApiError('Design not found.', 'not_found');
      const s = S().get(d.sellerId); const lv = S().levelOf(s);
      const base = baseFor(d.catalogId, { size, back: !!d.placement?.back, sellerId: d.sellerId });
      const month = new Date().toISOString().slice(0, 7);
      const usedFree = (db().all('samples') || []).filter((x) => x.sellerId === s.id && x.free && x.status !== 'rejected' && new Date(x.createdAt).toISOString().slice(0, 7) === month).length;
      const free = (lv.freeSamples || 0) > usedFree;
      // Elite: one free a month, then the Pro discount (30%)
      const disc = lv.sampleDiscount >= 1 ? 0.3 : lv.sampleDiscount || 0;
      const price = free ? 0 : Math.round(base * (1 - disc));
      return { base, price, free, discount: free ? 1 : disc, freeLeft: Math.max(0, (lv.freeSamples || 0) - usedFree), level: lv.name };
    },
    requestSample(designId, { color, size, address }) {
      const d = db().find('designs', designId); if (!d) throw new ApiError('Design not found.', 'not_found');
      if (!['live', 'review', 'paused', 'draft'].includes(d.status)) throw new ApiError('This design can’t be sampled.', 'invalid');
      if (!d.colors.includes(color)) throw new ApiError('Choose one of the design’s colours.', 'invalid');
      const c = catalogItem(d.catalogId); if (!c?.sizes.includes(size)) throw new ApiError('Choose a size.', 'invalid');
      if (!address || String(address).trim().length < 8) throw new ApiError('Enter the delivery address.', 'invalid');
      if (SA.services.factory && !SA.services.factory.canMake(d.catalogId)) throw new ApiError('The factory can’t make this product right now. Try again later.', 'unavailable');
      const p = pod.samplePrice(designId, size);
      const s = S().get(d.sellerId);
      const e = pod.earnings(s.id);
      if (p.price > e.available) throw new ApiError(`The sample costs ${p.price} EGP — your available balance is ${e.available} EGP. Wait for more profit to clear.`, 'insufficient');
      const x = db().insert('samples', { sellerId: s.id, designId, title: d.title, catalogId: d.catalogId, color, size, address: String(address).trim(), price: p.price, free: p.free, status: 'requested' }, 'sm');
      db().log(s.accountId, 'sample.requested', `${s.name} ordered a sample of “${d.title}” (${color}, ${size})`, x.id);
      N()?.toAdmins({ cat: 'designs', title: `Sample request: ${d.title}`, body: `${s.name} · ${color} · ${size} · ${p.free ? 'free (Elite)' : `${p.price} EGP`}`, link: '#/admin/samples' });
      return x;
    },
    samples: (sellerId) => (db().all('samples') || []).filter((x) => !sellerId || x.sellerId === sellerId).sort((a, b) => b.createdAt - a.createdAt),

    /* ---------- Seller earnings ---------- */
    earnings(sellerId) {
      const hold = cfg().holdDays * DAY;
      const now = Date.now();
      const os = pod.orders({ sellerId });
      const s = S().get(sellerId);
      const sum = (arr) => arr.reduce((t, o) => t + o.profit, 0);
      const delivered = os.filter((o) => o.status === 'delivered');
      const cleared = delivered.filter((o) => now - o.deliveredAt >= hold);
      const pending = os.filter((o) => ['received', 'printing', 'shipped'].includes(o.status)).concat(delivered.filter((o) => now - o.deliveredAt < hold));
      const paidOut = db().all('sellerPayouts').filter((p) => p.sellerId === sellerId && p.status !== 'rejected').reduce((t, p) => t + p.amount, 0);
      const history = (s?.history?.profit || 0); // earned (and already paid) before the recent ledger
      // Admin adjustments (refund shares, sample charges, corrections) on the seller's account
      const adjustments = (db().all('adjustments') || []).filter((x) => x.accountId === s?.accountId && x.wallet === 'pod');
      const adjusted = adjustments.reduce((t, x) => t + x.amount, 0);
      return {
        pending: sum(pending), available: Math.max(0, sum(cleared) - paidOut + adjusted), lifetime: history + sum(delivered), paidOut, adjustments, adjusted,
        nextClear: delivered.filter((o) => now - o.deliveredAt < hold).map((o) => ({ id: o.id, amount: o.profit, at: o.deliveredAt + hold })).sort((a, b) => a.at - b.at),
        minWithdraw: cfg().minWithdraw,
      };
    },
    withdraw(sellerId, amount) {
      const s = S().get(sellerId); if (!s) throw new ApiError('Seller not found.', 'not_found');
      const e = pod.earnings(sellerId);
      amount = Math.round(Number(amount));
      if (s.kyc !== 'verified') throw new ApiError('Verify your identity before your first withdrawal.', 'kyc_required');
      if (!(amount >= e.minWithdraw)) throw new ApiError(`The minimum withdrawal is ${e.minWithdraw} EGP.`, 'min_not_met');
      if (amount > e.available) throw new ApiError(`You can withdraw up to ${e.available} EGP now.`, 'insufficient');
      const acc = db().find('accounts', s.accountId);
      const p = db().insert('sellerPayouts', { sellerId, amount, method: acc?.payout?.method || 'InstaPay', detail: acc?.payout?.detail || '', status: 'processing' }, 'sp');
      db().log(s.accountId, 'seller.withdraw', `${s.name} requested a ${amount} EGP withdrawal`, p.id);
      N()?.toSeller(sellerId, { cat: 'money', title: `Withdrawal requested: ${amount.toLocaleString()} EGP`, body: `To ${p.method} ${p.detail}. You’ll get a notification when it’s sent.`, link: '#/dash/seller/wallet' });
      return p;
    },
  };

  SA.services.pod = pod;
})(window.SA);
