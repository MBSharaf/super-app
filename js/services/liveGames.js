/**
 * Find players on the real API (backend 8.6e). Loaded after matchService, gameService and liveBookings.
 *
 * In API mode the games list, a game's page, "my games", posting a booking as a game, asking to join (the share is
 * paid on the server — card or wallet), the host's accept / decline / remove, auto-accept, closing requests,
 * cancelling and the ratings after kick-off all go to /games. The pages keep their design and call the same
 * service methods; the methods they call synchronously answer from a small cache that the async ones fill.
 * The server keeps the match type and who can join in the game's title ("5 v 5 · Men"). Demo mode is unchanged.
 */
(function (SA) {
  const api = SA.services.api;
  const M = SA.services.matches;
  const G = SA.services.games;
  if (!api || !M || !G) return;
  const { ApiError } = SA.services.http;
  const { parseISO, haversineKm } = SA.core.utils;
  const store = SA.core.store;
  const demo = { matches: { ...M }, games: { ...G } };
  const live = () => api.isLive();
  const signedIn = () => live() && !!SA.services.session?.me();

  const LEVEL = { Any: 'All levels', Casual: 'Beginner', Intermediate: 'Intermediate', Competitive: 'Competitive' };
  const LEVEL_BACK = { Beginner: 'Casual', Intermediate: 'Intermediate', Advanced: 'Competitive', Competitive: 'Competitive' };
  const REQ = { Waiting: 'pending', Accepted: 'accepted', Waitlisted: 'waitlist', Declined: 'declined', Withdrawn: 'withdrawn', Removed: 'removed' };
  const games = new Map();   // code → hydrated game
  const details = new Map(); // code → server detail (players, requests, my status, host settings)
  let mineCache = { hosting: [], joined: [] };

  const isLive = (id) => String(id).startsWith('GM-');
  const myId = () => SA.services.session?.me()?.id;

  /** Server card (+ detail when loaded) → the shape the pages read */
  const hydrate = (g, d = details.get(g.code)) => {
    const p = SA.services.pitches.getSync(`p-${g.pitchSlug}`) || {};
    const [type, gender] = (g.title || '').split(' · ');
    const start = parseISO(g.date); start.setHours(0, 0, 0, 0);
    const players = d?.players || [];
    const host = { name: g.hostName, avatar: 0, role: 'Host', photo: false, isMe: g.hostId === myId() };
    const participants = [host, ...players.map((r) => ({ name: r.playerName, avatar: 0, role: r.position || 'Player', photo: false, isMe: r.playerId === myId(), playerId: r.playerId }))];
    const joinedCount = Math.max(g.joined, players.length);
    const loc = store.get().location || SA.config.fallbackLocation;
    const status = d?.myStatus || mineCache.joined.find((x) => x.game.code === g.code)?.myStatus;
    return {
      id: g.code, live: true, apiGame: g, pitchId: `p-${g.pitchSlug}`, date: g.date, hour: g.startHour, duration: g.duration,
      pitch: { id: `p-${g.pitchSlug}`, name: g.pitchName, area: g.areaEn, city: p.city || '', image: p.images?.[0], lat: p.lat, lng: p.lng, format: p.format || '', surface: p.surface || '', address: p.address || '' },
      level: LEVEL[g.level] || g.level, type: type || '5 v 5', gender: gender || 'Mixed', needsGK: /goalkeeper/i.test(g.positions || ''), notes: g.notes || '',
      total: g.playersNeeded + 1, filled: joinedCount + 1, spotsLeft: Math.max(0, g.playersNeeded - joinedCount), participants: d ? participants : [host],
      pricePerPlayer: g.sharePrice, hostName: g.hostName, isMine: g.hostId === myId(), joined: status === 'Accepted', myStatus: status,
      autoAccept: !!d?.autoAccept, closed: g.requestsClosed, bookingId: d?.bookingCode || '', cancelled: g.status === 'Cancelled',
      startsAt: start.getTime() + g.startHour * 36e5, distanceKm: p.lat ? haversineKm(loc, p) : 0,
    };
  };
  const keep = (g) => { const m = hydrate(g); games.set(g.code, m); return m; };
  const keepDetail = (d) => { details.set(d.game.code, d); return keep(d.game); };
  const fetchDetail = (code) => api.get(`/games/${code}`).then(keepDetail);
  const loadMine = async () => {
    if (!signedIn()) { mineCache = { hosting: [], joined: [] }; return mineCache; }
    mineCache = await api.get('/games/mine');
    mineCache.hosting.forEach(keep); mineCache.joined.forEach((x) => keep(x.game));
    return mineCache;
  };
  /** After a change: the game, my games and the wallet again, then the page (some pages don't wait for the call) */
  const refresh = async (code) => { await Promise.all([fetchDetail(code), loadMine()]); SA.services.liveMoney?.sync(); SA.app?.rerender?.(); };

  /* ---------------- Matches service ---------------- */
  M.list = async (f = {}) => {
    await api.ready;
    if (!live()) return demo.matches.list(f);
    const [open] = await Promise.all([api.get('/games'), loadMine().catch(() => null)]);
    let out = open.map(keep);
    if (f.level) out = out.filter((m) => m.level === f.level || m.level === 'All levels');
    if (f.gender) out = out.filter((m) => m.gender === f.gender);
    const today = SA.core.utils.isoDate(new Date());
    if (f.when === 'today') out = out.filter((m) => m.date === today);
    if (f.when === 'tomorrow') out = out.filter((m) => m.date === SA.core.utils.isoDate(SA.core.utils.addDays(new Date(), 1)));
    if (f.when === 'week') out = out.filter((m) => parseISO(m.date) <= SA.core.utils.addDays(new Date(), 7));
    if (f.gk) out = out.filter((m) => m.needsGK);
    if (f.q) { const q = f.q.toLowerCase(); out = out.filter((m) => `${m.pitch.name} ${m.pitch.area} ${m.hostName}`.toLowerCase().includes(q)); }
    if (f.openOnly !== false) out = out.filter((m) => m.spotsLeft > 0 || m.joined || m.isMine);
    const sort = { soon: (a, b) => a.startsAt - b.startsAt, distance: (a, b) => a.distanceKm - b.distanceKm, spots: (a, b) => a.spotsLeft - b.spotsLeft };
    return out.sort(sort[f.sort] || sort.soon);
  };
  M.get = async (id) => {
    await api.ready;
    if (!isLive(id)) return demo.matches.get(id);
    if (signedIn()) await loadMine().catch(() => null);
    try { return await fetchDetail(id); } catch { throw new ApiError('This match is no longer listed. It may have filled up or been cancelled.', 'not_found'); }
  };
  M.allSync = () => (live() ? [...games.values()].filter((m) => !m.cancelled) : demo.matches.allSync());
  M.mine = async () => {
    await api.ready;
    if (!live()) return demo.matches.mine();
    const d = await loadMine().catch(() => mineCache);
    // The host's rows show their requests to answer: load each posted game's details (a host posts a few at most)
    await Promise.all(d.hosting.map((g) => fetchDetail(g.code).catch(() => null)));
    return { hosting: d.hosting.map((g) => games.get(g.code)), joined: d.joined.map((x) => games.get(x.game.code)) };
  };
  M.join = (id, payment, opts = {}) => (isLive(id) ? G.request(id, { ...opts, payment, waiver: true }) : demo.matches.join(id, payment, opts));
  M.leave = (id) => (isLive(id) ? G.withdraw(`mine:${id}`) : demo.matches.leave(id));
  /** Post a booking (its code) as a game: players needed = total − the players the host already has */
  M.publish = async (data) => {
    if (!live()) return demo.matches.publish(data);
    const b = SA.services.bookings.getSync(data.bookingId);
    if (!b) throw new ApiError('Pick one of your upcoming bookings first.', 'no_booking');
    const total = +data.total, have = +data.have;
    if (!(have >= 1 && have < total)) throw new ApiError('Players you already have must be fewer than the total.', 'invalid_counts');
    const d = await api.post('/games', {
      bookingCode: b.code || b.id, playersNeeded: total - have, level: LEVEL_BACK[data.level] || 'Any', positions: data.needsGK ? 'Goalkeeper' : null,
      title: `${data.type} · ${data.gender}`, sharePrice: Math.ceil(b.total / total), autoAccept: !!data.autoAccept, notes: (data.notes || '').trim() || null,
    });
    keepDetail(d);
    loadMine().catch(() => null);
    return { id: d.game.code };
  };
  M.unpublish = async (id) => {
    if (!isLive(id)) return demo.matches.unpublish(id);
    await api.post(`/games/${id}/cancel`);
    games.delete(id);
    await loadMine().catch(() => null);
    SA.services.liveMoney?.sync();
    return { ok: true };
  };

  /* ---------------- Games service (requests, host decisions, ratings) ---------------- */
  const record = (r) => ({ games: r.games, attendance: r.games ? r.showedUp / r.games : null, reliable: !r.games ? 'new' : r.showedUp / r.games >= 0.85 ? 'good' : r.showedUp / r.games >= 0.6 ? 'ok' : 'low',
    rating: r.averageStars ? +r.averageStars : null, noShows: r.games - r.showedUp });
  const fromRequest = (code, r) => ({
    id: `${code}:${r.id}`, apiId: r.id, matchId: code, status: REQ[r.status] || r.status, position: r.position || 'Any position', message: r.message || '', at: Date.parse(r.createdAt),
    player: { id: r.playerId, name: r.playerName, avatar: 0, level: '' }, record: record(r), amount: games.get(code)?.pricePerPlayer || 0,
  });
  G.forMatch = (id) => {
    if (!isLive(id)) return demo.games.forMatch(id);
    const d = details.get(id);
    return (d?.requests || []).map((r) => fromRequest(id, r)).sort((a, b) => b.at - a.at);
  };
  G.myRequest = (id) => {
    if (!isLive(id)) return demo.games.myRequest(id);
    const m = games.get(id);
    return m && ['Waiting', 'Waitlisted', 'Accepted'].includes(m.myStatus) ? { id: `mine:${id}`, matchId: id, status: REQ[m.myStatus], amount: m.pricePerPlayer } : null;
  };
  G.hostSettings = (m) => (m.live ? { autoAccept: m.autoAccept, closed: m.closed, positions: m.needsGK ? 'Goalkeeper' : '' } : demo.games.hostSettings(m));
  G.request = async (id, { position, message = '', payment, waiver = false }) => {
    if (!isLive(id)) return demo.games.request(id, { position, message, payment, waiver });
    SA.services.legal?.requireWaiver(waiver, 'joining a game');
    if (!payment || payment.method === 'cash') throw new ApiError('Pay your share online — Super App holds it until the host accepts.', 'invalid');
    const d = await api.post(`/games/${id}/requests`, { position, message: String(message).trim().slice(0, 200) || null, payFromWallet: payment.method === 'wallet' });
    keepDetail(d);
    await loadMine().catch(() => null);
    SA.services.liveMoney?.sync();
    if (d.paymentUrl) window.location.assign(d.paymentUrl);
    return { status: REQ[d.myStatus] || 'pending' };
  };
  G.withdraw = async (requestId) => {
    const code = String(requestId).replace(/^mine:/, '');
    if (!isLive(code)) return demo.games.withdraw(requestId);
    await api.del(`/games/${code}/requests/mine`);
    await refresh(code);
    return { refunded: true };
  };
  const decide = async (requestId, action) => {
    const [code, id] = String(requestId).split(':');
    await api.post(`/games/${code}/requests/${id}/${action}`);
    await refresh(code);
  };
  G.accept = (requestId) => (isLive(String(requestId).split(':')[0])
    ? decide(requestId, 'accept').catch((err) => SA.ui.overlay.toast(err.message, { type: 'error' })) : demo.games.accept(requestId));
  G.decline = (requestId, reason) => (isLive(String(requestId).split(':')[0]) ? decide(requestId, 'decline') : demo.games.decline(requestId, reason));
  G.remove = (requestId, reason, by) => (isLive(String(requestId).split(':')[0]) ? decide(requestId, 'remove') : demo.games.remove(requestId, reason, by));
  /** Auto-accept and close requests: PUT /games/{code} with the game's current values */
  G.setHost = (id, patch) => {
    if (!isLive(id)) return demo.games.setHost(id, patch);
    const m = games.get(id), g = m.apiGame;
    api.put(`/games/${id}`, { playersNeeded: g.playersNeeded, level: g.level, positions: g.positions, title: g.title, requestsClosed: patch.closed ?? g.requestsClosed, autoAccept: patch.autoAccept ?? m.autoAccept })
      .then(keepDetail, (err) => SA.ui.overlay.toast(err.message, { type: 'error' }));
  };
  G.simulateApplicants = (id, n) => (isLive(id) ? undefined : demo.games.simulateApplicants(id, n));
  G.ratingFor = (id, playerId) => (isLive(id) ? null : demo.games.ratingFor(id, playerId));
  G.rate = (id, list) => {
    if (!isLive(id)) return demo.games.rate(id, list);
    const reqs = G.forMatch(id);
    const ratings = list.map((x) => ({ playerId: reqs.find((r) => r.id === x.requestId)?.player.id, stars: x.stars, showedUp: x.showed })).filter((x) => x.playerId);
    api.post(`/games/${id}/ratings`, { ratings }).then(() => fetchDetail(id), (err) => SA.ui.overlay.toast(err.message, { type: 'error' }));
  };

  api.ready.then(() => { if (live()) SA.services.session?.subscribe?.(() => { details.clear(); loadMine().catch(() => null); }); });
})(window.SA);
