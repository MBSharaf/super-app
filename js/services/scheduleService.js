/**
 * Match schedules and live scores across competitions.
 * Fixtures are generated deterministically per date (stable across reloads). Today's slate always
 * includes live matches so the live experience can be demonstrated at any time of day.
 * Future API: GET /fixtures?date=&competition=, GET /fixtures/:id (events, stats), websocket for live.
 */
(function (SA) {
  const store = SA.core.store;
  const { request } = SA.services.http;
  const { rng, seeded, isoDate, addDays, today, parseISO } = SA.core.utils;
  const C = () => SA.data.competitions;
  const MATCH_MIN = 115; // kick-off to full time incl. half-time

  const cache = new Map();

  /** Fixtures for one date */
  const generate = (dateISO) => {
    if (cache.has(dateISO)) return cache.get(dateISO);
    const d = parseISO(dateISO);
    const out = [];
    C().competitions.forEach((comp) => {
      if (!comp.days.includes(d.getDay())) return;
      const r = rng(Math.floor(seeded(`${comp.id}|${dateISO}`) * 1e9));
      const pool = comp.teams.slice().sort(() => r() - 0.5);
      const weekIdx = Math.floor((d - new Date(2026, 7, 7)) / (7 * 864e5));
      for (let k = 0; k < comp.perDay && pool.length >= 2; k++) {
        const home = pool.pop(), away = pool.pop();
        const hour = comp.times[k % comp.times.length];
        const ko = new Date(d); ko.setHours(Math.floor(hour), Math.round((hour % 1) * 60), 0, 0);
        const id = `fx-${comp.id}-${dateISO}-${k}`;
        // Goals: stronger home bias, 0–4 each
        const ev = [];
        const hg = Math.floor(r() * r() * 4.6), ag = Math.floor(r() * r() * 3.8);
        for (let g = 0; g < hg; g++) ev.push({ min: 1 + Math.floor(r() * 90), side: 'home', kind: 'goal' });
        for (let g = 0; g < ag; g++) ev.push({ min: 1 + Math.floor(r() * 90), side: 'away', kind: 'goal' });
        for (let c = 0; c < 2 + Math.floor(r() * 4); c++) ev.push({ min: 5 + Math.floor(r() * 85), side: r() > 0.5 ? 'home' : 'away', kind: 'yellow' });
        ev.sort((a, b) => a.min - b.min);
        out.push({
          id, compId: comp.id, date: dateISO, ko: ko.getTime(), home, away,
          round: comp.stage.includes('round') ? comp.stage : `${comp.stage} ${Math.max(1, weekIdx + (comp.id === 'epl-eg' ? 1 : 0))}`,
          venue: comp.venue[Math.floor(r() * comp.venue.length)], events: ev,
          stats: { poss: 38 + Math.floor(r() * 25), shotsH: 6 + Math.floor(r() * 14), shotsA: 4 + Math.floor(r() * 12), onH: 2 + Math.floor(r() * 6), onA: 1 + Math.floor(r() * 5), cornH: 2 + Math.floor(r() * 8), cornA: 1 + Math.floor(r() * 7) },
        });
      }
    });
    // Demo: today's slate always has two live matches and one about to start
    if (dateISO === isoDate(today()) && out.length) {
      const now = Date.now(), round5 = (t) => Math.round(t / 3e5) * 3e5;
      const order = out.slice().sort((a, b) => (a.compId === 'epl-eg' ? -1 : 0) - (b.compId === 'epl-eg' ? -1 : 0));
      if (order[0]) order[0].ko = round5(now - 34 * 6e4);
      if (order[1]) order[1].ko = round5(now - 76 * 6e4);
      if (order[2]) order[2].ko = round5(now + 25 * 6e4);
    }
    out.sort((a, b) => a.ko - b.ko);
    cache.set(dateISO, out);
    return out;
  };

  /** Live state at the current moment */
  const stateOf = (m) => {
    const el = (Date.now() - m.ko) / 6e4;
    let status, minute = null, label;
    if (el < 0) { status = 'upcoming'; label = new Date(m.ko).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' }); }
    else if (el < MATCH_MIN) {
      status = 'live';
      if (el < 47) { minute = Math.max(1, Math.floor(el)); label = minute > 45 ? `45+${minute - 45}'` : `${minute}'`; }
      else if (el < 62) { minute = 45; label = 'HT'; }
      else { minute = Math.min(94, Math.floor(el - 17)); label = minute > 90 ? `90+${minute - 90}'` : `${minute}'`; }
    } else { status = 'finished'; minute = 90; label = 'FT'; }
    const upTo = status === 'upcoming' ? -1 : minute;
    const seen = m.events.filter((e) => e.min <= upTo);
    const hs = seen.filter((e) => e.kind === 'goal' && e.side === 'home').length;
    const as = seen.filter((e) => e.kind === 'goal' && e.side === 'away').length;
    const frac = status === 'finished' ? 1 : status === 'live' ? Math.min(1, minute / 90) : 0;
    const s = m.stats;
    const stats = { possession: [s.poss + 12, 100 - s.poss - 12], shots: [Math.round(s.shotsH * frac), Math.round(s.shotsA * frac)], onTarget: [Math.round(s.onH * frac), Math.round(s.onA * frac)], corners: [Math.round(s.cornH * frac), Math.round(s.cornA * frac)] };
    return { status, minute, label, hs, as, seen, stats };
  };

  const hydrate = (m) => {
    const T = C().teams, comp = C().competitions.find((c) => c.id === m.compId);
    const egy = [C().egyptiansAbroad[m.home], C().egyptiansAbroad[m.away]].filter(Boolean);
    return { ...m, comp, homeT: T[m.home], awayT: T[m.away], ...stateOf(m), reminder: (store.get().reminders || []).includes(m.id), egyptians: egy, isDerby: [m.home, m.away].sort().join() === 'AHL,ZAM' };
  };

  const importance = (m) => (m.isDerby ? 100 : 0) + (['AHL', 'ZAM', 'PYR', 'EGY'].some((t) => t === m.home || t === m.away) ? 40 : 0) + (m.compId === 'ucl' ? 30 : 0) + (m.status === 'live' ? 25 : 0) + (m.egyptians.length ? 15 : 0);

  const scheduleService = {
    competitions: () => C().competitions,
    teams: () => C().teams,
    /** Day strip: date, label, number of matches, number live */
    days: (back = 3, ahead = 7) => Array.from({ length: back + ahead + 1 }, (_, i) => {
      const d = addDays(today(), i - back), iso = isoDate(d);
      const list = generate(iso).map(hydrate);
      return { iso, d, count: list.length, live: list.filter((m) => m.status === 'live').length, offset: i - back };
    }),
    list: (f = {}) => request(() => {
      let out = generate(f.date || isoDate(today())).map(hydrate);
      if (f.comp) out = out.filter((m) => m.compId === f.comp);
      if (f.region) out = out.filter((m) => m.comp.region === f.region);
      if (f.live) out = out.filter((m) => m.status === 'live');
      return out;
    }, { latency: 160 }),
    listSync: (dateISO) => generate(dateISO).map(hydrate),
    get: (id) => { const date = id.split('-').slice(-4, -1).join('-'); const m = generate(date).find((x) => x.id === id); return m ? hydrate(m) : null; },
    refresh: (m) => ({ ...m, ...stateOf(m), reminder: (store.get().reminders || []).includes(m.id) }),
    featured: (dateISO) => generate(dateISO).map(hydrate).sort((a, b) => importance(b) - importance(a))[0] || null,
    /** Live + next matches across today and tomorrow (home page strip) */
    spotlight: (n = 8) => {
      const t = isoDate(today()), tm = isoDate(addDays(today(), 1));
      const all = [...generate(t), ...generate(tm)].map(hydrate).filter((m) => m.status !== 'finished');
      return all.sort((a, b) => (b.status === 'live') - (a.status === 'live') || importance(b) - importance(a) || a.ko - b.ko).slice(0, n);
    },
    toggleReminder(id) {
      const cur = store.get().reminders || [];
      const on = !cur.includes(id);
      store.set({ reminders: on ? [...cur, id] : cur.filter((x) => x !== id) }, 'reminder');
      return on;
    },
  };

  SA.services.schedule = scheduleService;
})(window.SA);
