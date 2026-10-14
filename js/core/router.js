/**
 * Hash router: '#/pitches/:id?date=2026-10-09'. Hash routing keeps the app working from any static host
 * (and from file://). In React this becomes react-router with the same path table.
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

  const parse = () => {
    const raw = location.hash.replace(/^#/, '') || '/';
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

  const router = {
    add(path, page, opts = {}) { routes.push({ path, page, ...opts, ...compile(path) }); return router; },
    go(to, { replace = false } = {}) {
      const target = to.startsWith('#') ? to : '#' + to;
      if (replace) history.replaceState(null, '', target), router.resolve();
      else if (location.hash === target) router.resolve();
      else location.hash = target;
    },
    back(fallback = '/') { if (history.length > 1) history.back(); else router.go(fallback); },
    current: () => current,
    onChange(fn) { listeners.add(fn); return () => listeners.delete(fn); },
    resolve() { current = parse(); listeners.forEach((fn) => fn(current)); },
    start() { window.addEventListener('hashchange', router.resolve); router.resolve(); },
    /** Build a link with query params */
    href(path, query) {
      const q = query ? '?' + new URLSearchParams(Object.entries(query).filter(([, v]) => v !== undefined && v !== '' && v !== null)).toString() : '';
      return '#' + path + (q === '?' ? '' : q);
    },
  };

  SA.core.router = router;
})(window.SA);
