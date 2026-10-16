/**
 * Site content managed from the admin console: home banners, the announcement bar, Super App news posts (plus hide /
 * breaking flags on feed stories) and hidden pickup games. The site reads it here; changes go through SA.services.admin.
 * Future API: GET /content/banners, GET /content/announcement, GET /news (merged), /admin/content/*
 */
(function (SA) {
  const db = () => SA.services.db;
  const { isoDate, today } = SA.core.utils;
  const settings = () => db().settings();

  /** shape: cube (icons on six faces) · coin (badge text on a coin) · tee (the 3D T-shirt) */
  const PROMO_DEFAULTS = [
    { id: 'pp-seller', active: true, shape: 'tee', color: '#E8590C', badge: 'No stock, no cost', title: 'Join as a seller and earn', text: 'Upload your designs — we print, ship and pay you for every sale.', cta: 'Start selling', link: '#/sell', forNonSellers: true },
    { id: 'pp-fantasy', active: true, shape: 'cube', icons: ['trophy', 'ball', 'star', 'users'], color: '#0B6E4F', badge: 'Free to play', title: 'Play Egyptian League fantasy', text: 'Pick your 15, join a league with your friends and win prizes every gameweek.', cta: 'Pick my team', link: '#/fantasy' },
    { id: 'pp-kickoff', active: true, shape: 'coin', color: '#C2410C', badge: '10%', title: '10% off your first order', text: 'Use code KICKOFF10 at checkout in the Super App shop.', cta: 'Shop now', link: '#/shop' },
    { id: 'pp-pitches', active: true, shape: 'cube', icons: ['pitch', 'clock', 'calendar', 'ball'], color: '#15803D', badge: 'Before 4 PM', title: 'Pitches from 500 EGP an hour', text: 'Weekday mornings and afternoons cost less. See the free hours near you.', cta: 'Book a pitch', link: '#/pitches?sort=price' },
    { id: 'pp-shop', active: true, shape: 'cube', icons: ['bag', 'boot', 'shirt', 'ball'], color: '#1D4ED8', badge: 'Delivered across Egypt', title: 'Boots, kits and printed designs', text: 'Football gear and original designs from Egyptian creators in one shop.', cta: 'Open the shop', link: '#/shop' },
    { id: 'pp-weekly', active: true, shape: 'coin', color: '#0F766E', badge: '−10%', title: 'Same pitch every week? Save 10%', text: 'Book a weekly package and lock your hour for the whole month.', cta: 'See pitches', link: '#/pitches' },
    { id: 'pp-tournaments', active: true, shape: 'cube', icons: ['trophy', 'users', 'calendar', 'star'], color: '#B45309', badge: 'Prizes', title: 'Enter a 5-a-side tournament', text: 'Bring your team, play the group stage and go for the trophy.', cta: 'See tournaments', link: '#/tournaments' },
    { id: 'pp-players', active: true, shape: 'cube', icons: ['users', 'ball', 'pitch', 'bell'], color: '#7C3AED', badge: 'Short of players?', title: 'Fill your squad in minutes', text: 'Post your game and players near you ask to join.', cta: 'Post a game', link: '#/players/new' },
    { id: 'pp-points', active: true, shape: 'coin', color: '#A16207', badge: 'pts', title: 'Points on every booking and order', text: 'Collect points and swap them for vouchers from 100 EGP.', cta: 'My points', link: '#/wallet' },
    { id: 'pp-partner', active: true, shape: 'cube', icons: ['pitch', 'wallet', 'calendar', 'chart'], color: '#0B6E4F', badge: 'For pitch owners', title: 'List your pitch on Super App', text: 'Online bookings and payments, plus a free dashboard for your team.', cta: 'Apply now', link: '#/partner/apply' },
  ];

  const content = {
    /** Live home banners (max 3, in order, inside their dates) */
    banners() {
      const t = isoDate(today());
      return (db().all('banners') || []).filter((b) => b.active && (!b.from || b.from <= t) && (!b.to || b.to >= t)).sort((a, b) => a.order - b.order).slice(0, 3);
    },
    /** Announcement bar at the top of the site (null when switched off or expired) */
    announcement() {
      const a = settings().announcement;
      if (!a?.on || !a.text) return null;
      if (a.until && a.until < isoDate(today())) return null;
      return a;
    },
    /** Pop-up ads for Super App's own services (SA.ui.promoPop). Settings live in settings.promoPops; defaults below. */
    promoPops() {
      const cfg = settings().promoPops || {};
      return { on: cfg.on ?? true, first: cfg.first ?? 6, interval: Math.max(5, cfg.interval ?? 10), stay: Math.max(4, cfg.stay ?? 7), max: cfg.max ?? 6, items: cfg.items || structuredClone(PROMO_DEFAULTS) };
    },
    promoStat(id, kind) {
      const st = { ...(settings().promoStats || {}) };
      st[id] = { shown: 0, clicked: 0, ...(st[id] || {}) }; st[id][kind] += 1;
      db().setSettings({ promoStats: st });
    },
    promoStats: () => settings().promoStats || {},
    PROMO_DEFAULTS: () => structuredClone(PROMO_DEFAULTS),
    dismissed: (a) => { try { return localStorage.getItem('superapp.announce.closed') === String(a.v); } catch { return false; } },
    dismiss: (a) => { try { localStorage.setItem('superapp.announce.closed', String(a.v)); } catch { /* ignore */ } },
    /** Feed stories + Super App posts, with the admin's hide / breaking flags applied */
    stories() {
      const edits = settings().newsEdits || {};
      const own = (db().all('newsPosts') || []).filter((p) => !p.deleted).map((p) => ({ ...p, own: true }));
      return [...own, ...SA.data.news.stories].map((s) => ({ ...s, ...(edits[s.id] || {}) })).filter((s) => !s.hidden);
    },
    allStories() {
      const edits = settings().newsEdits || {};
      const own = (db().all('newsPosts') || []).filter((p) => !p.deleted).map((p) => ({ ...p, own: true }));
      return [...own, ...SA.data.news.stories].map((s) => ({ ...s, ...(edits[s.id] || {}) })).sort((a, b) => b.at - a.at);
    },
    /** Maintenance mode: bookings and/or shop checkout paused by the Super App team */
    paused(what) { const m = settings().maintenance; return m && m[what] ? `${what === 'shop' ? 'Shop checkout is' : 'Pitch bookings are'} paused for maintenance. ${m.message || 'Please try again soon.'}` : ''; },
    assertOpen(what) { const msg = content.paused(what); if (msg) throw new SA.services.http.ApiError(msg, 'paused'); },
    /** Pickup games hidden by the Super App team */
    matchHidden: (id) => !!(settings().hiddenMatches || {})[id],
  };
  SA.services.content = content;
  // Delivery prices set in the admin console replace the defaults everywhere the shop reads them
  const syncShipping = () => Object.assign(SA.data.products.shipping, settings().shipping || {});
  // Shop categories edited in the admin console (names, order, hidden, new ones) replace the default list in place
  const DEFAULT_CATS = SA.data.products.categories.map((c) => ({ ...c }));
  const syncCategories = () => {
    const list = settings().shopCategories;
    const arr = SA.data.products.categories;
    arr.length = 0;
    (list && list.length ? list : DEFAULT_CATS).forEach((c) => arr.push({ ...c }));
  };
  content.defaultCategories = () => DEFAULT_CATS.map((c) => ({ ...c }));
  syncShipping(); syncCategories();
  db().subscribe((col) => { if (col === 'settings' || col === '*') { syncShipping(); syncCategories(); } });
})(window.SA);
