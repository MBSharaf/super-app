(function (SA) {
  const { esc, money, fantasyPrice, fmtNum, fmtDate, sum } = SA.core.utils;
  const { icon } = SA.ui;
  const c = SA.ui.c;
  const F = () => SA.services.fantasy;

  const short = (name) => { const parts = name.split(' '); return parts.length > 1 ? parts.slice(1).join(' ') : name; };
  const fdrCls = (n) => `fdr-${n}`;

  /* ---------- Shared header for all fantasy routes ---------- */
  const header = (active) => {
    const f = F(), meta = f.meta(), team = f.team(), tot = f.totals(), last = f.lastResult();
    const nav = [['team', 'My team', '#/fantasy'], ['squad', team ? 'Transfers' : 'Pick squad', '#/fantasy/squad'], ['leagues', 'Leagues', '#/fantasy/leagues'], ['fixtures', 'Fixtures & table', '#/fantasy/fixtures'], ['stats', 'Player stats', '#/fantasy/stats']];
    return `<section class="fx-head">
      <div class="container">
        <div class="fx-head-top">
          <div>
            <div class="fx-leagues" role="tablist" aria-label="Choose the league you play">${f.leagues().map((l) => `<button class="fx-league" role="tab" data-league="${l.key}" aria-selected="${l.key === meta.league}">${esc(l.label)}${l.hasTeam && l.key !== meta.league ? ` <span class="fx-dot" aria-label="team ready"></span>` : ''}</button>`).join('')}</div>
            <span class="xs fx-dim">${esc(meta.leagueLabel)} · Fantasy ${meta.season}${f.pass() ? ` · ${icon('crown', 11, 2)} Fantasy Pass` : ''}</span>
            <h1 class="h1 fx-title">${team ? esc(team.name) : 'Fantasy'}</h1>
          </div>
          <div class="fx-deadline">
            <span class="xs fx-dim">Gameweek ${meta.gw} deadline · ${fmtDate(meta.deadline)}, ${meta.deadline.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })}</span>
            ${c.countdown(meta.deadline)}
          </div>
        </div>
        ${team ? `<div class="fx-stats">
          <div><span class="fx-dim xs">GW${last ? last.gw : meta.gw} points</span><b class="num">${last ? last.pts : '–'}</b></div>
          <div><span class="fx-dim xs">Total points</span><b class="num">${tot.total}</b></div>
          <div><span class="fx-dim xs">Overall rank</span><b class="num">${last ? fmtNum(last.rank) : '–'}</b></div>
          <div><span class="fx-dim xs">Free transfers</span><b class="num">${team.freeTransfers}</b></div>
          <div><span class="fx-dim xs">Bank</span><b class="num">${fantasyPrice(meta.budget - f.cost(team.squad))}</b></div>
          <button class="btn btn-blaze fx-sim" data-simulate title="Plays every GW${meta.gw} fixture and scores your team">${icon('play', 16)} Simulate GW${meta.gw}</button>
        </div>` : ''}
        <nav class="subnav fx-subnav" aria-label="Fantasy">${nav.map(([k, l, h]) => `<a href="${h}" ${k === active ? 'aria-current="page"' : ''}>${l}</a>`).join('')}</nav>
      </div>
    </section>`;
  };

  /** Runs the GW simulation from any fantasy page */
  const bindHeader = (el) => {
    const offs = [c.tickCountdowns(el)];
    el.querySelectorAll('[data-league]').forEach((b) => b.addEventListener('click', () => {
      if (b.getAttribute('aria-selected') === 'true') return;
      F().use(b.dataset.league);
      swapFrom = null; st.team = ''; // these refer to the previous league's players and clubs
      SA.ui.overlay.toast(`Now playing: ${F().leagueInfo().label}`);
      // Pages that show one player or league from the other dataset go back to the team page
      if (/^\/fantasy\/(league|player)\//.test(SA.core.router.location())) SA.core.router.go('/fantasy'); else SA.app.rerender();
    }));
    el.querySelector('[data-simulate]')?.addEventListener('click', async (e) => {
      const b = e.currentTarget; const gw = F().meta().gw;
      b.disabled = true; b.innerHTML = `<span class="spinner"></span> Playing GW${gw}…`;
      const { result } = await F().simulateGameweek();
      await SA.app.rerender();
      if (result) { showResult(result); setTimeout(() => SA.core.motion.confetti({ y: innerHeight * 0.3 }), 250); }
    });
    return () => offs.forEach((f) => f());
  };

  const showResult = (r) => {
    const f = F();
    const top = r.xi.map((id) => ({ p: f.byId[id], pts: f.byId[id].gw[r.gw] * (id === r.captain ? 2 : 1) })).sort((a, b) => b.pts - a.pts).slice(0, 3);
    SA.ui.overlay.open({
      title: `Gameweek ${r.gw} result`,
      body: `<div class="gw-result"><b class="gw-big num">${r.pts}</b><span class="soft">points${r.hits ? ` (after −${r.hits} transfer cost)` : ''}</span></div>
        <dl class="kv" style="margin:18px 0"><div><dt>Gameweek rank</dt><dd class="num">${fmtNum(r.gwRank)}</dd></div><div><dt>Overall rank</dt><dd class="num">${fmtNum(r.rank)}</dd></div><div><dt>Captain</dt><dd>${esc(f.byId[r.captain].name)} · ${f.byId[r.captain].gw[r.gw] * 2} pts</dd></div></dl>
        <h3 class="small strong">Your top scorers</h3><ul class="mini-bookings">${top.map((x) => `<li><span class="row grow">${c.kit(f.teamById[x.p.teamId], 28, x.p.pos === 'GK')}<b class="small">${esc(x.p.name)}</b></span><b class="num">${x.pts}</b></li>`).join('')}</ul>`,
      footer: `<a class="btn btn-outline" href="#/fantasy/leagues" data-close>See league tables</a><button class="btn btn-primary" data-close>Done</button>`,
    });
  };

  /* ---------- Player info modal ---------- */
  const openPlayer = (id, { actions = '' , onMount } = {}) => {
    const f = F(), p = f.player(id);
    SA.ui.overlay.open({
      wide: true, label: p.name,
      body: `<div class="pl-head" style="--kit:${p.team.kit.a}">
          <div class="pl-kit">${c.kit(p.team, 96, p.pos === 'GK')}</div>
          <div class="grow"><span class="badge badge-glass">${p.pos === 'GK' ? 'Goalkeeper' : p.pos === 'DEF' ? 'Defender' : p.pos === 'MID' ? 'Midfielder' : 'Forward'}</span>
            <h2 class="h2" style="margin-top:8px">${esc(p.name)}</h2><p class="soft">${esc(p.team.name)} · ${esc(p.team.city)}</p></div>
          <button class="icon-btn" data-close aria-label="Close">${icon('x')}</button>
        </div>
        ${p.news ? `<div class="notice ${p.status === 'injured' ? 'blaze' : 'gold'}" style="margin-top:14px">${icon('alert', 18)}<span>${esc(p.news)}</span></div>` : ''}
        <div class="pl-kpis">
          <div><span class="xs muted">Price</span><b class="num">${fantasyPrice(p.price)}</b></div>
          <div><span class="xs muted">Total points</span><b class="num">${p.total}</b></div>
          <div><span class="xs muted">Form</span><b class="num">${p.form.toFixed(1)}</b></div>
          <div><span class="xs muted">Selected by</span><b class="num">${p.selectedBy}%</b></div>
        </div>
        <div class="pl-grid">
          <div>
            <h3 class="small strong">Season stats</h3>
            <dl class="kv" style="margin-top:10px">
              <div><dt>Minutes</dt><dd class="num">${fmtNum(p.stats.minutes)}</dd></div><div><dt>Goals</dt><dd class="num">${p.stats.goals}</dd></div><div><dt>Assists</dt><dd class="num">${p.stats.assists}</dd></div>
              ${p.pos === 'GK' || p.pos === 'DEF' ? `<div><dt>Clean sheets</dt><dd class="num">${p.stats.cleanSheets}</dd></div>` : ''}${p.pos === 'GK' ? `<div><dt>Saves</dt><dd class="num">${p.stats.saves}</dd></div>` : ''}
              <div><dt>Bonus</dt><dd class="num">${p.stats.bonus}</dd></div><div><dt>Yellow cards</dt><dd class="num">${p.stats.yellow}</dd></div>
            </dl>
            <h3 class="small strong" style="margin-top:20px">Next fixtures</h3>
            <ul class="fix-strip">${p.upcoming.map((u) => `<li class="${fdrCls(u.fdr)}"><span class="xs">GW${u.gw}</span><b>${u.opp}</b><span class="xs">${u.home ? 'Home' : 'Away'}</span></li>`).join('')}</ul>
          </div>
          <div>
            <h3 class="small strong">Points by gameweek</h3>
            <div class="gw-bars">${p.past.slice().reverse().map((g) => `<div class="gw-bar" title="GW${g.gw}: ${g.pts} pts"><span style="height:${Math.max(4, g.pts * 6)}px" class="${g.pts >= 8 ? 'is-high' : ''}"></span><i class="xs">${g.gw}</i></div>`).join('')}</div>
            <div class="table-wrap" style="margin-top:14px"><table class="table"><thead><tr><th>GW</th><th class="r">Min</th><th class="r">G</th><th class="r">A</th><th class="r">Pts</th></tr></thead>
              <tbody>${p.past.slice(0, 5).map((g) => `<tr><td>${g.gw}</td><td class="r num">${g.minutes}</td><td class="r num">${g.goals}</td><td class="r num">${g.assists}</td><td class="r num strong">${g.pts}</td></tr>`).join('')}</tbody></table></div>
          </div>
        </div>`,
      footer: actions,
      onMount,
    });
  };

  /* ---------- My team (pitch view) ---------- */
  let swapFrom = null;
  let view = 'pitch';

  const token = (id, team, { bench = false } = {}) => {
    const f = F(), p = f.byId[id];
    const nx = f.nextOpponent(p.teamId);
    const isC = team.captain === id, isV = team.vice === id;
    const lastGW = f.meta().gw - 1;
    const eligible = swapFrom && swapFrom !== id && (team.xi.includes(swapFrom) !== team.xi.includes(id));
    return `<button class="ptoken ${swapFrom === id ? 'is-swapping' : ''} ${eligible ? 'is-eligible' : ''} ${p.status !== 'fit' ? 'is-' + p.status : ''}" data-pid="${id}" aria-label="${esc(p.name)}${isC ? ', captain' : isV ? ', vice-captain' : ''}">
      <span class="ptoken-kit">${c.kit(f.teamById[p.teamId], bench ? 46 : 54, p.pos === 'GK')}${isC ? '<i class="cap">C</i>' : isV ? '<i class="cap vice">V</i>' : ''}${p.status !== 'fit' ? `<i class="flag">!</i>` : ''}</span>
      <span class="ptoken-name">${esc(short(p.name))}</span>
      <span class="ptoken-sub ${nx ? fdrCls(nx.fdr) : ''}">${team.history?.length && lastGW >= team.createdGW ? `${(p.gw[lastGW] || 0) * (isC ? 2 : 1)} pts` : nx ? `${nx.opp} (${nx.home ? 'H' : 'A'})` : '–'}</span>
    </button>`;
  };

  const pitchView = (team) => {
    const f = F();
    const rows = ['GK', 'DEF', 'MID', 'FWD'].map((pos) => team.xi.filter((id) => f.byId[id].pos === pos));
    return `<div class="fx-pitch">
        <svg class="fx-pitch-lines" viewBox="0 0 100 140" preserveAspectRatio="none" aria-hidden="true"><g fill="none" stroke="rgba(255,255,255,.35)" stroke-width=".4"><rect x="3" y="3" width="94" height="134"/><line x1="3" y1="70" x2="97" y2="70"/><circle cx="50" cy="70" r="11"/><rect x="25" y="3" width="50" height="18"/><rect x="38" y="3" width="24" height="7"/><rect x="25" y="119" width="50" height="18"/><rect x="38" y="130" width="24" height="7"/></g></svg>
        ${rows.map((r) => `<div class="fx-row">${r.map((id) => token(id, team)).join('')}</div>`).join('')}
      </div>
      <div class="fx-bench"><span class="xs fx-bench-label">Substitutes</span><div class="fx-row">${team.bench.map((id, i) => `<div class="bench-slot"><span class="xs muted">${i === 0 ? 'GK' : i}</span>${token(id, team, { bench: true })}</div>`).join('')}</div></div>`;
  };

  const listView = (team) => {
    const f = F();
    const row = (id, role) => { const p = f.byId[id]; const nx = f.nextOpponent(p.teamId); return `<tr data-pid="${id}" class="clickable"><td><span class="row">${c.kit(f.teamById[p.teamId], 30, p.pos === 'GK')}<span><b class="small">${esc(p.name)}</b> ${team.captain === id ? '<span class="badge badge-ink">C</span>' : team.vice === id ? '<span class="badge">V</span>' : ''} ${c.statusDot(p.status)}<span class="xs muted" style="display:block">${f.teamById[p.teamId].name} · ${p.pos}</span></span></span></td><td class="r num">${fantasyPrice(p.price)}</td><td class="r num">${p.form.toFixed(1)}</td><td class="r num">${p.total}</td><td class="r">${nx ? `<span class="fdr-pill ${fdrCls(nx.fdr)}">${nx.opp} ${nx.home ? 'H' : 'A'}</span>` : ''}</td><td class="xs muted">${role}</td></tr>`; };
    return `<div class="table-wrap panel"><table class="table"><thead><tr><th>Player</th><th class="r">Price</th><th class="r">Form</th><th class="r">Pts</th><th class="r">Next</th><th></th></tr></thead>
      <tbody>${team.xi.map((id) => row(id, 'Starting')).join('')}${team.bench.map((id) => row(id, 'Bench')).join('')}</tbody></table></div>`;
  };

  const onboarding = () => `
    <div class="container section-tight">
      <div class="fx-onboard">
        <div>
          <h2 class="h1">Build your ${esc(F().leagueInfo().label)} team</h2>
          <p class="lead" style="margin-top:12px">Pick 15 players with a 100M budget. Score points every gameweek from their goals, assists and clean sheets — then beat your friends in private groups or win prizes from our sponsors in Fantasy Pass leagues.</p>
          <ol class="fx-steps">
            ${[['Pick 15 players', '2 goalkeepers, 5 defenders, 5 midfielders, 3 forwards. Max 3 from one club.'], ['Set your starting XI', 'Choose a formation and a captain — captains score double.'], ['Join leagues', 'Create a private group for friends or enter an official prize league.'], ['Score every gameweek', 'Make a free transfer each week and climb the rankings.']].map(([t, d], i) => `<li><span class="flow-n">${i + 1}</span><div><b>${t}</b><p class="small soft">${d}</p></div></li>`).join('')}
          </ol>
          <div class="row wrap" style="margin-top:24px">${c.cta('Pick my squad', { href: '#/fantasy/squad', size: 'lg' })}<button class="btn btn-outline btn-lg" data-autocreate>${icon('sparkle', 18)} Auto-pick for me</button></div>
        </div>
        <div class="fx-onboard-art">${(() => { const f = F(); const ids = f.autoPick('preview-team'); const { xi } = f.lineupFor(ids, '4-3-3'); return `<div class="fx-pitch is-preview">${['GK', 'DEF', 'MID', 'FWD'].map((pos) => `<div class="fx-row">${xi.filter((id) => f.byId[id].pos === pos).map((id) => `<span class="ptoken"><span class="ptoken-kit">${c.kit(f.teamById[f.byId[id].teamId], 46, pos === 'GK')}</span><span class="ptoken-name">${esc(short(f.byId[id].name))}</span></span>`).join('')}</div>`).join('')}</div>`; })()}</div>
      </div>
    </div>`;

  SA.pages.fantasy = {
    title: 'Fantasy — Super App',
    header, bindHeader, openPlayer, short,
    render() {
      const f = F(), team = f.team();
      if (!team) return header('team') + onboarding();
      const hist = team.history || [];
      const cap = f.byId[team.captain], vice = f.byId[team.vice];
      return `${header('team')}
      <div class="container fx-body">
        <div class="fx-team-layout">
          <div>
            <div class="row-between wrap" style="margin-bottom:14px">
              <div class="tabs" role="tablist"><button class="tab" data-view="pitch" aria-selected="${view === 'pitch'}">Pitch view</button><button class="tab" data-view="list" aria-selected="${view === 'list'}">List view</button></div>
              <label class="sort-select"><span class="small soft">Formation</span><select class="select" id="formation" style="width:auto">${f.D.rules.formations.map((fm) => `<option ${fm === f.formationOf(team.xi) ? 'selected' : ''}>${fm}</option>`).join('')}</select></label>
            </div>
            <div class="swap-hint small" ${swapFrom ? '' : 'hidden'}>${icon('swap', 16)} <span>Pick a highlighted player to swap with <b>${swapFrom ? esc(f.byId[swapFrom].name) : ''}</b>.</span> <button class="link" data-cancel-swap>Cancel</button></div>
            ${view === 'pitch' ? pitchView(team) : listView(team)}
            <p class="xs muted" style="margin-top:12px">Tap a player to view stats, change captain or substitute. Changes save automatically until the deadline.</p>
          </div>
          <aside class="stack" style="--gap:16px">
            <div class="panel panel-pad">
              <h2 class="h4">Armband</h2>
              <div class="armband"><span class="cap">C</span>${c.kit(f.teamById[cap.teamId], 34, cap.pos === 'GK')}<span class="grow"><b class="small">${esc(cap.name)}</b><span class="xs muted" style="display:block">Scores double points</span></span></div>
              <div class="armband"><span class="cap vice">V</span>${c.kit(f.teamById[vice.teamId], 34, vice.pos === 'GK')}<span class="grow"><b class="small">${esc(vice.name)}</b><span class="xs muted" style="display:block">Takes over if the captain doesn’t play</span></span></div>
            </div>
            <div class="panel panel-pad">
              <div class="row-between"><h2 class="h4">Your gameweeks</h2><span class="xs muted">${hist.length ? `${hist.length} played` : `Started GW${team.createdGW}`}</span></div>
              ${hist.length ? `<div class="gw-bars" style="margin-top:14px">${hist.map((h) => `<div class="gw-bar" title="GW${h.gw}: ${h.pts} pts"><span style="height:${Math.max(6, h.pts * 1.3)}px" class="${h.pts >= 60 ? 'is-high' : ''}"></span><i class="xs">${h.gw}</i></div>`).join('')}</div>
                <table class="table" style="margin-top:12px"><thead><tr><th>GW</th><th class="r">Pts</th><th class="r">Rank</th></tr></thead><tbody>${hist.slice().reverse().slice(0, 5).map((h) => `<tr><td>${h.gw}</td><td class="r num strong">${h.pts}</td><td class="r num">${fmtNum(h.rank)}</td></tr>`).join('')}</tbody></table>`
                : `<p class="small soft" style="margin-top:8px">Your first gameweek is GW${team.createdGW}. Use <b>Simulate GW${f.meta().gw}</b> above to play it now and see your points.</p>`}
            </div>
            <div class="panel panel-pad">
              <h2 class="h4">Leagues</h2>
              <ul class="mini-bookings">${f.privateGroups().slice(0, 3).map((g) => { const me = f.standings(g).find((r) => r.isMe); return `<li><a href="#/fantasy/league/${g.id}"><span class="grow"><b class="small">${esc(g.name)}</b><span class="xs muted" style="display:block">${g.members.length + (g.members.includes(SA.services.user.me().name) ? 0 : 1)} managers</span></span><span class="num strong">${me ? '#' + me.rank : ''}</span></a></li>`; }).join('')}</ul>
              <a class="btn btn-outline btn-block btn-sm" style="margin-top:12px" href="#/fantasy/leagues">All leagues</a>
            </div>
          </aside>
        </div>
      </div>`;
    },
    mount(el) {
      const off = bindHeader(el);
      const f = F();
      el.querySelector('[data-autocreate]')?.addEventListener('click', () => SA.pages.fantasySquad.promptCreate(f.autoPick()));
      if (!f.team()) return off;
      el.addEventListener('click', (e) => {
        const v = e.target.closest('[data-view]');
        if (v) { view = v.dataset.view; SA.app.rerender(); return; }
        if (e.target.closest('[data-cancel-swap]')) { swapFrom = null; SA.app.rerender(); return; }
        const t = e.target.closest('[data-pid]');
        if (!t) return;
        const id = t.dataset.pid;
        if (swapFrom) {
          if (swapFrom === id) { swapFrom = null; SA.app.rerender(); return; }
          const err = f.swap(swapFrom, id);
          if (err) { SA.ui.overlay.toast(err, { type: 'error' }); return; }
          swapFrom = null; SA.app.rerender(); SA.ui.overlay.toast('Substitution made');
          return;
        }
        const team = f.team();
        const starter = team.xi.includes(id);
        openPlayer(id, {
          actions: `${starter ? `<button class="btn btn-outline btn-sm" data-cap ${team.captain === id ? 'disabled' : ''}>Make captain</button><button class="btn btn-outline btn-sm" data-vice ${team.vice === id ? 'disabled' : ''}>Make vice-captain</button>` : ''}
            <button class="btn btn-primary btn-sm" data-sub>${icon('swap', 14)} Substitute</button><a class="btn btn-ghost btn-sm" href="#/fantasy/squad?out=${id}" data-close>Transfer out</a>`,
          onMount: (m, close) => {
            m.querySelector('[data-cap]')?.addEventListener('click', () => { f.setCaptain(id); close(); SA.app.rerender(); SA.ui.overlay.toast(`${f.byId[id].name} is your captain`); });
            m.querySelector('[data-vice]')?.addEventListener('click', () => { f.setVice(id); close(); SA.app.rerender(); SA.ui.overlay.toast(`${f.byId[id].name} is your vice-captain`); });
            m.querySelector('[data-sub]').addEventListener('click', () => { swapFrom = id; close(); SA.app.rerender(); });
          },
        });
      });
      el.querySelector('#formation')?.addEventListener('change', (e) => { f.setFormation(e.target.value); SA.app.rerender(); SA.ui.overlay.toast(`Formation changed to ${e.target.value}`); });
      return off;
    },
  };

  /* ---------- Fixtures & league table ---------- */
  SA.pages.fantasyFixtures = {
    title: 'Fixtures — Super App Fantasy',
    render(ctx) {
      const f = F(), meta = f.meta();
      const gw = Math.min(meta.totalGW, Math.max(1, +ctx.query.gw || meta.gw));
      const tab = ctx.query.tab === 'table' ? 'table' : 'fixtures';
      const fx = f.fixtures(gw);
      const byDay = {};
      fx.forEach((x) => { const k = fmtDate(f.kickoff(x), 'long'); (byDay[k] ||= []).push(x); });
      const table = f.table();
      return `${header('fixtures')}
      <div class="container fx-body">
        <div class="row-between wrap" style="margin-bottom:18px">
          <div class="tabs"><a class="tab" href="#/fantasy/fixtures?gw=${gw}" aria-selected="${tab === 'fixtures'}">Fixtures & results</a><a class="tab" href="#/fantasy/fixtures?tab=table" aria-selected="${tab === 'table'}">League table</a></div>
          ${tab === 'fixtures' ? `<div class="gw-pager"><a class="icon-btn" href="#/fantasy/fixtures?gw=${gw - 1}" ${gw <= 1 ? 'aria-disabled="true" style="pointer-events:none;opacity:.3"' : ''} aria-label="Previous gameweek">${icon('chevronLeft')}</a><b>Gameweek ${gw}</b>${gw < meta.gw ? '<span class="badge">Finished</span>' : gw === meta.gw ? '<span class="badge badge-blaze">Next up</span>' : '<span class="badge">Upcoming</span>'}<a class="icon-btn" href="#/fantasy/fixtures?gw=${gw + 1}" ${gw >= meta.totalGW ? 'aria-disabled="true" style="pointer-events:none;opacity:.3"' : ''} aria-label="Next gameweek">${icon('chevronRight')}</a></div>` : ''}
        </div>
        ${tab === 'fixtures' ? Object.entries(byDay).map(([day, list]) => `<h2 class="small strong fx-day">${day}</h2>
          <div class="panel fixture-list">${list.map((x) => { const h = f.teamById[x.home], a = f.teamById[x.away]; return `<div class="fixture">
            <span class="fx-team home"><span class="truncate">${esc(h.name)}</span>${c.kit(h, 30)}</span>
            ${x.played ? `<span class="fx-score num">${x.hs} – ${x.as}</span>` : `<span class="fx-time num">${f.kickoff(x).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })}</span>`}
            <span class="fx-team away">${c.kit(a, 30)}<span class="truncate">${esc(a.name)}</span></span></div>`; }).join('')}</div>`).join('')
        : `<div class="panel table-wrap"><table class="table league-table"><thead><tr><th>#</th><th>Club</th><th class="r">P</th><th class="r">W</th><th class="r">D</th><th class="r">${SA.i18n?.isAr ? "خسارة" : "L"}</th><th class="r">GD</th><th class="r">Pts</th><th class="hide-s">Form</th></tr></thead>
          <tbody>${table.map((r, i) => `<tr class="${i < 2 ? 'zone-cl' : i >= table.length - 3 ? 'zone-rel' : ''}"><td class="num">${i + 1}</td><td><span class="row">${c.kit(r.team, 26)}<b class="small">${esc(r.team.name)}</b></span></td><td class="r num">${r.p}</td><td class="r num">${r.w}</td><td class="r num">${r.d}</td><td class="r num">${r.l}</td><td class="r num">${r.gf - r.ga > 0 ? '+' : ''}${r.gf - r.ga}</td><td class="r num strong">${r.pts}</td><td class="hide-s"><span class="form-pills">${r.form.slice(-5).map((x) => `<i class="f-${x}">${x}</i>`).join('')}</span></td></tr>`).join('')}</tbody></table></div>
          <p class="xs muted" style="margin-top:10px">Top 2 qualify for the CAF Champions League. Bottom 3 are relegated. Results are simulated for this demo.</p>`}
      </div>`;
    },
    mount: (el) => bindHeader(el),
  };

  /* ---------- Player stats ---------- */
  const st = { pos: '', team: '', sort: 'total', q: '' };
  SA.pages.fantasyStats = {
    title: 'Player stats — Super App Fantasy',
    render() {
      const f = F();
      return `${header('stats')}
      <div class="container fx-body">
        <div class="stat-filters">
          <div class="tabs">${[['', 'All'], ['GK', 'GK'], ['DEF', 'DEF'], ['MID', 'MID'], ['FWD', 'FWD']].map(([v, l]) => `<button class="tab" data-pos="${v}" aria-selected="${st.pos === v}">${l}</button>`).join('')}</div>
          <select class="select" id="st-team" style="width:auto"><option value="">All clubs</option>${f.D.teams.map((t) => `<option value="${t.id}" ${st.team === t.id ? 'selected' : ''}>${t.name}</option>`).join('')}</select>
          <select class="select" id="st-sort" style="width:auto">${[['total', 'Total points'], ['form', 'Form'], ['price', 'Price'], ['selected', 'Selected by'], ['goals', 'Goals'], ['value', 'Points per million']].map(([v, l]) => `<option value="${v}" ${st.sort === v ? 'selected' : ''}>Sort: ${l}</option>`).join('')}</select>
          <div class="input-icon grow" style="min-width:200px">${icon('search', 18)}<input class="input" id="st-q" placeholder="Search players or clubs" value="${esc(st.q)}" /></div>
        </div>
        <div class="panel table-wrap" style="margin-top:16px"><table class="table"><thead><tr><th>Player</th><th class="r">Price</th><th class="r">Sel.</th><th class="r">Form</th><th class="r hide-s">G</th><th class="r hide-s">A</th><th class="r hide-s">CS</th><th class="r">Pts</th></tr></thead><tbody data-rows></tbody></table></div>
      </div>`;
    },
    mount(el) {
      const off = bindHeader(el);
      const f = F();
      const load = async () => {
        const rows = (await f.players(st)).slice(0, 60);
        el.querySelector('[data-rows]').innerHTML = rows.map((p) => `<tr class="clickable" data-pid="${p.id}"><td><span class="row">${c.kit(f.teamById[p.teamId], 30, p.pos === 'GK')}<span><b class="small">${esc(p.name)}</b> ${c.statusDot(p.status)}<span class="xs muted" style="display:block">${f.teamById[p.teamId].name} · ${p.pos}</span></span></span></td><td class="r num">${fantasyPrice(p.price)}</td><td class="r num">${p.selectedBy}%</td><td class="r num">${p.form.toFixed(1)}</td><td class="r num hide-s">${p.stats.goals}</td><td class="r num hide-s">${p.stats.assists}</td><td class="r num hide-s">${p.stats.cleanSheets}</td><td class="r num strong">${p.total}</td></tr>`).join('') || `<tr><td colspan="8">${c.empty({ icon: 'search', title: 'No players found', text: 'Try a different name or club.' })}</td></tr>`;
        SA.core.motion.enhance(el.querySelector('[data-rows]'));
      };
      el.addEventListener('click', (e) => {
        const p = e.target.closest('[data-pos]'); if (p) { st.pos = p.dataset.pos; el.querySelectorAll('[data-pos]').forEach((b) => b.setAttribute('aria-selected', b === p)); load(); }
        const r = e.target.closest('[data-pid]'); if (r) openPlayer(r.dataset.pid);
      });
      el.querySelector('#st-team').addEventListener('change', (e) => { st.team = e.target.value; load(); });
      el.querySelector('#st-sort').addEventListener('change', (e) => { st.sort = e.target.value; load(); });
      el.querySelector('#st-q').addEventListener('input', SA.core.utils.debounce((e) => { st.q = e.target.value; load(); }, 200));
      load();
      return off;
    },
  };
})(window.SA);
