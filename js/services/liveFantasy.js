/**
 * Fantasy and content from the real API (backend Phase 6). Loaded after the fantasy, content and support
 * services.
 *
 * In API mode this file loads the server's data into the shapes the pages already read and sends the pages' changes
 * to the server first, then loads again. Demo mode (GitHub Pages) is unchanged and the design stays the same.
 *   - Fantasy: players, clubs, fixtures and gameweeks (cached from API-FOOTBALL or the simulated provider), my team,
 *     lineup, transfers, groups and the free leagues. Gameweeks are scored by the server.
 *   - Content: home banners, the announcement bar, pop-ups (views and clicks counted on the server).
 *   - Support: my tickets and replies.
 */
(function (SA) {
  const api = SA.services.api;
  const store = SA.core.store;
  const db = SA.services.db;
  const { ApiError } = SA.services.http;
  const F = SA.services.fantasy;
  const live = () => api.isLive();
  const ar = () => SA.i18n?.lang === 'ar';
  const signedIn = () => !!SA.services.session?.me();
  const toast = (msg) => SA.ui?.overlay?.toast?.(msg, { type: 'error' });
  const quiet = (p) => p.catch((e) => { console.warn('[live]', e?.message || e); return null; });

  /* ======================================================================
   * Fantasy
   * ==================================================================== */

  const LEAGUES = SA.data.fantasyLeagues;
  const STATE_KEY = { egy: 'fantasy', epl: 'fantasyEpl' };
  const POS_STATUS = { Available: 'fit', Doubtful: 'doubt', Injured: 'injured', LeftTheClub: 'injured' };
  const demo = { fantasy: { ...F } };
  const server = { egy: null, epl: null };   // per league: { competition, clubs, players, team, groups, tables }
  let pass = null;                           // GET /fantasy/pass

  const key = () => F.league();
  const S = (k = key()) => server[k];
  const blank = () => ({ team: null, joinedLeagues: [], createdGroups: [] });
  const fstate = (k = key()) => store.get()[STATE_KEY[k]] || blank();
  const setF = (k, patch) => store.set((s) => ({ [STATE_KEY[k]]: { ...(s[STATE_KEY[k]] || blank()), ...patch } }), 'fantasy');
  const pid = (id) => String(id);

  /** Replaces the demo dataset of a league in place with the server's (pages keep the same arrays and objects). */
  const applyBootstrap = (k, boot, fixtures) => {
    const D = LEAGUES[k];
    const shortOf = Object.fromEntries(boot.clubs.map((c) => [c.id, c.shortName]));
    D.teams.splice(0, D.teams.length, ...boot.clubs.map((c) => ({ id: c.shortName, apiId: c.id, name: c.name, city: '', strength: 75, kit: { a: c.kitColor, b: c.kitColor2, p: 'solid' } })));
    D.players.splice(0, D.players.length, ...boot.players.map((p) => ({
      id: pid(p.id), apiId: p.id, teamId: shortOf[p.clubId] || p.club, name: p.name, pos: p.position, price: +p.price, total: p.totalPoints, form: +p.form,
      selectedBy: +p.selectedBy, status: POS_STATUS[p.status] || 'fit', news: '', gw: {}, log: {},
      stats: { goals: p.goals, assists: p.assists, cleanSheets: p.cleanSheets, minutes: p.minutes, bonus: p.bonus, yellow: p.yellowCards, saves: p.saves },
    })));
    D.fixtures.splice(0, D.fixtures.length, ...fixtures.map((f, i) => ({
      id: `fx-${f.id}`, apiId: f.id, gw: f.gameweek, home: shortOf[f.homeClubId], away: shortOf[f.awayClubId], slot: i, kickoffAt: f.kickoffAt,
      ...(f.status === 'Finished' ? { hs: f.homeGoals, as: f.awayGoals, played: true } : {}),
    })));
    D.currentGW = boot.competition.gameweek || (boot.competition.lastFinishedGameweek || 0) + 1;
    D.totalGW = boot.competition.totalGameweeks || D.totalGW;
    D.season = boot.competition.season ? `${boot.competition.season}/${String(boot.competition.season + 1).slice(2)}` : D.season;
    D.rules.budget = +boot.competition.squadBudget;
    D.rules.maxPerTeam = boot.competition.maxPerClub;
    D.applyRounds = () => {};
    D.playFixture = (fx) => fx;
    D.live = true;
    reindex(k);
  };

  /** The fantasy service indexes players by id when a league is chosen; refresh that index in place. */
  const reindex = (k) => {
    if (k !== key()) return;
    const D = LEAGUES[k];
    const byId = F.byId, teamById = F.teamById;
    Object.keys(byId).forEach((x) => delete byId[x]);
    Object.keys(teamById).forEach((x) => delete teamById[x]);
    D.players.forEach((p) => { byId[p.id] = p; });
    D.teams.forEach((t) => { teamById[t.id] = t; });
  };

  /** Server team → the local team the squad and lineup pages edit. */
  const toLocalTeam = (t, history) => {
    const picks = t.picks.slice().sort((a, b) => a.slot - b.slot);
    return {
      id: `FT-${t.id}`, apiId: t.id, name: t.name, squad: picks.map((p) => pid(p.playerId)), xi: picks.filter((p) => p.slot <= 11).map((p) => pid(p.playerId)),
      bench: picks.filter((p) => p.slot > 11).map((p) => pid(p.playerId)), captain: pid(picks.find((p) => p.isCaptain)?.playerId), vice: pid(picks.find((p) => p.isViceCaptain)?.playerId),
      freeTransfers: t.freeTransfers, hits: t.transfersCost, bank: +t.bank, chipsUsed: t.chipsUsed, activeChip: t.activeChip, createdGW: t.gameweek,
      history: (history?.gameweeks || []).map((g) => ({
        gw: g.gameweek, pts: g.points, raw: g.points + g.transfersCost, hits: g.transfersCost, rank: history.overallRank || g.rank || 0, gwRank: g.rank || 0,
        captain: pid(g.picks.find((p) => p.isCaptain)?.playerId), xi: g.picks.filter((p) => p.multiplier > 0).map((p) => pid(p.playerId)),
      })),
      live: true,
    };
  };

  const loadTeam = async (k) => {
    if (!signedIn()) return null;
    try {
      const t = await api.get(`/fantasy/${k}/team`);
      const h = await quiet(api.get(`/fantasy/${k}/team/history`));
      setF(k, { team: toLocalTeam(t, h) });
      return t;
    } catch (e) {
      if (e.status === 404 || e.status === 401) { setF(k, { team: null }); return null; }
      throw e;
    }
  };

  const loadGroups = async (k) => {
    if (!signedIn()) return;
    const o = await quiet(api.get(`/fantasy/${k}/groups`));
    if (!o) return;
    S(k).groups = o;
    const tables = await Promise.all(o.mine.map((g) => quiet(api.get(`/fantasy/groups/${g.id}/table`))));
    S(k).tables = Object.fromEntries(tables.filter(Boolean).map((t) => [t.group.id, t.rows]));
    setF(k, { joinedLeagues: o.official.filter((g) => g.joined).map((g) => `og-${g.id}`) });
  };

  const loadLeague = async (k) => {
    const [boot, fixtures] = await Promise.all([api.get(`/fantasy/${k}/bootstrap`), api.get(`/fantasy/${k}/fixtures`)]);
    server[k] = { competition: boot.competition, groups: null, tables: {} };
    applyBootstrap(k, boot, fixtures);
    await loadTeam(k);
    await loadGroups(k);
  };

  const loadPass = async () => {}; // no Fantasy Pass any more: every league is free

  /** Lineup of the local team → PUT /team/lineup (after a swap, formation or armband change). */
  const pushLineup = (k = key()) => {
    const t = fstate(k).team;
    if (!t?.live) return Promise.resolve();
    return api.put(`/fantasy/${k}/team/lineup`, { starters: t.xi.map(Number), bench: t.bench.map(Number), captainId: Number(t.captain), viceCaptainId: Number(t.vice) })
      .catch(async (e) => { await loadTeam(k); toast(e.message); });
  };

  /** Pairs outgoing and incoming players by position (the server takes transfers one for one). */
  const pairTransfers = (current, next) => {
    const out = current.filter((id) => !next.includes(id));
    const inn = next.filter((id) => !current.includes(id));
    return out.map((o) => {
      const i = inn.findIndex((x) => F.byId[x]?.pos === F.byId[o]?.pos);
      return { out: Number(o), in: Number(inn.splice(i, 1)[0]) };
    });
  };

  const officialOf = (g) => ({
    id: `og-${g.id}`, apiId: g.id, name: g.name, kind: 'free', participants: g.members, cap: 0, prizeValue: +g.prizeValue, scope: 'Season', from: 1,
    blurb: g.prizeText || '', color: 'ink', prizes: g.prizeText ? [['1st', g.prizeText]] : [], sponsor: g.sponsor ? { name: g.sponsor.name, color: g.sponsor.color } : null,
    joined: g.joined, code: g.inviteCode,
  });

  const grandOf = (k) => ({
    id: `grand-${k}`, name: 'Super App League', kind: 'official', participants: S(k)?.groups?.overall.managers || 0,
    prizeValue: 0, scope: 'Every gameweek', from: 1, blurb: 'Super App’s own league, run by the Super App team. Everyone who builds a team is in it. The top 3 of every gameweek win Super Points.', color: 'ink',
    prizes: demo.fantasy.weeklyPoints().map((p, i) => [`${['1st', '2nd', '3rd'][i]} every gameweek`, `${p.toLocaleString()} Super Points`]), joined: !!fstate(k).team,
  });

  const privateOf = (g, k) => ({
    id: `pg-${g.id}`, apiId: g.id, name: g.name, code: g.inviteCode, owner: g.isOwner ? 'You' : '', createdAt: String(g.createdAt).slice(0, 10), mine: g.isOwner,
    members: (S(k)?.tables?.[g.id] || []).map((r) => r.manager), live: true,
  });

  const standingsOf = (apiId) => (S()?.tables?.[apiId] || []).map((r) => ({ name: r.isMe ? (store.get().user?.name || r.manager) : r.manager, team: r.team, total: r.totalPoints, gw: r.lastGameweekPoints, isMe: r.isMe, rank: r.rank }));

  const fantasyLive = {
    meta() {
      if (!live() || !S()) return demo.fantasy.meta();
      const c = S().competition, D = LEAGUES[key()];
      return { gw: D.currentGW, deadline: c.deadline ? new Date(c.deadline) : new Date(), season: D.season, totalGW: D.totalGW, budget: D.rules.budget,
        managers: S().groups?.overall.managers || 0, league: D.key, leagueLabel: D.label };
    },
    use(k) {
      demo.fantasy.use(k);
      if (live() && server[k]) reindex(k);
    },
    async saveSquad(ids, opts = {}) {
      if (!live() || !S()) return demo.fantasy.saveSquad(ids, opts);
      const k = key(), cur = fstate(k).team;
      const errs = F.validateSquad(ids);
      if (errs.length) throw new ApiError(errs[0], 'invalid_squad', errs);
      if (!cur?.live) {
        const { xi, bench } = F.lineupFor(ids, '4-4-2');
        const lineup = { starters: xi.map(Number), bench: bench.map(Number), captainId: Number(xi[xi.length - 1]), viceCaptainId: Number(xi[xi.length - 2]) };
        await api.post(`/fantasy/${k}/team`, { name: (opts.name || '').trim() || `${store.get().user?.firstName || 'My'} FC`, picks: ids.map(Number), lineup });
        await loadTeam(k);
        return { created: true, team: fstate(k).team };
      }
      const transfers = pairTransfers(cur.squad, ids);
      if (!transfers.length) return { created: false, made: 0, hits: 0, team: cur };
      const r = await api.post(`/fantasy/${k}/team/transfers`, { transfers });
      await loadTeam(k);
      return { created: false, made: r.made, hits: r.hits, team: fstate(k).team };
    },
    rename(name) {
      demo.fantasy.rename(name);
      if (live() && fstate().team?.live) api.patch(`/fantasy/${key()}/team`, { name }).catch((e) => toast(e.message));
    },
    swap(a, b) { const err = demo.fantasy.swap(a, b); if (!err && live()) pushLineup(); return err; },
    setFormation(f) { demo.fantasy.setFormation(f); if (live()) pushLineup(); },
    setCaptain(id) { demo.fantasy.setCaptain(id); if (live()) pushLineup(); },
    setVice(id) { demo.fantasy.setVice(id); if (live()) pushLineup(); },
    async playChip(chip) {
      if (!live()) throw new ApiError('Chips are played on the server.', 'demo');
      await api.post(`/fantasy/${key()}/team/chips/${chip}`);
      await loadTeam(key());
      return fstate().team;
    },
    async simulateGameweek() {
      if (!live() || !S()) return demo.fantasy.simulateGameweek();
      throw new ApiError('Gameweeks are scored automatically after the matches.', 'live_scoring');
    },

    /* Groups and leagues */
    officialLeagues() {
      if (!live() || !S()?.groups) return demo.fantasy.officialLeagues();
      return [grandOf(key()), ...S().groups.official.map(officialOf)];
    },
    allOfficial(lg) {
      if (!live() || !server[lg || key()]?.groups) return demo.fantasy.allOfficial(lg);
      return server[lg || key()].groups.official.map(officialOf);
    },
    privateGroups() {
      if (!live() || !S()?.groups) return demo.fantasy.privateGroups();
      return S().groups.mine.filter((g) => g.type !== 'Official').map((g) => privateOf(g, key()));
    },
    group(id) {
      if (!live() || !S()?.groups) return demo.fantasy.group(id);
      const apiId = Number(String(id).replace(/^(pg|og)-/, ''));
      if (String(id).startsWith('grand-')) return { ...grandOf(key()), standings: [] };
      const g = [...S().groups.mine, ...S().groups.official].find((x) => x.id === apiId);
      if (!g) return null;
      return { ...(g.type === 'Official' ? officialOf(g) : privateOf(g, key())), standings: standingsOf(apiId) };
    },
    async createGroup(name) {
      if (!live() || !S()) return demo.fantasy.createGroup(name);
      const g = await api.post(`/fantasy/${key()}/groups`, { name });
      await loadGroups(key());
      return privateOf(g, key());
    },
    async joinGroupByCode(code) {
      if (!live() || !S()) return demo.fantasy.joinGroupByCode(code);
      const g = await api.post('/fantasy/groups/join', { inviteCode: code });
      await loadGroups(key());
      return privateOf(g, key());
    },
    leaveGroup(id) {
      if (!live() || !S()) return demo.fantasy.leaveGroup(id);
      return api.post(`/fantasy/groups/${String(id).replace(/^(pg|og)-/, '')}/leave`).then(() => loadGroups(key()));
    },
    async joinOfficial(id) {
      if (!live() || !S()?.groups) return demo.fantasy.joinOfficial(id);
      const g = S().groups.official.find((x) => `og-${x.id}` === id);
      if (!g) throw new ApiError('League not found.', 'not_found');
      await api.post('/fantasy/groups/join', { inviteCode: g.inviteCode });
      await loadGroups(key());
      return { ok: true };
    },

    /* Leagues are free (owner, 2026-11-03): no Fantasy Pass */
    pass: () => null, passCovers: () => true, passPlans: () => [],
  };

  /** POST with an Idempotency-Key (one purchase per click, even when the network repeats it). */
  const fetchWithKey = (path, body) => api.post(path, body, { headers: { 'Idempotency-Key': `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}` } });

  Object.keys(fantasyLive).forEach((name) => { F[name] = fantasyLive[name]; });

  /* ======================================================================
   * Content: banners, announcement, pop-ups
   * ==================================================================== */

  const C = SA.services.content;
  const demoContent = { ...C };
  let home = null, popups = null;

  const toPopup = (p) => ({
    id: `pop-${p.id}`, apiId: p.id, active: p.isActive, shape: p.shape, icons: p.icons, color: p.color, badge: p.badge,
    title: ar() ? p.titleAr : p.titleEn, text: ar() ? p.textAr : p.textEn, cta: ar() ? p.ctaAr : p.ctaEn, link: p.link, forNonSellers: p.hideFromSellers,
  });

  Object.assign(C, {
    banners() {
      if (!live() || !home) return demoContent.banners();
      return home.banners.map((b) => ({ id: `bn-${b.id}`, title: b.title, badge: b.badge, cta: b.ctaText, link: b.link, image: b.imageKey, active: b.isActive, order: b.sortOrder, from: b.showFrom, to: b.showUntil }));
    },
    announcement() {
      if (!live() || !home) return demoContent.announcement();
      const a = home.announcement;
      return a ? { on: true, text: a.text, link: a.link, cta: a.cta, tone: a.tone, until: a.until, v: a.text } : null;
    },
    promoPops() {
      if (!live() || !popups) return demoContent.promoPops();
      const t = popups.timing;
      return { on: t.on, first: t.first, interval: Math.max(5, t.interval), stay: Math.max(4, t.stay), max: t.max, items: popups.items.map(toPopup) };
    },
    promoStat(id, kind) {
      const p = live() && popups?.items.find((x) => `pop-${x.id}` === id);
      if (!p) return demoContent.promoStat(id, kind);
      quiet(api.post(`/content/popups/${p.id}/event`, { kind }));
      return undefined;
    },
  });

  const loadContent = async () => {
    [home, popups] = await Promise.all([quiet(api.get('/content/home')), quiet(api.get('/content/popups'))]);
  };

  /* ======================================================================
   * Support: my tickets
   * ==================================================================== */

  const SP = SA.services.support;
  const demoSupport = { ...SP };
  const TOPIC = { booking: 'Booking', order: 'Order', print: 'Order', payment: 'Payment', account: 'Account', other: 'Other' };
  const CAT = { Booking: 'booking', Order: 'order', Payment: 'payment', Account: 'account', Fantasy: 'other', Seller: 'account', Partner: 'account', Other: 'other' };
  const TSTATUS = { Open: 'open', WaitingForTeam: 'open', WaitingForCustomer: 'pending', Resolved: 'resolved', Closed: 'resolved' };

  const toTicket = (t) => {
    const u = store.get().user || SA.data.user.profile;
    const ref = t.bookingCode ? { type: 'booking', id: t.bookingCode, label: `Booking ${t.bookingCode}` } : t.orderCode ? { type: 'order', id: t.orderCode, label: `Order ${t.orderCode}` } : null;
    return {
      id: t.code, category: CAT[t.topic] || 'other', subject: t.subject, from: { kind: 'customer', id: u.id, name: u.name }, ref, against: null, amount: +(t.disputeAmount || 0),
      status: TSTATUS[t.status] || 'open', priority: String(t.priority).toLowerCase(), refund: null, unread: t.unread,
      messages: (t.messages || []).map((m) => ({ by: m.fromTeam ? 'admin' : 'customer', name: m.sender, text: m.body, at: new Date(m.createdAt).getTime() })),
      createdAt: new Date(t.createdAt).getTime(), updatedAt: Date.now(), live: true,
    };
  };

  const upsertTicket = (t) => {
    const rec = toTicket(t);
    if (db.find('tickets', rec.id)) db.update('tickets', rec.id, rec); else db.insert('tickets', rec);
    return rec;
  };

  const loadTickets = async () => {
    if (!signedIn()) return;
    const mine = await quiet(api.get('/support/tickets/mine'));
    if (!mine) return;
    const full = await Promise.all(mine.map((t) => quiet(api.get(`/support/tickets/${t.code}`))));
    full.filter(Boolean).forEach(upsertTicket);
  };

  Object.assign(SP, {
    async create(data) {
      if (!live() || (data.from && data.from.kind !== 'customer')) return demoSupport.create(data);
      const ref = data.ref?.id;
      const t = await api.post('/support/tickets', {
        topic: TOPIC[data.category] || 'Other', subject: data.subject, message: data.text, openedFrom: 'Site',
        bookingCode: data.ref?.type === 'booking' ? ref : null, orderCode: data.ref?.type === 'order' ? ref : null, disputeAmount: null,
      });
      return upsertTicket(t);
    },
    async reply(id, text, from = null) {
      if (!live() || !db.find('tickets', id)?.live || from) return demoSupport.reply(id, text, from);
      await api.post(`/support/tickets/${id}/messages`, { body: text });
      upsertTicket(await api.get(`/support/tickets/${id}`));
      return undefined;
    },
  });

  /* ======================================================================
   * Leagues on offer: only those the server has fantasy for (Egyptian fantasy is paused, decisions 2026-10-27)
   * ==================================================================== */

  let leaguesKnown = false;
  F.leagues = () => (live() && leaguesKnown ? demo.fantasy.leagues().filter((l) => server[l.key]) : demo.fantasy.leagues());
  F.use = (k) => (live() && leaguesKnown && !server[k] ? undefined : demo.fantasy.use(k));
  /** After the first load: a visitor who had a paused league selected moves to one that is on */
  const settleLeague = () => {
    leaguesKnown = true;
    const on = ['egy', 'epl'].filter((k) => server[k]);
    if (on.length && !server[F.league()]) demo.fantasy.use(on[0]);
  };

  /* ======================================================================
   * Start
   * ==================================================================== */

  api.ready.then(async (mode) => {
    if (mode !== 'api') return;
    await quiet(SA.services.session?.load() || Promise.resolve());
    // Signing in or out changes my team, pass, groups and tickets
    SA.services.session?.subscribe(() => quiet(Promise.all(['egy', 'epl'].filter((k) => server[k]).map((k) => loadTeam(k).then(() => loadGroups(k))).concat([loadPass(), loadTickets()]))));
    await Promise.all([
      quiet(loadLeague('egy')), quiet(loadLeague('epl')), quiet(loadPass()), quiet(loadContent()), quiet(loadTickets()),
    ]);
    settleLeague();
    store.set({ liveFantasyAt: Date.now() }, 'fantasy');
  });

  SA.services.liveFantasy = { reload: () => Promise.all([loadLeague(key()), loadPass(), loadContent(), loadTickets()]) };
})(window.SA);
