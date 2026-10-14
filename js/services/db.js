/**
 * Mock platform database shared by the public site and the Partner Dashboard.
 *
 * Collections: accounts, stores, pitches, products, bookings (real bookings: app + walk-in),
 * bookingOverrides (status changes on historical bookings), closures, sales (store order lines),
 * payouts, activity, settings, reviews (with owner replies), series (weekly team bookings and
 * memberships), promos (off-peak offers), saleOverrides (fulfilment of historical store orders).
 *
 * It is persisted to localStorage under its own key, separate from the shopper's personal state.
 * When the real backend exists, this file is replaced by API calls; services keep their signatures.
 */
(function (SA) {
  const KEY = 'superapp.db.v1';
  const VERSION = 1;
  const DAY = 864e5;

  const OWNERS = {
    'acc-o1': ['p-zayed-22', 'p-october-dome', 'p-fifth-arena'],
    'acc-o2': ['p-gezira-fives', 'p-dokki-river', 'p-helio-night'],
    'acc-o3': ['p-maadi-cage', 'p-mokattam-hill', 'p-nasr-hub'],
    'acc-o4': ['p-smouha-seven', 'p-rehab-club', 'p-madinaty-park'],
  };
  const ownerOf = (pitchId) => Object.keys(OWNERS).find((k) => OWNERS[k].includes(pitchId)) || 'acc-admin';

  /* ---------- Seeds added in later releases (applied by migrate(), existing data is kept) ---------- */
  const REPLY = {
    5: (n) => `Thanks ${n}! Glad you enjoyed it — see you on the pitch again soon.`,
    4: (n) => `Thanks for the feedback, ${n}. We’ve passed your note on to the team.`,
    3: (n) => `Sorry about that, ${n}. We’ve fixed it and added a staff check between games. Hope to see you again.`,
  };
  const seedReviews = (pitches, accounts) => {
    const { seeded } = SA.core.utils;
    const taken = new Set(accounts.map((a) => a.name));
    const people = SA.data.matches.people.filter((n) => !taken.has(n));
    const pool = SA.data.pitches.reviewPool;
    const out = [];
    pitches.filter((p) => p.seed).forEach((p) => {
      // Each of an owner's pitches gets a different slice of the pool, so texts don't repeat for one partner
      const k = Math.max(0, (OWNERS[p.ownerId] || []).indexOf(p.id));
      const start = (k * 5 + Math.floor(seeded(p.ownerId) * pool.length)) % pool.length;
      for (let i = 0; i < 5; i++) {
        const t = pool[(start + i) % pool.length];
        const name = people[(Math.floor(seeded(`${p.id}|rv`) * people.length) + i * 3) % people.length]; // distinct reviewers per pitch
        const at = Date.now() - (1 + i * 6 + Math.floor(seeded(`${p.id}|d|${i}`) * 5)) * DAY - Math.floor(seeded(`${p.id}|h|${i}`) * 12) * 36e5;
        const replied = i >= 2 && seeded(`${p.id}|rp|${i}`) < 0.6;
        out.push({ id: `rv-${p.id}-${i}`, pitchId: p.id, ownerId: p.ownerId, customer: { id: `c-${i}`, name, avatar: Math.floor(seeded(name) * 15) }, rating: t.rating, text: t.text, createdAt: at,
          reply: replied ? { text: REPLY[t.rating](name.split(' ')[0]), at: at + 9 * 36e5, by: p.ownerId } : null, reported: null, hidden: false, seed: true });
      }
    });
    return out.sort((a, b) => b.createdAt - a.createdAt);
  };
  /** Demo sellers, their accounts, live designs and the last month of print-on-demand orders */
  const seedSellers = () => {
    const P = SA.data.pod;
    const { seeded } = SA.core.utils;
    const now = Date.now();
    const people = SA.data.matches.people;
    const accounts = P.sellers.map((s) => ({ id: s.accountId, role: 'seller', name: s.owner, company: s.name, email: s.email, phone: s.phone, avatar: s.avatar, joinedAt: now - s.joinedDays * DAY, payout: { method: 'InstaPay', detail: `${s.handle}@instapay` } }));
    accounts.push({ id: 'acc-print', role: 'printer', name: 'Print partner', company: 'Partner Print House', email: 'orders@printpartner.example', phone: '+20 2 2390 1100', avatar: 4, joinedAt: now - 120 * DAY, payout: null });
    data.accounts.push(...accounts);
    const sellers = P.sellers.map((s) => ({
      id: s.id, accountId: s.accountId, userId: null, handle: s.handle, name: s.name, bio: s.bio, cover: s.cover, logo: null, avatar: s.avatar, mode: s.mode,
      storeId: s.mode === 'pod' ? null : `st-${s.handle}`, status: 'active', strikes: [], frozenUntil: 0, kyc: 'verified', followers: Math.round(40 + s.history.sales * 3.2),
      history: { ...s.history }, createdAt: now - s.joinedDays * DAY, updatedAt: now - 2 * DAY,
    }));
    sellers.filter((s) => s.storeId).forEach((s) => data.stores.push({ id: s.storeId, ownerId: s.accountId, name: s.name, slug: s.handle, tagline: 'Printed and packed in Cairo', description: s.bio, logo: null, cover: s.cover, phone: '', status: 'active', createdAt: s.createdAt }));
    const designs = P.designs.map(([sellerId, art, title, category, catalogId, colors, price, tags, desc, status = 'live'], i) => ({
      id: `ds-${i + 1}`, sellerId, title, desc, tags, category, catalogId, colors, price, art: `seed:${art}`,
      placement: { front: { x: 0.5, y: 0.36, w: catalogId === 'pod-cap' ? 0.34 : 0.42 }, back: null },
      status, reason: '', views: status === 'live' ? Math.round(300 + seeded(`ds${i}v`) * 2600) : 0, sales: 0,
      createdAt: now - (status === 'live' ? 8 + i * 6 : 1) * DAY, updatedAt: now - (status === 'live' ? 8 + i * 6 : 1) * DAY,
      reviewedAt: status === 'live' ? now - (7 + i * 6) * DAY : 0, submittedAt: status === 'review' ? now - (i % 2 ? 5 : 20) * 36e5 : 0,
    }));
    // Recent orders (the older history is summarised on each seller) — statuses spread over the print pipeline
    const podOrders = [];
    designs.filter((d) => d.status === 'live').forEach((d, i) => {
      const cat = P.catalog.find((c) => c.id === d.catalogId);
      const n = 2 + Math.floor(seeded(`${d.id}n`) * 4);
      for (let k = 0; k < n; k++) {
        const ago = (1 + Math.floor(seeded(`${d.id}a${k}`) * 30)) * DAY;
        const placed = now - ago;
        const days = ago / DAY;
        const status = days < 1.5 ? 'received' : days < 3 ? 'printing' : days < 5 ? 'shipped' : seeded(`${d.id}r${k}`) < 0.08 ? 'refused' : 'delivered';
        const size = cat.sizes[Math.floor(seeded(`${d.id}s${k}`) * Math.min(4, cat.sizes.length))];
        const base = cat.base;
        podOrders.push({ id: `PO-${String(5100 + podOrders.length)}`, orderId: `ORD-${String(88000 + podOrders.length * 3)}`, designId: d.id, sellerId: d.sellerId, catalogId: d.catalogId,
          title: d.title, color: d.colors[k % d.colors.length], size, qty: 1, price: d.price, base, profit: d.price - base, partnerCost: cat.partnerCost,
          customer: { id: `c-pod-${i}-${k}`, name: people[(i * 5 + k * 7) % people.length], city: ['Cairo', 'Giza', 'Alexandria', 'Mansoura'][k % 4] },
          status, history: [{ status: 'received', at: placed }], placedAt: placed, deliveredAt: status === 'delivered' ? placed + 4 * DAY : 0, loss: status === 'refused' ? cat.partnerCost : 0,
          ...(status === 'shipped' || status === 'delivered' || status === 'refused' ? { courier: 'Bosta', tracking: `BST-${40000 + podOrders.length * 37}` } : {}), createdAt: placed, updatedAt: placed, seed: true });
        if (status === 'delivered') d.sales += 1;
      }
    });
    podOrders.sort((a, b) => b.placedAt - a.placedAt);
    const reports = [{ id: 'rp-seed-1', designId: 'ds-2', sellerId: 'sl-koora', reason: 'trademark', detail: 'The slogan layout looks like a sportswear brand campaign.', contact: '', status: 'open', createdAt: now - 7 * 36e5, updatedAt: now - 7 * 36e5 }];
    return { sellers, designs, podOrders, podCatalog: structuredClone(P.catalog), podCategories: structuredClone(P.categories), reports, sellerPayouts: [], podCustomers: {} };
  };
  const now0 = () => Date.now();
  /** A few recent notifications so the demo sellers' bells are not empty */
  const seedNotifications = (now) => {
    const H = 36e5;
    const n = (to, cat, title, body, link, ago, read = false, tone = '') => ({ id: `nt-seed-${to}-${ago}`, to, cat, title, body, link, icon: { designs: 'image', orders: 'package', money: 'wallet', level: 'trend', store: 'bag', account: 'shield' }[cat], tone, at: now - ago * H, read });
    return [
      n('acc-s1', 'orders', 'New order: Safe Hands No.1', 'Hoodie · Black · L · the print partner starts printing today.', '#/dash/seller/orders', 3),
      n('acc-s1', 'money', '420 EGP is ready to withdraw', 'Profit from 3 delivered items has cleared the 7-day hold.', '#/dash/seller/wallet', 20, false, 'turf'),
      n('acc-s1', 'orders', 'Delivered: Cairo Nights FC', 'Profit is released 7 days after delivery.', '#/dash/seller/orders', 30, true),
      n('acc-s1', 'level', 'You reached Elite!', 'New perks: unlimited active designs, 35 EGP off every base price, 8% store commission.', '#/dash/seller', 24 * 9, true, 'gold'),
      n('acc-s2', 'designs', 'Ya Habibi Cap is in review', 'Pro sellers are reviewed first — usually within a few hours.', '#/dash/seller/designs', 6),
      n('acc-s2', 'orders', 'New order: Giza Run Club', 'Sand · M', '#/dash/seller/orders', 11),
      n('acc-s3', 'designs', 'Press Start Hoodie is in review', 'We review every design, usually within 24 hours.', '#/dash/seller/designs', 18),
      n('acc-s3', 'level', '31 more sales to Pro', 'Keep going — Pro unlocks priority review and team kits.', '#/dash/seller', 24 * 2, true, 'gold'),
    ];
  };
  const POLICIES = ['standard', 'flexible', 'moderate', 'strict'];
  const migrate = () => {
    const { seeded } = SA.core.utils;
    let changed = false;
    const ensure = (k, make) => { if (data[k] === undefined) { data[k] = make(); changed = true; } };
    ensure('reviews', () => seedReviews(data.pitches, data.accounts));
    ensure('series', () => []);
    ensure('promos', () => []);
    ensure('saleOverrides', () => ({}));
    ensure('waitlist', () => []);
    ensure('tournaments', () => []);
    ensure('codes', () => {
      const t = Date.now(), d = (n) => SA.core.utils.isoDate(new Date(t + n * DAY));
      const base = { status: 'active', usedBy: [], perCustomer: 1, createdAt: t - 9 * DAY, updatedAt: t - 9 * DAY, note: '' };
      return [
        { ...base, id: 'cd-turf22', code: 'TURF22', kind: 'pitch', ownerId: 'acc-o1', storeId: null, pitchIds: [], type: 'percent', value: 15, min: 0, maxUses: 200, from: d(-9), to: d(40), uses: 37, discountGiven: 4180, revenue: 23690 },
        { ...base, id: 'cd-bibs', code: 'TURFKIT10', kind: 'store', ownerId: 'acc-o1', storeId: 'st-turf22', pitchIds: [], type: 'percent', value: 10, min: 400, maxUses: 0, from: d(-9), to: '', uses: 6, discountGiven: 312, revenue: 2808 },
        { ...base, id: 'cd-gezira', code: 'GEZIRA100', kind: 'pitch', ownerId: 'acc-o2', storeId: null, pitchIds: ['p-gezira-fives'], type: 'fixed', value: 100, min: 800, maxUses: 100, from: d(-4), to: d(25), uses: 12, discountGiven: 1200, revenue: 9600 },
      ];
    });
    // Seller layer (release 4): sellers, print-on-demand catalogue/designs/orders, copyright reports, seller payouts
    if (!data.sellers) { Object.assign(data, seedSellers()); changed = true; }
    if (!data.settings.pod) { data.settings.pod = { holdDays: 7, minWithdraw: 500, refuseLimit: 2 }; changed = true; }
    if (!data.settings.sellerLevels) { data.settings.sellerLevels = structuredClone(SA.data.pod.levels); changed = true; }
    data.settings.sellerLevels.forEach((l) => { if (l.id === 'new' && l.name === 'New') { l.name = 'Starter'; changed = true; } });
    ensure('notifications', () => seedNotifications(now0()));
    // Admin console (release 5): Fantasy Pass sales ledger, refunds/adjustments, platform promo uses, support, content, samples, level reviews
    ['passSales', 'refunds', 'adjustments', 'promoUses', 'broadcasts', 'samples', 'levelReviews'].forEach((k) => ensure(k, () => []));
    if (!data.codes.some((c) => c.kind === 'platform')) {
      const t = Date.now(), d = (n) => SA.core.utils.isoDate(new Date(t + n * DAY));
      const base = { kind: 'platform', ownerId: 'acc-admin', storeId: null, pitchIds: [], min: 0, maxUses: 0, perCustomer: 0, from: d(-60), to: '', status: 'active', usedBy: [], createdAt: t - 60 * DAY, updatedAt: t - 60 * DAY };
      data.codes.push(
        { ...base, id: 'cd-kickoff10', code: 'KICKOFF10', scope: 'shop', type: 'percent', value: 10, label: '10% off your order', note: 'Welcome code', uses: 412, discountGiven: 51870, revenue: 466830 },
        { ...base, id: 'cd-pitch50', code: 'PITCH50', scope: 'shop', type: 'fixed', value: 50, min: 500, label: '50 EGP off', note: '', uses: 188, discountGiven: 9400, revenue: 151200 },
        { ...base, id: 'cd-freeship', code: 'FREESHIP', scope: 'shop', type: 'shipping', value: 0, label: 'Free delivery', note: '', uses: 265, discountGiven: 15900, revenue: 198750 },
        { ...base, id: 'cd-firstgame', code: 'FIRSTGAME', scope: 'bookings', type: 'percent', value: 15, label: '15% off your first booking', perCustomer: 1, note: 'New players — Super App pays, venues get the full price', uses: 96, discountGiven: 9120, revenue: 51680, from: d(-20) },
      );
      changed = true;
    }
    ensure('banners', () => [
      { id: 'bn-boots', title: 'Up to 20% off turf and FG boots', badge: 'Boots week', cta: 'Shop boots', link: '#/shop?cat=boots', image: 'photo-1529900748604-07564a03e7a6', active: true, order: 1, from: '', to: '' },
      { id: 'bn-mornings', title: 'Pitches from 500 EGP an hour before 4 PM', badge: 'Weekday mornings', cta: '', link: '#/pitches?sort=price', image: 'nightGrass', active: true, order: 2, from: '', to: '' },
      { id: 'bn-coaching', title: 'Private coaching with CAF-licensed coaches', badge: 'New', cta: '', link: '#/shop?cat=services', image: 'kidsTraining', active: true, order: 3, from: '', to: '' },
    ]);
    ensure('newsPosts', () => []);
    // Fantasy sponsors (fictional demo companies) and announced winners
    ensure('sponsors', () => [
      { id: 'sp-nilefuel', name: 'Nile Fuel Drinks', tier: 'gold', contact: 'partnerships@nilefuel.example', value: 60000, from: SA.core.utils.isoDate(new Date(Date.now() - 40 * DAY)), to: SA.core.utils.isoDate(new Date(Date.now() + 140 * DAY)), color: '#0B6E4F', note: 'Grand League prizes and the derby trip' },
      { id: 'sp-deltakits', name: 'Delta Kits', tier: 'silver', contact: 'hello@deltakits.example', value: 25000, from: SA.core.utils.isoDate(new Date(Date.now() - 20 * DAY)), to: SA.core.utils.isoDate(new Date(Date.now() + 70 * DAY)), color: '#2459D6', note: 'Signed shirts every gameweek' },
    ]);
    ensure('fantasyWinners', () => []);
    if (!data.settings.fantasyLeagues) { data.settings.fantasyLeagues = { 'ol-grand': { sponsorId: 'sp-nilefuel' }, 'ol-derby': { sponsorId: 'sp-nilefuel' }, 'ol-weekly': { sponsorId: 'sp-deltakits' } }; changed = true; }
    if (!data.settings.announcement) { data.settings.announcement = { on: false, text: '', link: '', cta: '', tone: 'info', until: '', v: 0 }; changed = true; }
    if (!data.settings.adminSeed) {
      // A few withdrawal requests so the admin console has something to approve (demo)
      const H = 36e5, t = Date.now();
      data.payouts.push(
        { id: 'PO-SEED-1', accountId: 'acc-o1', amount: 4200, method: 'Bank transfer', detail: 'NBE •••• 7731', status: 'processing', createdAt: t - 5 * H, updatedAt: t - 5 * H },
        { id: 'PO-SEED-2', accountId: 'acc-o3', amount: 2750, method: 'Bank transfer', detail: 'QNB •••• 1180', status: 'processing', createdAt: t - 26 * H, updatedAt: t - 26 * H },
        { id: 'PO-SEED-3', accountId: 'acc-o2', amount: 6100, method: 'InstaPay', detail: 'hazem.tarek@instapay', status: 'paid', paidAt: t - 8 * 24 * H, ref: 'TRX-8KQ2LM', createdAt: t - 9 * 24 * H, updatedAt: t - 8 * 24 * H },
      );
      if (data.sellerPayouts && data.sellers?.some((s) => s.id === 'sl-koora')) data.sellerPayouts.push({ id: 'sp-seed-1', sellerId: 'sl-koora', amount: 500, method: 'InstaPay', detail: 'koora-ink@instapay', status: 'processing', createdAt: t - 3 * H, updatedAt: t - 3 * H });
      data.settings.adminSeed = 1; changed = true;
    }
    if (!data.settings.fantasySubs) { data.settings.fantasySubs = { one: 50, both: 80 }; changed = true; }
    if (!data.settings.loyalty) { data.settings.loyalty = { earnPer: 10, pointValue: 0.2, minRedeem: 500 }; changed = true; }
    if (data.settings.vat == null) { data.settings.vat = 0.14; changed = true; }
    data.pitches.forEach((p) => {
      if (!p.cancelPolicy) { const r = seeded(`${p.id}|policy`); p.cancelPolicy = p.seed ? POLICIES[r < 0.45 ? 0 : r < 0.65 ? 1 : r < 0.88 ? 2 : 3] : 'standard'; changed = true; }
      if (!p.weekly) { p.weekly = { enabled: true, discount: 0.1 }; changed = true; }
    });
    data.accounts.forEach((a) => { if (!a.taxId) { const r = seeded(`${a.id}|tax`); a.taxId = `${String(100 + Math.floor(r * 899))}-${String(100 + Math.floor(seeded(a.id + 'b') * 899))}-${String(100 + Math.floor(seeded(a.id + 'c') * 899))}`; changed = true; } });
    data.products.forEach((p) => { if (p.lowStockAt == null) { p.lowStockAt = 5; changed = true; } });
    if (changed) persist();
  };

  const seed = () => {
    const now = Date.now();
    const since = now - 120 * DAY; // the platform has history for ~4 months
    const accounts = [
      { id: 'acc-admin', role: 'admin', name: 'Super App HQ', company: 'Super App Egypt', email: 'admin@superapp.example', phone: '+20 2 2735 0000', avatar: 7, joinedAt: since, payout: { method: 'Bank transfer', detail: 'CIB •••• 0042' } },
      { id: 'acc-o1', role: 'owner', name: 'Karim Adel', company: 'Turf 22 Group', email: 'karim@turf22.example', phone: '+20 100 555 2201', avatar: 0, joinedAt: since, payout: { method: 'Bank transfer', detail: 'NBE •••• 7731' } },
      { id: 'acc-o2', role: 'owner', name: 'Hazem Tarek', company: 'Gezira Sports Co.', email: 'hazem@gezira.example', phone: '+20 111 444 9020', avatar: 5, joinedAt: since, payout: { method: 'InstaPay', detail: 'hazem.tarek@instapay' } },
      { id: 'acc-o3', role: 'owner', name: 'Mariam Fathy', company: 'Maadi Sports Ventures', email: 'mariam@maadisports.example', phone: '+20 122 310 4488', avatar: 10, joinedAt: since, payout: { method: 'Bank transfer', detail: 'QNB •••• 1180' } },
      { id: 'acc-o4', role: 'owner', name: 'Ali Reda', company: 'Coastline Sports', email: 'ali@coastline.example', phone: '+20 155 002 7714', avatar: 3, joinedAt: since, payout: { method: 'InstaPay', detail: 'ali.reda@instapay' } },
      { id: 'acc-new', role: 'owner', name: 'Youssef Adel', company: 'New partner', email: 'youssef.adel@example.com', phone: '+20 100 482 7713', avatar: 1, joinedAt: now - 2 * DAY, payout: null },
    ];
    const pitches = SA.data.pitches.list.map((p) => ({
      ...structuredClone(p), ownerId: ownerOf(p.id), status: 'active', closedReason: '', seed: true, createdAt: since, updatedAt: since,
      seedSnapshot: { price: p.price, peakPrice: p.peakPrice, open: p.open.slice() },
    }));
    const stores = [
      { id: 'st-official', ownerId: 'acc-admin', name: 'Super App Store', slug: 'super-app-store', tagline: 'Official gear, delivered across Egypt', description: 'The official Super App shop: original boots, balls, kits and training gear.', logo: null, cover: 'photo-1579952363873-27f3bade9f55', phone: '+20 2 2735 0000', status: 'active', official: true, createdAt: since },
      { id: 'st-turf22', ownerId: 'acc-o1', name: 'Turf 22 Pro Shop', slug: 'turf-22-pro-shop', tagline: 'Matchday kit from the Turf 22 team', description: 'Bibs, balls and grip socks we use on our own pitches every night.', logo: null, cover: 'photo-1551958219-acbc608c6377', phone: '+20 100 555 2201', status: 'active', createdAt: since + 20 * DAY },
    ];
    const products = [
      ...SA.data.products.list.map((p) => ({ ...structuredClone(p), storeId: 'st-official', status: 'active', seed: true, createdAt: since, updatedAt: since })),
      { id: 'tp-bibs', storeId: 'st-turf22', cat: 'training', brand: 'Turf 22', name: 'Training Bibs — Set of 10', price: 450, was: 520, rating: 4.6, reviews: 38, images: ['photo-1552066379-e7bfd22155c5'], colors: [{ name: 'Fluo Yellow', hex: '#F6D33C' }], sizes: ['One size'], stock: 40, badges: [], desc: 'Lightweight mesh bibs, numbered 1–10. The same set our staff hand out before every game.', specs: [['Includes', '10 numbered bibs'], ['Material', 'Polyester mesh']], status: 'active', seed: true, createdAt: since + 21 * DAY, updatedAt: since + 21 * DAY },
      { id: 'tp-ball', storeId: 'st-turf22', cat: 'balls', brand: 'Turf 22', name: 'Turf 22 Match Ball — Size 5', price: 690, was: 690, rating: 4.7, reviews: 52, images: ['photo-1575361204480-aadea25e6e68'], colors: [{ name: 'White / Black', hex: '#F4F4F4' }], sizes: ['5'], stock: 25, badges: ['Venue favourite'], desc: 'The ball used on every Turf 22 pitch. Built for artificial turf.', specs: [['Size', '5'], ['Cover', 'PU']], status: 'active', seed: true, createdAt: since + 21 * DAY, updatedAt: since + 21 * DAY },
      { id: 'tp-socks', storeId: 'st-turf22', cat: 'accessories', brand: 'Turf 22', name: 'Turf 22 Grip Socks', price: 260, was: 300, rating: 4.5, reviews: 21, images: ['photo-1774414048830-bff390f869a4'], colors: [{ name: 'White', hex: '#FAFAFA' }], sizes: ['S', 'M', 'L'], stock: 3, badges: [], desc: 'Anti-slip grip socks in Turf 22 colours.', specs: [['Material', 'Cotton blend, silicone grip']], status: 'active', seed: true, createdAt: since + 21 * DAY, updatedAt: since + 21 * DAY },
    ];
    return {
      version: VERSION, createdAt: now, platformSince: since,
      settings: { commission: { bookings: 0.10, store: 0.12 }, serviceFee: 0.03, settlementDay: 1 },
      accounts, stores, pitches, products,
      bookings: [], bookingOverrides: {}, closures: [], sales: [], payouts: [],
      activity: [{ id: 'act-seed', at: since, actor: 'acc-admin', action: 'platform.launch', text: 'Super App partner platform launched' }],
    };
  };

  let data;
  const load = () => {
    try { const raw = JSON.parse(localStorage.getItem(KEY)); if (raw && raw.version === VERSION) return raw; } catch { /* corrupted → reseed */ }
    return null;
  };
  const subs = new Set();
  const persist = () => {
    try { localStorage.setItem(KEY, JSON.stringify(data)); return true; }
    catch (e) { console.warn('DB persist failed', e); SA.ui?.overlay?.toast('Storage is full on this device — use smaller images or reset the demo data.', { type: 'error', duration: 6000 }); return false; }
  };
  const emit = (col) => subs.forEach((fn) => { try { fn(col); } catch (e) { console.error(e); } });

  const uid = (p) => `${p}-${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;

  const db = {
    init() {
      data = load();
      if (!data) {
        data = seed();
        // Bring bookings the shopper already made on this device into the platform ledger
        (SA.core.store.get().bookings || []).forEach((b) => data.bookings.push(db._fromShopperBooking(b)));
        persist();
      }
      migrate();
      // Another tab (e.g. the shop in one tab, the dashboard in another) changed the data: pick it up
      window.addEventListener('storage', (e) => { if (e.key !== KEY) return; const next = load(); if (next) { data = next; emit('*'); } });
    },
    _fromShopperBooking(b) {
      const u = SA.core.store.get().user || {};
      const p = data.pitches.find((x) => x.id === b.pitchId);
      return { id: b.id, pitchId: b.pitchId, ownerId: p?.ownerId || 'acc-admin', date: b.date, hour: b.hour, duration: b.duration, amount: b.pitchTotal, fee: b.serviceFee,
        customer: { id: u.id || 'u-001', name: u.name || 'Customer', phone: u.phone || '' }, source: 'app', payment: b.payment?.method === 'wallet' ? 'Wallet' : 'Card',
        status: b.status === 'cancelled' ? 'cancelled' : 'confirmed', refund: b.refund || 0, createdAt: b.createdAt || Date.now(),
        policy: b.policy || p?.cancelPolicy || 'standard', seriesId: b.seriesId || null, sessionNo: b.sessionNo || null, discount: b.discount || 0, promo: b.promo || null };
    },
    subscribe(fn) { subs.add(fn); return () => subs.delete(fn); },
    raw: () => data,
    settings: () => data.settings,
    setSettings(patch) { data.settings = { ...data.settings, ...patch }; persist(); emit('settings'); },
    all: (col) => data[col],
    find: (col, id) => data[col].find((x) => x.id === id),
    insert(col, obj, prefix = col.slice(0, 2)) {
      const rec = { id: obj.id || uid(prefix), createdAt: Date.now(), updatedAt: Date.now(), ...obj };
      data[col].unshift(rec);
      if (!persist()) { data[col].shift(); throw new Error('storage_full'); }
      emit(col);
      return rec;
    },
    update(col, id, patch) {
      const i = data[col].findIndex((x) => x.id === id);
      if (i < 0) return null;
      const before = data[col][i];
      data[col][i] = { ...before, ...patch, updatedAt: Date.now() };
      if (!persist()) { data[col][i] = before; throw new Error('storage_full'); }
      emit(col);
      return data[col][i];
    },
    remove(col, id) { const before = data[col]; data[col] = before.filter((x) => x.id !== id); if (!persist()) { data[col] = before; throw new Error('storage_full'); } emit(col); },
    override(bookingId, patch) { data.bookingOverrides[bookingId] = { ...(data.bookingOverrides[bookingId] || {}), ...patch, at: Date.now() }; persist(); emit('bookings'); },
    log(actor, action, text, ref) { data.activity.unshift({ id: uid('act'), at: Date.now(), actor, action, text, ref }); data.activity = data.activity.slice(0, 1000); persist(); },
    overrideSale(saleId, patch) { data.saleOverrides[saleId] = { ...(data.saleOverrides[saleId] || {}), ...patch, at: Date.now() }; if (!persist()) throw new Error('storage_full'); emit('sales'); },
    /** Save in-place changes to a collection and tell subscribers */
    touch(col) { if (!persist()) throw new Error('storage_full'); emit(col); },
    reset() { localStorage.removeItem(KEY); data = seed(); migrate(); persist(); emit('*'); },
    uid,
  };

  SA.services.db = db;
  db.init();
})(window.SA);
