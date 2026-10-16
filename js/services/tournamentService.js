/**
 * Tournaments & leagues organised by venues. A tournament reserves its match hours on the pitch
 * (closures tagged with tournamentId), teams register and pay from the site, the organiser
 * generates fixtures and enters results; standings / brackets update live. Entry fees are venue
 * income (10% commission when paid online), available once the first match day has passed.
 * Future API: /tournaments, /tournaments/:id/register, /partner/tournaments/*
 */
(function (SA) {
  const { ApiError, request } = SA.services.http;
  const { isoDate, parseISO, addDays, today, fmtDate, fmtHour, seeded, uid } = SA.core.utils;
  const db = SA.services.db;
  const PS = () => SA.services.pitches;
  const D = () => SA.services.dash;
  const DAY = 864e5;

  const SIZES = { '5-a-side': 5, '7-a-side': 7, '11-a-side': 11 };
  const isPow2 = (n) => n >= 2 && (n & (n - 1)) === 0;
  const roundName = (matches) => ({ 1: 'Final', 2: 'Semi-finals', 4: 'Quarter-finals', 8: 'Round of 16' }[matches] || `Round of ${matches * 2}`);

  /** Match days needed: [{ date, matches }] for a format and team count */
  const plan = (format, teams, startDate, everyDays = 7) => {
    const days = [];
    if (format === 'knockout') { for (let m = teams / 2; m >= 1; m /= 2) days.push(m); }
    else { const n = teams % 2 ? teams + 1 : teams; for (let k = 0; k < n - 1; k++) days.push(Math.floor(teams / 2)); }
    return days.map((matches, i) => ({ date: isoDate(addDays(parseISO(startDate), i * everyDays)), matches }));
  };

  /** Round robin (circle method). Returns rounds: [[homeId, awayId], …] */
  const roundRobin = (ids) => {
    const list = ids.length % 2 ? [...ids, null] : ids.slice();
    const n = list.length, rounds = [];
    for (let r = 0; r < n - 1; r++) {
      const pairs = [];
      for (let i = 0; i < n / 2; i++) { const a = list[i], b = list[n - 1 - i]; if (a && b) pairs.push(r % 2 ? [b, a] : [a, b]); }
      rounds.push(pairs);
      list.splice(1, 0, list.pop());
    }
    return rounds;
  };

  const teamName = (t, id) => t.teams.find((x) => x.id === id)?.name || 'TBD';

  const standings = (t) => {
    const rows = new Map(t.teams.map((x) => [x.id, { id: x.id, name: x.name, p: 0, w: 0, d: 0, l: 0, gf: 0, ga: 0, pts: 0, form: [] }]));
    t.fixtures.filter((f) => f.status === 'played').sort((a, b) => a.date.localeCompare(b.date) || a.hour - b.hour).forEach((f) => {
      const h = rows.get(f.home), a = rows.get(f.away); if (!h || !a) return;
      h.p++; a.p++; h.gf += f.hs; h.ga += f.as; a.gf += f.as; a.ga += f.hs;
      if (f.hs > f.as) { h.w++; a.l++; h.pts += 3; h.form.push('W'); a.form.push('L'); }
      else if (f.hs < f.as) { a.w++; h.l++; a.pts += 3; a.form.push('W'); h.form.push('L'); }
      else { h.d++; a.d++; h.pts++; a.pts++; h.form.push('D'); a.form.push('D'); }
    });
    return [...rows.values()].map((r) => ({ ...r, gd: r.gf - r.ga, form: r.form.slice(-5) })).sort((x, y) => y.pts - x.pts || y.gd - x.gd || y.gf - x.gf || x.name.localeCompare(y.name));
  };
  const winnerOf = (f) => (f.status !== 'played' ? null : f.hs > f.as ? f.home : f.hs < f.as ? f.away : f.pens || null);
  const bracket = (t) => {
    const rounds = [];
    t.fixtures.forEach((f) => { (rounds[f.round] ||= []).push(f); });
    return rounds.filter(Boolean).map((list) => ({ name: roundName(list.length), fixtures: list.sort((a, b) => a.slot - b.slot) }));
  };

  /** Decorated, read-only view used by both the site and the dashboard */
  const view = (t) => {
    const p = db.find('pitches', t.pitchId);
    const owner = db.find('accounts', t.ownerId);
    const now = Date.now();
    const days = [...new Set(t.fixtures.length ? t.fixtures.map((f) => f.date) : t.reserved.map((r) => r.date))].sort();
    const first = days[0] || t.startDate, last = days[days.length - 1] || t.startDate;
    const regOpen = t.status === 'open' && t.teams.length < t.maxTeams && isoDate(today()) <= t.regDeadline;
    const champion = t.status === 'finished' ? (t.format === 'league' ? standings(t)[0]?.name : teamName(t, winnerOf(t.fixtures.find((f) => f.round === Math.max(...t.fixtures.map((x) => x.round)))))) : null;
    const next = t.fixtures.filter((f) => f.status !== 'played').sort((a, b) => a.date.localeCompare(b.date) || a.hour - b.hour)[0] || null;
    return {
      ...t, pitch: p, pitchName: p?.name || 'Removed pitch', area: p?.area || '', organiser: owner?.company || 'Venue', firstDay: first, lastDay: last, matchDays: days.length,
      spotsLeft: Math.max(0, t.maxTeams - t.teams.length), regOpen, champion, next, live: t.status === 'running' && first <= isoDate(today()),
      mine: t.teams.filter((x) => x.userId === (SA.core.store.get().user?.id || 'u-001')), played: t.fixtures.filter((f) => f.status === 'played').length, startsIn: Math.ceil((parseISO(first) - now) / DAY),
    };
  };

  /* ---------- Pitch time ---------- */
  const reserve = (t) => {
    db.all('closures').filter((c) => c.tournamentId === t.id).forEach((c) => db.remove('closures', c.id));
    t.reserved.forEach((r) => db.insert('closures', { pitchId: t.pitchId, from: r.date, to: r.date, hours: r.hours, reason: `Tournament: ${t.name}`, tournamentId: t.id }, 'cl'));
  };
  const reservedFrom = (p, days, hour) => days.map((d) => ({ date: d.date, hours: Array.from({ length: d.matches }, (_, i) => hour + i) }));
  const conflicts = (p, reserved) => {
    const out = [];
    reserved.forEach((r) => {
      const slots = PS().slotsSync(p.id, r.date);
      r.hours.forEach((h) => { const s = slots.find((x) => x.hour === h); if (!s) out.push(`${fmtDate(r.date)} ${fmtHour(h)} (outside opening hours)`); else if (!['available'].includes(s.status)) out.push(`${fmtDate(r.date)} ${fmtHour(h)}`); });
    });
    return out;
  };

  /* ---------- Money ---------- */
  const rate = () => db.settings().commission.bookings;
  /** Entry-fee lines for tournaments on the given pitches, optionally between two ISO dates (by registration date) */
  const income = (pitchIds, from, to) => {
    const out = [];
    db.all('tournaments').forEach((t) => {
      if (!pitchIds.includes(t.pitchId) || t.status === 'cancelled') return;
      const firstDay = (t.reserved[0] || {}).date || t.startDate;
      t.teams.forEach((tm) => {
        if (!tm.paid) return;
        const d = isoDate(new Date(tm.createdAt));
        if (from && (d < from || d > to)) return;
        const online = tm.payment === 'Card' || tm.payment === 'Wallet';
        const commission = online ? Math.round(t.fee * rate()) : 0;
        out.push({ id: `${t.id}|${tm.id}`, tournamentId: t.id, name: t.name, team: tm.name, date: d, gross: t.fee, commission, net: t.fee - commission, online: online ? t.fee : 0, cash: online ? 0 : t.fee, availableAt: parseISO(firstDay).getTime() + DAY, payment: tm.payment });
      });
    });
    return out;
  };

  const validateCreate = (d, p) => {
    const e = {};
    if (!d.name || d.name.trim().length < 4) e.name = 'Give the tournament a name (at least 4 characters).';
    if (!['knockout', 'league'].includes(d.format)) e.format = 'Choose a format.';
    const n = +d.maxTeams;
    if (d.format === 'knockout' && ![4, 8, 16].includes(n)) e.maxTeams = 'Knockout cups need 4, 8 or 16 teams.';
    if (d.format === 'league' && !(n >= 3 && n <= 10)) e.maxTeams = 'Leagues can have 3 to 10 teams.';
    if (!(+d.fee >= 0 && +d.fee <= 50000)) e.fee = 'Entry fee must be between 0 and 50,000 EGP.';
    if (!d.startDate || d.startDate <= isoDate(today())) e.startDate = 'The first match day must be after today.';
    if (!d.regDeadline || d.regDeadline < isoDate(today()) || d.regDeadline >= d.startDate) e.regDeadline = 'Registration must close before the first match day.';
    if (p && !(+d.hour >= p.open[0] && +d.hour < p.open[1])) e.hour = 'Kick-off must be inside the opening hours.';
    if ((d.rules || '').trim().length < 10) e.rules = 'Write a few lines of rules (at least 10 characters).';
    return e;
  };

  const tournaments = {
    SIZES, plan, standings, bracket, winnerOf, roundName, income,
    /* ---------- Site ---------- */
    list: (f = {}) => request(() => db.all('tournaments').filter((t) => t.status !== 'cancelled' && (!f.status || (f.status === 'open' ? t.status === 'open' : f.status === 'running' ? ['running', 'full'].includes(t.status) : t.status === f.status))).map(view).sort((a, b) => (a.status === 'finished') - (b.status === 'finished') || a.firstDay.localeCompare(b.firstDay))),
    get: (id) => request(() => { const t = db.find('tournaments', id); if (!t || t.status === 'cancelled') throw new ApiError('This tournament isn’t available.', 'not_found'); return view(t); }),
    getSync: (id) => { const t = db.find('tournaments', id); return t ? view(t) : null; },
    forPitch: (pitchId) => db.all('tournaments').filter((t) => t.pitchId === pitchId && ['open', 'full', 'running'].includes(t.status)).map(view),
    teamName,
    async register(id, { team, captain, phone, players, payment, waiver = false }) {
      SA.services.user.assertActive();
      SA.services.legal?.requireWaiver(waiver, 'tournament entry');
      const t0 = db.find('tournaments', id);
      if (!t0) throw new ApiError('Tournament not found.', 'not_found');
      const v = view(t0);
      const e = {};
      if (!team || team.trim().length < 3) e.team = 'Enter your team name (at least 3 characters).';
      else if (t0.teams.some((x) => x.name.toLowerCase() === team.trim().toLowerCase())) e.team = 'A team with this name is already registered.';
      if (!captain || captain.trim().length < 3) e.captain = 'Enter the captain’s name.';
      if (!/^\+?[0-9 ]{8,16}$/.test((phone || '').trim())) e.phone = 'Enter a valid phone number.';
      if (!(+players >= SIZES[t0.teamSize] && +players <= SIZES[t0.teamSize] * 2 + 2)) e.players = `Squad size must be ${SIZES[t0.teamSize]}–${SIZES[t0.teamSize] * 2 + 2} players.`;
      if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e);
      if (!v.regOpen) throw new ApiError(v.spotsLeft ? 'Registration has closed.' : 'This tournament is full.', 'closed');
      const paid = t0.fee ? await SA.services.payment.charge({ ...payment, amount: t0.fee, label: `Tournament entry — ${t0.name}` }) : { method: 'free', label: 'Free entry', id: uid('FREE') };
      return request(() => {
        const t = db.find('tournaments', id);
        if (t.teams.length >= t.maxTeams) { if (t0.fee) SA.services.payment.refund(t0.fee, `Refund — ${t0.name} (full)`); throw new ApiError('The last spot was just taken — your payment was refunded.', 'closed'); }
        const u = SA.core.store.get().user || {};
        const tm = { id: uid('tm'), name: team.trim(), captain: captain.trim(), phone: phone.trim(), players: +players, paid: !!t0.fee, payment: paid.method === 'wallet' ? 'Wallet' : paid.method === 'free' ? 'Free' : 'Card', ref: paid.id, userId: u.id || 'u-001', createdAt: Date.now() };
        const teams = [...t.teams, tm];
        db.update('tournaments', id, { teams, status: teams.length >= t.maxTeams ? 'full' : t.status });
        db.log('customer', 'tournament.registered', `${tm.name} registered for ${t.name}`, id);
        SA.services.user.notify({ kind: 'match', title: `${tm.name} is in!`, body: `You’re registered for ${t.name}. Fixtures are published after registration closes.`, link: `/tournaments/${id}` });
        return tm;
      });
    },

    /* ---------- Organiser (dashboard) ---------- */
    mine({ ownerId } = {}) {
      const a = D().me(); if (!a) throw new ApiError('Sign in first.', 'unauthenticated');
      const ids = new Set(D().myPitches({ ownerId, includeDeleted: true }).map((p) => p.id));
      return db.all('tournaments').filter((t) => ids.has(t.pitchId)).map(view).sort((x, y) => (x.status === 'cancelled') - (y.status === 'cancelled') || (x.status === 'finished') - (y.status === 'finished') || y.createdAt - x.createdAt);
    },
    manage(id) { const t = db.find('tournaments', id); if (!t) throw new ApiError('Tournament not found.', 'not_found'); D().pitch(t.pitchId); return view(t); },
    preview(d) {
      const p = db.find('pitches', d.pitchId);
      if (!p || !d.startDate || !(+d.maxTeams > 1)) return null;
      const days = plan(d.format, +d.maxTeams, d.startDate);
      const reserved = reservedFrom(p, days, +d.hour);
      return { days, reserved, conflicts: conflicts(p, reserved), fits: reserved.every((r) => r.hours[r.hours.length - 1] < p.open[1]) };
    },
    create(d) {
      const p = D().pitch(d.pitchId);
      const e = validateCreate(d, p);
      if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e);
      const pv = tournaments.preview(d);
      if (!pv.fits) throw new ApiError('The matches run past closing time. Start earlier or use fewer teams.', 'invalid', { hour: 'Too late for all matches' });
      if (pv.conflicts.length) throw new ApiError(`These hours are already taken: ${pv.conflicts.slice(0, 4).join(', ')}${pv.conflicts.length > 4 ? '…' : ''}`, 'invalid', { startDate: 'Some match hours are already booked' });
      const t = db.insert('tournaments', {
        id: `tr-${d.name.trim().toLowerCase().replace(/[^a-z0-9]+/g, '-').slice(0, 20)}-${Math.random().toString(36).slice(2, 5)}`, ownerId: p.ownerId, pitchId: p.id, name: d.name.trim(), format: d.format, teamSize: p.format,
        maxTeams: +d.maxTeams, fee: Math.round(+d.fee), prize: (d.prize || '').trim(), startDate: d.startDate, regDeadline: d.regDeadline, hour: +d.hour, rules: d.rules.trim(), cover: d.cover || p.images[0],
        status: 'open', teams: [], fixtures: [], reserved: pv.reserved,
      }, 'tr');
      reserve(t);
      db.log(D().me().id, 'tournament.created', `Created ${t.format === 'league' ? 'league' : 'cup'} “${t.name}” at ${p.name} (${pv.days.length} match days reserved)`, t.id);
      return t;
    },
    addTeam(id, d) {
      tournaments.manage(id);
      const t = db.find('tournaments', id);
      if (t.teams.length >= t.maxTeams) throw new ApiError('All spots are taken.', 'closed');
      if (t.fixtures.length) throw new ApiError('Fixtures are already out — teams can’t be added now.', 'closed');
      if (!d.name || d.name.trim().length < 3) throw new ApiError('Enter the team name.', 'invalid', { name: 'Required' });
      if (t.teams.some((x) => x.name.toLowerCase() === d.name.trim().toLowerCase())) throw new ApiError('That team is already registered.', 'invalid', { name: 'Duplicate' });
      const tm = { id: uid('tm'), name: d.name.trim(), captain: (d.captain || '').trim(), phone: (d.phone || '').trim(), players: SIZES[t.teamSize], paid: !!t.fee && d.paid !== false, payment: 'Cash at venue', userId: null, createdAt: Date.now() };
      const teams = [...t.teams, tm];
      db.update('tournaments', id, { teams, status: teams.length >= t.maxTeams && t.status === 'open' ? 'full' : t.status });
      db.log(D().me().id, 'tournament.team', `Added ${tm.name} to ${t.name}`, id);
      return tm;
    },
    async removeTeam(id, teamId) {
      tournaments.manage(id);
      const t = db.find('tournaments', id);
      if (t.fixtures.length) throw new ApiError('Fixtures are already out — teams can’t be removed now.', 'closed');
      const tm = t.teams.find((x) => x.id === teamId); if (!tm) return;
      if (tm.userId === (SA.core.store.get().user?.id || 'u-001') && tm.paid && t.fee) {
        await SA.services.payment.refund(t.fee, `Refund — ${t.name} entry`);
        SA.services.user.notify({ kind: 'match', title: `${tm.name} was removed from ${t.name}`, body: `${t.fee} EGP is back in your wallet.`, link: `/tournaments/${id}` });
      }
      db.update('tournaments', id, { teams: t.teams.filter((x) => x.id !== teamId), status: t.status === 'full' ? 'open' : t.status });
      db.log(D().me().id, 'tournament.team-removed', `Removed ${tm.name} from ${t.name}`, id);
    },
    /** Draw fixtures for the registered teams; frees reserved hours that aren't needed */
    generate(id) {
      tournaments.manage(id);
      const t = db.find('tournaments', id);
      const n = t.teams.length;
      if (t.fixtures.length) throw new ApiError('Fixtures are already out.', 'invalid');
      if (t.format === 'knockout' && !isPow2(n)) throw new ApiError(`A knockout cup needs 4, 8 or 16 teams — you have ${n}.`, 'invalid');
      if (t.format === 'league' && n < 3) throw new ApiError('A league needs at least 3 teams.', 'invalid');
      const ids = t.teams.map((x) => x.id).sort((a, b) => seeded(`${t.id}|${a}`) - seeded(`${t.id}|${b}`));
      const days = plan(t.format, n, t.startDate);
      const fixtures = [];
      if (t.format === 'league') {
        roundRobin(ids).forEach((pairs, r) => pairs.forEach(([h, a], j) => fixtures.push({ id: uid('fx'), round: r, slot: j, date: days[r].date, hour: t.hour + j, home: h, away: a, hs: null, as: null, status: 'scheduled' })));
      } else {
        for (let j = 0; j < n / 2; j++) fixtures.push({ id: uid('fx'), round: 0, slot: j, date: days[0].date, hour: t.hour + j, home: ids[2 * j], away: ids[2 * j + 1], hs: null, as: null, status: 'scheduled' });
      }
      const reserved = reservedFrom(db.find('pitches', t.pitchId), days, t.hour);
      db.update('tournaments', id, { fixtures, reserved, status: 'running' });
      reserve(db.find('tournaments', id));
      const me = SA.core.store.get().user?.id || 'u-001';
      t.teams.filter((x) => x.userId === me).forEach((x) => { const f = fixtures.find((y) => y.home === x.id || y.away === x.id); if (f) SA.services.user.notify({ kind: 'match', title: `${t.name}: fixtures are out`, body: `${x.name} plays ${teamName(t, f.home === x.id ? f.away : f.home)} on ${fmtDate(f.date)} at ${fmtHour(f.hour)}.`, link: `/tournaments/${id}` }); });
      db.log(D().me().id, 'tournament.fixtures', `Published fixtures for ${t.name} (${fixtures.length} matches)`, id);
      return fixtures;
    },
    setResult(id, fixtureId, { hs, as, pens }) {
      tournaments.manage(id);
      const t = db.find('tournaments', id);
      const f = t.fixtures.find((x) => x.id === fixtureId);
      if (!f) throw new ApiError('Match not found.', 'not_found');
      const h = +hs, a = +as;
      if (!(Number.isInteger(h) && Number.isInteger(a) && h >= 0 && a >= 0 && h <= 40 && a <= 40)) throw new ApiError('Enter whole-number scores.', 'invalid');
      if (t.format === 'knockout' && h === a && ![f.home, f.away].includes(pens)) throw new ApiError('It’s a draw — choose who won on penalties.', 'invalid', { pens: 'Required' });
      if (t.format === 'knockout' && t.fixtures.some((x) => x.round > f.round && x.status === 'played')) throw new ApiError('A later round is already played — this result can’t change.', 'invalid');
      let fixtures = t.fixtures.map((x) => (x.id === fixtureId ? { ...x, hs: h, as: a, pens: h === a ? pens || null : null, status: 'played' } : x));
      let status = t.status;
      if (t.format === 'knockout') {
        const round = fixtures.filter((x) => x.round === f.round);
        fixtures = fixtures.filter((x) => x.round <= f.round); // a changed result re-draws later rounds
        if (round.every((x) => x.status === 'played')) {
          if (round.length === 1) status = 'finished';
          else {
            const days = plan('knockout', t.teams.length, t.startDate);
            const winners = round.sort((x, y) => x.slot - y.slot).map(winnerOf);
            for (let j = 0; j < winners.length / 2; j++) fixtures.push({ id: uid('fx'), round: f.round + 1, slot: j, date: days[f.round + 1].date, hour: t.hour + j, home: winners[2 * j], away: winners[2 * j + 1], hs: null, as: null, status: 'scheduled' });
            status = 'running';
          }
        } else status = 'running';
      } else status = fixtures.every((x) => x.status === 'played') ? 'finished' : 'running';
      db.update('tournaments', id, { fixtures, status });
      if (status === 'finished') {
        const v = view(db.find('tournaments', id));
        db.log(D().me().id, 'tournament.finished', `${t.name} finished — champion: ${v.champion}`, id);
        v.mine.forEach((x) => SA.services.user.notify({ kind: 'match', title: `${t.name} is over`, body: v.champion === x.name ? `${x.name} are the champions! 🏆` : `Champion: ${v.champion}. Thanks for playing, ${x.name}.`, link: `/tournaments/${id}` }));
      }
      return view(db.find('tournaments', id));
    },
    async cancel(id) {
      tournaments.manage(id);
      const t = db.find('tournaments', id);
      const me = SA.core.store.get().user?.id || 'u-001';
      for (const tm of t.teams) if (tm.userId === me && tm.paid && t.fee) { await SA.services.payment.refund(t.fee, `Refund — ${t.name} cancelled`); SA.services.user.notify({ kind: 'match', title: `${t.name} was cancelled`, body: `${t.fee} EGP is back in your wallet.`, link: '/tournaments' }); }
      db.all('closures').filter((c) => c.tournamentId === id).forEach((c) => db.remove('closures', c.id));
      db.update('tournaments', id, { status: 'cancelled' });
      db.log(D().me().id, 'tournament.cancelled', `Cancelled ${t.name} — ${t.teams.length} team${t.teams.length === 1 ? '' : 's'} refunded`, id);
    },
  };

  /* ---------- Demo seed: an open cup, a running league and a finished cup ---------- */
  const NAMES = ['Zamalek Street FC', 'Maadi Falcons', 'Nasr City Lions', 'Dokki Dynamos', 'Heliopolis Hawks', 'October Eagles', 'Zayed United', 'New Cairo Rangers', 'Giza Pharaohs', 'Mokattam Wolves', 'Shoubra Stars', 'Rehab Rockets'];
  const PEOPLE = SA.data.matches.people;
  const nextWeekday = (from, wd) => { const d = new Date(from); while (d.getDay() !== wd) d.setDate(d.getDate() + 1); return d; };
  const seedTeams = (key, n, start, online) => Array.from({ length: n }, (_, i) => ({ id: `tm-${key}-${i}`, name: NAMES[(i * 5 + key.length) % NAMES.length], captain: PEOPLE[(i * 7 + key.length) % PEOPLE.length], phone: `+20 10${i} ${100 + i * 37} ${4000 + i * 211}`, players: 0, paid: true, payment: i < online ? 'Card' : 'Cash at venue', userId: null, createdAt: start - (n - i) * 2 * DAY }));
  const score = (s) => [Math.floor(seeded(s + 'h') * 5), Math.floor(seeded(s + 'a') * 4)];
  const seed = () => {
    if (db.settings().seededTournaments) return;
    const now = Date.now();
    const list = [];
    // 1) Open knockout cup at Turf 22 Arena, two Fridays from now, 5 of 8 teams in
    {
      const start = isoDate(nextWeekday(addDays(today(), 9), 5));
      const t = { id: 'tr-autumn-cup', ownerId: 'acc-o1', pitchId: 'p-zayed-22', name: 'Turf 22 Autumn Cup', format: 'knockout', teamSize: '5-a-side', maxTeams: 8, fee: 1200, prize: '6,000 EGP + trophy for the winners, 2,000 EGP for the runners-up', startDate: start, regDeadline: isoDate(addDays(parseISO(start), -2)), hour: 16, rules: '5-a-side, 2 × 20 minutes, rolling subs. Draws go straight to penalties. Shin pads required. Arrive 20 minutes before your kick-off.', cover: 'stringLights', status: 'open', fixtures: [], createdAt: now - 6 * DAY, updatedAt: now - 6 * DAY };
      t.teams = seedTeams('cup', 5, now, 3).map((x) => ({ ...x, players: 7 }));
      t.reserved = reservedFrom(db.find('pitches', t.pitchId), plan('knockout', 8, start), t.hour);
      list.push(t);
    }
    // 2) Weekday league at Maadi Cage: started three Mondays ago, 3 match days played
    {
      const start = isoDate(addDays(nextWeekday(addDays(today(), -27), 1), 0));
      const t = { id: 'tr-maadi-league', ownerId: 'acc-o3', pitchId: 'p-maadi-cage', name: 'Maadi Weekday League', format: 'league', teamSize: db.find('pitches', 'p-maadi-cage')?.format || '5-a-side', maxTeams: 6, fee: 900, prize: '4,500 EGP for the champions', startDate: start, regDeadline: isoDate(addDays(parseISO(start), -3)), hour: 20, rules: 'Round robin, 3 points for a win. Ties broken by goal difference, then goals scored. Kick-offs are on time — 10 minutes late is a forfeit.', cover: 'fieldLights', status: 'running', createdAt: now - 40 * DAY, updatedAt: now - 3 * DAY };
      t.teams = seedTeams('league', 6, parseISO(start).getTime(), 4).map((x) => ({ ...x, players: 8 }));
      const days = plan('league', 6, start);
      t.fixtures = [];
      roundRobin(t.teams.map((x) => x.id)).forEach((pairs, r) => pairs.forEach(([h, a], j) => {
        const date = days[r].date, played = parseISO(date).getTime() + (t.hour + j + 1) * 36e5 < now;
        const [hs, as] = score(`${t.id}${r}${j}`);
        t.fixtures.push({ id: `fx-l-${r}-${j}`, round: r, slot: j, date, hour: t.hour + j, home: h, away: a, hs: played ? hs : null, as: played ? as : null, status: played ? 'played' : 'scheduled' });
      }));
      t.reserved = reservedFrom(null, days, t.hour);
      list.push(t);
    }
    // 3) Finished 4-team cup at Gezira Fives last month
    {
      const start = isoDate(addDays(today(), -44));
      const t = { id: 'tr-gezira-cup', ownerId: 'acc-o2', pitchId: 'p-gezira-fives', name: 'Gezira Summer Cup', format: 'knockout', teamSize: '5-a-side', maxTeams: 4, fee: 800, prize: '3,000 EGP for the winners', startDate: start, regDeadline: isoDate(addDays(parseISO(start), -2)), hour: 19, rules: 'Single elimination. 2 × 15 minutes, penalties if level.', cover: 'nightPlayers', status: 'finished', createdAt: now - 60 * DAY, updatedAt: now - 37 * DAY };
      t.teams = seedTeams('gz', 4, parseISO(start).getTime(), 2).map((x) => ({ ...x, players: 6 }));
      const days = plan('knockout', 4, start);
      const [a, b, c, d] = t.teams.map((x) => x.id);
      t.fixtures = [
        { id: 'fx-g-0', round: 0, slot: 0, date: days[0].date, hour: 19, home: a, away: b, hs: 3, as: 1, status: 'played' },
        { id: 'fx-g-1', round: 0, slot: 1, date: days[0].date, hour: 20, home: c, away: d, hs: 2, as: 2, pens: d, status: 'played' },
        { id: 'fx-g-2', round: 1, slot: 0, date: days[1].date, hour: 19, home: a, away: d, hs: 1, as: 2, status: 'played' },
      ];
      t.reserved = reservedFrom(null, days, t.hour);
      list.push(t);
    }
    list.forEach((t) => { db.insert('tournaments', t); reserve(t); });
    db.setSettings({ seededTournaments: true });
  };
  try { seed(); } catch (e) { console.warn('Tournament seed skipped', e); }
  db.subscribe((col) => { if (col === '*' && !db.settings().seededTournaments) try { seed(); } catch (e) { console.warn(e); } });

  SA.services.tournaments = tournaments;
})(window.SA);
