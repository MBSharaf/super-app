(function (SA) {
  const { esc, money, fmtNum, fantasyPrice } = SA.core.utils;
  const { icon } = SA.ui;
  const c = SA.ui.c;
  const F = () => SA.services.fantasy;

  const leagueCard = (l, meta) => {
    const f = F();
    const me = l.joined && f.team() ? f.standings(l).find((r) => r.isMe) : null;
    const lock = meta.deadline;
    const full = l.cap && l.participants >= l.cap;
    return `<article class="league-card lc-${l.color}">
      <div class="lc-top">
        <div class="row wrap" style="--gap:6px">${l.kind === 'official' ? '<span class="badge badge-glass">Free · Super App</span>' : '<span class="badge badge-glass">Free</span>'}<span class="badge badge-glass">${esc(l.scope)}</span></div>
        <h3 class="h3" style="margin-top:14px">${esc(l.name)}</h3>
        <p class="small lc-dim">${esc(l.blurb)}</p>
        ${l.sponsor ? `<p class="xs lc-dim lc-sponsor">${icon('sparkle', 12)} Prizes from <b>${esc(l.sponsor.name)}</b></p>` : ''}
      </div>
      <div class="lc-figs">
        <div><span class="xs lc-dim">1st prize</span><b class="small lc-prize">${esc(l.prizes?.[0]?.[1] || l.prizeText || '—')}</b></div>
        ${l.kind === 'official' ? `<div><span class="xs lc-dim">Every gameweek</span><b class="small">Top 3 win Super Points</b></div>` : `<div><span class="xs lc-dim">Prizes worth</span><b class="num">${money(l.prizeValue || 0)}</b></div>`}
        <div><span class="xs lc-dim">Managers</span><b class="num">${fmtNum(l.participants)}${l.cap ? `<span class="xs lc-dim"> / ${fmtNum(l.cap)}</span>` : ''}</b></div>
        <div><span class="xs lc-dim">${me ? 'Your rank' : `GW${meta.gw} locks in`}</span><b class="num">${me ? '#' + fmtNum(me.rank) : c.countdown(lock, { compact: true })}</b></div>
      </div>
      ${l.cap ? `<div class="meter lc-meter"><span style="width:${Math.min(100, (l.participants / l.cap) * 100)}%"></span></div>` : ''}
      <div class="lc-foot">
        <a class="btn btn-ghost btn-sm lc-ghost" href="#/fantasy/league/${l.id}">Prizes & rules</a>
        ${l.joined ? `<a class="btn btn-sm lc-joined" href="#/fantasy/league/${l.id}">${icon('check', 14, 2.6)} Entered · view table</a>`
          : full ? `<button class="btn btn-sm" disabled>League full</button>`
          : `<button class="btn btn-sm lc-join" data-join="${l.id}">Enter league</button>`}
      </div>
    </article>`;
  };

  /** Enter a league: every league is free */
  const openJoinLeague = (id) => {
    const f = F();
    if (!f.team()) {
      SA.ui.overlay.toast('Create your fantasy team first — it takes two minutes.', { type: 'info', action: 'Create team', onAction: () => SA.core.router.go('/fantasy/squad') });
      return;
    }
    const l = f.officialLeagues().find((x) => x.id === id);
    SA.ui.overlay.open({
      title: `Enter ${esc(l.name)}`, subtitle: `${esc(l.scope)} · ${fmtNum(l.participants)} managers so far`,
      body: `<div class="prize-mini">${l.prizes.slice(0, 3).map(([p, v]) => `<div><span class="xs muted">${p}</span><b>${esc(v)}</b></div>`).join('')}</div>
        <ul class="assurance small" style="margin:14px 0 18px"><li>${icon('check', 14)} Your current team (${esc(f.team().name)}) is entered — no separate squad needed</li><li>${icon('check', 14)} Ranking uses points scored from GW${l.from}${l.to ? ` to GW${l.to}` : ''}</li><li>${icon('check', 14)} Prizes are sent within 7 days of the final gameweek — vouchers and codes to your account</li></ul>`,
      footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-blaze" data-enter>Enter league</button>`,
      onMount(el, close) {
        el.querySelector('[data-enter]').addEventListener('click', async () => {
          try { await f.joinOfficial(id); SA.core.motion.confetti(); close(); SA.ui.overlay.toast(`You’re in ${l.name}. Good luck!`); SA.app.rerender(); }
          catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
        });
      },
    });
  };

  SA.pages.fantasyLeagues = {
    title: 'Leagues — Super App Fantasy',
    render() {
      const f = F(), meta = f.meta();
      const official = f.officialLeagues();
      const groups = f.privateGroups();
      return `${SA.pages.fantasy.header('leagues')}
      <div class="container fx-body">
        <div class="sec-head"><div><h2 class="h2">Leagues</h2><p>Every league is free. The Super App League is run by Super App: the top 3 of every gameweek win ${f.weeklyPoints().map((x) => x.toLocaleString()).join(' / ')} Super Points.</p></div></div>
        <div class="league-grid">${official.map((l) => leagueCard(l, meta)).join('')}</div>

        <div class="sec-head" style="margin-top:56px"><div><h2 class="h2">Private groups</h2><p>Play against friends, colleagues or your weekly five-a-side crew. Free, invite-only.</p></div>
          <div class="row wrap"><button class="btn btn-outline" data-joincode>${icon('ticket', 16)} Join with a code</button><button class="btn btn-primary" data-create>${icon('plus', 16)} Create group</button></div></div>
        ${groups.length ? `<div class="panel table-wrap"><table class="table"><thead><tr><th>Group</th><th class="r">Managers</th><th class="r">Your rank</th><th class="r hide-s">Leader</th><th></th></tr></thead><tbody>
          ${groups.map((g) => { const s = f.standings(g); const me = s.find((r) => r.isMe); return `<tr class="clickable" data-href="#/fantasy/league/${g.id}"><td><b>${esc(g.name)}</b><span class="xs muted" style="display:block">Code ${esc(g.code)}${g.mine ? ' · You own this group' : ''}</span></td><td class="r num">${s.length}</td><td class="r num strong">${me ? '#' + me.rank : '–'}</td><td class="r hide-s small">${esc(s[0].isMe ? 'You' : s[0].name)}</td><td class="r">${icon('chevronRight', 18)}</td></tr>`; }).join('')}
        </tbody></table></div>` : c.empty({ icon: 'users', title: 'No private groups yet', text: 'Create one and share the code in your WhatsApp group.' })}
        ${!f.team() ? `<div class="notice blaze" style="margin-top:16px">${icon('info', 18)}<span>You’ll appear in league tables once you create your team. <a class="link" href="#/fantasy/squad">Pick your squad</a></span></div>` : ''}
      </div>`;
    },
    mount(el) {
      const off = SA.pages.fantasy.bindHeader(el);
      const f = F();
      el.addEventListener('click', (e) => {
        const j = e.target.closest('[data-join]'); if (j) return openJoinLeague(j.dataset.join);
        const r = e.target.closest('[data-href]'); if (r) return SA.core.router.go(r.dataset.href);
        if (e.target.closest('[data-create]')) {
          SA.ui.overlay.open({
            title: 'Create a private group', subtitle: 'You’ll get an invite code to share.',
            body: `<form id="cg" class="stack" style="--gap:12px"><div class="field"><label for="cg-name">Group name</label><input class="input" id="cg-name" name="name" maxlength="40" placeholder="e.g. Maadi Thursday Crew" autofocus /><span class="field-error" data-err="name"></span></div></form>`,
            footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-primary" form="cg" type="submit">Create group</button>`,
            onMount(m, close) {
              m.querySelector('#cg').addEventListener('submit', async (ev) => {
                ev.preventDefault();
                try { const g = await f.createGroup(ev.target.name.value); close(); SA.core.router.go(`/fantasy/league/${g.id}?created=1`); }
                catch (err) { m.querySelector('[data-err="name"]').textContent = err.message; }
              });
            },
          });
        }
        if (e.target.closest('[data-joincode]')) {
          SA.ui.overlay.open({
            title: 'Join a private group', subtitle: 'Ask the group owner for the invite code.',
            body: `<form id="jc" class="stack" style="--gap:12px"><div class="field"><label for="jc-code">Invite code</label><input class="input num" id="jc-code" name="code" placeholder="SA-XXXXXX" autocomplete="off" style="text-transform:uppercase;letter-spacing:.08em" autofocus /><span class="field-hint">Try SA-GEZ5A1 for the demo.</span><span class="field-error" data-err="code"></span></div></form>`,
            footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-primary" form="jc" type="submit">Join group</button>`,
            onMount(m, close) {
              m.querySelector('#jc').addEventListener('submit', async (ev) => {
                ev.preventDefault();
                try { const g = await f.joinGroupByCode(ev.target.code.value); close(); SA.ui.overlay.toast(`You joined ${g.name}`); SA.core.router.go(`/fantasy/league/${g.id}`); }
                catch (err) { m.querySelector('[data-err="code"]').textContent = err.message; }
              });
            },
          });
        }
      });
      return off;
    },
  };

  /* ---------- One league / group ---------- */
  SA.pages.fantasyLeague = {
    title: 'League — Super App Fantasy',
    render(ctx) {
      const f = F(), meta = f.meta();
      const g = f.group(ctx.params.id);
      if (!g) return `${SA.pages.fantasy.header('leagues')}<div class="container">${c.empty({ icon: 'users', title: 'League not found', text: 'You may have left this group.', action: '<a class="btn btn-primary" href="#/fantasy/leagues">All leagues</a>' })}</div>`;
      const isPrivate = !!g.members;
      const official = !isPrivate ? f.officialLeagues().find((l) => l.id === g.id) : null;
      const link = SA.core.router.url(`/fantasy/leagues?code=${g.code}`);
      return `${SA.pages.fantasy.header('leagues')}
      <div class="container fx-body">
        <a class="back-link" href="#/fantasy/leagues">${icon('chevronLeft', 16)} All leagues</a>
        <div class="row-between wrap" style="margin:12px 0 22px;align-items:flex-end">
          <div><h2 class="h1">${esc(g.name)}</h2><p class="soft" style="margin-top:6px">${isPrivate ? `Private group · created by ${esc(g.owner)} · ${g.standings.length} managers` : `${esc(official.scope)} · ${fmtNum(official.participants)} managers · Gameweek ${meta.gw}`}</p></div>
          ${isPrivate ? '' : official.joined ? `<span class="badge badge-turf">${icon('check', 12, 2.6)} You’ve entered</span>` : `<button class="btn btn-blaze" data-join="${g.id}">Enter league</button>`}
        </div>
        ${ctx.query.created ? `<div class="notice turf" style="margin-bottom:18px">${icon('check', 18)}<span><b>Group created.</b> Share the code below with your friends — they join from Leagues → Join with a code.</span></div>` : ''}
        <div class="league-layout">
          <div>
            <div class="panel table-wrap"><table class="table standings"><thead><tr><th>Rank</th><th>Team & manager</th><th class="r">GW</th><th class="r">Total</th>${isPrivate ? '<th></th>' : ''}</tr></thead>
              <tbody>${g.standings.map((r) => `<tr class="${r.isMe ? 'is-me' : ''} ${isPrivate && !r.isMe ? 'clickable' : ''}" ${isPrivate && !r.isMe ? `data-compare="${esc(r.name)}"` : ''}>
                <td class="num strong">${r.rank <= 3 ? `<span class="medal m${r.rank}">${r.rank}</span>` : fmtNum(r.rank)}</td>
                <td><b class="small">${esc(r.team)}</b><span class="xs muted" style="display:block">${esc(r.isMe ? 'You' : r.name)}</span></td>
                <td class="r num">${r.gw}</td><td class="r num strong">${fmtNum(r.total)}</td>${isPrivate ? `<td class="r">${r.isMe ? '' : `<span class="link small">Compare</span>`}</td>` : ''}</tr>`).join('')}</tbody></table></div>
            ${isPrivate && !f.team() ? `<div class="notice blaze" style="margin-top:14px">${icon('info', 18)}<span>Create your team to appear in this table. <a class="link" href="#/fantasy/squad">Pick your squad</a></span></div>` : ''}
            ${isPrivate && f.team() && !(f.team().history || []).length ? `<p class="xs muted" style="margin-top:10px">Your points start counting from GW${f.team().createdGW}. Simulate the gameweek to see the table move.</p>` : ''}
          </div>
          <aside class="stack" style="--gap:16px">
            ${isPrivate ? `<div class="panel panel-pad">
              <h3 class="h4">Invite friends</h3>
              <p class="small soft" style="margin-top:6px">Share this code or link. Anyone with it can join.</p>
              <div class="invite-code"><b class="num">${esc(g.code)}</b><button class="btn btn-outline btn-sm" data-copy="${esc(g.code)}">${icon('copy', 14)} Copy</button></div>
              <div class="row wrap" style="margin-top:12px;--gap:8px">
                <a class="btn btn-turf btn-sm" target="_blank" rel="noopener" href="https://wa.me/?text=${encodeURIComponent(`Join my Super App fantasy group "${g.name}" — code ${g.code} ${link}`)}">${icon('whatsapp', 15)} WhatsApp</a>
                <button class="btn btn-outline btn-sm" data-copy="${esc(link)}">${icon('link', 14)} Copy link</button>
              </div>
              ${g.mine || g.owner !== SA.services.user.me().name ? `<button class="btn btn-ghost btn-sm btn-block" style="margin-top:14px;color:var(--danger)" data-leave>${g.mine ? 'Delete group' : 'Leave group'}</button>` : ''}
            </div>` : `<div class="panel panel-pad">
              <h3 class="h4">Prizes</h3>
              <ul class="prize-list">${official.prizes.map(([p, v], i) => `<li><span class="medal m${Math.min(i + 1, 4)}">${i < 3 ? i + 1 : ''}</span><span class="grow small">${p}</span><b class="small">${esc(v)}</b></li>`).join('')}</ul>
              <p class="xs muted" style="margin-top:10px">${official.kind === 'official' ? 'Every gameweek, by gameweek points. The Super Points go straight to the winners’ accounts. No cash prizes.' : `Prizes worth ${money(official.prizeValue || 0)}, provided by sponsors and Super App partners. No cash prizes.`}</p>
            </div>
            ${official.kind === 'official' ? (() => { const ww = f.weeklyWinners().slice(0, 3); return `<div class="panel panel-pad"><h3 class="h4">Weekly winners</h3>${ww.length ? ww.map((w) => `<div style="margin-top:12px"><b class="small">Gameweek ${w.gw}</b><ul class="prize-list">${w.winners.map((x) => `<li><span class="medal m${x.place}">${x.place}</span><span class="grow small">${esc(x.isMe ? 'You' : x.name)} <span class="xs muted">· ${x.gw} pts</span></span><b class="small num">+${x.points.toLocaleString()}</b></li>`).join('')}</ul></div>`).join('') : '<p class="small soft" style="margin-top:6px">The first winners appear when this gameweek is finished.</p>'}</div>`; })() : ''}
            <div class="panel panel-pad">
              <h3 class="h4">Rules</h3>
              <ul class="assurance small" style="margin-top:10px">
                <li>${icon('check', 14)} ${official.kind === 'official' ? 'Each gameweek is a new race: the 3 highest gameweek scores win' : `Points count from GW${official.from}${official.to ? ` to GW${official.to}` : ' to the end of the season'}`}</li>
                <li>${icon('check', 14)} Ties are split by fewest transfer hits, then GW points</li>
                <li>${icon('check', 14)} Free for every manager — no entry fee</li>
                <li>${icon('check', 14)} One entry per account; vouchers and codes go to your account, physical prizes are delivered</li>
              </ul>
              <div class="row-between" style="margin-top:14px"><span class="small soft">GW${meta.gw} locks in</span>${c.countdown(meta.deadline, { compact: true })}</div>
            </div>`}
            <div class="panel panel-pad"><h3 class="h4">How points work</h3><dl class="kv" style="margin-top:10px">${f.D.rules.scoring.slice(0, 7).map(([k, v]) => `<div><dt>${k}</dt><dd class="num">${v > 0 ? '+' + v : v}</dd></div>`).join('')}</dl></div>
          </aside>
        </div>
      </div>`;
    },
    mount(el, ctx) {
      const off = SA.pages.fantasy.bindHeader(el);
      const f = F();
      el.addEventListener('click', async (e) => {
        const cp = e.target.closest('[data-copy]'); if (cp) { navigator.clipboard?.writeText(cp.dataset.copy); SA.ui.overlay.toast('Copied to clipboard'); return; }
        const j = e.target.closest('[data-join]'); if (j) return openJoinLeague(j.dataset.join);
        if (e.target.closest('[data-leave]')) {
          if (await SA.ui.overlay.confirm({ title: 'Leave this group?', text: 'You can rejoin later with the invite code.', confirmLabel: 'Leave group', danger: true })) { f.leaveGroup(ctx.params.id); SA.core.router.go('/fantasy/leagues'); }
          return;
        }
        const cmp = e.target.closest('[data-compare]');
        if (cmp) {
          const name = cmp.dataset.compare;
          const rival = f.rivalSquad(name);
          const team = f.team();
          const hist = f.rivalHistory(name);
          const mine = Object.fromEntries((team?.history || []).map((h) => [h.gw, h.pts]));
          const common = team ? rival.xi.filter((id) => team.xi.includes(id)) : [];
          const col = (title, xi, cap) => `<div><h3 class="small strong">${title}</h3><ul class="cmp-list">${xi.map((id) => { const p = f.byId[id]; return `<li class="${common.includes(id) ? 'is-common' : ''}">${c.kit(f.teamById[p.teamId], 24, p.pos === 'GK')}<span class="grow small truncate">${esc(p.name)}</span>${id === cap ? '<span class="badge badge-ink">C</span>' : ''}<span class="xs muted">${p.pos}</span></li>`; }).join('')}</ul></div>`;
          SA.ui.overlay.open({
            wide: true, title: `You vs ${esc(name)}`, subtitle: `${common.length} players in common · highlighted`,
            body: `<div class="gw-compare">${hist.slice(-8).map((h) => `<div class="gwc"><div class="gwc-bars"><span class="them" style="height:${h.pts}px" title="${esc(name)}: ${h.pts}"></span><span class="me" style="height:${mine[h.gw] || 0}px" title="You: ${mine[h.gw] ?? '–'}"></span></div><i class="xs">GW${h.gw}</i></div>`).join('')}</div>
              <div class="row small" style="gap:16px;margin:8px 0 18px"><span class="row" style="--gap:6px"><i class="key them"></i>${esc(name.split(' ')[0])}</span><span class="row" style="--gap:6px"><i class="key me"></i>You</span></div>
              <div class="cmp-grid">${team ? col('Your starting XI', team.xi, team.captain) : '<div class="small muted">Create a team to compare line-ups.</div>'}${col(`${esc(name.split(' ')[0])}'s starting XI`, rival.xi, rival.captain)}</div>`,
          });
        }
      });
      return off;
    },
  };
})(window.SA);
