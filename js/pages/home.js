(function (SA) {
  const { esc, money, fmtKm, fmtHour, relDay, fantasyPrice } = SA.core.utils;
  const { icon, cards } = SA.ui;
  const c = SA.ui.c;
  const media = SA.data.media;
  const S = SA.services;

  /** The signature hero visual: a 3D floodlit football pitch and a 3D padel court (glass walls, net). The sport switch flips between them. */
  const pitchSurface = () => `
      <div class="pitch3d stage-surface" data-surface="football">
        <div class="pitch3d-grass"></div>
        <svg class="pitch3d-lines" viewBox="0 0 400 260" preserveAspectRatio="none">
          <g fill="none" stroke="rgba(255,255,255,.85)" stroke-width="2">
            <rect x="6" y="6" width="388" height="248" rx="2"/><line x1="200" y1="6" x2="200" y2="254"/><circle cx="200" cy="130" r="34"/>
            <rect x="6" y="70" width="52" height="120"/><rect x="342" y="70" width="52" height="120"/><rect x="6" y="104" width="18" height="52"/><rect x="376" y="104" width="18" height="52"/>
          </g><circle cx="200" cy="130" r="3" fill="#fff"/>
        </svg>
        ${[[22, 50, 0], [33, 22, 0], [33, 78, 0], [44, 50, 1], [12, 50, 0], [62, 30, 0], [62, 70, 0], [72, 50, 1], [86, 50, 0], [56, 50, 2]].map(([x, y, k]) => `<span class="pdot ${k === 1 ? 'is-open' : k === 2 ? 'is-ball' : x > 50 ? 'is-away' : ''}" style="left:${x}%;top:${y}%"></span>`).join('')}
        <div class="pitch3d-glow"></div>
        <span class="pitch3d-anchor" aria-hidden="true"></span>
      </div>`;
  // Padel: 20 m × 10 m, service lines 6.95 m from the net, glass back walls and the first 4 m of the side walls, mesh above
  const courtSurface = () => `
      <div class="court3d stage-surface" data-surface="padel">
        <svg class="court3d-lines" viewBox="0 0 400 200" preserveAspectRatio="none">
          <g fill="none" stroke="rgba(255,255,255,.9)" stroke-width="2.2">
            <rect x="3" y="3" width="394" height="194"/><line x1="61" y1="3" x2="61" y2="197"/><line x1="339" y1="3" x2="339" y2="197"/><line x1="61" y1="100" x2="339" y2="100"/>
          </g>
        </svg>
        <span class="court3d-net"></span>
        <span class="court3d-wall w-back-a"></span><span class="court3d-wall w-back-b"></span>
        <span class="court3d-wall w-side w-s1"></span><span class="court3d-wall w-side w-s2"></span><span class="court3d-wall w-side w-s3"></span><span class="court3d-wall w-side w-s4"></span>
        ${[[18, 30], [18, 70], [82, 30], [82, 70, 1]].map(([x, y, k]) => `<span class="pdot ${x > 50 ? 'is-away' : ''} ${k ? 'is-open' : ''}" style="left:${x}%;top:${y}%"></span>`).join('')}
        <span class="pdot is-padel-ball" style="left:40%;top:46%"></span>
        <div class="pitch3d-glow"></div>
      </div>`;
  const placeCard = (p, sport) => (p ? `<div class="float-card fc-pitch" data-sport-only="${sport}">
        <img src="${media.photo(p.images[0], 160, 160)}" alt="" />
        <div><b>${esc(p.name)}</b><span>${fmtKm(p.distanceKm)} · ${p.freeSlots} slots today</span></div>
        <span class="fc-price">${money(p.price)}</span>
      </div>` : '');
  const matchCard = (m, sport) => (m ? `<div class="float-card fc-match" data-sport-only="${sport}">
        <div class="row-between"><span class="badge badge-blaze"><span class="live-dot"></span>${m.spotsLeft} ${m.spotsLeft === 1 ? 'spot' : 'spots'} open</span><span class="xs muted">${relDay(m.date)}, ${fmtHour(m.hour)}</span></div>
        <b>${esc(m.pitch.name)}</b>
        ${c.spots(m.filled, m.total)}
      </div>` : '');
  const heroStage = (h, meta) => `
    <div class="hero-stage" data-tilt aria-hidden="true">
      ${pitchSurface()}${courtSurface()}
      ${placeCard(h.pitch, 'football')}${placeCard(h.court, 'padel')}
      ${matchCard(h.match, 'football')}${matchCard(h.padelMatch, 'padel')}
      <div class="float-card fc-fantasy">
        <span class="xs">Gameweek ${meta.gw} deadline</span>
        ${c.countdown(meta.deadline, { compact: true })}
      </div>
      ${h.product ? `<div class="float-card fc-product">
        <img src="${media.u(h.product.images[0], 200, 200)}" alt="" />
        <div><span class="xs muted">${esc(h.product.brand)}</span><b>${esc(h.product.name.split(' ').slice(0, 2).join(' '))}</b><span class="price small">${money(h.product.price)}</span></div>
      </div>` : ''}
    </div>`;

  /** Everything in the app, one tap away (icon grid on phones, a row on desktop) */
  const QUICK = [['pitches', 'Pitches'], ['padel', 'Padel'], ['players', 'Find players'], ['post', 'Post a match'], ['fantasy', 'Fantasy'], ['matches', 'Matches'], ['shop', 'Shop'], ['tournaments', 'Cups']];

  SA.pages.home = {
    title: 'Super Malaab — Book, play, compete',
    async render() {
      const [nearby, courts, matches, products] = await Promise.all([S.pitches.list({ sort: 'distance' }), S.pitches.list({ sort: 'distance', sport: 'padel' }), S.matches.list({ sort: 'soon' }), SA.config.storeEnabled ? S.shop.list({ sort: 'featured' }) : Promise.resolve([])]);  // the store is paused: its API answers 423
      const meta = S.fantasy.meta();
      const loc = S.location.current();
      const upcoming = S.bookings.upcomingSync()[0];
      const topPlayers = S.fantasy.playersSync().slice().sort((a, b) => b.form - a.form).slice(0, 5);
      const team = S.fantasy.team();
      const open = matches.filter((m) => m.spotsLeft > 0 && !m.joined);
      const hero = {
        pitch: nearby.find((p) => p.freeSlots > 0) || nearby[0],
        court: courts.find((p) => p.freeSlots > 0) || courts[0],
        match: open.find((m) => m.sport !== 'padel') || matches[0],
        padelMatch: open.find((m) => m.sport === 'padel'),
        product: products.find((p) => p.discount > 0) || products[0],
      };
      const leagues = S.fantasy.officialLeagues();
      const spotlight = S.schedule.spotlight(8);
      const sport = sessionStorage.getItem('superapp.heroSport') === 'padel' ? 'padel' : 'football';
      const sportBtn = (k, ic, l) => `<button type="button" class="sport-switch-btn" data-sport="${k}" aria-pressed="${sport === k}">${icon(ic, 18)}<span>${l}</span></button>`;

      return `
      <section class="hero container" data-hero data-sport="${sport}">
        <div class="hero-copy">
          <div class="sport-switch" role="group" aria-label="Sport">${sportBtn('football', 'ball', 'Football')}${sportBtn('padel', 'padel', 'Padel')}</div>
          <h1 class="display hero-title"><span>Book.</span><span>Play.</span><span>Win.</span></h1>
          <p class="lead">${SA.config.fantasyEnabled ? 'Pitches, padel courts, players for your game and Premier League fantasy — in one app.' : 'Pitches, padel courts and players for your game — in one app.'}</p>
          <div class="hero-ctas">
            <a class="btn btn-primary btn-lg" data-sport-only="football" href="#/pitches?locate=1">${icon('pitch', 19)} Book a pitch</a>
            <a class="btn btn-primary btn-lg" data-sport-only="padel" href="#/pitches?sport=padel">${icon('padel', 19)} Book a padel court</a>
            <a class="btn btn-outline btn-lg" href="#/players">${icon('users', 19)} Find a game</a>
          </div>
          <div class="hero-proof row">
            ${c.avatarStack([0, 3, 9, 5].map((a) => ({ avatar: a, name: 'x' })), 4)}
            <div><span class="row" style="--gap:6px">${c.stars(5, 13)}<b class="small">4.8</b></span><span class="xs muted">62,000+ players across Cairo, Giza and Alexandria</span></div>
          </div>
        </div>
        ${heroStage(hero, meta)}
      </section>

      <section class="container">
        <nav class="quick-nav" aria-label="Everything in Super Malaab">
          ${QUICK.filter(([k]) => (k !== 'shop' || SA.config.storeEnabled) && (k !== 'fantasy' || SA.config.fantasyEnabled)).map(([k, l]) => { const n = SA.ui.shell.LINKS[k]; const live = k === 'matches' ? SA.ui.shell.liveCount() : 0; return `<a href="${n.href}" class="quick-nav-item qn-${k}"><span class="quick-nav-ico">${icon(n.icon, 24)}${live ? '<span class="quick-nav-dot"></span>' : ''}</span><span>${l}</span></a>`; }).join('')}
        </nav>
      </section>

      ${upcoming ? `<section class="container section-tight"><div class="next-up reveal">
        <div class="media next-up-media"><img src="${media.photo(upcoming.image, 300, 300)}" alt="" /></div>
        <div class="grow"><span class="xs muted">Your next game</span><h2 class="h4">${esc(upcoming.pitchName)} · ${relDay(upcoming.date)} at ${fmtHour(upcoming.hour)}</h2></div>
        <div class="row wrap">${upcoming.publishedMatchId ? `<a class="btn btn-outline btn-sm" href="#/players/${upcoming.publishedMatchId}">View listing</a>` : `<a class="btn btn-primary btn-sm" href="#/players/new?booking=${upcoming.id}">${icon('users', 16)} Find players</a>`}<a class="btn btn-ghost btn-sm" href="#/booking/${upcoming.id}">Details</a></div>
      </div></section>` : ''}

      <section class="container section-tight">
        <div class="sec-head"><div><h2 class="h2">${icon('pitch', 24)} Pitches near you</h2><p>${loc ? `Closest to ${esc(loc.label)}` : 'Near Downtown Cairo'}</p></div>
          <div class="row">${loc?.source === 'gps' ? '' : `<button class="btn btn-outline btn-sm" data-locate>${icon('locate', 16)} <span class="hide-xs">Use my location</span></button>`}<a class="btn btn-ghost btn-sm" href="#/pitches">See all ${icon('arrowRight', 16)}</a></div></div>
        <div class="scroller" style="--col:clamp(250px, 30vw, 330px)">${nearby.slice(0, 6).map((p) => cards.pitch(p)).join('')}</div>
      </section>

      ${courts.length ? `<section class="container section-tight">
        <div class="sec-head"><div><h2 class="h2">${icon('padel', 24)} Padel courts</h2><p>Book 60, 90 or 120 minutes</p></div><a class="btn btn-ghost btn-sm" href="#/pitches?sport=padel">See all ${icon('arrowRight', 16)}</a></div>
        <div class="scroller courts-scroller" style="--col:clamp(250px, 30vw, 330px)">${courts.slice(0, 6).map((p) => cards.pitch(p)).join('')}</div>
      </section>` : ''}

      <section class="container section-tight">
        <div class="sec-head"><div><h2 class="h2">${icon('users', 24)} Games that need you</h2><p>Join in two taps and pay your share</p></div>
          <a class="btn btn-ghost btn-sm" href="#/players">See all ${icon('arrowRight', 16)}</a></div>
        <div class="match-grid">${matches.filter((m) => m.spotsLeft > 0).slice(0, 4).map((m) => cards.match(m)).join('')}</div>
        <a class="post-match-cta" href="#/players/new"><span class="pmc-ico">${icon('plus', 20)}</span><span class="grow"><b>Short on players?</b><span class="xs">Post your match and players nearby join</span></span>${icon('chevronRight', 18)}</a>
      </section>

      <section class="container section-tight">
        <div class="fantasy-band" data-spotlight>
          <div class="fb-copy">
            <span class="badge badge-glass">${icon('trophy', 13, 2)} ${esc(meta.leagueLabel || 'Premier League')} ${meta.season}</span>
            <h2 class="h1" style="color:#fff;margin-top:14px">Gameweek ${meta.gw} locks in</h2>
            <div style="margin-top:14px">${c.countdown(meta.deadline)}</div>
            <div class="row wrap" style="margin-top:20px">${c.cta(team ? 'Manage my team' : 'Create my team', { href: team ? '#/fantasy' : '#/fantasy/squad', variant: 'blaze' })}<a class="btn btn-ghost" style="color:#fff" href="#/fantasy/leagues">Free leagues</a></div>
          </div>
          <div class="fb-side">
            <h3 class="small" style="color:rgba(255,255,255,.7);font-weight:600">In form this week</h3>
            <ul class="fb-players">${topPlayers.map((p) => `<li>${c.kit(S.fantasy.teamById[p.teamId], 34, p.pos === 'GK')}<span class="grow"><b>${esc(p.name)}</b><span class="xs">${S.fantasy.teamById[p.teamId].name} · ${p.pos} · ${fantasyPrice(p.price)}</span></span><span class="fb-form num">${p.form.toFixed(1)}</span></li>`).join('')}</ul>
            <div class="fb-leagues">${leagues.slice(0, 2).map((l) => `<a href="#/fantasy/league/${l.id}" class="fb-league"><span><b>${esc(l.name)}</b><span class="xs">Free · ${l.participants.toLocaleString()} managers</span></span><span class="fb-prize">${l.kind === 'official' ? `${S.fantasy.weeklyPoints()[0].toLocaleString()} pts / GW` : money(l.prizeValue || 0)}</span></a>`).join('')}</div>
          </div>
        </div>
      </section>

      <section class="section matchday-band">
        <div class="container">
          <div class="sec-head"><div><h2 class="h2" style="color:#fff">Live & next up</h2><p style="color:rgba(255,255,255,.65)">Every kick-off in Cairo time</p></div>
            <a class="btn btn-sm home-sched-btn" href="#/matches">${icon('calendar', 15)} Schedule</a></div>
          <div class="scroller home-matches" style="--col:clamp(250px, 26vw, 300px)">${spotlight.map((m) => `<a class="home-match ${m.status === 'live' ? 'is-live' : ''}" href="#/matches${m.date === SA.core.utils.isoDate(SA.core.utils.today()) ? '' : '?date=' + m.date}" style="--ca:${m.homeT.a};--cb:${m.awayT.a}">
              <span class="hm-comp xs"><i class="cat-dot" style="background:${m.comp.color}"></i>${esc(m.comp.name)}</span>
              <span class="hm-teams"><span>${SA.ui.sports.crest(m.homeT, 40)}<b>${esc(m.homeT.short)}</b></span>
                <span class="hm-mid">${m.status === 'live' ? `<b class="num">${m.hs} – ${m.as}</b><em><span class="live-dot"></span>${m.label}</em>` : `<b class="num">${m.label}</b><em>${SA.core.utils.relDay(m.date)}</em>`}</span>
                <span>${SA.ui.sports.crest(m.awayT, 40)}<b>${esc(m.awayT.short)}</b></span></span>
              <span class="hm-names xs">${esc(m.homeT.name)} vs ${esc(m.awayT.name)}</span></a>`).join('')}</div>
        </div>
      </section>

      ${(() => { const tours = SA.services.db.all('tournaments').filter((t) => (SA.config.tournamentsSoon ? ['open', 'full'] : ['open', 'full', 'running']).includes(t.status)).map((t) => S.tournaments.getSync(t.id)).slice(0, 3); return tours.length ? `<section class="container section-tight home-tours">
        <div class="sec-head"><div><h2 class="h2">${icon('medal', 24)} Cups & leagues${SA.config.tournamentsSoon ? ' <span class="badge badge-blaze">Coming soon</span>' : ''}</h2><p>Tournaments at pitches near you</p></div><a class="btn btn-ghost btn-sm" href="#/tournaments">See all ${icon('arrowRight', 16)}</a></div>
        <div class="scroller tour-scroller" style="--col:clamp(270px, 32vw, 380px)">${tours.map((t, i) => SA.ui.tourCard(t, i)).join('')}</div></section>` : ''; })()}

      ${SA.config.storeEnabled ? `<section class="container section-tight">
        <div class="sec-head"><div><h2 class="h2">${icon('bag', 24)} Popular in the shop</h2><p>Original gear, delivered across Egypt</p></div><a class="btn btn-ghost btn-sm" href="#/shop">Shop all ${icon('arrowRight', 16)}</a></div>
        <div class="product-grid">${products.filter((p) => !p.service).slice(0, 8).map((p) => cards.product(p)).join('')}</div>
      </section>` : '<!-- store paused -->'}

      <section class="container section-tight desk-only">
        <div class="stats-strip reveal">
          <div><b class="num" data-countup="420">0</b><span>partner pitches</span></div>
          <div><b class="num" data-countup="18500">0</b><span>games filled last month</span></div>
          ${SA.config.fantasyEnabled ? '<div><b class="num" data-countup="184320">0</b><span>fantasy managers</span></div>' : '<div><b class="num" data-countup="62000">0</b><span>players across Egypt</span></div>'}
          <div><b class="num" data-countup="98.4" data-decimals="1">0</b><span>% bookings confirmed instantly</span></div>
        </div>
      </section>`;
    },
    mount(el) {
      const offs = [];
      offs.push(SA.core.motion.tilt(el.querySelector('[data-tilt]'), { max: 6 }));
      offs.push(SA.ui.c.tickCountdowns(el));
      // Football / padel switch: flips the 3D stage, the cards, the main button and the 3D ball
      const hero = el.querySelector('[data-hero]');
      const setSport = (k) => {
        hero.dataset.sport = k; sessionStorage.setItem('superapp.heroSport', k);
        hero.querySelectorAll('[data-sport]').forEach((b) => b.setAttribute('aria-pressed', String(b.dataset.sport === k)));
        SA.ui.ball3d.setKind(k);
      };
      hero.querySelectorAll('.sport-switch-btn').forEach((b) => b.addEventListener('click', () => setSport(b.dataset.sport)));
      SA.ui.ball3d.setKind(hero.dataset.sport);
      // Spotlight follows the pointer across the fantasy band
      const band = el.querySelector('[data-spotlight]');
      if (band) band.addEventListener('pointermove', (e) => { const r = band.getBoundingClientRect(); band.style.setProperty('--sx', `${e.clientX - r.left}px`); band.style.setProperty('--sy', `${e.clientY - r.top}px`); });
      el.querySelector('[data-locate]')?.addEventListener('click', async (e) => {
        e.currentTarget.innerHTML = '<span class="spinner"></span> Locating…';
        const loc = await S.location.requestGPS();
        SA.ui.overlay.toast(loc.source === 'gps' ? 'Showing pitches closest to you' : 'Location unavailable — showing pitches near Downtown Cairo', { type: loc.source === 'gps' ? 'success' : 'info' });
        SA.app.rerender();
      });
      // 3D ball: lands on the pitch, then jumps section to section as you scroll
      offs.push(SA.ui.ball3d.mount(el, [
        { sel: '.hero-stage', x: 0.5, at: 'center', y: 0.55 },
        { sel: '.quick-nav', x: 0.86 },
        { sel: '.scroller', x: 0.5 },
        { sel: '.courts-scroller', x: 0.2 },
        { sel: '.match-grid', x: 0.5 },
        { sel: '.fantasy-band', x: 0.3 },
        { sel: '.home-matches', x: 0.72 },
        { sel: '.product-grid', x: 0.16 },
        { sel: '.stats-strip', x: 0.82 },
      ]));
      return () => offs.forEach((f) => f && f());
    },
  };
})(window.SA);
