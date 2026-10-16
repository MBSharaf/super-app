/**
 * QA sweep — opens every page of every system, follows the links it finds (one page per route pattern, plus up to
 * 6 tab/filter variants), and records: console errors, error pages, things sticking out of the screen, and visible
 * English text when the site is in Arabic. Navigation only: it never clicks buttons or submits forms.
 *
 * Load it in the browser console on the running site:
 *   await import('/super-app/tools/qa-sweep.js?' + Date.now());  QA.run();   // then read QA.report() when QA.done
 */
(function () {
  const SA = window.SA;
  const START = ['/help', '/legal', '/legal/terms', '/sell/bulk', '/admin/returns', '/admin/finance-approvals', '/seller/returns', '/ceo/legal', '/ceo/sitemap', '/', '/pitches', '/players', '/fantasy', '/fantasy/squad', '/fantasy/leagues', '/fantasy/fixtures', '/fantasy/stats', '/shop', '/cart', '/orders', '/wishlist', '/profile', '/wallet', '/news', '/matches', '/tournaments', '/sell', '/sell/studio', '/support', '/community', '/alerts', '/bookings', '/my-games', '/partner/apply', '/players/new',
    '/dash', '/dash/sell', '/seller', '/admin', '/ceo', '/factory'];
  const SKIP = /^$/;
  const wait = (ms) => new Promise((r) => setTimeout(r, ms));
  const state = { errors: [], pages: [], seen: new Map(), done: false, running: false };
  const origError = console.error;
  let cur = '';
  console.error = (...a) => { state.errors.push({ route: cur, msg: a.map((x) => (x && x.stack) || String(x)).join(' ').slice(0, 300) }); origError.apply(console, a); };
  window.addEventListener('error', (e) => state.errors.push({ route: cur, msg: `${e.message} @ ${(e.filename || '').split('/').pop()}:${e.lineno}` }));
  window.addEventListener('unhandledrejection', (e) => state.errors.push({ route: cur, msg: `promise: ${e.reason?.message || e.reason}` }));

  const signInAll = () => {
    try { localStorage.setItem('superapp.dash.session', 'acc-o1'); } catch { /* */ }
    try { const s = SA.services.db.all('sellers').find((x) => x.status === 'active'); if (s) localStorage.setItem('superapp.seller.session', s.accountId); } catch { /* */ }
    try { localStorage.setItem('superapp.admin.session', 'acc-admin'); } catch { /* */ }
    try { localStorage.setItem('superapp.factory.session', 'fs-owner'); } catch { /* */ }
    try { SA.services.ceo.auth.signIn('2026'); } catch { /* */ }
  };
  const scrollParent = (el) => { for (let p = el.parentElement; p && p !== document.body; p = p.parentElement) { const o = getComputedStyle(p).overflowX; if (o !== 'visible' && o !== 'clip') return true; if (getComputedStyle(p).position === 'fixed') return true; } return false; };
  const overflow = () => {
    const W = document.documentElement.clientWidth, out = [];
    document.querySelectorAll('#app *').forEach((e) => {
      const r = e.getBoundingClientRect();
      // Hidden, decorative, or still waiting for its scroll-in animation (the page clips it, nothing scrolls)
      if (!r.width || !r.height || e.closest('[aria-hidden="true"]') || e.closest('.reveal:not(.is-in)')) return;
      if ((r.right > W + 2 || r.left < -2) && !scrollParent(e)) {
        const pr = e.parentElement.getBoundingClientRect();
        if (!(pr.right > W + 2 || pr.left < -2)) out.push(`${e.tagName.toLowerCase()}.${String(e.className).split(' ').filter(Boolean).slice(0, 2).join('.')} (${Math.round(r.left)}→${Math.round(r.right)})`);
      }
    });
    if (document.documentElement.scrollWidth > W + 1) out.unshift(`page scrolls sideways (${document.documentElement.scrollWidth}px)`);
    return [...new Set(out)].slice(0, 4);
  };
  const english = () => {
    if (!SA.i18n.isAr) return [];
    const out = new Set();
    const w = document.createTreeWalker(document.getElementById('app'), NodeFilter.SHOW_TEXT);
    for (let n = w.nextNode(); n; n = w.nextNode()) {
      const t = n.nodeValue.replace(/\s+/g, ' ').trim();
      if (!/\b[a-z]{2,}\s+[a-z]{2,}\b/.test(t) || /@|https?:|\.(js|css|com)\b/.test(t)) continue;
      const el = n.parentElement;
      if (!el || el.closest('script,style,code,textarea,[data-i18n-skip],[translate="no"]') || !el.getClientRects().length) continue;
      out.add(t.slice(0, 120));
    }
    return [...out].slice(0, 12);
  };
  const errorPage = () => { const t = document.querySelector('#app .empty-title, #app .empty h2, #app .empty h3, #app .empty b')?.textContent || ''; return /Something went wrong|حصلت مشكلة/.test(t) ? t : ''; };
  const linksHere = () => [...document.querySelectorAll('#app a[href]')].map((a) => a.getAttribute('href')).filter((h) => h && (h.startsWith('#/') || (h.startsWith('/') && !h.startsWith('//')))).map((h) => h.replace(/^#/, '').replace(/^\/super-app/, '') || '/');

  const visit = async (path) => {
    cur = path;
    const before = state.errors.length;
    SA.core.router.go(path);
    await wait(650);
    document.querySelectorAll('.overlay [data-close], .overlay .overlay-close').forEach((b) => b.click());
    state.pages.push({ path, errors: state.errors.slice(before).map((e) => e.msg), errorPage: errorPage(), overflow: overflow(), english: english() });
    return linksHere();
  };
  const QA = {
    state,
    async run({ max = 600 } = {}) {
      if (state.running) return 'already running';
      Object.assign(state, { errors: [], pages: [], seen: new Map(), done: false, running: true });
      signInAll();
      // Pages no link points to on their own: forms, receipts, tickets
      const db = SA.services.db, st = SA.core.store.get();
      const sel = db.all('sellers').find((x) => x.status === 'active' && x.mode !== 'pod');
      if (sel) localStorage.setItem('superapp.seller.session', sel.accountId);
      const prod = sel && db.all('products').find((x) => x.storeId === sel.storeId && !x.deleted);
      const extra = ['/checkout', '/players/new', '/dash/pitches/new', '/dash/store/product/new', '/admin/store/product/new', '/seller/products/product/new', '/seller/store-orders', '/seller/codes', '/dash/partners', '/dash/settings',
        st.bookings?.[0] && `/booking/${st.bookings[0].id}`, st.orders?.[0] && `/order/${st.orders[0].id}`, (db.all('tickets') || [])[0] && `/support/${db.all('tickets')[0].id}`,
        (db.all('tickets') || []).find((t) => t.from?.kind !== 'customer') && `/dash/support/${db.all('tickets').find((t) => t.from?.kind !== 'customer').id}`, prod && `/seller/products/product/${prod.id}`].filter(Boolean);
      const queue = [...START, ...extra];
      const key = (p) => { const pat = SA.core.router.patternOf(p) || p.split('?')[0]; const q = new URLSearchParams(p.split('?')[1] || ''); q.delete('from'); q.delete('to'); return [pat, [...q.keys()].sort().map((k) => `${k}=${k === 'tab' || k === 'kind' || k === 'view' ? q.get(k) : ''}`).join('&')]; };
      const variants = new Map();
      try {
        while (queue.length && state.pages.length < max) {
          const p = queue.shift();
          if (SKIP.test(p.split('?')[0])) continue;
          const [pat, qk] = key(p);
          const id = `${pat}?${qk}`;
          if (state.seen.has(id)) continue;
          const n = variants.get(pat) || 0;
          if (qk && n >= 6) continue;
          variants.set(pat, n + 1);
          state.seen.set(id, p);
          const links = await visit(p);
          links.forEach((l) => { const [pt, q] = key(l); if (!state.seen.has(`${pt}?${q}`)) queue.push(l); });
        }
      } finally { state.running = false; state.done = true; cur = ''; }
      return state.pages.length;
    },
    report() {
      const bad = state.pages.filter((p) => p.errors.length || p.errorPage || p.overflow.length || p.english.length);
      return { pages: state.pages.length, problems: bad.length, mode: `${SA.i18n.isAr ? 'ar' : 'en'} · ${SA.core.theme.current()} · ${innerWidth}px`, bad };
    },
  };
  window.QA = QA;
})();
