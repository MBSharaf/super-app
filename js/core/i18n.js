/**
 * Arabic interface (RTL). Screens are written in English; when Arabic is on, every rendered text
 * node and UI attribute is translated from the dictionary in js/data/i18n-ar.js — including
 * strings with numbers, dates, times and prices inside them ("{0} free on {1} {2} {3}").
 * Proper nouns (pitch, team, product and people names) stay as they are.
 *   SA.i18n.lang → 'en' | 'ar'    SA.i18n.t('Book a pitch')    SA.i18n.set('ar')
 * In a React build this becomes the usual t() helper; the dictionary format carries over.
 */
(function (SA) {
  const KEY = 'superapp.lang';
  const read = () => { try { return localStorage.getItem(KEY) === 'ar' ? 'ar' : 'en'; } catch { return 'en'; } };
  const lang = read();

  const DAYS = { Sun: 'الأحد', Mon: 'الاثنين', Tue: 'الثلاثاء', Wed: 'الأربعاء', Thu: 'الخميس', Fri: 'الجمعة', Sat: 'السبت',
    Sunday: 'الأحد', Monday: 'الاثنين', Tuesday: 'الثلاثاء', Wednesday: 'الأربعاء', Thursday: 'الخميس', Friday: 'الجمعة', Saturday: 'السبت',
    Sundays: 'كل أحد', Mondays: 'كل اثنين', Tuesdays: 'كل ثلاثاء', Wednesdays: 'كل أربعاء', Thursdays: 'كل خميس', Fridays: 'كل جمعة', Saturdays: 'كل سبت' };
  const MONTHS = { Jan: 'يناير', Feb: 'فبراير', Mar: 'مارس', Apr: 'أبريل', May: 'مايو', Jun: 'يونيو', Jul: 'يوليو', Aug: 'أغسطس', Sep: 'سبتمبر', Oct: 'أكتوبر', Nov: 'نوفمبر', Dec: 'ديسمبر',
    January: 'يناير', February: 'فبراير', March: 'مارس', April: 'أبريل', June: 'يونيو', July: 'يوليو', August: 'أغسطس', September: 'سبتمبر', October: 'أكتوبر', November: 'نوفمبر', December: 'ديسمبر' };
  const UNITS = { AM: 'ص', PM: 'م', EGP: 'ج.م', km: 'كم', m: 'م', h: 'س', min: 'د' };
  // Tokens that carry values: numbers (1,250 / 4.8 / 12:30 / 25%), weekdays, months, AM/PM, currency
  const TOKEN = new RegExp(`(\\d[\\d,]*(?:[.:]\\d+)?%?|\\b(?:${Object.keys(DAYS).sort((a, b) => b.length - a.length).join('|')})\\b|\\b(?:${Object.keys(MONTHS).sort((a, b) => b.length - a.length).join('|')})\\b|\\b(?:AM|PM|EGP)\\b)`, 'g');
  const tokenValue = (tk) => DAYS[tk] || MONTHS[tk] || UNITS[tk] || tk;

  const cache = new Map();
  const missing = new Set();
  let dict = null, patterns = [];
  const load = () => { if (!dict) { const d = SA.data.i18nAr || {}; dict = new Map(Object.entries(d.strings || {})); patterns = (d.patterns || []).map(([re, out]) => [new RegExp(re), out]); } };

  /** Translate one string; returns null when there is nothing to change */
  const tr = (raw) => {
    const s = raw.replace(/\s+/g, ' ').trim();
    if (!s || !/[A-Za-z]/.test(s) || /[؀-ۿ]/.test(s)) return null;
    if (cache.has(s)) return cache.get(s);
    load();
    let out = dict.get(s) ?? null;
    if (out == null) {
      // Template: values become {0}, {1}… in order of appearance
      const vals = [];
      const key = s.replace(TOKEN, (tk) => { vals.push(tk); return `{${vals.length - 1}}`; });
      const fill = (t) => t.replace(/\{(\d+)\}/g, (_, i) => tokenValue(vals[+i]));
      if (vals.length && dict.has(key)) out = fill(dict.get(key));
      else if (vals.length && !/[A-Za-z]/.test(key.replace(/\{\d+\}/g, ''))) out = fill(key);
    }
    if (out == null) {
      for (const [re, rep] of patterns) {
        const m = s.match(re);
        if (m) { out = rep.replace(/\{t(\d)\}/g, (_, i) => tr(m[+i]) ?? m[+i]).replace(/\{(\d)\}/g, (_, i) => m[+i]); break; }
      }
    }
    if (out == null) {
      // Composite UI strings: translate each part around common separators
      // Accepted only when every part is translated or is a proper name (pitch, team, person…)
      const nameLike = (p) => /^[\s\d{}#.+&/()'’-]*[A-Z0-9][\w'’.&+/()-]*(\s+(?:[A-Z0-9&(][\w'’.&+/()-]*|of|the|de|el|al|vs|v|and|x))*[\s.]*$/.test(p.trim()) || !/[a-z]{2}/.test(p);
      for (const sep of [' · ', ' — ', ' – ', ' | ', ': ', ', ']) {
        if (!s.includes(sep)) continue;
        const parts = s.split(sep);
        const done = parts.map((p) => tr(p));
        if (done.some((x) => x != null) && parts.every((p, i) => done[i] != null || nameLike(p))) { out = parts.map((p, i) => done[i] ?? p).join(sep === ', ' ? '، ' : sep); break; }
      }
    }
    if (out == null) { let k = 0; missing.add(s.replace(TOKEN, () => `{${k++}}`)); }
    cache.set(s, out);
    return out;
  };
  const t = (s) => (lang === 'ar' ? tr(String(s)) ?? s : s);

  /* ---------- DOM translation ---------- */
  const ATTRS = ['placeholder', 'aria-label', 'title', 'alt', 'data-tip'];
  const SKIP = 'script,style,textarea,code,[data-i18n-skip],[translate="no"]';
  const doText = (n) => {
    if (n.__tr && n.nodeValue === n.__tr) return;
    const v = n.nodeValue;
    if (!v || !/[A-Za-z]/.test(v)) return;
    const p = n.parentElement;
    if (!p || p.closest(SKIP)) return;
    const out = tr(v);
    if (out == null) return;
    const lead = v.match(/^\s*/)[0], trail = v.match(/\s*$/)[0];
    n.__tr = lead + out + trail;
    n.nodeValue = n.__tr;
  };
  const doAttrs = (el) => {
    if (el.closest?.(SKIP)) return;
    ATTRS.forEach((a) => {
      const v = el.getAttribute?.(a);
      if (!v || !/[A-Za-z]/.test(v) || el['__tr_' + a] === v) return;
      const out = tr(v);
      if (out != null) { el['__tr_' + a] = out; el.setAttribute(a, out); }
    });
  };
  const walk = (root) => {
    if (!root) return;
    if (root.nodeType === 3) return doText(root);
    if (root.nodeType !== 1 && root.nodeType !== 11) return;
    if (root.nodeType === 1) doAttrs(root);
    const it = document.createTreeWalker(root, NodeFilter.SHOW_TEXT | NodeFilter.SHOW_ELEMENT);
    for (let n = it.nextNode(); n; n = it.nextNode()) { if (n.nodeType === 3) doText(n); else doAttrs(n); }
  };

  let queued = new Set(), scheduled = false;
  const flush = () => { scheduled = false; const list = [...queued]; queued = new Set(); list.forEach((n) => n.isConnected && walk(n)); };
  const observe = () => {
    new MutationObserver((muts) => {
      muts.forEach((m) => {
        if (m.type === 'childList') m.addedNodes.forEach((n) => queued.add(n));
        else if (m.type === 'characterData') { if (m.target.nodeValue !== m.target.__tr) queued.add(m.target); }
        else if (m.type === 'attributes') queued.add(m.target);
      });
      if (!scheduled && queued.size) { scheduled = true; queueMicrotask(flush); }
    }).observe(document.documentElement, { childList: true, subtree: true, characterData: true, attributes: true, attributeFilter: ATTRS });
  };

  SA.i18n = {
    lang, t, isAr: lang === 'ar', missing,
    /** Switch language: saved, then the app reloads in place (keeps the current page) */
    set(next) { try { localStorage.setItem(KEY, next); } catch { /* ignore */ } location.reload(); },
    toggle() { SA.i18n.set(lang === 'ar' ? 'en' : 'ar'); },
    translateTree: walk,
    start() {
      if (lang !== 'ar') return;
      walk(document.body);
      observe();
      // Page titles are set by the router after each render
      const desc = Object.getOwnPropertyDescriptor(Document.prototype, 'title');
      Object.defineProperty(document, 'title', { configurable: true, get: () => desc.get.call(document), set: (v) => desc.set.call(document, tr(v) ?? v) });
    },
  };
})(window.SA);
