(function (SA) {
  const { esc, isoDate, today, fmtDate, DAY, countdownParts } = SA.core.utils;
  const { icon } = SA.ui;
  const c = SA.ui.c;
  const { crest, matchRow } = SA.ui.sports;
  const S = SA.services;

  const REGIONS = [['', 'All'], ['live', 'Live now'], ['Egypt', 'Egypt'], ['Africa', 'Africa'], ['Europe', 'Europe'], ['National teams', 'National teams']];
  const collapsed = new Set();

  const filterMatches = (list, f) => {
    if (f.region === 'live') return list.filter((m) => m.status === 'live');
    if (f.region) list = list.filter((m) => m.comp.region === f.region);
    if (f.comp) list = list.filter((m) => m.compId === f.comp);
    return list;
  };

  const motd = (m) => {
    if (!m) return '';
    const live = m.status === 'live', up = m.status === 'upcoming';
    return `<section class="motd" data-match="${m.id}" style="--ca:${m.homeT.a};--cb:${m.awayT.a}">
      <div class="motd-bg" aria-hidden="true"></div>
      <div class="motd-top"><span class="badge badge-glass">${live ? '<span class="live-dot"></span> Live now' : 'Match of the day'}</span><span class="xs motd-comp">${esc(m.comp.name)} · ${esc(m.round)}</span></div>
      <div class="motd-main">
        <div class="motd-team home">${crest(m.homeT, 88)}<b>${esc(m.homeT.name)}</b></div>
        <div class="motd-center">
          ${up ? `<span class="xs" style="opacity:.75">Kick-off in</span>${c.countdown(m.ko)}<span class="motd-time">${m.label} Cairo time</span>`
            : `<span class="motd-score num" data-score><b>${m.hs}</b><i>:</i><b>${m.as}</b></span><span class="motd-state" data-minute>${live ? `<span class="live-dot"></span>${m.label}` : 'Full time'}</span>`}
          <span class="motd-vs" aria-hidden="true">VS</span>
        </div>
        <div class="motd-team away">${crest(m.awayT, 88)}<b>${esc(m.awayT.name)}</b></div>
      </div>
      <div class="motd-foot xs">${icon('pin', 13)} ${esc(m.venue)}${m.egyptians.length ? ` · <span class="egy-chip">${esc(m.egyptians.join(', '))}</span>` : ''}<button class="btn btn-sm motd-btn" data-open="${m.id}">Match centre ${icon('arrowRight', 14)}</button></div>
    </section>`;
  };

  const groups = (list) => {
    const by = new Map();
    list.forEach((m) => { if (!by.has(m.compId)) by.set(m.compId, []); by.get(m.compId).push(m); });
    return [...by.entries()].map(([id, ms]) => {
      const comp = ms[0].comp, live = ms.filter((m) => m.status === 'live').length, closed = collapsed.has(id);
      return `<section class="comp-block ${closed ? 'is-closed' : ''}" style="--comp:${comp.color}">
        <button class="comp-head" data-collapse="${id}" aria-expanded="${!closed}">
          <span class="comp-badge">${esc(comp.short.split(' ')[0].slice(0, 3).toUpperCase())}</span>
          <span class="grow"><b>${esc(comp.name)}</b><span class="xs muted">${esc(comp.region)} · ${esc(ms[0].round)}</span></span>
          ${live ? `<span class="badge badge-danger"><span class="live-dot"></span>${live} live</span>` : ''}<span class="xs muted">${ms.length} match${ms.length > 1 ? 'es' : ''}</span>
          <span class="comp-chev">${icon('chevronDown', 18)}</span>
        </button>
        <div class="comp-body"><div class="comp-inner">${ms.map(matchRow).join('')}</div></div>
      </section>`;
    }).join('');
  };

  /* -------- Match centre (modal) -------- */
  const openCentre = (id) => {
    const m = S.schedule.get(id);
    if (!m) return;
    const statRow = (label, [h, a], pct = false) => {
      const tot = Math.max(1, h + a);
      return `<div class="stat-row"><b class="num">${h}${pct ? '%' : ''}</b><div class="stat-bars"><span class="sb-h" style="--w:${(h / tot) * 100}%"></span><span class="sb-a" style="--w:${(a / tot) * 100}%"></span></div><b class="num">${a}${pct ? '%' : ''}</b><span class="stat-label xs">${label}</span></div>`;
    };
    const timeline = m.seen.length ? m.seen.map((e, i) => `<li class="tl-${e.side}" style="--i:${i}"><span class="tl-min num">${e.min}'</span><span class="tl-ico ${e.kind}">${e.kind === 'goal' ? icon('ball', 15) : ''}</span><span class="tl-text">${e.kind === 'goal' ? 'Goal' : 'Yellow card'} · ${esc(e.side === 'home' ? m.homeT.name : m.awayT.name)}</span></li>`).join('')
      : `<li class="tl-empty small muted">${m.status === 'upcoming' ? 'Events will appear here once the match kicks off.' : m.noDetail ? 'Goal times aren’t available for this match.' : 'No events yet.'}</li>`;
    SA.ui.overlay.open({
      wide: true, label: `${m.homeT.name} vs ${m.awayT.name}`,
      body: `<div class="mc-head" style="--ca:${m.homeT.a};--cb:${m.awayT.a}">
          <div class="mc-team">${crest(m.homeT, 64)}<b>${esc(m.homeT.name)}</b></div>
          <div class="mc-mid">${m.status === 'upcoming' ? `<span class="mc-score num">${m.label}</span><span class="xs">${fmtDate(new Date(m.ko))}</span>` : `<span class="mc-score num">${m.hs} – ${m.as}</span><span class="xs ${m.status === 'live' ? 'mc-live' : ''}">${m.status === 'live' ? `<span class="live-dot"></span> ${m.label}` : 'Full time'}</span>`}</div>
          <div class="mc-team">${crest(m.awayT, 64)}<b>${esc(m.awayT.name)}</b></div>
          <button class="icon-btn mc-close" data-close aria-label="Close">${icon('x')}</button>
        </div>
        <p class="xs muted" style="text-align:center;margin-top:12px">${esc(m.comp.name)} · ${esc(m.round)} · ${esc(m.venue)}</p>
        <div class="mc-grid">
          <div><h3 class="small strong">Timeline</h3><ol class="timeline">${timeline}</ol></div>
          <div><h3 class="small strong">Match stats</h3>${m.status === 'upcoming' ? '<p class="small muted" style="margin-top:10px">Stats go live at kick-off.</p>' : m.noDetail ? '<p class="small muted" style="margin-top:10px">Match stats aren’t available for this match.</p>' : `<div class="stats">${statRow('Possession', m.stats.possession, true)}${statRow('Shots', m.stats.shots)}${statRow('On target', m.stats.onTarget)}${statRow('Corners', m.stats.corners)}</div>`}</div>
        </div>`,
      footer: m.status === 'upcoming' ? `<button class="btn ${m.reminder ? 'btn-outline' : 'btn-primary'}" data-mc-remind>${icon('bell', 16)} ${m.reminder ? 'Reminder on' : 'Remind me at kick-off'}</button>` : '',
      onMount(el, close) {
        requestAnimationFrame(() => el.querySelectorAll('.stat-bars').forEach((b) => b.classList.add('is-in')));
        el.querySelector('[data-mc-remind]')?.addEventListener('click', () => { const on = S.schedule.toggleReminder(id); close(); SA.ui.overlay.toast(on ? 'We’ll remind you before kick-off' : 'Reminder removed', { type: 'info' }); SA.app.rerender(); });
      },
    });
  };

  SA.pages.matches = {
    title: 'Matches — Super App',
    openCentre,
    async render(ctx) {
      const date = ctx.query.date || isoDate(today());
      const f = { region: ctx.query.region || '', comp: ctx.query.comp || '' };
      const all = await S.schedule.list({ date });
      const list = filterMatches(all, f);
      const days = S.schedule.days();
      const featured = f.region || f.comp ? null : S.schedule.featured(date);
      const liveToday = S.schedule.listSync(isoDate(today())).filter((m) => m.status === 'live').length;
      const href = (patch) => SA.core.router.href('/matches', { date: date === isoDate(today()) ? undefined : date, region: f.region || undefined, comp: f.comp || undefined, ...patch });
      const comps = S.schedule.competitions();

      return `<section class="container news-head">
          <span class="watermark" data-parallax="-0.3" aria-hidden="true" style="top:-10px;right:-8%">MATCHDAY</span>
          <nav class="crumbs" aria-label="Breadcrumb"><a href="#/">Home</a>${icon('chevronRight', 14)}<span>Matches</span></nav>
          <h1 class="display news-display">Every kick-off, one schedule.</h1>
          <p class="lead">Egyptian Premier League, CAF Champions League, the Champions League, Europe’s big five leagues and national teams — in Cairo time.</p>
          ${liveToday ? `<a class="live-pill" href="${href({ region: 'live', date: undefined })}"><span class="live-dot"></span>${liveToday} match${liveToday > 1 ? 'es' : ''} live now</a>` : ''}
        </section>
        <section class="container">
          <div class="date-strip match-days" role="tablist" aria-label="Match day">${days.map((d) => `<a role="tab" class="date-chip md-chip ${d.iso === date ? 'is-active' : ''}" aria-selected="${d.iso === date}" href="${href({ date: d.offset === 0 ? undefined : d.iso })}">
            <span class="xs">${d.offset === 0 ? 'Today' : d.offset === 1 ? 'Tmrw' : d.offset === -1 ? 'Ystdy' : DAY[d.d.getDay()]}</span><b class="num">${d.d.getDate()}</b><span class="md-count xs">${d.live ? '<span class="live-dot"></span>' : ''}${d.count}</span></a>`).join('')}</div>
          <div class="chips" style="margin-top:12px" role="group" aria-label="Competition">
            ${REGIONS.map(([v, l]) => `<a class="chip" href="${href({ region: v || undefined, comp: undefined })}" aria-pressed="${f.region === v && !f.comp}">${v === 'live' ? '<span class="live-dot" style="color:var(--danger)"></span>' : ''}${l}</a>`).join('')}
          </div>
          <div class="chips comp-chips" style="margin-top:8px" role="group" aria-label="League">
            ${comps.map((k) => `<a class="chip" href="${href({ comp: f.comp === k.id ? undefined : k.id, region: undefined })}" aria-pressed="${f.comp === k.id}"><i class="cat-dot" style="background:${k.color}"></i>${esc(k.name)}</a>`).join('')}
          </div>
        </section>
        <section class="container section-tight">
          ${motd(featured)}
          <div class="row-between" style="margin:26px 0 12px"><h2 class="h3">${date === isoDate(today()) ? 'Today' : fmtDate(date, 'long')}</h2><span class="small muted">${list.length} match${list.length === 1 ? '' : 'es'} · times in Cairo (EET)</span></div>
          <div class="comp-list">${list.length ? groups(list) : c.empty({ icon: 'calendar', title: f.region === 'live' ? 'No live matches right now' : 'No matches on this day', text: 'Pick another day or competition — there’s football almost every day.' })}</div>
          <p class="xs muted demo-note" style="margin-top:18px">${icon('info', 12)} Demo fixtures and scores. Connect a live data provider for official schedules.</p>
        </section>`;
    },
    mount(el) {
      const offs = [c.tickCountdowns(el)];
      el.addEventListener('click', (e) => {
        const bell = e.target.closest('[data-remind]');
        if (bell) {
          e.stopPropagation();
          const on = S.schedule.toggleReminder(bell.dataset.remind);
          bell.classList.toggle('is-on', on); bell.setAttribute('aria-pressed', on);
          bell.classList.remove('ring'); void bell.offsetWidth; bell.classList.add('ring');
          SA.ui.overlay.toast(on ? 'Reminder set — we’ll notify you before kick-off' : 'Reminder removed', { type: 'info', duration: 2200 });
          return;
        }
        const col = e.target.closest('[data-collapse]');
        if (col) {
          const id = col.dataset.collapse, block = col.closest('.comp-block');
          const closed = block.classList.toggle('is-closed');
          col.setAttribute('aria-expanded', !closed);
          closed ? collapsed.add(id) : collapsed.delete(id);
          return;
        }
        const open = e.target.closest('[data-open], .mrow');
        if (open) openCentre(open.dataset.open || open.dataset.match);
      });
      el.addEventListener('keydown', (e) => { const r = e.target.closest('.mrow'); if (r && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); openCentre(r.dataset.match); } });

      // Live ticker: refresh minutes and scores; celebrate goals
      const live = () => {
        el.querySelectorAll('[data-match]').forEach((row) => {
          const m = S.schedule.get(row.dataset.match);
          if (!m || m.status === 'upcoming') return;
          const scoreEl = row.querySelector('[data-score]');
          const minEl = row.querySelector('[data-minute]');
          if (minEl && m.status === 'live') minEl.innerHTML = `<span class="live-dot"></span>${m.label}`;
          if (scoreEl) {
            const [hb, ab] = scoreEl.querySelectorAll('b');
            const prev = `${hb.textContent}-${ab.textContent}`;
            if (prev !== `${m.hs}-${m.as}`) {
              hb.textContent = m.hs; ab.textContent = m.as;
              row.classList.remove('goal'); void row.offsetWidth; row.classList.add('goal');
              const r = row.getBoundingClientRect();
              SA.core.motion.confetti({ x: r.left + r.width / 2, y: r.top + r.height / 2, count: 40, spread: 0.5 });
              SA.ui.overlay.toast(`GOAL! ${esc(m.homeT.name)} ${m.hs} – ${m.as} ${esc(m.awayT.name)}`, { type: 'success' });
            }
          }
        });
      };
      const t = setInterval(live, 10000);
      offs.push(() => clearInterval(t));
      return () => offs.forEach((f) => f());
    },
  };
})(window.SA);
