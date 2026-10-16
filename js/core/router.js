/**
 * Router with clean URLs: /pitches/:id?date=2026-10-09 (no "#").
 * - Uses the History API. The site can live under a sub-folder (GitHub Pages serves it at /super-app/); the <base>
 *   tag in index.html holds that folder and every route is relative to it.
 * - Links in the code are still written as "#/path" (SA.core.router.href, notifications, nav tables). One click
 *   handler turns them into clean navigation, so pages don't need to change.
 * - Old links with "#/" (shared before, bookmarks) still open the right page: they are rewritten to the clean URL.
 * - Static hosts need every path to serve index.html: tools/serve.ps1 does that locally, and 404.html does it on
 *   GitHub Pages (it remembers the path and opens index.html, which restores it).
 * In React this becomes react-router with the same path table.
 */
(function (SA) {
  const routes = [];
  let current = null;
  const listeners = new Set();

  const compile = (path) => {
    const keys = [];
    if (path === '*') return { re: /^$^/, keys };
    const re = new RegExp('^' + path.replace(/\//g, '\\/').replace(/:(\w+)/g, (_, k) => { keys.push(k); return '([^/]+)'; }) + '\\/?$');
    return { re, keys };
  };

  /** The folder the site lives in, without the trailing slash ('' at the domain root, '/super-app' on GitHub Pages) */
  const base = () => (document.querySelector('base')?.getAttribute('href') || '/').replace(/\/$/, '');
  /** The current route: '/pitches/p-1?date=…' */
  const location_ = () => {
    let p = location.pathname;
    const b = base();
    if (b && (p === b || p.startsWith(`${b}/`))) p = p.slice(b.length);
    p = p.replace(/\/index\.html$/, '/') || '/';
    return p + location.search;
  };
  /** '#/x', '/x' or 'x' → the real URL path '/super-app/x' */
  const toUrl = (to) => { const r = String(to || '/').replace(/^#/, ''); return `${base()}${r.startsWith('/') ? r : `/${r}`}`; };

  const parse = () => {
    const raw = location_();
    const [path, qstr = ''] = raw.split('?');
    const query = Object.fromEntries(new URLSearchParams(qstr));
    for (const r of routes) {
      const m = path.match(r.re);
      if (m) {
        const params = {};
        r.keys.forEach((k, i) => (params[k] = decodeURIComponent(m[i + 1])));
        return { route: r, params, query, path };
      }
    }
    return { route: routes.find((r) => r.path === '*'), params: {}, query, path };
  };

  /** Old "#/…" URLs → clean URLs (returns true if it rewrote one) */
  const legacy = () => {
    if (!/^#\//.test(location.hash)) return false;
    history.replaceState(null, '', toUrl(location.hash.slice(1)));
    return true;
  };

  /** One handler for every link on every page */
  const onClick = (e) => {
    const a = e.target.closest?.('a[href]');
    if (!a || e.defaultPrevented || a.hasAttribute('download') || (a.target && a.target !== '_self')) return;
    const h = a.getAttribute('href');
    const modified = e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey;
    if (h.startsWith('#/')) {
      // Ctrl/⌘-click or middle-click: let the browser open the clean URL in a new tab
      if (modified) { a.href = toUrl(h); return; }
      e.preventDefault(); router.go(h); return;
    }
    if (h === '#') { e.preventDefault(); return; }
    if (h.startsWith('#')) { e.preventDefault(); document.getElementById(h.slice(1))?.scrollIntoView({ behavior: 'smooth' }); return; }
    // A clean link to a page of this site
    if (!modified && a.origin === location.origin && (a.pathname === base() || a.pathname.startsWith(`${base()}/`)) && !/\.\w{2,5}$/.test(a.pathname)) {
      e.preventDefault(); router.go(a.pathname.slice(base().length) + a.search);
    }
  };

  const router = {
    add(path, page, opts = {}) { routes.push({ path, page, ...opts, ...compile(path) }); return router; },
    go(to, { replace = false } = {}) {
      const url = toUrl(to);
      if (replace) history.replaceState(null, '', url);
      else if (url !== location.pathname + location.search) history.pushState(null, '', url);
      router.resolve();
    },
    back(fallback = '/') { if (history.length > 1) history.back(); else router.go(fallback); },
    current: () => current,
    /** The route pattern a path matches ('/pitches/:id'), for tools like the QA sweep */
    patternOf(path) { const p = String(path).replace(/^#/, '').split('?')[0]; const r = routes.find((x) => x.path !== '*' && x.re.test(p)); return r ? r.path : null; },
    /** The current route as a string ('/admin/payouts?tab=…'), readable before the first render */
    location: location_,
    /** Full shareable URL for a route: 'https://…/super-app/players/m-1' */
    url: (to) => `${location.origin}${toUrl(to)}`,
    onChange(fn) { listeners.add(fn); return () => listeners.delete(fn); },
    resolve() { current = parse(); listeners.forEach((fn) => fn(current)); },
    start() {
      legacy();
      window.addEventListener('popstate', router.resolve);
      // Something set location.hash = '#/…' (old code, a pasted link): switch to the clean URL
      window.addEventListener('hashchange', () => { if (legacy()) router.resolve(); });
      document.addEventListener('click', onClick);
      router.resolve();
    },
    /** Build a link with query params (written as "#/…"; the click handler makes it a clean URL) */
    href(path, query) {
      const q = query ? '?' + new URLSearchParams(Object.entries(query).filter(([, v]) => v !== undefined && v !== '' && v !== null)).toString() : '';
      return '#' + path + (q === '?' ? '' : q);
    },
  };

  SA.core.router = router;
})(window.SA);
