/**
 * Games, join requests, player record and slot alerts — built on top of "Find players" (matchService).
 *
 * - A player asks to join a game (position + message) and pays their share online straight away; Super App holds it.
 *   The host accepts or declines (or turns on auto-accept). Declined, withdrawn or removed players are refunded in full.
 * - The host controls the game: auto-accept on/off, close requests, remove a player, a waitlist when it's full.
 * - After kick-off the host rates players (stars + showed up / no-show). That builds each player's record:
 *   games played, attendance and average rating — shown on player cards and used later (matching, trust, freelancers).
 * - Slot alerts: a player follows a pitch or an area (optionally a time window) and is notified when a free slot that
 *   matches is posted in "Pitches & free slots".
 *
 * Demo: only this device's shopper is real. Other players are simulated (their requests, payments and the decisions of
 * other hosts), so every side of the flow can be tried on one phone.
 * Data: db.gameRequests, db.playerRatings, settings.games (blocks), state.slotAlerts.
 * Future API: POST /games/:id/requests, PATCH /requests/:id, POST /games/:id/ratings, /me/alerts
 */
(function (SA) {
  const { ApiError } = SA.services.http;
  const { seeded, relDay, fmtHour } = SA.core.utils;
  const db = () => SA.services.db;
  const store = SA.core.store;
  const M = () => SA.services.matches;
  const POSITIONS = ['Goalkeeper', 'Defender', 'Midfielder', 'Winger', 'Forward', 'Any position'];
  const me = () => store.get().user || SA.data.user.profile;
  const uid = (p) => `${p}-${Date.now().toString(36)}${Math.random().toString(36).slice(2, 5)}`;

  const requests = () => db().all('gameRequests') || [];
  const ratings = () => db().all('playerRatings') || [];
  const blocks = () => (db().settings().games?.blocked || []).filter((b) => !b.until || b.until > Date.now());

  /** Simulated people (for other hosts and applicants), stable per name */
  const PEOPLE = SA.data.matches.people;
  const simPlayer = (name) => {
    const r = seeded(`pl|${name}`);
    return { id: `sim-${name.toLowerCase().replace(/[^a-z]+/g, '-')}`, name, avatar: Math.floor(r * 15), level: SA.data.matches.levels[Math.floor(r * 4)], position: POSITIONS[Math.floor(seeded(`pos|${name}`) * 5)], sim: true };
  };

  /** A player's record: games, attendance and rating (real ratings + a simulated history for demo people) */
  const record = (player) => {
    const mine = ratings().filter((x) => x.playerId === player.id);
    const base = player.sim ? { games: 4 + Math.floor(seeded(`g|${player.name}`) * 40), showed: 0, stars: 0, n: 0 } : { games: me().matchesPlayed || 0, showed: 0, stars: 0, n: 0 };
    if (player.sim) { base.n = Math.max(2, Math.floor(base.games * 0.6)); base.showed = Math.round(base.n * (0.7 + seeded(`a|${player.name}`) * 0.3)); base.stars = base.n * (3.4 + seeded(`s|${player.name}`) * 1.5); }
    const n = base.n + mine.length, showed = base.showed + mine.filter((x) => x.showed).length;
    const starList = mine.filter((x) => x.showed && x.stars);
    const stars = base.stars + starList.reduce((t, x) => t + x.stars, 0), sn = base.n + starList.length;
    const attendance = n ? showed / n : null;
    return { games: base.games + mine.length, rated: n, attendance, rating: sn ? stars / sn : null, noShows: n - showed,
      reliable: attendance == null ? 'new' : attendance >= 0.9 ? 'high' : attendance >= 0.7 ? 'ok' : 'low' };
  };

  /** Is this player blocked from joining games by Super App? */
  const blockedFor = (playerId) => blocks().find((b) => b.playerId === playerId) || null;

  /** Host settings: the shopper's own published games live in the device store; others are demo hosts */
  const hostSettings = (m) => (m.isMine ? { autoAccept: !!m.autoAccept, closed: !!m.closed, positions: m.positions || '' } : { autoAccept: seeded(`aa|${m.id}`) < 0.5, closed: false, positions: m.needsGK ? 'Goalkeeper' : '', demoHost: true });
  const setMine = (matchId, patch) => store.set((s) => ({ publishedMatches: s.publishedMatches.map((x) => (x.id === matchId ? { ...x, ...(typeof patch === 'function' ? patch(x) : patch) } : x)) }), 'match');

  /** Add an accepted demo applicant to the shopper's own game */
  const addParticipant = (matchId, p, position) => setMine(matchId, (x) => ({ participants: [...x.participants, { name: p.name, avatar: p.avatar, role: position, photo: true, playerId: p.id }], filled: (x.filled || x.participants.length) + 1 }));
  const removeParticipant = (matchId, playerId) => setMine(matchId, (x) => ({ participants: x.participants.filter((pp) => pp.playerId !== playerId), filled: Math.max(1, (x.filled || x.participants.length) - 1) }));

  const notifyMe = (n) => SA.services.user.notify({ kind: 'match', ...n });

  const games = {
    POSITIONS, record, simPlayer, hostSettings, blockedFor,
    meAsPlayer: () => ({ id: me().id, name: me().name, avatar: me().avatar ?? 0, level: me().level, position: me().position }),

    /** Requests for one game (newest first) with each player's record */
    forMatch: (matchId) => requests().filter((r) => r.matchId === matchId).map((r) => ({ ...r, record: record(r.player) })).sort((a, b) => b.at - a.at),
    myRequest: (matchId) => requests().find((r) => r.matchId === matchId && r.player.id === me().id && ['pending', 'accepted', 'waitlist'].includes(r.status)) || null,
    myRequests: () => requests().filter((r) => r.player.id === me().id),

    /** Ask to join: the share is paid online now and held by Super App until the host decides */
    async request(matchId, { position, message = '', payment }) {
      SA.services.user.assertActive();
      const b = blockedFor(me().id); if (b) throw new ApiError(`You can’t join games right now: ${b.reason}`, 'blocked');
      const m = M().allSync().find((x) => x.id === matchId);
      if (!m) throw new ApiError('This game is no longer listed.', 'not_found');
      if (m.isMine) throw new ApiError('This is your own game.', 'invalid');
      if (games.myRequest(matchId)) throw new ApiError('You already asked to join this game.', 'already');
      const hs = hostSettings(m);
      if (hs.closed) throw new ApiError('The host closed requests for this game.', 'closed');
      if (!POSITIONS.includes(position)) throw new ApiError('Choose your position.', 'invalid');
      if (!payment || payment.method === 'cash') throw new ApiError('Pay your share online — Super App holds it until the host accepts.', 'invalid');
      const full = m.spotsLeft <= 0;
      const paid = await SA.services.payment.charge({ ...payment, amount: m.pricePerPlayer, label: `Game at ${m.pitch.name} (held until the host accepts)` });
      const r = db().insert('gameRequests', { matchId, hostName: m.hostName, pitchName: m.pitch.name, date: m.date, hour: m.hour, player: games.meAsPlayer(), position, message: String(message).trim().slice(0, 200),
        status: full ? 'waitlist' : 'pending', amount: m.pricePerPlayer, paid: true, payRef: paid?.id || '', at: Date.now() }, 'gr');
      if (!full && hs.autoAccept) games._accept(r.id, 'auto');
      else if (!full) notifyMe({ title: 'Request sent', body: `${m.hostName} will accept or decline. ${m.pricePerPlayer} EGP is held and comes back in full if you’re not accepted.`, link: `/players/${matchId}` });
      return db().find('gameRequests', r.id);
    },
    /** Withdraw a request (refund), or leave after being accepted (refund up to 6 hours before) */
    async withdraw(requestId) {
      const r = db().find('gameRequests', requestId); if (!r || r.player.id !== me().id) throw new ApiError('Request not found.', 'not_found');
      const m = M().allSync().find((x) => x.id === r.matchId);
      const late = m && m.startsAt - Date.now() < 6 * 36e5;
      const refund = r.status !== 'accepted' || !late;
      if (r.status === 'accepted') store.set((s) => ({ joinedMatchIds: s.joinedMatchIds.filter((x) => x !== r.matchId) }), 'match');
      if (refund && r.paid) await SA.services.payment.refund(r.amount, `Refund — game at ${r.pitchName}`);
      db().update('gameRequests', r.id, { status: 'withdrawn', decidedAt: Date.now(), refunded: refund });
      return { refunded: refund };
    },

    /** Host decisions on the shopper's own game */
    accept(requestId) { return games._accept(requestId, 'host'); },
    _accept(requestId, by) {
      const r = db().find('gameRequests', requestId); if (!r) throw new ApiError('Request not found.', 'not_found');
      const m = M().allSync().find((x) => x.id === r.matchId);
      if (m && m.spotsLeft <= 0 && by !== 'admin') throw new ApiError('The game is full. Remove someone first, or keep them on the waitlist.', 'full');
      db().update('gameRequests', r.id, { status: 'accepted', decidedAt: Date.now(), by });
      if (r.player.id === me().id) {
        store.set((s) => ({ joinedMatchIds: [...new Set([...s.joinedMatchIds, r.matchId])], matchRequests: { ...s.matchRequests, [r.matchId]: { status: 'joined', at: Date.now(), payment: 'Paid online' } } }), 'match');
        notifyMe({ title: by === 'auto' ? 'You’re in!' : 'The host accepted you', body: `${r.pitchName}, ${relDay(r.date)} at ${fmtHour(r.hour)}. Your share is paid.`, link: `/players/${r.matchId}` });
      } else if (m?.isMine) addParticipant(r.matchId, r.player, r.position);
      return db().find('gameRequests', r.id);
    },
    async decline(requestId, reason = '') {
      const r = db().find('gameRequests', requestId); if (!r) throw new ApiError('Request not found.', 'not_found');
      db().update('gameRequests', r.id, { status: 'declined', decidedAt: Date.now(), reason: String(reason).trim() });
      if (r.player.id === me().id) { if (r.paid) await SA.services.payment.refund(r.amount, `Refund — not accepted for ${r.pitchName}`); notifyMe({ title: 'Not accepted this time', body: `${r.pitchName} — your ${r.amount} EGP is back in your wallet.${reason ? ` “${reason}”` : ''}`, link: '/players' }); }
    },
    /** Remove an accepted player (refunded in full) */
    async remove(requestId, reason = '', by = 'host') {
      const r = db().find('gameRequests', requestId); if (!r || r.status !== 'accepted') throw new ApiError('Only accepted players can be removed.', 'invalid');
      db().update('gameRequests', r.id, { status: 'removed', decidedAt: Date.now(), reason: String(reason).trim(), by });
      const m = M().allSync().find((x) => x.id === r.matchId);
      if (m?.isMine) removeParticipant(r.matchId, r.player.id);
      if (r.player.id === me().id) { store.set((s) => ({ joinedMatchIds: s.joinedMatchIds.filter((x) => x !== r.matchId) }), 'match'); await SA.services.payment.refund(r.amount, `Refund — removed from the game at ${r.pitchName}`); notifyMe({ title: 'You were removed from a game', body: `${r.pitchName}. Your ${r.amount} EGP is back in your wallet.`, link: '/players' }); }
      // Someone on the waitlist moves up
      const next = requests().filter((x) => x.matchId === r.matchId && x.status === 'waitlist').sort((a, b) => a.at - b.at)[0];
      if (next) db().update('gameRequests', next.id, { status: 'pending' });
    },
    setHost(matchId, patch) { setMine(matchId, patch); },

    /** Demo: players nearby ask to join your newly published game */
    simulateApplicants(matchId, n = 3) {
      const m = M().allSync().find((x) => x.id === matchId); if (!m) return;
      const taken = new Set([...m.participants.map((p) => p.name), me().name]);
      const pool = PEOPLE.filter((x) => !taken.has(x));
      const msgs = ['I can be there 15 minutes early.', 'Played 7-a-side every week last year.', 'Happy to play anywhere.', 'I’ll bring a ball too.', ''];
      for (let i = 0; i < n; i++) {
        const name = pool[(Math.floor(seeded(`${matchId}|ap`) * pool.length) + i * 5) % pool.length];
        const p = simPlayer(name);
        const r = db().insert('gameRequests', { matchId, hostName: me().name, pitchName: m.pitch.name, date: m.date, hour: m.hour, player: p, position: i === 0 && m.needsGK ? 'Goalkeeper' : p.position, message: msgs[i % msgs.length], status: 'pending', amount: m.pricePerPlayer, paid: true, at: Date.now() - (n - i) * 7 * 6e4 }, 'gr');
        if (hostSettings(m).autoAccept && M().allSync().find((x) => x.id === matchId).spotsLeft > 0) games._accept(r.id, 'auto');
      }
      notifyMe({ title: `${n} player${n > 1 ? 's' : ''} asked to join your game`, body: `${m.pitch.name}, ${relDay(m.date)} at ${fmtHour(m.hour)}. Accept or decline them.`, link: `/players/${matchId}` });
    },
    /** Demo: hosts who decide by hand answer pending requests after a short while */
    sweep() {
      requests().filter((r) => r.status === 'pending' && r.player.id === me().id && Date.now() - r.at > 25e3).forEach((r) => {
        const m = M().allSync().find((x) => x.id === r.matchId);
        if (!m || m.isMine) return;
        if (m.spotsLeft > 0) games._accept(r.id, 'host'); else games.decline(r.id, 'The game filled up.');
      });
    },

    /** After kick-off the host rates the players who were accepted */
    canRate: (m) => m.isMine && m.startsAt <= Date.now(),
    rate(matchId, list) {
      const m = M().allSync().find((x) => x.id === matchId) || store.get().publishedMatches.find((x) => x.id === matchId);
      if (!m) throw new ApiError('Game not found.', 'not_found');
      list.forEach(({ requestId, stars, showed }) => {
        const r = db().find('gameRequests', requestId); if (!r) return;
        const old = ratings().find((x) => x.matchId === matchId && x.playerId === r.player.id);
        const rec = { matchId, playerId: r.player.id, name: r.player.name, stars: showed ? Math.max(1, Math.min(5, Math.round(+stars || 0))) : 0, showed: !!showed, by: me().id, at: Date.now() };
        if (old) db().update('playerRatings', old.id, rec); else db().insert('playerRatings', rec, 'pr');
      });
      db().log('customer', 'game.rated', `${me().name} rated ${list.length} player${list.length > 1 ? 's' : ''} after the game at ${m.pitch?.name || ''}`, matchId);
    },
    ratingFor: (matchId, playerId) => ratings().find((x) => x.matchId === matchId && x.playerId === playerId) || null,

    /* ---------- Slot alerts ---------- */
    alerts: () => store.get().slotAlerts || [],
    addAlert({ pitchId = '', area = '', from = 0, to = 24 }) {
      if (!pitchId && !area) throw new ApiError('Choose a pitch or an area.', 'invalid');
      if (+from >= +to) throw new ApiError('The end time must be after the start time.', 'invalid');
      const a = { id: uid('al'), pitchId, area, from: +from, to: +to, at: Date.now() };
      store.set((s) => ({ slotAlerts: [a, ...(s.slotAlerts || [])].slice(0, 20) }), 'alerts');
      return a;
    },
    removeAlert(id) { store.set((s) => ({ slotAlerts: (s.slotAlerts || []).filter((a) => a.id !== id) }), 'alerts'); },
    /** Called when a free slot is posted: notify the shopper if one of their alerts matches */
    matchAlerts(slot) {
      const p = SA.services.pitches.getSync(slot.pitchId); if (!p) return 0;
      const hits = games.alerts().filter((a) => (a.pitchId ? a.pitchId === p.id : a.area === p.area || a.area === p.city) && slot.hour >= a.from && slot.hour < a.to);
      if (hits.length) notifyMe({ title: `Free slot: ${p.name}`, body: `${relDay(slot.date)} at ${fmtHour(slot.hour)}${slot.pct ? ` · ${Math.round(slot.pct * 100)}% off` : ''}. Book it before someone else does.`, link: `/book/${p.id}?date=${slot.date}&hour=${slot.hour}&dur=1` });
      return hits.length;
    },
  };

  SA.services.games = games;
  if (!db().all('gameRequests')) { db().raw().gameRequests = []; db().raw().playerRatings = []; try { db().touch('gameRequests'); } catch { /* storage full */ } }
  setInterval(() => { try { games.sweep(); } catch (e) { console.warn(e); } }, 10e3);
})(window.SA);
