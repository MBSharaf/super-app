/** Site: tournaments & leagues organised by venues — browse, register a team, follow fixtures. */
(function (SA) {
  const { esc, money, fmtDate, fmtHour, isoDate, today } = SA.core.utils;
  const { icon } = SA.ui;
  const c = SA.ui.c;
  const media = SA.data.media;
  const S = SA.services;

  const STATUS = { open: ['badge-turf', 'Registration open'], full: ['badge-gold', 'Full'], running: ['badge-blaze', 'In progress'], finished: ['badge', 'Finished'] };
  const statusBadge = (t) => { const [cls, l] = STATUS[t.status] || ['', t.status]; return `<span class="badge ${cls}">${t.status === 'running' && t.live ? '<span class="live-dot"></span>' : ''}${l}</span>`; };
  const formatLabel = (t) => (t.format === 'league' ? 'League' : 'Knockout cup');

  /** CSS 3D trophy used in the hero */
  const trophy = () => `<div class="trophy3d" aria-hidden="true"><div class="tr-spin"><span class="tr-cup"></span><span class="tr-handle l"></span><span class="tr-handle r"></span><span class="tr-stem"></span><span class="tr-base"></span><span class="tr-shine"></span></div><span class="tr-glow"></span></div>`;

  const card = (t, i = 0) => `<a class="card card-link tour-card" href="#/tournaments/${t.id}" style="--i:${i}">
      <div class="media tour-media"><img src="${media.photo(t.cover, 800, 480)}" alt="" loading="lazy" />
        <div class="media-top">${statusBadge(t)}<span class="badge badge-glass">${formatLabel(t)}</span></div>
        ${t.champion ? `<span class="tour-champ">${icon('trophy', 14)} ${esc(t.champion)}</span>` : ''}</div>
      <div class="tour-body">
        <h3 class="h4">${esc(t.name)}</h3>
        <p class="small soft">${esc(t.pitchName)} · ${esc(t.area)} · ${esc(t.teamSize)}</p>
        <div class="tour-facts">
          <span>${icon('calendar', 14)} ${fmtDate(t.firstDay)}${t.matchDays > 1 ? ` – ${fmtDate(t.lastDay)}` : ''}</span>
          <span>${icon('wallet', 14)} ${t.fee ? `${money(t.fee)} / team` : 'Free entry'}</span>
        </div>
        ${t.status === 'open' || t.status === 'full' ? `<div class="tour-spots"><span class="meter"><span style="width:${(t.teams.length / t.maxTeams) * 100}%"></span></span><span class="xs">${t.teams.length}/${t.maxTeams} teams${t.spotsLeft ? ` · ${t.spotsLeft} spot${t.spotsLeft === 1 ? '' : 's'} left` : ''}</span></div>`
          : `<p class="xs muted">${t.played} match${t.played === 1 ? '' : 'es'} played${t.next ? ` · next ${fmtDate(t.next.date)}` : ''}</p>`}
        ${t.mine.length ? `<span class="badge badge-turf" style="margin-top:8px">${icon('check', 12, 2.6)} ${esc(t.mine[0].name)} is in</span>` : ''}
      </div></a>`;

  SA.pages.tournaments = {
    title: 'Tournaments — Super App',
    async render(ctx) {
      const tab = ctx.query.status || '';
      const list = await S.tournaments.list({ status: tab || undefined });
      const all = await S.tournaments.list();
      const open = all.filter((t) => t.regOpen);
      return `<section class="tour-hero">
          <div class="container tour-hero-in">
            <div><span class="eyebrow">${icon('trophy', 14)} Cups & leagues</span>
              <h1 class="display">Play for a trophy.</h1>
              <p class="lead">Venues across Egypt run knockout cups and weekly leagues. Register your team, pay the entry fee from your wallet and follow every result here.</p>
              <div class="row wrap" style="margin-top:20px;--gap:10px">${open[0] ? c.cta(`Join ${esc(open[0].name)}`, { href: `#/tournaments/${open[0].id}` }) : ''}<a class="btn btn-outline" href="#/tournaments?status=running">Results & tables</a></div></div>
            ${trophy()}
          </div></section>
        <div class="container section-tight">
          <div class="tabs" role="tablist" style="margin-bottom:18px">${[['', 'All'], ['open', 'Open for teams'], ['running', 'In progress'], ['finished', 'Finished']].map(([v, l]) => `<a class="tab" role="tab" href="${SA.core.router.href('/tournaments', { status: v || undefined })}" aria-selected="${tab === v}">${l}</a>`).join('')}</div>
          <div class="tour-grid">${list.map(card).join('') || `<div class="panel" style="grid-column:1/-1">${c.empty({ icon: 'trophy', title: 'No tournaments here yet', text: 'Check the other tabs, or ask your favourite venue to run one on Super App.' })}</div>`}</div>
        </div>`;
    },
    mount(el) { const off = SA.core.motion.tilt(el.querySelector('.trophy3d'), { max: 14 }); return off; },
  };

  /* ---------------- Detail ---------------- */
  const fixturesHtml = (t) => {
    if (!t.fixtures.length) return `<div class="notice">${icon('info', 18)}<span>Fixtures are drawn when registration closes${t.regOpen ? ` (${fmtDate(t.regDeadline)})` : ''}. Match days are already reserved: ${t.reserved.map((r) => `${fmtDate(r.date)} from ${fmtHour(r.hours[0])}`).join(' · ')}.</span></div>`;
    const byDay = new Map();
    t.fixtures.slice().sort((a, b) => a.date.localeCompare(b.date) || a.hour - b.hour).forEach((f) => { const k = f.date; byDay.set(k, [...(byDay.get(k) || []), f]); });
    const mine = new Set(t.mine.map((x) => x.id));
    return [...byDay.entries()].map(([date, list], i) => `<section class="fx-day" style="--i:${i}"><h3 class="small strong">${t.format === 'league' ? `Match day ${list[0].round + 1}` : S.tournaments.roundName(list.length)} · ${fmtDate(date, 'long')}</h3>
      <ul>${list.map((f) => { const w = S.tournaments.winnerOf(f); return `<li class="fx ${mine.has(f.home) || mine.has(f.away) ? 'is-mine' : ''}">
        <span class="fx-time num">${fmtHour(f.hour)}</span>
        <span class="fx-team ${w === f.home ? 'is-win' : ''}">${esc(S.tournaments.teamName(t, f.home))}</span>
        <span class="fx-score num">${f.status === 'played' ? `${f.hs} – ${f.as}` : 'vs'}</span>
        <span class="fx-team away ${w === f.away ? 'is-win' : ''}">${esc(S.tournaments.teamName(t, f.away))}</span>
        ${f.pens ? `<span class="xs muted fx-pens">${esc(S.tournaments.teamName(t, f.pens))} won on penalties</span>` : ''}</li>`; }).join('')}</ul></section>`).join('');
  };
  const tableHtml = (t) => {
    if (t.format === 'league') {
      const rows = S.tournaments.standings(t);
      const mine = new Set(t.mine.map((x) => x.id));
      return `<div class="table-wrap"><table class="table standings"><thead><tr><th>#</th><th>Team</th><th class="r">P</th><th class="r">W</th><th class="r">D</th><th class="r">${SA.i18n?.isAr ? "خسارة" : "L"}</th><th class="r">GD</th><th class="r">Pts</th><th>Form</th></tr></thead><tbody>
        ${rows.map((r, i) => `<tr class="${mine.has(r.id) ? 'is-mine' : ''} ${i === 0 && r.p ? 'is-top' : ''}"><td class="num">${i + 1}</td><td><b>${esc(r.name)}</b></td><td class="r num">${r.p}</td><td class="r num">${r.w}</td><td class="r num">${r.d}</td><td class="r num">${r.l}</td><td class="r num">${r.gd > 0 ? '+' : ''}${r.gd}</td><td class="r num strong">${r.pts}</td><td><span class="form">${r.form.map((x) => `<i class="f-${x}" title="${{ W: 'Win', D: 'Draw', L: 'Loss' }[x]}">${x}</i>`).join('')}</span></td></tr>`).join('')}
      </tbody></table></div>`;
    }
    const rounds = S.tournaments.bracket(t);
    if (!rounds.length) return `<p class="small muted">The bracket appears when fixtures are drawn.</p>`;
    const total = Math.log2(t.teams.length);
    const all = [...rounds]; while (all.length < total) all.push({ name: S.tournaments.roundName(t.teams.length / 2 ** (all.length + 1)), fixtures: Array.from({ length: t.teams.length / 2 ** (all.length + 1) }, () => null) });
    return `<div class="bracket" style="--rounds:${all.length}">${all.map((r, ri) => `<div class="br-round" style="--i:${ri}"><span class="xs strong br-name">${r.name}</span><div class="br-matches">${r.fixtures.map((f) => {
      if (!f) return '<div class="br-match is-tbd"><span>TBD</span><span>TBD</span></div>';
      const w = S.tournaments.winnerOf(f);
      return `<div class="br-match ${f.status === 'played' ? 'is-played' : ''}"><span class="${w === f.home ? 'is-win' : ''}"><b>${esc(S.tournaments.teamName(t, f.home))}</b><i class="num">${f.status === 'played' ? f.hs : ''}</i></span><span class="${w === f.away ? 'is-win' : ''}"><b>${esc(S.tournaments.teamName(t, f.away))}</b><i class="num">${f.status === 'played' ? f.as : ''}</i></span></div>`;
    }).join('')}</div></div>`).join('')}
      ${t.champion ? `<div class="br-round br-champ"><span class="xs strong br-name">Champion</span><div class="br-matches"><div class="champ-card">${icon('trophy', 26)}<b>${esc(t.champion)}</b></div></div></div>` : ''}</div>`;
  };

  const registerModal = (t) => {
    const u = S.user.me();
    const size = S.tournaments.SIZES[t.teamSize];
    SA.ui.overlay.open({
      wide: true, title: `Register for ${esc(t.name)}`, subtitle: `${t.fee ? `${money(t.fee)} entry fee per team` : 'Free entry'} · ${esc(t.teamSize)} · ${t.spotsLeft} spot${t.spotsLeft === 1 ? '' : 's'} left`,
      body: `<form id="tr-reg" class="stack" style="--gap:16px" novalidate>
        <div class="form-grid">
          <div class="field span-2"><label for="tr-team">Team name</label><input class="input" id="tr-team" name="team" maxlength="32" placeholder="e.g. New Cairo Rangers" /><span class="field-error" data-err="team"></span></div>
          <div class="field"><label for="tr-cap">Captain</label><input class="input" id="tr-cap" name="captain" value="${esc(u.name)}" /><span class="field-error" data-err="captain"></span></div>
          <div class="field"><label for="tr-ph">Captain’s phone</label><input class="input num" id="tr-ph" name="phone" inputmode="tel" value="${esc(u.phone)}" /><span class="field-error" data-err="phone"></span></div>
          <div class="field"><label for="tr-pl">Squad size</label><input class="input num" id="tr-pl" name="players" type="number" min="${size}" max="${size * 2 + 2}" value="${size + 2}" /><span class="field-hint">${size} on the pitch, up to ${size * 2 + 2} in the squad</span><span class="field-error" data-err="players"></span></div>
        </div>
        <label class="choice"><input type="checkbox" name="agree" /><span class="radio-dot" style="border-radius:6px"></span><span class="grow small">I’ve read the rules. Entry fees are refunded only if the venue cancels or removes the team.</span></label>
        <span class="field-error" data-err="agree"></span>
        ${SA.ui.legal.field()}
        ${t.fee ? `<section data-pay>${SA.ui.payment.render({ amount: t.fee, name: 'trpay' })}</section>` : ''}
      </form>`,
      footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-blaze" form="tr-reg" type="submit">${icon('trophy', 16)} ${t.fee ? `Pay ${money(t.fee)} & register` : 'Register team'}</button>`,
      onMount(m, close) {
        const f = m.querySelector('#tr-reg');
        const pay = m.querySelector('[data-pay]');
        if (pay) SA.ui.payment.bind(pay, { name: 'trpay' });
        f.addEventListener('submit', async (e) => {
          e.preventDefault();
          const d = SA.core.utils.formData(f);
          f.querySelectorAll('[data-err]').forEach((x) => (x.textContent = ''));
          if (!d.agree) { f.querySelector('[data-err="agree"]').textContent = 'Please accept the rules to register.'; return; }
          if (!SA.ui.legal.check(f)) return;
          const payment = pay ? SA.ui.payment.read(pay, { name: 'trpay' }) : null;
          try {
            const run = () => S.tournaments.register(t.id, { ...d, payment, waiver: true });
            const tm = pay ? await SA.ui.payment.process(run, { root: pay, label: 'Registering your team', methodHint: payment.method }) : await run();
            close(); SA.core.motion.confetti({ count: 90 }); SA.ui.overlay.toast(`${tm.name} is registered!`, { type: 'success' }); SA.app.rerender();
          } catch (err) { if (err.details) Object.entries(err.details).forEach(([k, v]) => { const n = f.querySelector(`[data-err="${k}"]`); if (n) n.textContent = v; }); else if (!pay) SA.ui.overlay.toast(err.message, { type: 'error' }); }
        });
      },
    });
  };

  SA.pages.tournament = {
    title: 'Tournament — Super App',
    async render(ctx) {
      const t = await S.tournaments.get(ctx.params.id);
      this.title = `${t.name} — Super App`;
      const tab = ctx.query.tab || (t.fixtures.length ? (t.format === 'league' ? 'table' : 'bracket') : 'overview');
      const days = Math.max(0, t.startsIn);
      return `<section class="tour-head" style="--cover:url('${media.photo(t.cover, 1600, 700)}')">
          <div class="container tour-head-in">
            <nav class="crumbs" aria-label="Breadcrumb"><a href="#/tournaments">Tournaments</a>${icon('chevronRight', 14)}<span aria-current="page">${esc(t.name)}</span></nav>
            <div class="row wrap" style="--gap:8px;margin-top:14px">${statusBadge(t)}<span class="badge badge-glass">${formatLabel(t)}</span><span class="badge badge-glass">${esc(t.teamSize)}</span></div>
            <h1 class="display tour-title">${esc(t.name)}</h1>
            <p class="lead">${esc(t.organiser)} · <a href="#/pitches/${t.pitchId}">${esc(t.pitchName)}</a>, ${esc(t.area)}</p>
            <div class="tour-stats">
              <div><span class="xs">Entry</span><b class="num">${t.fee ? money(t.fee) : 'Free'}</b></div>
              <div><span class="xs">Teams</span><b class="num">${t.teams.length}/${t.maxTeams}</b></div>
              <div><span class="xs">${t.status === 'finished' ? 'Champion' : days ? 'Kicks off in' : 'Match days'}</span><b class="num">${t.status === 'finished' ? esc(t.champion || '—') : days ? `${days} day${days === 1 ? '' : 's'}` : t.matchDays}</b></div>
            </div>
            <div class="row wrap" style="margin-top:22px;--gap:10px">
              ${t.mine.length ? `<span class="badge badge-turf tour-in">${icon('check', 14, 2.6)} ${esc(t.mine.map((x) => x.name).join(', '))} registered</span>` : ''}
              ${t.regOpen ? `<button class="btn btn-blaze btn-lg" data-register>${icon('trophy', 17)} Register a team</button>` : t.status === 'open' || t.status === 'full' ? `<span class="badge">${t.spotsLeft ? 'Registration closed' : 'All spots taken'}</span>` : ''}
            </div>
          </div>
        </section>
        <div class="container section-tight">
          <div class="tabs" role="tablist">${[['overview', 'Overview'], ['fixtures', t.format === 'league' ? 'Fixtures & results' : 'Matches'], [t.format === 'league' ? 'table' : 'bracket', t.format === 'league' ? 'Table' : 'Bracket'], ['teams', `Teams (${t.teams.length})`]].map(([v, l]) => `<a class="tab" role="tab" href="${SA.core.router.href(`/tournaments/${t.id}`, { tab: v })}" aria-selected="${tab === v}">${l}</a>`).join('')}</div>
          <div class="tour-panel">
            ${tab === 'overview' ? `<div class="tour-over">
              <section class="panel panel-pad"><h2 class="h4">${icon('trophy', 18)} Prize</h2><p class="lead" style="margin-top:8px">${esc(t.prize || 'Bragging rights')}</p>
                <h2 class="h4" style="margin-top:22px">Rules</h2><p class="soft" style="margin-top:8px">${esc(t.rules)}</p></section>
              <section class="panel panel-pad"><h2 class="h4">Key dates</h2>
                <ol class="tour-timeline">
                  <li><b>Registration closes</b><span class="small soft">${fmtDate(t.regDeadline, 'long')}</span></li>
                  ${t.reserved.map((r, i) => `<li><b>${t.format === 'league' ? `Match day ${i + 1}` : S.tournaments.roundName(r.hours.length)}</b><span class="small soft">${fmtDate(r.date, 'long')} · ${fmtHour(r.hours[0])}–${fmtHour(r.hours[r.hours.length - 1] + 1)}</span></li>`).join('')}
                </ol></section>
            </div>` : tab === 'fixtures' ? fixturesHtml(t) : tab === 'teams' ? `<ul class="team-list">${t.teams.map((x, i) => `<li style="--i:${i}" class="${t.mine.some((m) => m.id === x.id) ? 'is-mine' : ''}"><span class="team-ava">${esc(SA.core.utils.initials(x.name))}</span><span class="grow"><b>${esc(x.name)}</b><span class="xs muted" style="display:block">Captain ${esc(x.captain || '—')}</span></span>${t.mine.some((m) => m.id === x.id) ? '<span class="badge badge-turf">Your team</span>' : ''}</li>`).join('') || `<li class="small muted">No teams yet — be the first.</li>`}${Array.from({ length: t.status === 'open' ? t.spotsLeft : 0 }, () => `<li class="is-empty"><span class="team-ava">?</span><span class="grow small muted">Open spot</span></li>`).join('')}</ul>`
              : `<div class="panel panel-pad">${tableHtml(t)}</div>`}
          </div>
        </div>`;
    },
    mount(el, ctx) {
      el.querySelector('[data-register]')?.addEventListener('click', () => registerModal(S.tournaments.getSync(ctx.params.id)));
    },
  };

  SA.ui.tourCard = card;
  SA.ui.tour = { tableHtml, fixturesHtml, statusBadge, trophy };
})(window.SA);
