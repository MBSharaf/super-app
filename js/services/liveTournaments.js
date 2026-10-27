/**
 * Tournaments from the real API (backend 3.7b). Loaded after tournamentService and livePartner.
 *
 * In API mode the tournaments pages list the server's cups and leagues (site id `t-<id>`), registration sends the
 * team to the server (the entry fee is paid there: simulated at once, or the Paymob page), and the venue dashboard
 * creates, draws, scores and cancels through `/partner/tournaments`. The server's tournament is turned into the
 * demo's shape so the pages and their helpers (standings, bracket) stay the same. The organiser methods the pages
 * call synchronously answer from a cache and send the change in the background; the page re-renders when it lands.
 * Demo tournaments (GitHub Pages) are unchanged.
 */
(function (SA) {
  const api = SA.services.api;
  const T = SA.services.tournaments;
  const { ApiError } = SA.services.http;
  const { parseISO, isoDate, today } = SA.core.utils;
  const demo = { ...T };
  const live = () => api.isLive();
  const isLiveId = (id) => String(id).startsWith('t-');
  const apiId = (id) => String(id).replace(/^t-/, '');
  const byId = new Map();      // site id → site shape (public or organiser view)
  let mineCache = [];

  const STATUS = { open: 'open', full: 'full', running: 'running', finished: 'finished', cancelled: 'cancelled' };
  const pitchOf = (slug) => SA.services.pitches.getSync?.(`p-${slug}`) || SA.services.db.find('pitches', `p-${slug}`);

  /** Server tournament → the demo's raw tournament, then the same decorated view the pages read */
  const fromApi = (d) => {
    const p = pitchOf(d.pitchSlug);
    const teams = d.teams.map((x) => ({ id: x.id, name: x.name, captain: x.captain, phone: x.phone || '', players: 0, paid: x.paid, payment: x.paid ? 'Card' : 'Cash at venue', userId: x.mine ? 'me' : null, createdAt: Date.now(), status: x.status }));
    const fixtures = d.matches.filter((m) => d.status !== 'open' && d.status !== 'full').map((m) => ({
      id: m.id, round: m.round - 1, slot: m.slot, date: m.date, hour: m.hour, home: m.homeTeamId, away: m.awayTeamId,
      hs: m.homeGoals, as: m.awayGoals, pens: m.pensWinnerId, status: m.played ? 'played' : 'scheduled',
    }));
    const days = new Map();
    d.matches.forEach((m) => days.set(m.date, [...(days.get(m.date) || []), m.hour].sort((a, b) => a - b)));
    const reserved = [...days.entries()].sort(([a], [b]) => a.localeCompare(b)).map(([date, hours]) => ({ date, hours }));
    const active = teams.filter((x) => x.status === 'Registered');
    const firstDay = reserved[0]?.date || d.startsOn;
    const lastDay = reserved[reserved.length - 1]?.date || d.endsOn;
    const regOpen = ['open'].includes(d.status) && isoDate(today()) <= d.registrationClosesOn;
    const next = fixtures.filter((f) => f.status !== 'played').sort((a, b) => a.date.localeCompare(b.date) || a.hour - b.hour)[0] || null;
    return {
      id: `t-${d.id}`, live: true, apiId: d.id, ownerId: SA.services.livePartner?.ACCOUNT, pitchId: `p-${d.pitchSlug}`, name: d.name, format: d.type,
      teamSize: p?.format || '5-a-side', maxTeams: d.teamCount, fee: d.entryFee, prize: d.prizeText || '', startDate: d.startsOn, regDeadline: d.registrationClosesOn,
      hour: d.hour, rules: d.rulesText || '', cover: p?.images?.[0] || 'stringLights', status: STATUS[d.status] || d.status, teams: active, allTeams: teams, fixtures, reserved,
      pitch: p, pitchName: d.pitchName, area: p?.area || '', organiser: d.organiser, firstDay, lastDay, matchDays: reserved.length, spotsLeft: d.spotsLeft, regOpen,
      champion: d.championTeamId ? teams.find((x) => x.id === d.championTeamId)?.name : null, next, live: d.status === 'running' && firstDay <= isoDate(today()),
      mine: active.filter((x) => x.userId === 'me'), played: fixtures.filter((f) => f.status === 'played').length, startsIn: Math.ceil((parseISO(firstDay) - Date.now()) / 864e5),
      createdAt: Date.now(), table: d.table,
    };
  };
  const keep = (d) => { const t = fromApi(d); byId.set(t.id, t); return t; };
  const fail = (err) => SA.ui.overlay.toast(err.message, { type: 'error', duration: 6000 });

  /* ---------- Site ---------- */
  T.list = async (f = {}) => {
    await api.ready;
    if (!live()) return demo.list(f);
    const list = (await api.get(`/tournaments${f.status ? `?status=${f.status === 'running' ? 'running' : f.status}` : ''}`)).map(keep);
    return f.status === 'open' ? list.filter((t) => t.status === 'open' || t.status === 'full') : list;
  };
  T.get = async (id) => {
    await api.ready;
    if (!live() || !isLiveId(id)) return demo.get(id);
    return keep(await api.get(`/tournaments/${apiId(id)}`));
  };
  T.getSync = (id) => (isLiveId(id) ? byId.get(id) || null : demo.getSync(id));
  T.forPitch = (pitchId) => (live() ? [...byId.values()].filter((t) => t.pitchId === pitchId && ['open', 'full', 'running'].includes(t.status)) : demo.forPitch(pitchId));
  T.teamName = (t, id) => (t.allTeams || t.teams).find((x) => x.id === id)?.name || 'TBD';

  /** The captain is the signed-in account; the entry fee is paid on the server (Paymob opens its page) */
  T.register = async (id, d) => {
    if (!isLiveId(id)) return demo.register(id, d);
    SA.services.legal?.requireWaiver(d.waiver, 'tournament entry');
    const r = await api.post(`/tournaments/${apiId(id)}/teams`, { teamName: (d.team || '').trim(), phone: (d.phone || '').trim(), acceptWaiver: !!d.waiver, payFromWallet: d.payment?.method === 'wallet' });
    keep(r);
    SA.services.liveMoney?.sync();
    if (r.paymentUrl) window.location.assign(r.paymentUrl);
    return { name: (d.team || '').trim() };
  };

  /* ---------- Organiser (dashboard) ---------- */
  const isLivePartner = () => live() && SA.services.dash.auth?.current?.()?.id === SA.services.livePartner?.ACCOUNT;
  const reloadMine = () => api.get('/partner/tournaments').then((list) => { mineCache = list.map(keep); SA.app?.rerender?.(); }, () => {});
  const send = (promise, after) => promise.then((d) => { if (d?.id) keep(d); reloadMine(); after?.(d); }, fail);

  let loaded = false;
  /** The first read (a direct page load) fetches the venue's tournaments and re-renders */
  T.mine = (opts = {}) => {
    if (!isLivePartner()) return demo.mine(opts);
    if (!loaded) { loaded = true; reloadMine(); }
    return mineCache;
  };
  T.manage = (id) => {
    if (!isLiveId(id)) return demo.manage(id);
    const t = byId.get(id);
    if (!t) { api.get(`/partner/tournaments/${apiId(id)}`).then((d) => { keep(d); SA.app?.rerender?.(); }, fail); throw new ApiError('Loading the tournament…', 'loading'); }
    return t;
  };
  T.create = (d) => {
    if (!isLivePartner()) return demo.create(d);
    const p = SA.services.db.find('pitches', d.pitchId);
    if (!p?.apiId) throw new ApiError('Choose one of your pitches.', 'invalid', { pitchId: 'Required' });
    send(api.post('/partner/tournaments', {
      pitchId: p.apiId, name: (d.name || '').trim(), type: d.format, teamCount: +d.maxTeams, entryFee: +d.fee || 0, prizeText: (d.prize || '').trim() || null,
      startsOn: d.startDate, hour: +d.hour, rulesText: (d.rules || '').trim(),
    }), (t) => SA.core.router.go(`/dash/tournaments/t-${t.id}`));
    return { id: '', name: (d.name || '').trim() };
  };
  T.addTeam = (id, d) => {
    if (!isLiveId(id)) return demo.addTeam(id, d);
    send(api.post(`/partner/tournaments/${apiId(id)}/teams`, { teamName: (d.name || '').trim(), phone: (d.phone || '').trim() || null }));
    return { name: (d.name || '').trim() };
  };
  T.removeTeam = async (id, teamId) => {
    if (!isLiveId(id)) return demo.removeTeam(id, teamId);
    keep(await api.del(`/partner/tournaments/${apiId(id)}/teams/${teamId}`));
    reloadMine();
  };
  T.generate = (id) => {
    if (!isLiveId(id)) return demo.generate(id);
    const t = byId.get(id);
    send(api.post(`/partner/tournaments/${apiId(id)}/draw`));
    return t?.reserved.flatMap((r) => r.hours) || [];
  };
  T.setResult = (id, fixtureId, { hs, as, pens }) => {
    if (!isLiveId(id)) return demo.setResult(id, fixtureId, { hs, as, pens });
    send(api.put(`/partner/tournaments/${apiId(id)}/matches/${fixtureId}`, { homeGoals: +hs, awayGoals: +as, pensWinnerId: pens ? +pens : null }),
      (d) => { if (d.status === 'finished') SA.ui.overlay.toast(`Finished — ${keep(d).champion} are the champions!`, { type: 'success' }); });
    return { status: 'running' };
  };
  T.cancel = async (id) => {
    if (!isLiveId(id)) return demo.cancel(id);
    keep(await api.post(`/partner/tournaments/${apiId(id)}/cancel`));
    reloadMine();
  };

  SA.services.liveTournaments = { reloadMine };
  api.ready.then(() => { if (live()) SA.services.session?.subscribe(() => { if (isLivePartner()) reloadMine(); }); });
  window.addEventListener('hashchange', () => { if (/#\/(dash|admin)\/tournaments/.test(location.hash) && isLivePartner()) reloadMine(); });
})(window.SA);
