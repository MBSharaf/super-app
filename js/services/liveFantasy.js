/**
 * Fantasy and content from the real API (backend Phase 6). Loaded after the fantasy, news, content, community and
 * support services.
 *
 * In API mode this file loads the server's data into the shapes the pages already read and sends the pages' changes
 * to the server first, then loads again. Demo mode (GitHub Pages) is unchanged and the design stays the same.
 *   - Fantasy: players, clubs, fixtures and gameweeks (cached from API-FOOTBALL or the simulated provider), my team,
 *     lineup, transfers, groups, prize leagues and the Fantasy Pass. Gameweeks are scored by the server.
 *   - Content: home banners, the announcement bar, pop-ups (views and clicks counted on the server), Super App news.
 *   - Communities: communities, posts, comments, reactions, polls, reports, join / leave.
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

  const loadPass = async () => { pass = signedIn() ? await quiet(api.get('/fantasy/pass')) : null; };

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
    id: `og-${g.id}`, apiId: g.id, name: g.name, kind: 'pass', participants: g.members, cap: 0, prizeValue: +g.prizeValue, scope: 'Season', from: 1,
    blurb: g.prizeText || '', color: 'ink', prizes: g.prizeText ? [['1st', g.prizeText]] : [], sponsor: g.sponsor ? { name: g.sponsor.name, color: g.sponsor.color } : null,
    joined: g.joined, code: g.inviteCode,
  });

  const grandOf = (k) => ({
    id: `grand-${k}`, name: k === 'epl' ? 'Super App Premier League Cup' : 'Super App Grand League', kind: 'official', participants: S(k)?.groups?.overall.managers || 0,
    prizeValue: 0, scope: 'Season', from: 1, blurb: 'Everyone who builds a team is entered automatically.', color: 'ink', prizes: [], joined: !!fstate(k).team,
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

    /* Fantasy Pass */
    passPlans() {
      if (!live() || !pass) return demo.fantasy.passPlans();
      const label = (p) => (p === 'both' ? 'Both leagues' : LEAGUES[p]?.label || p);
      return pass.plans.map((p) => ({ id: p.plan, label: label(p.plan), leagues: p.plan === 'both' ? ['egy', 'epl'] : [p.plan], list: +p.listPrice, price: +p.price, discount: +p.discount, best: p.plan === 'both' }));
    },
    pass() {
      if (!live() || !pass) return demo.fantasy.pass();
      const ends = Object.fromEntries(Object.entries(pass.coversUntil).filter(([, v]) => v).map(([k, v]) => [k, new Date(v).getTime()]));
      const leagues = Object.keys(ends);
      if (!leagues.length) return null;
      return { leagues, until: ends[key()] || Math.max(...Object.values(ends)), ends };
    },
    passCovers(lg = key()) { return !live() || !pass ? demo.fantasy.passCovers(lg) : !!pass.coversUntil[lg]; },
    async buyPass(planId, payment) {
      if (!live()) return demo.fantasy.buyPass(planId, payment);
      const res = await fetchWithKey('/fantasy/pass', { plan: planId });
      pass = res;
      SA.services.user?.notify?.({ kind: 'fantasy', title: 'Fantasy Pass active', body: 'Prize leagues are open.', link: '/fantasy/leagues' });
      return { pass: fantasyLive.pass(), paid: null };
    },
  };

  /** POST with an Idempotency-Key (one purchase per click, even when the network repeats it). */
  const fetchWithKey = (path, body) => api.post(path, body, { headers: { 'Idempotency-Key': `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}` } });

  Object.keys(fantasyLive).forEach((name) => { F[name] = fantasyLive[name]; });

  /* ======================================================================
   * Content: banners, announcement, pop-ups, news
   * ==================================================================== */

  const C = SA.services.content;
  const demoContent = { ...C };
  let home = null, popups = null, news = [];

  const toPopup = (p) => ({
    id: `pop-${p.id}`, apiId: p.id, active: p.isActive, shape: p.shape, icons: p.icons, color: p.color, badge: p.badge,
    title: ar() ? p.titleAr : p.titleEn, text: ar() ? p.textAr : p.textEn, cta: ar() ? p.ctaAr : p.ctaEn, link: p.link, forNonSellers: p.hideFromSellers,
  });

  const toStory = (n) => ({
    id: `sn-${n.id}`, apiId: n.id, cat: n.category, clubs: [], type: 'Super App', breaking: n.isBreaking, at: new Date(n.publishedAt).getTime(),
    hoursAgo: (Date.now() - new Date(n.publishedAt).getTime()) / 36e5, read: 2, img: n.imageUrl ? `${location.origin}${n.imageUrl}` : 'stadiumNight',
    title: n.title, excerpt: n.excerpt, body: [n.excerpt], own: true, live: true,
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
    stories() { return live() && news.length ? [...news, ...demoContent.stories().filter((s) => !s.own)] : demoContent.stories(); },
    allStories() { return live() && news.length ? [...news, ...demoContent.allStories().filter((s) => !s.own)] : demoContent.allStories(); },
  });

  const loadContent = async () => {
    [home, popups] = await Promise.all([quiet(api.get('/content/home')), quiet(api.get('/content/popups'))]);
    const n = await quiet(api.get('/news?pageSize=50'));
    news = (n?.items || []).map(toStory);
  };

  /* ======================================================================
   * Communities
   * ==================================================================== */

  const CM = SA.services.community;
  const demoCommunity = { ...CM };
  const REACT_TO = { '👍': 'Like', '🔥': 'Fire', '😂': 'Laugh', '❤️': 'Clap' };
  const REACT_FROM = { Like: '👍', Fire: '🔥', Laugh: '😂', Clap: '❤️', Goal: '🔥', Sad: '👍' };
  const AUD = { Everyone: 'everyone', Sellers: 'sellers', ProAndEliteSellers: 'sellers-pro' };
  const cmId = (slug) => `cm-${slug}`;
  const isLiveC = (id) => live() && !!db.find('communities', id)?.live;
  const isLiveP = (id) => live() && !!db.find('communityPosts', id)?.live;
  const meId = () => store.get().user?.id || SA.data.user.profile.id;

  const toCommunity = (c, i) => ({
    id: cmId(c.slug), apiId: c.id, slug: c.slug, name: c.name, about: c.about || '', audience: AUD[c.audience] || 'everyone', icon: c.icon || 'users', color: c.color || '#0B6E4F',
    cover: 'stadium', baseMembers: Math.max(0, c.members - (c.joined ? 1 : 0)), order: i, rules: c.rules, slowMode: Math.ceil(c.slowModeSeconds / 60), status: c.isArchived ? 'archived' : 'active',
    createdAt: Date.now(), createdBy: 'acc-admin', live: true,
  });

  const toAuthor = (a, asAdmin, mine) => (asAdmin ? { ...CM.ADMIN } : {
    kind: 'user', id: mine ? meId() : `u-${a.name}`, name: a.name, avatar: 0,
    badge: a.badges.includes('seller') ? { label: `${a.badges[1] || ''} seller`.trim(), tone: a.badges[1] || 'starter' } : null,
  });

  const toComment = (c) => ({ id: `cc-${c.id}`, apiId: c.id, author: toAuthor(c.author, c.asSuperAppAdmin, c.isMine), text: c.text, at: new Date(c.createdAt).getTime(), replyTo: c.parentId ? `cc-${c.parentId}` : null });

  const toPost = (p, comments = []) => {
    const mine = p.myReaction ? REACT_FROM[p.myReaction] : null;
    return {
      id: `cp-${p.id}`, apiId: p.id, communityId: cmId(p.community), author: toAuthor(p.author, p.asSuperAppAdmin, p.isMine), text: p.text, image: p.imageUrl ? `${location.origin}${p.imageUrl}` : null,
      card: p.cards[0] ? { type: p.cards[0].kind, id: p.cards[0].id, hash: p.cards[0].link } : null,
      poll: p.poll ? { options: p.poll.options.map((o) => ({ apiId: o.id, text: o.text, votes: p.poll.myVote === o.id ? [meId()] : [], seed: o.votes - (p.poll.myVote === o.id ? 1 : 0) })) } : null,
      reactions: mine ? { [mine]: [meId()] } : {}, seedReactions: p.reactionsCount - (mine ? 1 : 0), comments: comments.map(toComment),
      pinned: p.isPinned, announcement: p.isAnnouncement, locked: p.commentsClosed, at: new Date(p.createdAt).getTime(), live: true,
    };
  };

  const loadPosts = async (c) => {
    const page = await quiet(api.get(`/communities/${c.slug}/posts?pageSize=30`));
    if (!page) return [];
    return Promise.all(page.items.map(async (p) => toPost(p, p.commentsCount ? (await quiet(api.get(`/posts/${p.id}/comments`))) || [] : [])));
  };

  const loadCommunities = async () => {
    const list = await quiet(api.get('/communities'));
    if (!list) return;
    const posts = (await Promise.all(list.filter((c) => c.canJoin || c.joined).map(loadPosts))).flat();
    const raw = db.raw();
    raw.communities = list.map(toCommunity);
    raw.communityPosts = posts;
    store.set({ communities: list.filter((c) => c.joined).map((c) => cmId(c.slug)) }, 'community');
    try { db.touch('communities'); db.touch('communityPosts'); } catch { /* storage full */ }
  };

  const refreshPost = async (postId) => {
    const apiId = db.find('communityPosts', postId)?.apiId;
    const [p, comments] = await Promise.all([api.get(`/posts/${apiId}`), api.get(`/posts/${apiId}/comments`)]);
    db.update('communityPosts', postId, toPost(p, comments));
  };

  const slugOf = (communityId) => db.find('communities', communityId)?.slug;

  Object.assign(CM, {
    async join(id) {
      if (!isLiveC(id)) return demoCommunity.join(id);
      await api.post(`/communities/${slugOf(id)}/join`);
      store.set((s) => ({ communities: [...new Set([...(s.communities || []), id])] }), 'community');
      return undefined;
    },
    async leave(id) {
      if (!isLiveC(id)) return demoCommunity.leave(id);
      await api.del(`/communities/${slugOf(id)}/join`);
      store.set((s) => ({ communities: (s.communities || []).filter((x) => x !== id) }), 'community');
      return undefined;
    },
    async createPost(communityId, data) {
      if (!isLiveC(communityId)) return demoCommunity.createPost(communityId, data);
      if (!(store.get().communities || []).includes(communityId)) await CM.join(communityId);
      const text = [data.text, data.card?.hash].filter(Boolean).join(' ');
      const p = await api.post(`/communities/${slugOf(communityId)}/posts`, { text, pollOptions: (data.poll || []).filter(Boolean) });
      if (data.image && String(data.image).startsWith('data:')) {
        const form = new FormData();
        form.append('image', api.blob(data.image), 'photo.jpg');
        await quiet(api.form(`/posts/${p.id}/image`, form));
      }
      const fresh = toPost(await api.get(`/posts/${p.id}`));
      db.insert('communityPosts', fresh);
      return fresh;
    },
    async comment(postId, text, replyTo = null) {
      if (!isLiveP(postId)) return demoCommunity.comment(postId, text, replyTo);
      const apiId = db.find('communityPosts', postId).apiId;
      const parent = replyTo ? Number(String(replyTo).replace(/^cc-/, '')) : null;
      const c = await api.post(parent ? `/posts/${apiId}/comments/${parent}/replies` : `/posts/${apiId}/comments`, { text });
      await refreshPost(postId);
      return toComment(c);
    },
    async react(postId, emoji) {
      if (!isLiveP(postId)) return demoCommunity.react(postId, emoji);
      const p = db.find('communityPosts', postId);
      const had = (p.reactions?.[emoji] || []).includes(meId());
      if (had) await api.del(`/posts/${p.apiId}/reaction`);
      else await api.put(`/posts/${p.apiId}/reaction`, { type: REACT_TO[emoji] || 'Like' });
      await refreshPost(postId);
      return undefined;
    },
    async vote(postId, i) {
      if (!isLiveP(postId)) return demoCommunity.vote(postId, i);
      const p = db.find('communityPosts', postId);
      await api.post(`/posts/${p.apiId}/vote`, { optionId: p.poll.options[i].apiId });
      await refreshPost(postId);
      return undefined;
    },
    async report(postId, { commentId = null, reason }) {
      if (!isLiveP(postId)) return demoCommunity.report(postId, { commentId, reason });
      const p = db.find('communityPosts', postId);
      const why = { spam: 'Spam', abuse: 'Abuse', scam: 'Scam', offtopic: 'OffTopic', other: 'Other' }[reason] || 'Other';
      const path = commentId ? `/comments/${String(commentId).replace(/^cc-/, '')}/report` : `/posts/${p.apiId}/report`;
      await api.post(path, { reason: why, detail: null });
      return undefined;
    },
    async deleteOwn(postId, commentId = null) {
      if (!isLiveP(postId) || commentId) return demoCommunity.deleteOwn(postId, commentId);
      await api.del(`/posts/${db.find('communityPosts', postId).apiId}`);
      db.update('communityPosts', postId, { deleted: true });
      return undefined;
    },
  });

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
   * Start
   * ==================================================================== */

  api.ready.then(async (mode) => {
    if (mode !== 'api') return;
    await quiet(SA.services.session?.load() || Promise.resolve());
    // Signing in or out changes my team, pass, groups and tickets
    SA.services.session?.subscribe(() => quiet(Promise.all(['egy', 'epl'].filter((k) => server[k]).map((k) => loadTeam(k).then(() => loadGroups(k))).concat([loadPass(), loadTickets()]))));
    await Promise.all([
      quiet(loadLeague('egy')), quiet(loadLeague('epl')), quiet(loadPass()), quiet(loadContent()), quiet(loadCommunities()), quiet(loadTickets()),
    ]);
    store.set({ liveFantasyAt: Date.now() }, 'fantasy');
  });

  SA.services.liveFantasy = { reload: () => Promise.all([loadLeague(key()), loadPass(), loadContent(), loadCommunities(), loadTickets()]) };
})(window.SA);
