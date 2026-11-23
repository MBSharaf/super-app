/**
 * The matches page on the real API (backend 8.5b). Loaded after scheduleService.
 *
 * In API mode the schedule service answers from the server instead of the generated demo slate: the competitions
 * (`/matches/competitions`, with their colours and regions) and eleven days of matches (`/matches?date&days=11`, three
 * back and seven ahead) are loaded once and every five minutes, turned into the shape the pages already read, and the
 * page re-renders when they land. While a match is on (or about to start) they are read again every minute: the server
 * keeps live scores, minutes and the clock text ("HT", "45+2'") from its live feed, and the page's ticker shows them
 * without a full re-render (only a status change, e.g. kick-off or full time, re-renders). The free sources give no
 * minute-by-minute events or match stats, so those boxes say so instead of showing made-up numbers. Demo mode
 * (GitHub Pages) is unchanged.
 */
(function (SA) {
  const api = SA.services.api;
  const S = SA.services.schedule;
  const store = SA.core.store;
  const { isoDate, addDays, today } = SA.core.utils;
  const demo = { ...S };
  const live = () => api.isLive();
  const BACK = 3, AHEAD = 7, EVERY = 5 * 60 * 1000, LIVE_EVERY = 60 * 1000;

  let competitions = [];
  const teams = {};            // `c<club id>` → { name, short, a, b }
  const byDate = new Map();    // yyyy-mm-dd → raw matches
  const byId = new Map();      // `m-<id>` → raw match
  let loadedAt = 0;
  let loading = null;

  const CUP_ROUNDS = { 32: 'Knockout play-off', 16: 'Round of 16', 125: 'Quarter-final', 150: 'Semi-final', 200: 'Final' };
  const roundLabel = (key, n) => CUP_ROUNDS[n] && !['egy', 'epl', 'laliga', 'seriea', 'bundesliga', 'ligue1', 'superlig'].includes(key)
    ? CUP_ROUNDS[n] : `${key === 'epl' ? 'Matchweek' : key === 'egy' ? 'Gameweek' : 'Matchday'} ${n}`;
  const STATUS = { Scheduled: 'upcoming', Postponed: 'upcoming', Cancelled: 'upcoming', Live: 'live', Finished: 'finished' };

  const club = (c) => {
    const id = `c${c.id}`;
    teams[id] = { name: c.name, short: c.shortName, a: c.kit, b: c.kit2, logo: c.logo || null };
    return id;
  };

  /** Server match → the demo's raw match (plus `srv` with the real state) */
  const fromApi = (m) => {
    const ko = new Date(m.kickoffAt.endsWith('Z') ? m.kickoffAt : `${m.kickoffAt}Z`).getTime();
    return {
      id: `m-${m.id}`, apiId: m.id, compId: m.competition, date: isoDate(new Date(ko)), ko, home: club(m.home), away: club(m.away),
      round: roundLabel(m.competition, m.gameweek), venue: m.venue || '', events: [], stats: null, live: true,
      srv: { status: m.status, hs: m.homeGoals, as: m.awayGoals, minute: m.minute, clock: m.clock || null },
    };
  };

  /** Without a live clock from the server: the minute from kick-off, with the 15-minute half-time break */
  const guessClock = (ko) => {
    const el = (Date.now() - ko) / 6e4;
    if (el < 47) { const n = Math.max(1, Math.floor(el)); return { minute: Math.min(n, 45), label: n > 45 ? `45+${n - 45}'` : `${n}'` }; }
    if (el < 62) return { minute: 45, label: 'HT' };
    const n = Math.floor(el - 17);
    return { minute: Math.min(n, 90), label: n > 90 ? `90+${Math.min(n - 90, 15)}'` : `${n}'` };
  };

  /** The live state the pages read (status, label, score), from the server's answer */
  const stateOf = (m) => {
    const status = STATUS[m.srv.status] || 'upcoming';
    const time = new Date(m.ko).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });
    const guess = status === 'live' ? guessClock(m.ko) : null;
    const minute = status === 'live' ? (m.srv.minute ?? guess.minute) : status === 'finished' ? 90 : null;
    const label = m.srv.status === 'Postponed' ? 'Postponed' : m.srv.status === 'Cancelled' ? 'Cancelled'
      : status === 'upcoming' ? time : status === 'live' ? (m.srv.clock || (m.srv.minute != null ? `${m.srv.minute}'` : guess.label)) : 'FT';
    return { status, minute, label, hs: m.srv.hs ?? 0, as: m.srv.as ?? 0, seen: [], stats: null, noDetail: true };
  };

  const hydrate = (m) => {
    const comp = competitions.find((c) => c.id === m.compId) || { id: m.compId, name: m.compId, short: m.compId, region: '', color: '#0B6E4F' };
    const names = [teams[m.home]?.name || '', teams[m.away]?.name || ''].join('|');
    return { ...m, comp, homeT: teams[m.home], awayT: teams[m.away], ...stateOf(m), reminder: (store.get().reminders || []).includes(m.id), egyptians: [], isDerby: /Ahly/.test(names) && /Zamalek/.test(names) };
  };

  const importance = (m) => (m.isDerby ? 100 : 0) + (m.compId === 'egy' ? 40 : 0) + (m.compId === 'ucl' ? 30 : 0) + (m.status === 'live' ? 25 : 0) + (m.compId === 'epl' ? 15 : 0);

  /** A match on now, or kicking off within ten minutes (then scores are read every minute) */
  const hot = () => [...byId.values()].some((m) => m.srv.status === 'Live'
    || (m.srv.status === 'Scheduled' && m.ko - Date.now() < 10 * 6e4 && Date.now() - m.ko < 3 * 36e5));

  /** Fresh scores for the matches already shown: updated in place; re-render only when a status changed or a match is new */
  const quiet = () => {
    if (loading) return loading;
    const from = isoDate(addDays(today(), -BACK));
    loading = api.get(`/matches?date=${from}&days=${BACK + AHEAD + 1}`).then((list) => {
      let changed = false;
      list.map(fromApi).forEach((m) => {
        const cur = byId.get(m.id);
        if (!cur) { changed = true; return; }
        if (cur.srv.status !== m.srv.status) changed = true;
        cur.srv = m.srv;
      });
      loadedAt = Date.now();
      if (changed) { loading = null; return load(); }
      return undefined;
    }, (err) => console.warn('[liveSchedule]', err?.message || err)).finally(() => { loading = null; });
    return loading;
  };

  const load = () => {
    if (loading) return loading;
    const from = isoDate(addDays(today(), -BACK));
    loading = Promise.all([api.get('/matches/competitions'), api.get(`/matches?date=${from}&days=${BACK + AHEAD + 1}`)]).then(([comps, list]) => {
      competitions = comps.map((c) => ({ id: c.key, name: c.name, nameAr: c.nameAr, short: c.shortName, region: c.region, color: c.color, fantasy: c.fantasy }));
      byDate.clear(); byId.clear();
      list.map(fromApi).sort((a, b) => a.ko - b.ko).forEach((m) => {
        byId.set(m.id, m);
        byDate.set(m.date, [...(byDate.get(m.date) || []), m]);
      });
      loadedAt = Date.now();
      SA.app?.rerender?.();
    }, (err) => console.warn('[liveSchedule]', err?.message || err)).finally(() => { loading = null; });
    return loading;
  };
  /** Synchronous readers answer from what is loaded and ask for fresh data when it is older than five minutes */
  const ready = () => { if (Date.now() - loadedAt > EVERY) load(); return loadedAt > 0; };
  const day = (iso) => (ready() ? byDate.get(iso) || [] : []);

  S.competitions = () => (live() ? competitions : demo.competitions());
  S.teams = () => (live() ? { ...demo.teams(), ...teams } : demo.teams());
  S.days = (back = BACK, ahead = AHEAD) => (live() ? Array.from({ length: back + ahead + 1 }, (_, i) => {
    const d = addDays(today(), i - back), iso = isoDate(d);
    const list = day(iso).map(hydrate);
    return { iso, d, count: list.length, live: list.filter((m) => m.status === 'live').length, offset: i - back };
  }) : demo.days(back, ahead));
  S.list = async (f = {}) => {
    await api.ready;
    if (!live()) return demo.list(f);
    if (!loadedAt) await load();
    let out = day(f.date || isoDate(today())).map(hydrate);
    if (f.comp) out = out.filter((m) => m.compId === f.comp);
    if (f.region) out = out.filter((m) => m.comp.region === f.region);
    if (f.live) out = out.filter((m) => m.status === 'live');
    return out;
  };
  S.listSync = (iso) => (live() ? day(iso).map(hydrate) : demo.listSync(iso));
  S.get = (id) => (String(id).startsWith('m-') ? (byId.has(id) ? hydrate(byId.get(id)) : null) : demo.get(id));
  S.refresh = (m) => (m.srv ? hydrate(byId.get(m.id) || m) : demo.refresh(m));
  S.featured = (iso) => (live() ? day(iso).map(hydrate).sort((a, b) => importance(b) - importance(a))[0] || null : demo.featured(iso));
  S.spotlight = (n = 8) => {
    if (!live()) return demo.spotlight(n);
    const all = [...day(isoDate(today())), ...day(isoDate(addDays(today(), 1)))].map(hydrate).filter((m) => m.status !== 'finished');
    return all.sort((a, b) => (b.status === 'live') - (a.status === 'live') || importance(b) - importance(a) || a.ko - b.ko).slice(0, n);
  };

  SA.services.liveSchedule = { load };
  api.ready.then(() => {
    if (!live()) return;
    load();
    // Every minute while a match is on and the tab is visible
    setInterval(() => { if (loadedAt && !document.hidden && hot() && Date.now() - loadedAt >= LIVE_EVERY - 5000) quiet(); }, 15000);
  });
})(window.SA);
