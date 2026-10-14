(function (SA) {
  const { config } = SA;

  const esc = (v) => String(v ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

  /** Join template parts; arrays are flattened, falsy values dropped. */
  const html = (strings, ...vals) => strings.reduce((out, s, i) => {
    let v = vals[i - 1];
    if (Array.isArray(v)) v = v.join('');
    if (v === false || v === null || v === undefined) v = '';
    return out + v + s;
  });

  const money = (n, opts = {}) => {
    const v = Math.round(Number(n) * 100) / 100;
    const s = v.toLocaleString('en-US', { minimumFractionDigits: opts.decimals ?? 0, maximumFractionDigits: opts.decimals ?? 0 });
    return `${s} ${config.currency}`;
  };
  const fmtNum = (n, d = 0) => Number(n).toLocaleString('en-US', { minimumFractionDigits: d, maximumFractionDigits: d });
  const fantasyPrice = (m) => `${Number(m).toFixed(1)}M`;

  // Dates
  const pad = (n) => String(n).padStart(2, '0');
  const isoDate = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  const parseISO = (s) => { const [y, m, d] = s.split('-').map(Number); return new Date(y, m - 1, d); };
  const addDays = (d, n) => { const x = new Date(d); x.setDate(x.getDate() + n); return x; };
  const today = () => { const d = new Date(); d.setHours(0, 0, 0, 0); return d; };
  const DAY = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
  const MON = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  const fmtDate = (d, style = 'short') => {
    d = typeof d === 'string' ? parseISO(d) : d;
    if (style === 'day') return DAY[d.getDay()];
    if (style === 'long') return `${DAY[d.getDay()]}, ${d.getDate()} ${MON[d.getMonth()]} ${d.getFullYear()}`;
    return `${DAY[d.getDay()]} ${d.getDate()} ${MON[d.getMonth()]}`;
  };
  const relDay = (iso) => {
    const diff = Math.round((parseISO(iso) - today()) / 864e5);
    if (diff === 0) return 'Today';
    if (diff === 1) return 'Tomorrow';
    if (diff === -1) return 'Yesterday';
    return fmtDate(iso);
  };
  /** Hours are stored as numbers; 24+ means after midnight (late games are common in Egypt). */
  const fmtHour = (h) => {
    const hh = ((Math.floor(h) % 24) + 24) % 24;
    const mm = Math.round((h % 1) * 60);
    const suffix = hh >= 12 ? 'PM' : 'AM';
    const h12 = hh % 12 === 0 ? 12 : hh % 12;
    return `${h12}${mm ? ':' + pad(mm) : ''} ${suffix}`;
  };
  const fmtRange = (h, dur) => `${fmtHour(h)} – ${fmtHour(h + dur)}`;
  const timeAgo = (ts) => {
    const s = Math.max(1, Math.round((Date.now() - ts) / 1000));
    if (s < 60) return 'Just now';
    const m = Math.round(s / 60); if (m < 60) return `${m} min ago`;
    const h = Math.round(m / 60); if (h < 24) return `${h} h ago`;
    const d = Math.round(h / 24); return d === 1 ? 'Yesterday' : `${d} days ago`;
  };
  const countdownParts = (target) => {
    const ms = Math.max(0, new Date(target) - Date.now());
    return { d: Math.floor(ms / 864e5), h: Math.floor(ms / 36e5) % 24, m: Math.floor(ms / 6e4) % 60, s: Math.floor(ms / 1000) % 60, done: ms === 0 };
  };

  // Geo
  const haversineKm = (a, b) => {
    const R = 6371, rad = (x) => (x * Math.PI) / 180;
    const dLat = rad(b.lat - a.lat), dLng = rad(b.lng - a.lng);
    const h = Math.sin(dLat / 2) ** 2 + Math.cos(rad(a.lat)) * Math.cos(rad(b.lat)) * Math.sin(dLng / 2) ** 2;
    return 2 * R * Math.asin(Math.sqrt(h));
  };
  const fmtKm = (km) => (km < 1 ? `${Math.round(km * 1000)} m` : `${km.toFixed(km < 10 ? 1 : 0)} km`);

  /** Deterministic pseudo-random in [0,1) from any string — keeps mock data stable across reloads. */
  const seeded = (str) => {
    let h = 2166136261;
    for (let i = 0; i < str.length; i++) { h ^= str.charCodeAt(i); h = Math.imul(h, 16777619); }
    h += h << 13; h ^= h >>> 7; h += h << 3; h ^= h >>> 17; h += h << 5;
    return ((h >>> 0) % 100000) / 100000;
  };
  const rng = (seed) => { let s = seed >>> 0 || 1; return () => { s = (Math.imul(s, 1664525) + 1013904223) >>> 0; return s / 4294967296; }; };

  const uid = (prefix = 'ID') => `${prefix}-${Date.now().toString(36).slice(-4).toUpperCase()}${Math.random().toString(36).slice(2, 6).toUpperCase()}`;
  const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
  const clamp = (v, a, b) => Math.min(b, Math.max(a, v));
  const debounce = (fn, ms = 200) => { let t; return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); }; };
  const initials = (name) => name.split(' ').filter(Boolean).slice(0, 2).map((p) => p[0]).join('').toUpperCase();
  const pick = (arr, r) => arr[Math.floor(r * arr.length) % arr.length];
  const groupBy = (arr, fn) => arr.reduce((acc, x) => { const k = fn(x); (acc[k] ||= []).push(x); return acc; }, {});
  const sum = (arr, fn = (x) => x) => arr.reduce((a, x) => a + fn(x), 0);
  const plural = (n, one, many = one + 's') => `${n} ${n === 1 ? one : many}`;
  const qs = (sel, root = document) => root.querySelector(sel);
  const qsa = (sel, root = document) => [...root.querySelectorAll(sel)];
  const formData = (form) => Object.fromEntries(new FormData(form).entries());

  SA.core.utils = {
    esc, html, money, fmtNum, fantasyPrice, isoDate, parseISO, addDays, today, fmtDate, relDay, fmtHour, fmtRange, timeAgo,
    countdownParts, haversineKm, fmtKm, seeded, rng, uid, sleep, clamp, debounce, initials, pick, groupBy, sum, plural, qs, qsa, formData, DAY, MON,
  };
})(window.SA);
