/**
 * Fantasy (Egyptian Premier League). Future API:
 *   GET /fantasy/bootstrap, GET /fantasy/players, PUT /fantasy/team, POST /fantasy/transfers,
 *   GET /fantasy/leagues, POST /fantasy/leagues/:id/join, POST /fantasy/groups, GET /fantasy/groups/:id/standings
 * Gameweek simulation stands in for the live scoring feed.
 */
(function (SA) {
  const store = SA.core.store;
  const { request, ApiError } = SA.services.http;
  const { seeded, rng, addDays, uid, sum } = SA.core.utils;
  // Two leagues to play: the Egyptian Premier League ("egy") and the English Premier League ("epl").
  // Each has its own dataset and its own saved state (team, groups, leagues entered).
  const LEAGUES = SA.data.fantasyLeagues;
  const STATE_KEY = { egy: 'fantasy', epl: 'fantasyEpl' };
  const blank = () => ({ team: null, joinedLeagues: [], createdGroups: [] });
  const league = () => (LEAGUES[store.get().fantasyLeague] ? store.get().fantasyLeague : 'egy');
  let D, byId, teamById;
  const index = () => {
    D = LEAGUES[league()];
    byId = Object.fromEntries(D.players.map((p) => [p.id, p]));
    teamById = Object.fromEntries(D.teams.map((t) => [t.id, t]));
  };
  index();
  const POS = ['GK', 'DEF', 'MID', 'FWD'];

  const fstate = () => store.get()[STATE_KEY[league()]] || blank();
  const setF = (patch) => store.set((s) => { const k = STATE_KEY[league()]; const cur = s[k] || blank(); return { [k]: { ...cur, ...(typeof patch === 'function' ? patch(cur) : patch) } }; }, 'fantasy');
  const liveGW = () => fstate().liveGW || D.currentGW;

  // Bring simulated rounds back after reload
  const syncRounds = () => {
    const gw = liveGW();
    if (gw > D.currentGW) D.applyRounds(gw);
    D.fixtures.filter((f) => f.gw < gw).forEach(D.playFixture);
  };

  /** Deadline: Friday 17:30 for the first open GW, +7 days per simulated round. */
  const deadlineFor = (gw) => {
    const now = new Date();
    const d = new Date(now); d.setHours(17, 30, 0, 0);
    const toFri = (5 - d.getDay() + 7) % 7;
    d.setDate(d.getDate() + toFri);
    if (d < now) d.setDate(d.getDate() + 7);
    return addDays(d, (gw - D.currentGW) * 7);
  };

  const score = (p) => p.total + p.form * 3 + (p.status === 'fit' ? 0 : -15);

  // ---------- Squad & lineup rules ----------
  const counts = (ids) => { const c = { GK: 0, DEF: 0, MID: 0, FWD: 0 }; ids.forEach((id) => c[byId[id].pos]++); return c; };
  const cost = (ids) => +sum(ids, (id) => byId[id].price).toFixed(1);
  const teamCounts = (ids) => ids.reduce((a, id) => ((a[byId[id].teamId] = (a[byId[id].teamId] || 0) + 1), a), {});

  const validateSquad = (ids) => {
    const errs = [];
    const c = counts(ids);
    POS.forEach((pos) => { if (c[pos] !== D.rules.squad[pos]) errs.push(`Pick ${D.rules.squad[pos]} ${posName(pos, 2)} (you have ${c[pos]}).`); });
    Object.entries(teamCounts(ids)).forEach(([t, n]) => { if (n > D.rules.maxPerTeam) errs.push(`Max ${D.rules.maxPerTeam} players from ${teamById[t].name}.`); });
    if (cost(ids) > D.rules.budget) errs.push(`Over budget by ${(cost(ids) - D.rules.budget).toFixed(1)}M.`);
    return errs;
  };
  const posName = (pos, n = 1) => ({ GK: ['goalkeeper', 'goalkeepers'], DEF: ['defender', 'defenders'], MID: ['midfielder', 'midfielders'], FWD: ['forward', 'forwards'] }[pos][n > 1 ? 1 : 0]);

  const formationOf = (xi) => { const c = counts(xi); return `${c.DEF}-${c.MID}-${c.FWD}`; };
  const validXI = (xi) => { const c = counts(xi); return xi.length === 11 && c.GK === 1 && c.DEF >= 3 && c.MID >= 2 && c.FWD >= 1; };

  /** Best XI for a formation, preferring players already starting. */
  const lineupFor = (squad, formation = '4-4-2', prefer = []) => {
    const [d, m, f] = formation.split('-').map(Number);
    const need = { GK: 1, DEF: d, MID: m, FWD: f };
    const ranked = squad.slice().sort((a, b) => (prefer.includes(b) - prefer.includes(a)) * 1000 + score(byId[b]) - score(byId[a]));
    const xi = [];
    POS.forEach((pos) => xi.push(...ranked.filter((id) => byId[id].pos === pos).slice(0, need[pos])));
    const rest = squad.filter((id) => !xi.includes(id));
    const bench = [...rest.filter((id) => byId[id].pos === 'GK'), ...rest.filter((id) => byId[id].pos !== 'GK').sort((a, b) => score(byId[b]) - score(byId[a]))];
    return { xi, bench };
  };

  /** Greedy auto-pick that always produces a valid squad. Seed makes it repeatable (used for rival teams). */
  const autoPick = (seedStr = String(Date.now())) => {
    const r = rng(Math.floor(seeded(seedStr) * 1e9));
    const need = { ...D.rules.squad };
    const picked = [], perTeam = {};
    let budget = D.rules.budget;
    const pool = D.players.filter((p) => p.status !== 'injured').map((p) => ({ p, w: score(p) + r() * 45 })).sort((a, b) => b.w - a.w);
    const slotsLeft = () => sum(Object.values(need));
    for (const { p } of pool) {
      if (!need[p.pos] || (perTeam[p.teamId] || 0) >= D.rules.maxPerTeam) continue;
      const reserve = (slotsLeft() - 1) * 4.0;
      if (budget - p.price < reserve) continue;
      picked.push(p.id); need[p.pos]--; budget -= p.price; perTeam[p.teamId] = (perTeam[p.teamId] || 0) + 1;
      if (!slotsLeft()) break;
    }
    return picked;
  };

  // ---------- Scoring ----------
  const gwPointsFor = (team, gw) => {
    const played = (id) => (byId[id].log[gw]?.minutes || 0) > 0;
    let xi = team.xi.slice();
    // Automatic substitutions: a non-playing starter is replaced by the first eligible bench player who played.
    team.bench.forEach((b) => {
      const out = xi.find((id) => !played(id) && (byId[id].pos === 'GK') === (byId[b].pos === 'GK'));
      if (out && played(b)) { const cand = xi.map((x) => (x === out ? b : x)); if (validXI(cand)) xi = cand; }
    });
    const cap = played(team.captain) ? team.captain : team.vice;
    const breakdown = xi.map((id) => ({ id, pts: (byId[id].gw[gw] || 0) * (id === cap ? 2 : 1), captain: id === cap }));
    return { xi, total: sum(breakdown, (x) => x.pts), breakdown, captainUsed: cap };
  };

  const rivalPoints = (name, gw) => 28 + Math.floor(seeded(`${name}|${gw}`) * 46) + (seeded(name) > 0.6 ? 6 : 0);
  const rivalTotal = (name, upto = liveGW() - 1) => { let t = 0; for (let g = 1; g <= upto; g++) t += rivalPoints(name, g); return t; };

  // Normal CDF approximation for overall rank
  const phi = (z) => { const t = 1 / (1 + 0.2316419 * Math.abs(z)); const d = 0.3989423 * Math.exp(-z * z / 2); const p = d * t * (0.3193815 + t * (-0.3565638 + t * (1.781478 + t * (-1.821256 + t * 1.330274)))); return z > 0 ? 1 - p : p; };
  const managers = () => D.managers || 184320;
  const rankFor = (pts, rounds) => (rounds <= 0 ? managers() : Math.max(1, Math.round(managers() * (1 - phi((pts - 47 * rounds) / (13 * Math.sqrt(rounds)))))));

  /* ---------- Fantasy Pass: monthly subscription for prize leagues (one league or both) ---------- */
  const PASS_DAYS = 30;
  const passPrices = () => SA.services.db?.settings().fantasySubs || { one: 50, both: 80 };
  /** Sellers get their level discount (Pro 50%, Elite free) */
  const sellerDiscount = () => { const s = SA.services.sellers?.mine(); return s ? SA.services.sellers.levelOf(s).fantasyDiscount || 0 : 0; };
  const passPlans = () => {
    const p = passPrices(), off = sellerDiscount();
    const price = (v) => Math.round(v * (1 - off));
    return [
      { id: 'egy', label: LEAGUES.egy.label, leagues: ['egy'], list: p.one, price: price(p.one) },
      { id: 'epl', label: LEAGUES.epl.label, leagues: ['epl'], list: p.one, price: price(p.one) },
      { id: 'both', label: 'Both leagues', leagues: ['egy', 'epl'], list: p.both, price: price(p.both), best: true },
    ].map((x) => ({ ...x, discount: off }));
  };
  // Each league has its own end date: { egy: timestamp, epl: timestamp }
  const passRaw = () => { const p = store.get().fantasyPass; return p && typeof p === 'object' && !p.leagues ? p : {}; };
  const pass = () => {
    const p = passRaw(), now = Date.now();
    const leagues = Object.keys(LEAGUES).filter((k) => (p[k] || 0) > now);
    if (!leagues.length) return null;
    const own = (p[league()] || 0) > now ? p[league()] : Math.max(...leagues.map((k) => p[k]));
    return { leagues, until: own, ends: Object.fromEntries(leagues.map((k) => [k, p[k]])) };
  };
  const passCovers = (lg = league()) => (passRaw()[lg] || 0) > Date.now();

  const userTotals = () => {
    const t = fstate().team;
    if (!t) return { total: 0, gwPts: 0, rounds: 0 };
    const h = t.history || [];
    return { total: sum(h, (x) => x.pts), gwPts: h.length ? h[h.length - 1].pts : 0, rounds: h.length };
  };

  // ---------- Public API ----------
  const fantasyService = {
    get byId() { return byId; }, get teamById() { return teamById; }, get D() { return D; },
    posName, formationOf, validXI, validateSquad, cost, counts, teamCounts,
    /* Leagues to play */
    leagues: () => Object.values(LEAGUES).map((l) => ({ key: l.key, label: l.label, short: l.short, hasTeam: !!(store.get()[STATE_KEY[l.key]] || {}).team })),
    league,
    leagueInfo: () => ({ key: D.key, label: D.label, short: D.short }),
    use(key) {
      if (!LEAGUES[key] || key === league()) return;
      store.set({ fantasyLeague: key }, 'fantasy');
      index(); syncRounds();
    },
    /* Fantasy Pass */
    passPlans, pass, passCovers,
    async buyPass(planId, payment) {
      const plan = passPlans().find((x) => x.id === planId);
      if (!plan) throw new ApiError('Choose a plan.', 'validation');
      let paid = null;
      if (plan.price > 0) paid = await SA.services.payment.charge({ ...payment, amount: plan.price, label: `Fantasy Pass — ${plan.label} (1 month)` });
      // Each league in the plan gets 30 more days (renewing early keeps the remaining days)
      const next = { ...passRaw() };
      plan.leagues.forEach((k) => { next[k] = Math.max(next[k] || 0, Date.now()) + PASS_DAYS * 864e5; });
      store.set({ fantasyPass: next }, 'fantasy');
      SA.services.admin?.recordPass({ plan: plan.id, amount: plan.price, customer: store.get().user?.name || 'Customer' });
      const until = Math.max(...plan.leagues.map((k) => next[k]));
      SA.services.user.notify({ kind: 'fantasy', title: 'Fantasy Pass active', body: `${plan.label} · prize leagues open until ${new Date(until).toLocaleDateString('en-GB', { day: 'numeric', month: 'short' })}.`, link: '/fantasy/leagues' });
      return { pass: pass(), paid };
    },
    meta: () => {
      syncRounds();
      const gw = liveGW();
      return { gw, deadline: deadlineFor(gw), season: D.season, totalGW: D.totalGW, budget: D.rules.budget, managers: managers(), league: D.key, leagueLabel: D.label };
    },
    team: () => fstate().team,
    hasTeam: () => !!fstate().team,

    players: (f = {}) => request(() => {
      syncRounds();
      let out = D.players.slice();
      if (f.pos) out = out.filter((p) => p.pos === f.pos);
      if (f.team) out = out.filter((p) => p.teamId === f.team);
      if (f.maxPrice) out = out.filter((p) => p.price <= +f.maxPrice);
      if (f.q) { const q = f.q.toLowerCase(); out = out.filter((p) => p.name.toLowerCase().includes(q) || teamById[p.teamId].name.toLowerCase().includes(q)); }
      const key = f.sort || 'total';
      const sorters = { total: (a, b) => b.total - a.total, price: (a, b) => b.price - a.price, form: (a, b) => b.form - a.form, selected: (a, b) => b.selectedBy - a.selectedBy, goals: (a, b) => b.stats.goals - a.stats.goals, value: (a, b) => b.total / b.price - a.total / a.price };
      return out.sort(sorters[key] || sorters.total);
    }, { latency: 120 }),

    playersSync: () => D.players,
    player: (id) => {
      syncRounds();
      const p = byId[id];
      const gw = liveGW();
      const upcoming = D.fixtures.filter((f) => f.gw >= gw && (f.home === p.teamId || f.away === p.teamId)).slice(0, 5)
        .map((f) => ({ gw: f.gw, home: f.home === p.teamId, opp: f.home === p.teamId ? f.away : f.home, fdr: D.fdr(f.home === p.teamId ? f.away : f.home) }));
      const past = Object.entries(p.gw).map(([g, pts]) => ({ gw: +g, pts, ...p.log[g] })).reverse();
      return { ...p, team: teamById[p.teamId], upcoming, past };
    },
    nextOpponent: (teamId) => {
      const gw = liveGW();
      const f = D.fixtures.find((x) => x.gw === gw && (x.home === teamId || x.away === teamId));
      return f ? { opp: f.home === teamId ? f.away : f.home, home: f.home === teamId, fdr: D.fdr(f.home === teamId ? f.away : f.home) } : null;
    },
    fixtures: (gw) => { syncRounds(); return D.fixtures.filter((f) => f.gw === gw); },
    kickoff: (fx) => {
      const base = deadlineFor(fx.gw);
      const day = fx.slot < 3 ? 0 : fx.slot < 6 ? 1 : 2;
      const d = addDays(base, day); d.setHours([19, 21, 17][fx.slot % 3] + (day === 0 ? 1 : 0), fx.slot % 2 ? 30 : 0, 0, 0);
      return d;
    },
    table: () => {
      syncRounds();
      const rows = Object.fromEntries(D.teams.map((t) => [t.id, { team: t, p: 0, w: 0, d: 0, l: 0, gf: 0, ga: 0, pts: 0, form: [] }]));
      D.fixtures.filter((f) => f.played).forEach((f) => {
        const h = rows[f.home], a = rows[f.away];
        h.p++; a.p++; h.gf += f.hs; h.ga += f.as; a.gf += f.as; a.ga += f.hs;
        if (f.hs > f.as) { h.w++; a.l++; h.pts += 3; h.form.push('W'); a.form.push('L'); }
        else if (f.hs < f.as) { a.w++; h.l++; a.pts += 3; h.form.push('L'); a.form.push('W'); }
        else { h.d++; a.d++; h.pts++; a.pts++; h.form.push('D'); a.form.push('D'); }
      });
      return Object.values(rows).sort((x, y) => y.pts - x.pts || (y.gf - y.ga) - (x.gf - x.ga) || y.gf - x.gf);
    },

    autoPick, lineupFor,

    /** Create the team or apply transfers. */
    saveSquad: (ids, { name } = {}) => request(() => {
      const errs = validateSquad(ids);
      if (errs.length) throw new ApiError(errs[0], 'invalid_squad', errs);
      const cur = fstate().team;
      if (!cur) {
        const { xi, bench } = lineupFor(ids, '4-4-2');
        const captain = xi.slice().sort((a, b) => score(byId[b]) - score(byId[a]))[0];
        const vice = xi.filter((x) => x !== captain).sort((a, b) => score(byId[b]) - score(byId[a]))[0];
        const team = { id: uid('FT'), name: (name || '').trim() || `${store.get().user.firstName} FC`, squad: ids, xi, bench, captain, vice, freeTransfers: 1, hits: 0, createdGW: liveGW(), history: [] };
        setF({ team });
        SA.services.user.notify({ kind: 'fantasy', title: 'Your fantasy team is ready', body: `${team.name} is entered into the Super App Grand League. Join a private group to play your friends.`, link: '/fantasy' });
        return { created: true, team };
      }
      const incoming = ids.filter((id) => !cur.squad.includes(id));
      const outgoing = cur.squad.filter((id) => !ids.includes(id));
      const made = incoming.length;
      const free = cur.freeTransfers;
      const hits = Math.max(0, made - free) * D.rules.transferCost;
      // Keep starting roles: incoming player takes the outgoing player's slot (same position)
      let xi = cur.xi.slice(), bench = cur.bench.slice();
      outgoing.forEach((out) => {
        const inn = incoming.find((i) => byId[i].pos === byId[out].pos && !xi.includes(i) && !bench.includes(i));
        xi = xi.map((x) => (x === out ? inn : x)); bench = bench.map((x) => (x === out ? inn : x));
      });
      let { captain, vice } = cur;
      if (!xi.includes(captain)) captain = xi.find((x) => x !== vice);
      if (!xi.includes(vice)) vice = xi.find((x) => x !== captain);
      const team = { ...cur, squad: ids, xi, bench, captain, vice, freeTransfers: Math.max(0, free - made), hits: cur.hits + hits };
      setF({ team });
      return { created: false, made, hits, team };
    }),

    rename: (name) => setF((f) => ({ team: { ...f.team, name: name.trim() || f.team.name } })),

    /** Swap a starter with a bench player (or reorder bench). Returns an error string if the swap breaks formation rules. */
    swap(a, b) {
      const t = fstate().team;
      const inXI = (id) => t.xi.includes(id);
      if (inXI(a) === inXI(b)) {
        if (inXI(a)) return 'Pick a substitute to swap with this starter.';
        const bench = t.bench.slice(); const i = bench.indexOf(a), j = bench.indexOf(b);
        if (byId[a].pos === 'GK' || byId[b].pos === 'GK') return 'The substitute goalkeeper always sits first on the bench.';
        [bench[i], bench[j]] = [bench[j], bench[i]];
        setF({ team: { ...t, bench } });
        return null;
      }
      const [starter, sub] = inXI(a) ? [a, b] : [b, a];
      const xi = t.xi.map((x) => (x === starter ? sub : x));
      if (!validXI(xi)) return byId[starter].pos === 'GK' || byId[sub].pos === 'GK' ? 'Goalkeepers can only swap with goalkeepers.' : 'That swap breaks the formation rules: at least 3 defenders, 2 midfielders and 1 forward.';
      const bench = t.bench.map((x) => (x === sub ? starter : x));
      let { captain, vice } = t;
      if (captain === starter) captain = sub;
      if (vice === starter) vice = xi.find((x) => x !== captain);
      setF({ team: { ...t, xi, bench, captain, vice } });
      return null;
    },
    setFormation(formation) {
      const t = fstate().team;
      const { xi, bench } = lineupFor(t.squad, formation, t.xi);
      let { captain, vice } = t;
      if (!xi.includes(captain)) captain = xi[xi.length - 1];
      if (!xi.includes(vice) || vice === captain) vice = xi.find((x) => x !== captain);
      setF({ team: { ...t, xi, bench, captain, vice } });
    },
    setCaptain(id) { const t = fstate().team; setF({ team: { ...t, captain: id, vice: t.vice === id ? t.captain : t.vice } }); },
    setVice(id) { const t = fstate().team; setF({ team: { ...t, vice: id, captain: t.captain === id ? t.vice : t.captain } }); },

    /** Simulate the live gameweek: play fixtures, score the team, roll the deadline forward. */
    simulateGameweek: () => request(() => {
      const gw = liveGW();
      if (gw > D.totalGW) throw new ApiError('The season is over.', 'season_over');
      D.applyRounds(gw + 1);
      D.fixtures.filter((f) => f.gw === gw).forEach(D.playFixture);
      const t = fstate().team;
      let result = null;
      if (t) {
        const r = gwPointsFor(t, gw);
        const pts = r.total - t.hits;
        const prev = userTotals();
        const total = prev.total + pts;
        const entry = { gw, pts, raw: r.total, hits: t.hits, captain: r.captainUsed, xi: r.xi, rank: rankFor(total, prev.rounds + 1), gwRank: rankFor(pts, 1) };
        setF({ team: { ...t, history: [...(t.history || []), entry], hits: 0, freeTransfers: Math.min(2, t.freeTransfers + 1) } });
        result = entry;
      }
      setF({ liveGW: gw + 1 });
      SA.services.user.notify({ kind: 'fantasy', title: `Gameweek ${gw} is complete`, body: result ? `You scored ${result.pts} points. Overall rank ${result.rank.toLocaleString()}.` : 'Build a team to start scoring next gameweek.', link: '/fantasy' });
      return { gw, result };
    }, { latency: 1100 }),

    lastResult: () => { const h = fstate().team?.history || []; return h[h.length - 1] || null; },
    totals: userTotals,
    overallRank: () => { const u = userTotals(); return u.rounds ? (fstate().team.history.at(-1).rank) : null; },

    // ---------- Leagues ----------
    officialLeagues: () => {
      const joined = fstate().joinedLeagues || [];
      return D.officialLeagues.map((l) => ({ ...l, joined: l.kind === 'official' ? !!fstate().team : joined.includes(l.id), participants: l.participants + (joined.includes(l.id) ? 1 : 0) }));
    },
    privateGroups: () => {
      const f = fstate();
      const ids = f.groupIds || D.privateGroups.map((g) => g.id);
      const seedGroups = [...D.privateGroups, ...D.joinableGroups].filter((g) => ids.includes(g.id));
      return [...(f.createdGroups || []), ...seedGroups];
    },
    group(id) {
      const g = fantasyService.privateGroups().find((x) => x.id === id) || D.officialLeagues.find((x) => x.id === id);
      if (!g) return null;
      return { ...g, standings: fantasyService.standings(g) };
    },
    standings(g) {
      const upto = liveGW() - 1;
      const me = store.get().user.name;
      const u = userTotals();
      const t = fstate().team;
      let rows;
      if (g.members) {
        rows = g.members.filter((n) => n !== me).map((name) => ({ name, team: `${name.split(' ')[0]}'s XI`, total: rivalTotal(name, upto), gw: rivalPoints(name, upto), isMe: false }));
      } else {
        // Official league: show the top of the table
        const names = SA.data.matches.people;
        rows = Array.from({ length: 10 }, (_, i) => ({ name: names[(i * 5 + 3) % names.length], team: ['Pharaohs United', 'Nile Strikers', 'Cairo Kings', 'Red Devils EG', 'Zamalek Ultras XI', 'Port Said Pride', 'The Gegenpress', 'Trezeguet’s Boots', 'Alex Seaside FC', 'Sphinx Sharks'][i], total: 0, gw: 0, isMe: false }))
          .map((r, i) => ({ ...r, total: Math.round(upto * (72 - i * 1.6) + seeded(r.name) * 8), gw: rivalPoints(r.name + g.id, upto) + 20 }));
      }
      if (t && (g.members || g.kind === 'official' || (fstate().joinedLeagues || []).includes(g.id))) rows.push({ name: me, team: t.name, total: u.total, gw: u.gwPts, isMe: true });
      rows.sort((a, b) => b.total - a.total);
      rows.forEach((r, i) => (r.rank = i + 1));
      if (!g.members && t) { const mine = rows.find((r) => r.isMe); if (mine && mine.rank > 10) mine.rank = Math.min(g.participants, rankFor(u.total, Math.max(1, u.rounds))); }
      if (!g.members) return rows.filter((r) => r.rank <= 10 || r.isMe).sort((a, b) => a.rank - b.rank);
      return rows;
    },
    rivalSquad: (name) => { const squad = autoPick(name); const { xi, bench } = lineupFor(squad, '4-4-2'); return { squad, xi, bench, captain: xi.slice().sort((a, b) => score(byId[b]) - score(byId[a]))[0] }; },
    rivalHistory: (name) => Array.from({ length: liveGW() - 1 }, (_, i) => ({ gw: i + 1, pts: rivalPoints(name, i + 1) })),

    createGroup: (name) => request(() => {
      if (!name || name.trim().length < 3) throw new ApiError('Give your group a name of at least 3 characters.', 'invalid_name');
      const code = 'SA-' + Math.random().toString(36).slice(2, 8).toUpperCase();
      const g = { id: uid('pg'), name: name.trim(), code, owner: store.get().user.name, createdAt: new Date().toISOString().slice(0, 10), members: [store.get().user.name], startGW: liveGW(), mine: true };
      setF((f) => ({ createdGroups: [g, ...(f.createdGroups || [])] }));
      return g;
    }),
    joinGroupByCode: (code) => request(() => {
      const c = code.trim().toUpperCase();
      const f = fstate();
      const ids = f.groupIds || D.privateGroups.map((g) => g.id);
      const found = [...D.privateGroups, ...D.joinableGroups].find((g) => g.code === c);
      if (!found) throw new ApiError('No group uses that code. Check it with the person who invited you.', 'not_found');
      if (ids.includes(found.id)) throw new ApiError(`You’re already in ${found.name}.`, 'already_member');
      setF({ groupIds: [...ids, found.id] });
      return found;
    }),
    leaveGroup(id) {
      const f = fstate();
      if ((f.createdGroups || []).some((g) => g.id === id)) return setF({ createdGroups: f.createdGroups.filter((g) => g.id !== id) });
      setF({ groupIds: (f.groupIds || D.privateGroups.map((g) => g.id)).filter((x) => x !== id) });
    },
    /** Enter a prize league: free with an active Fantasy Pass for this league (no entry fees, no cash prizes) */
    async joinOfficial(id) {
      const l = D.officialLeagues.find((x) => x.id === id);
      if (!l) throw new ApiError('League not found.', 'not_found');
      if (!fstate().team) throw new ApiError('Create your fantasy team before entering a league.', 'no_team');
      if ((fstate().joinedLeagues || []).includes(id)) throw new ApiError('You’ve already entered this league.', 'already');
      if (l.kind === 'pass' && !passCovers()) throw new ApiError(`Prize leagues need a Fantasy Pass for the ${D.label}.`, 'pass_required');
      if (l.cap && l.participants >= l.cap) throw new ApiError('This league is full.', 'full');
      setF((f) => ({ joinedLeagues: [...(f.joinedLeagues || []), id] }));
      SA.services.user.notify({ kind: 'fantasy', title: `You’ve entered ${l.name}`, body: `Competing from GW${l.from}. Good luck!`, link: `/fantasy/league/${id}` });
      return { ok: true };
    },
  };

  syncRounds();
  SA.services.fantasy = fantasyService;
})(window.SA);
