/**
 * Sellers: any site user can switch on seller mode and sell print-on-demand designs, products from their own store, or both.
 * A seller is linked to a dashboard account (role "seller") so the same Partner Dashboard shell serves them.
 * Levels are earned on lifetime delivered items and a minimum rating; the admin edits the rules in Platform settings.
 * Future API: POST /sellers, GET /sellers/:handle, GET /sellers/me/level
 */
(function (SA) {
  const { ApiError } = SA.services.http;
  const db = () => SA.services.db;
  const DAY = 864e5;

  const HANDLE = /^[a-z0-9](?:[a-z0-9-]{1,28}[a-z0-9])$/;
  const RESERVED = new Set(['admin', 'superapp', 'super-app', 'support', 'shop', 'store', 'official', 'help', 'dash', 'api']);
  const me = () => SA.core.store.get().user || SA.data.user.profile;
  const slug = (s) => String(s || '').toLowerCase().normalize('NFKD').replace(/[^\w\s-]/g, '').trim().replace(/[\s_]+/g, '-').replace(/-+/g, '-').slice(0, 30);

  const levels = () => db().settings().sellerLevels;

  /** Delivered items + rating for a seller (older history is summarised on the record) */
  const stats = (s) => {
    const orders = db().all('podOrders').filter((o) => o.sellerId === s.id && !o.sample);
    const delivered = orders.filter((o) => o.status === 'delivered');
    const delivered0 = (x) => (SA.services.dash ? SA.services.dash.saleState(x).status === 'delivered' : x.fulfil?.status === 'delivered');
    const storeSold = s.storeId ? db().all('sales').filter((x) => x.storeId === s.storeId && delivered0(x)).reduce((t, x) => t + (x.qty || 1), 0) : 0;
    const h = s.history || {};
    const sales = (h.sales || 0) + delivered.reduce((t, o) => t + o.qty, 0) + storeSold;
    return { sales, rating: h.rating || 0, reviews: h.reviews || 0, orders: orders.length, refused: orders.filter((o) => o.status === 'refused').length };
  };

  /** Current level, next level and what is missing to reach it (memoised until the data changes) */
  const memo = new Map();
  let watching = false;
  const levelOf = (s) => {
    if (!watching) { watching = true; db().subscribe(() => memo.clear()); }
    const key = `${s.id}|${s.updatedAt}|${s.frozenUntil || 0}|${s.levelKept ?? ''}`;
    if (!memo.has(key)) memo.set(key, computeLevel(s));
    return memo.get(key);
  };
  const computeLevel = (s) => {
    const L = levels();
    const st = stats(s);
    const frozen = (s.frozenUntil || 0) > Date.now();
    let idx = 0;
    L.forEach((l, i) => { if (st.sales >= l.sales && (i === 0 || st.rating >= l.rating)) idx = i; });
    const earned = idx;
    // Levels only go down at the monthly review, after a grace month: until then the seller keeps the reviewed level
    const kept = s.levelKept != null && idx < s.levelKept && s.levelKept < L.length;
    if (kept) idx = s.levelKept;
    // A copyright strike freezes the level: no promotion until it expires
    if (frozen && s.level != null && idx > s.level) idx = s.level;
    const next = L[idx + 1] || null;
    return {
      ...L[idx], index: idx, earned, grace: kept, graceSince: kept ? s.grace?.since || '' : '', stats: st, frozen, frozenUntil: s.frozenUntil || 0, next,
      toNext: next ? Math.max(0, next.sales - st.sales) : 0,
      ratingOk: next ? st.rating >= next.rating : true,
      progress: next ? Math.min(1, (st.sales - L[idx].sales) / Math.max(1, next.sales - L[idx].sales)) : 1,
    };
  };

  const activeDesigns = (s) => db().all('designs').filter((d) => d.sellerId === s.id && ['live', 'review', 'paused'].includes(d.status)).length;

  const sellers = {
    HANDLE, slug, levels, stats, levelOf,
    list: () => db().all('sellers'),
    get: (id) => db().find('sellers', id),
    byHandle: (h) => db().all('sellers').find((s) => s.handle === String(h || '').toLowerCase()),
    byAccount: (accountId) => db().all('sellers').find((s) => s.accountId === accountId),
    /** The seller profile of the shopper on this device, if seller mode is on */
    mine: () => db().all('sellers').find((s) => s.userId === me().id) || null,
    /** The seller being worked on: the dashboard's signed-in seller account, else this shopper's own seller profile */
    current() {
      const a = SA.services.dash?.me();
      return (a && sellers.byAccount(a.id)) || sellers.mine();
    },
    /** Open the seller dashboard as this shopper's seller account */
    openDashboard() { const s = sellers.mine(); if (s) SA.services.dash.auth.signIn(s.accountId, { as: 'seller' }); },
    /** Identity check before the first withdrawal (reviewed by the Super App team) */
    submitKyc(id, { fullName, nationalId, front, back }) {
      if (String(fullName || '').trim().split(/\s+/).length < 2) throw new ApiError('Enter your full name as it appears on your ID.', 'validation');
      if (!/^\d{14}$/.test(String(nationalId || ''))) throw new ApiError('The national ID number has 14 digits.', 'validation');
      if (!front || !back) throw new ApiError('Add photos of the front and back of your ID.', 'validation');
      const s = db().update('sellers', id, { kyc: 'pending', kycInfo: { fullName: String(fullName).trim(), idLast4: String(nationalId).slice(-4), submittedAt: Date.now() } });
      db().log(s.accountId, 'seller.kyc', `${s.name} sent ID documents for verification`, id);
      SA.services.notify?.toAdmins({ cat: 'account', title: `ID check: ${s.name}`, body: 'Verify before their first withdrawal.', link: '#/admin/designs?tab=sellers' });
      SA.services.notify?.toSeller(id, { cat: 'account', title: 'ID documents received', body: 'We’re checking them — usually within one working day.', link: '#/dash/seller/wallet' });
      return s;
    },
    verifyKyc(id, ok = true) {
      const s = db().update('sellers', id, { kyc: ok ? 'verified' : 'rejected' });
      db().log('acc-admin', 'seller.kyc', `${s.name} ID ${ok ? 'verified' : 'rejected'}`, id);
      SA.services.notify?.toSeller(id, ok
        ? { cat: 'account', tone: 'turf', title: 'Your ID is verified', body: 'Withdrawals are unlocked.', link: '#/dash/seller/wallet' }
        : { cat: 'account', tone: 'danger', title: 'We couldn’t verify your ID', body: 'The photos weren’t clear or didn’t match. Please send them again.', link: '#/dash/seller/wallet' });
      return s;
    },

    /** Store tags: up to 5, lower case, letters/numbers (Arabic too) */
    cleanTags: (v) => [...new Set((Array.isArray(v) ? v : String(v || '').split(/[,،#\n]/)).map((t) => String(t).trim().toLowerCase().replace(/\s+/g, '-')).filter((t) => t.length >= 2 && t.length <= 24))].slice(0, 5),
    handleFree(h) { h = String(h || '').toLowerCase(); return HANDLE.test(h) && !RESERVED.has(h) && !sellers.byHandle(h) && !db().all('stores').some((x) => x.slug === h); },

    /** A free store link made from the store name (the seller can change it later in Store settings) */
    freeHandle(name) { const base = (sellers.slug(name) || 'store').slice(0, 26).replace(/-+$/, '') || 'store'; let h = base.length >= 3 ? base : `${base}-store`; let i = 2; while (!sellers.handleFree(h)) h = `${base}-${i++}`; return h; },
    /**
     * Apply to sell: mode = pod | store | both. The application asks for the seller's ID and contact details; the
     * store link is made from the store name. `accountId` = a pitch partner applying from their dashboard.
     */
    start({ mode, name, handle = '', bio = '', payoutMethod = 'InstaPay', payoutDetail = '', agree, fullName = '', phone = '', nationalId = '', idPhoto = '', accountId: ownerId = '', requireId = false }) {
      const u = me();
      const owner = ownerId ? db().find('accounts', ownerId) : null;
      if (ownerId && (!owner || owner.role !== 'owner')) throw new ApiError('Only pitch partners can apply from their dashboard.', 'forbidden');
      if (owner ? sellers.byAccount(owner.id) : sellers.mine()) throw new ApiError('Seller mode is already on for this account.', 'exists');
      if (!['pod', 'store', 'both'].includes(mode)) throw new ApiError('Choose how you want to sell.', 'validation');
      name = String(name || '').trim();
      if (name.length < 2 || name.length > 40) throw new ApiError('Store name must be 2–40 characters.', 'validation', { name: '2–40 characters' });
      const e = {};
      if (requireId) {
        if (String(fullName).trim().split(/\s+/).length < 2) e.fullName = 'Your full name, as on your ID.';
        if (!/^\+?[\d\s]{10,16}$/.test(String(phone).trim())) e.phone = 'A mobile number, e.g. 010 1234 5678.';
        if (!/^\d{14}$/.test(String(nationalId).trim())) e.nationalId = 'The national ID number has 14 digits.';
        if (!idPhoto) e.idPhoto = 'Add a photo of your ID.';
        if (String(payoutDetail).trim().length < 4) e.payoutDetail = 'Where we send your money.';
      }
      if (!agree) e.agree = 'Please accept the seller rules.';
      if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'validation', e);
      handle = String(handle || '').trim().toLowerCase() || sellers.freeHandle(name);
      if (!HANDLE.test(handle)) throw new ApiError('Link name: 3–30 letters, numbers or dashes.', 'validation');
      if (!sellers.handleFree(handle)) throw new ApiError('That link is taken — try another.', 'taken');
      const now = Date.now();
      // When the admin switched on "approve new sellers", the store and designs go live after approval
      const pending = !!db().settings().sellerApproval;
      const accountId = owner ? owner.id : `acc-sl-${handle}`;
      if (!owner) db().insert('accounts', { id: accountId, role: 'seller', name: String(fullName).trim() || u.name, company: name, email: u.email, phone: String(phone).trim() || u.phone, avatar: u.avatar || 0, joinedAt: now, payout: payoutDetail ? { method: payoutMethod, detail: payoutDetail } : null });
      let storeId = null;
      if (mode !== 'pod') {
        storeId = `st-${handle}`;
        db().insert('stores', { id: storeId, ownerId: accountId, name, slug: handle, tagline: '', description: bio, logo: null, cover: null, phone: u.phone, status: pending ? 'suspended' : 'active' });
      }
      const s = db().insert('sellers', { id: `sl-${handle}`, accountId, userId: owner ? null : u.id, handle, name, bio: String(bio).slice(0, 280), cover: null, logo: null, avatar: u.avatar || 0, mode, storeId,
        status: pending ? 'pending' : 'active', strikes: [], frozenUntil: 0, followers: 0, history: { sales: 0, revenue: 0, profit: 0, rating: 0, reviews: 0 },
        ...(nationalId ? { kyc: 'pending', kycInfo: { fullName: String(fullName).trim(), idLast4: String(nationalId).trim().slice(-4), photo: idPhoto, submittedAt: now } } : { kyc: 'none' }),
        payout: payoutDetail ? { method: payoutMethod, detail: String(payoutDetail).trim() } : null });
      if (pending) {
        SA.services.notify?.toAdmins({ cat: 'account', title: `New seller to approve: ${name}`, body: `@${handle} · ${mode === 'pod' ? 'print on demand' : mode === 'store' ? 'own store' : 'both'}`, link: '#/admin/approvals?tab=sellers' });
        SA.services.notify?.toAccount(accountId, { cat: 'account', title: 'Your application is being reviewed', body: 'The Super App team checks new sellers within one working day. You can prepare designs and products meanwhile.', link: '#/dash/seller' });
        if (!owner) SA.services.user.notify({ kind: 'account', title: 'We got your seller application', body: `“${name}” — we review it within one working day. You can prepare designs meanwhile.`, link: '/sell' });
      }
      db().log(accountId, 'seller.started', `${name} started selling (${mode === 'pod' ? 'print on demand' : mode === 'store' ? 'own store' : 'print on demand + own store'})`, s.id);
      SA.services.notify?.toAccount(accountId, { cat: 'account', tone: 'turf', title: `Welcome to Super App, ${name}!`, body: mode === 'store' ? 'Add your first products, then share your store link.' : 'Create your first design in the studio — it takes a few minutes.', link: mode === 'store' ? '#/dash/store' : '#/sell/studio' });
      SA.services.notify?.toAccount(accountId, { cat: 'store', title: 'Make your store yours', body: 'Add a logo, a cover photo and a short description in Store settings.', link: '#/dash/seller/store' });
      return s;
    },

    update(id, patch) {
      const s = sellers.get(id); if (!s) throw new ApiError('Seller not found.', 'not_found');
      if (patch.tags != null) patch = { ...patch, tags: sellers.cleanTags(patch.tags) };
      const allowed = ['name', 'bio', 'tags', 'cover', 'logo', 'mode', 'handle', 'accent', 'featured', 'vacation', 'vacationNote', 'vacationUntil'];
      const p = Object.fromEntries(Object.entries(patch).filter(([k]) => allowed.includes(k)));
      if (p.name != null) { p.name = String(p.name).trim(); if (p.name.length < 2 || p.name.length > 40) throw new ApiError('Store name must be 2–40 characters.', 'validation'); }
      if (p.bio != null) p.bio = String(p.bio).trim().slice(0, 280);
      if (p.tagline != null) p.tagline = String(p.tagline).trim().slice(0, 60);
      if (p.accent != null && p.accent !== '' && !/^#[0-9a-f]{6}$/i.test(p.accent)) throw new ApiError('Pick a store colour.', 'validation');
      if (p.featured && db().find('designs', p.featured)?.sellerId !== id) throw new ApiError('Choose one of your own live designs.', 'validation');
      if (p.handle != null && p.handle !== s.handle) {
        // Changing the link: same rules as when the store was opened; the old link stops working
        p.handle = String(p.handle).trim().toLowerCase();
        if (!HANDLE.test(p.handle)) throw new ApiError('Link name: 3–30 letters, numbers or dashes.', 'validation');
        if (!sellers.handleFree(p.handle)) throw new ApiError('That link is taken — try another.', 'taken');
        if (s.storeId) db().update('stores', s.storeId, { slug: p.handle });
      } else delete p.handle;
      if (p.vacation === false) { p.vacationNote = ''; p.vacationUntil = ''; }
      if (p.mode && p.mode !== 'pod' && !s.storeId) {
        // Turning on the own-store side later creates the store
        const storeId = `st-${s.handle}`;
        db().insert('stores', { id: storeId, ownerId: s.accountId, name: p.name || s.name, slug: s.handle, tagline: '', description: s.bio, logo: null, cover: null, phone: '', status: 'active' });
        p.storeId = storeId;
      }
      // Keep the own-store record in step (shop "Sold by", dashboard store pages)
      if (s.storeId) {
        const sp = {};
        if (p.name) sp.name = p.name;
        if (p.bio != null) sp.description = p.bio;
        if (p.tagline != null) sp.tagline = p.tagline;
        if (p.logo !== undefined) sp.logo = p.logo;
        if (p.cover !== undefined) sp.cover = p.cover;
        if (Object.keys(sp).length) db().update('stores', s.storeId, sp);
      }
      if (p.name && p.name !== s.name) db().update('accounts', s.accountId, { company: p.name });
      const out = db().update('sellers', id, p);
      db().log(s.accountId, 'seller.store', `${out.name} updated the store page`, id);
      return out;
    },
    /** Where withdrawals are paid */
    setPayout(id, { method, detail }) {
      const s = sellers.get(id); if (!s) throw new ApiError('Seller not found.', 'not_found');
      if (!['InstaPay', 'Vodafone Cash', 'Bank transfer'].includes(method)) throw new ApiError('Choose how you want to be paid.', 'validation');
      detail = String(detail || '').trim();
      if (detail.length < 5) throw new ApiError('Add the account you want to be paid to.', 'validation');
      return db().update('accounts', s.accountId, { payout: { method, detail } });
    },
    /** A seller on a break: designs and products are hidden from the shop until they come back */
    onBreak: (s) => !!s?.vacation && (!s.vacationUntil || s.vacationUntil >= SA.core.utils.isoDate(SA.core.utils.today())),

    /** Limits that depend on the level */
    perks: (s) => levelOf(s),
    canAddDesign(s) {
      const l = levelOf(s);
      const used = activeDesigns(s);
      return { ok: l.designs == null || used < l.designs, used, limit: l.designs };
    },
    activeDesigns,

    /** Copyright strike: level frozen for 90 days; three active strikes suspend the seller */
    strike(id, reason, ref) {
      const s = sellers.get(id); if (!s) return null;
      const now = Date.now();
      const strikes = [...(s.strikes || []), { at: now, reason, ref }];
      const active = strikes.filter((x) => now - x.at < 90 * DAY);
      const patch = { strikes, frozenUntil: now + 90 * DAY, level: levelOf(s).index, status: active.length >= 3 ? 'suspended' : s.status };
      db().log('acc-admin', 'seller.strike', `Copyright strike for ${s.name}: ${reason}`, id);
      const out = db().update('sellers', id, { ...patch, unfrozenNoticed: false });
      SA.services.notify?.toSeller(id, { cat: 'account', tone: 'danger', title: `Copyright strike ${active.length} of 3`, body: `${reason} Your level is frozen for 90 days.${active.length >= 3 ? ' Your account is suspended.' : ' Three active strikes suspend the account.'}`, link: '#/dash/seller/designs' });
      return out;
    },
    setStatus(id, status, reason = '') {
      if (!['active', 'suspended'].includes(status)) throw new ApiError('Unknown status.', 'validation');
      const s = db().update('sellers', id, { status });
      // A suspended seller's own store leaves the shop too
      if (s.storeId && db().find('stores', s.storeId)) db().update('stores', s.storeId, { status: status === 'active' ? 'active' : 'suspended' });
      db().log('acc-admin', `seller.${status}`, `${s.name} ${status === 'active' ? 'reactivated' : 'suspended'}`, id);
      SA.services.notify?.toSeller(id, status === 'active'
        ? { cat: 'account', tone: 'turf', title: 'Your store is active again', body: 'Your designs and products are back in the shop.', link: '#/dash/seller' }
        : { cat: 'account', tone: 'danger', title: 'Your store is suspended', body: `${reason ? `Reason: ${reason}. ` : ''}Your designs and products are hidden from the shop. Contact Super App support to appeal.`, link: '#/dash/support' });
      return s;
    },

    follow(id) {
      const st = SA.core.store;
      const set = new Set(st.get().following || []);
      const s = sellers.get(id); if (!s) return false;
      const on = !set.has(id);
      on ? set.add(id) : set.delete(id);
      st.set({ following: [...set] });
      db().update('sellers', id, { followers: Math.max(0, (s.followers || 0) + (on ? 1 : -1)) });
      return on;
    },
    isFollowing: (id) => (SA.core.store.get().following || []).includes(id),
  };

  SA.services.sellers = sellers;
})(window.SA);
