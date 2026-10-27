(function (SA) {
  const { esc, money, fmtKm, fmtHour, relDay, fantasyPrice } = SA.core.utils;
  const { icon, cards } = SA.ui;
  const c = SA.ui.c;
  const media = SA.data.media;
  const S = SA.services;

  /** The signature hero visual: a 3D floodlit pitch with live cards orbiting it. */
  const heroStage = (pitch, match, product, meta) => `
    <div class="hero-stage" data-tilt aria-hidden="true">
      <div class="pitch3d">
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
      </div>
      ${pitch ? `<div class="float-card fc-pitch">
        <img src="${media.photo(pitch.images[0], 160, 160)}" alt="" />
        <div><b>${esc(pitch.name)}</b><span>${fmtKm(pitch.distanceKm)} · ${pitch.freeSlots} slots today</span></div>
        <span class="fc-price">${money(pitch.price)}</span>
      </div>` : ''}
      ${match ? `<div class="float-card fc-match">
        <div class="row-between"><span class="badge badge-blaze"><span class="live-dot"></span>${match.spotsLeft} spots open</span><span class="xs muted">${relDay(match.date)}, ${fmtHour(match.hour)}</span></div>
        <b>${esc(match.pitch.name)}</b>
        ${c.spots(match.filled, match.total)}
      </div>` : ''}
      <div class="float-card fc-fantasy">
        <span class="xs">Gameweek ${meta.gw} deadline</span>
        ${c.countdown(meta.deadline, { compact: true })}
      </div>
      ${product ? `<div class="float-card fc-product">
        <img src="${media.u(product.images[0], 200, 200)}" alt="" />
        <div><span class="xs muted">${esc(product.brand)}</span><b>${esc(product.name.split(' ').slice(0, 2).join(' '))}</b><span class="price small">${money(product.price)}</span></div>
      </div>` : ''}
    </div>`;

  const services = [
    { key: 'pitches', title: 'Book a pitch', text: 'Live availability for 5, 7 and 11-a-side pitches near you. Pay by Visa or wallet.', href: '#/pitches', icon: 'pitch', img: 'litField', cta: 'See pitches near me' },
    { key: 'players', title: 'Find players', text: 'Short on numbers? Post your booking and players nearby join in minutes.', href: '#/players', icon: 'users', img: 'menPlaying', cta: 'Browse open games' },
    { key: 'fantasy', title: 'Play fantasy', text: 'Egyptian Premier League fantasy with private groups and paid prize leagues.', href: '#/fantasy', icon: 'trophy', img: 'stadiumNight', cta: 'Pick your team' },
    { key: 'shop', title: 'Shop gear', text: 'Boots, balls, kits and padel — original brands, delivered across Egypt.', href: '#/shop', icon: 'bag', img: 'goalTrees', productImg: 'photo-1511886929837-354d827aae26', cta: 'Shop now' },
  ].filter((x) => x.key !== 'shop' || SA.config.storeEnabled);

  SA.pages.home = {
    title: 'Super App — Book, play, compete',
    async render() {
      const [nearby, matches, products] = await Promise.all([S.pitches.list({ sort: 'distance' }), S.matches.list({ sort: 'soon' }), SA.config.storeEnabled ? S.shop.list({ sort: 'featured' }) : Promise.resolve([])]);  // the store is paused: its API answers 423
      const meta = S.fantasy.meta();
      const loc = S.location.current();
      const user = S.user.me();
      const upcoming = S.bookings.upcomingSync()[0];
      const topPlayers = S.fantasy.playersSync().slice().sort((a, b) => b.form - a.form).slice(0, 5);
      const team = S.fantasy.team();
      const heroPitch = nearby.find((p) => p.freeSlots > 0) || nearby[0];
      const heroMatch = matches.find((m) => m.spotsLeft > 0 && !m.joined) || matches[0];
      const heroProduct = products.find((p) => p.discount > 0) || products[0];
      const tonight = matches.filter((m) => m.spotsLeft > 0).length;
      const leagues = S.fantasy.officialLeagues().filter((l) => l.kind === 'pass');
      const spotlight = S.schedule.spotlight(8);
      const newsList = await S.news.list();
      // Live activity ticker, built from real data in the app
      const people = SA.data.matches.people;
      const tickerItems = [
        ...matches.filter((m) => m.spotsLeft > 0).slice(0, 4).map((m, i) => `<span><i></i><b>${esc(people[(i * 7 + 3) % people.length].split(' ')[0])}</b> joined a ${m.type} at ${esc(m.pitch.name)} — ${m.spotsLeft} spot${m.spotsLeft > 1 ? 's' : ''} left</span>`),
        ...nearby.filter((p) => p.freeSlots > 0).slice(0, 3).map((p) => `<span class="t-turf"><i></i><b>${esc(p.name)}</b> has ${p.freeSlots} free slots today from ${money(p.price)}</span>`),
        ...topPlayers.slice(0, 2).map((p) => `<span class="t-gold"><i></i><b>${esc(p.name)}</b> is the most transferred-in player this week</span>`),
        `<span class="t-gold"><i></i><b>${leagues[0]?.participants.toLocaleString()}</b> managers are playing for ${esc(leagues[0]?.prizes?.[0]?.[1] || '')} in ${esc(leagues[0]?.name || '')}</span>`,
        ...products.filter((p) => p.discount && SA.config.storeEnabled).slice(0, 2).map((p) => `<span><i></i><b>${esc(p.name)}</b> is ${p.discount}% off today</span>`),
      ];
      const ticker = tickerItems.join('');

      return `
      <section class="hero container">
        <div class="hero-copy">
          <span class="badge badge-blaze hero-live"><span class="live-dot"></span>${tonight} games looking for players this week</span>
          <h1 class="display hero-title"><span>Book the pitch.</span><span>Fill the squad.</span><span>Win the <span class="rotator">${['week.', 'derby.', 'league.'].map((w, i) => `<span class="${i === 0 ? 'is-on' : ''}" ${i ? 'aria-hidden="true"' : ''}>${w}</span>`).join('')}</span></span></h1>
          <p class="lead">${SA.config.storeEnabled ? 'Super App finds open pitches near you, fills the empty spots in your match, runs your Egyptian Premier League fantasy team and delivers your gear — with one account and one wallet.' : 'Super App finds open pitches near you, fills the empty spots in your match and runs your Egyptian Premier League fantasy team — with one account and one wallet.'}</p>
          <div class="row wrap hero-ctas">
            ${c.cta('Book a pitch near me', { href: '#/pitches?locate=1', size: 'lg' })}
            <a class="btn btn-outline btn-lg" href="#/players">Find a game</a>
          </div>
          <div class="hero-proof row">
            ${c.avatarStack([0, 3, 9, 5].map((a) => ({ avatar: a, name: 'x' })), 4)}
            <div><span class="row" style="--gap:6px">${c.stars(5, 13)}<b class="small">4.8</b></span><span class="xs muted">62,000+ players across Cairo, Giza and Alexandria</span></div>
          </div>
        </div>
        ${heroStage(heroPitch, heroMatch, heroProduct, meta)}
      </section>

      <section class="container">
        <nav class="quick-nav reveal" aria-label="Everything in Super App">
          ${['pitches', 'players', 'tournaments', 'community', 'matches', 'news', 'fantasy', 'shop'].filter((k) => k !== 'shop' || SA.config.storeEnabled).map((k) => { const n = SA.ui.shell.LINKS[k]; const live = k === 'matches' ? SA.ui.shell.liveCount() : 0; return `<a href="${n.href}" class="quick-nav-item"><span class="quick-nav-ico">${icon(n.icon, 22)}${live ? '<span class="quick-nav-dot"></span>' : ''}</span><span>${n.label}</span></a>`; }).join('')}
        </nav>
      </section>

      <section class="container">
        <div class="ticker" aria-label="Live activity across Super App">
          <span class="ticker-label"><span class="live-dot" style="width:8px;height:8px;border-radius:50%;background:#fff;display:inline-block"></span>LIVE</span>
          <div class="ticker-track" aria-hidden="true">${ticker}${ticker}</div>
        </div>
      </section>

      <section class="container section-tight">
        <div class="service-grid">
          ${services.map((s, i) => `<a class="service-tile reveal" style="--d:${i * 70}ms" href="${s.href}">
            <div class="media service-media"><img src="${s.productImg ? media.u(s.productImg, 700, 500) : media.photo(s.img, 700, 500)}" alt="" loading="lazy" /></div>
            <div class="service-body">
              <span class="service-ico">${icon(s.icon, 20)}</span>
              <h2 class="h3">${s.title}</h2>
              <p class="small soft">${s.text}</p>
              <span class="link">${s.cta} ${icon('arrowRight', 16)}</span>
            </div></a>`).join('')}
        </div>
      </section>

      ${upcoming ? `<section class="container"><div class="next-up reveal">
        <div class="media next-up-media"><img src="${media.photo(upcoming.image, 300, 300)}" alt="" /></div>
        <div class="grow"><span class="xs muted">Your next game</span><h2 class="h4">${esc(upcoming.pitchName)} · ${relDay(upcoming.date)} at ${fmtHour(upcoming.hour)}</h2>
          <p class="small soft">${upcoming.publishedMatchId ? 'Listed on Find Players — players can still join.' : `Booked for ${upcoming.players} players. Missing some? Post it and let players nearby join.`}</p></div>
        <div class="row wrap">${upcoming.publishedMatchId ? `<a class="btn btn-outline" href="#/players/${upcoming.publishedMatchId}">View listing</a>` : `<a class="btn btn-primary" href="#/players/new?booking=${upcoming.id}">${icon('users', 16)} Find players</a>`}<a class="btn btn-ghost" href="#/booking/${upcoming.id}">Booking details</a></div>
      </div></section>` : ''}

      <section class="container section">
        <div class="sec-head"><div><h2 class="h2">Pitches closest to you</h2><p>${loc ? `Sorted by distance from ${esc(loc.label)}.` : 'Showing pitches near Downtown Cairo. Share your location for exact distances.'}</p></div>
          <div class="row">${loc?.source === 'gps' ? '' : `<button class="btn btn-outline btn-sm" data-locate>${icon('locate', 16)} Use my location</button>`}<a class="btn btn-ghost btn-sm" href="#/pitches">All pitches ${icon('arrowRight', 16)}</a></div></div>
        <div class="scroller" style="--col:clamp(260px, 30vw, 330px)">${nearby.slice(0, 6).map((p) => cards.pitch(p)).join('')}</div>
      </section>

      ${(() => { const tours = SA.services.db.all('tournaments').filter((t) => ['open', 'full', 'running'].includes(t.status)).map((t) => S.tournaments.getSync(t.id)).slice(0, 3); return tours.length ? `<section class="container section-tight home-tours">
        <div class="sec-head"><div><h2 class="h2">${icon('trophy', 26)} Cups & leagues</h2><p>Register your team for a tournament at a pitch near you — or follow the tables.</p></div><a class="btn btn-ghost btn-sm" href="#/tournaments">All tournaments ${icon('arrowRight', 16)}</a></div>
        <div class="tour-grid">${tours.map((t, i) => SA.ui.tourCard(t, i)).join('')}</div></section>` : ''; })()}

      <section class="section players-band" style="position:relative;overflow:hidden">
        <span class="watermark" data-parallax="-0.4" aria-hidden="true" style="top:10px;left:-4%">Missing players? Missing players?</span>
        <div class="container" style="position:relative">
          <div class="flow-strip" data-lightup>
            ${['You book a pitch', 'You’re missing players', 'You post the match', 'Players nearby join'].map((t, i) => `<div class="flow-step"><span class="flow-n">${i + 1}</span><span>${t}</span></div>`).join(`<span class="flow-arrow">${icon('arrowRight', 18)}</span>`)}
          </div>
          <div class="sec-head" style="margin-top:36px"><div><h2 class="h2">Games that need you</h2><p>Open spots in matches near you. Join in two taps and pay your share.</p></div>
            <div class="row"><a class="btn btn-outline btn-sm" href="#/players/new">${icon('plus', 16)} Post your match</a><a class="btn btn-ghost btn-sm" href="#/players">See all</a></div></div>
          <div class="match-grid">${matches.filter((m) => m.spotsLeft > 0).slice(0, 4).map((m) => cards.match(m)).join('')}</div>
        </div>
      </section>

      <section class="container section">
        <div class="fantasy-band" data-spotlight>
          <div class="fb-copy">
            <span class="badge badge-glass">${icon('trophy', 13, 2)} Egyptian Premier League ${meta.season}</span>
            <h2 class="h1" style="color:#fff;margin-top:16px">Gameweek ${meta.gw} locks in</h2>
            <div style="margin-top:16px">${c.countdown(meta.deadline)}</div>
            <p style="color:rgba(255,255,255,.75);margin-top:18px;max-width:46ch">${team ? `${esc(team.name)} is set. Check your captain and transfers before the deadline.` : 'Pick 15 players with 100M, choose a captain and take on your friends — or enter a paid league with real prizes.'}</p>
            <div class="row wrap" style="margin-top:22px">${c.cta(team ? 'Manage my team' : 'Create my team', { href: team ? '#/fantasy' : '#/fantasy/squad', variant: 'blaze' })}<a class="btn btn-ghost" style="color:#fff" href="#/fantasy/leagues">Prize leagues</a></div>
          </div>
          <div class="fb-side">
            <h3 class="small" style="color:rgba(255,255,255,.7);font-weight:600">In form this week</h3>
            <ul class="fb-players">${topPlayers.map((p) => `<li>${c.kit(S.fantasy.teamById[p.teamId], 34, p.pos === 'GK')}<span class="grow"><b>${esc(p.name)}</b><span class="xs">${S.fantasy.teamById[p.teamId].name} · ${p.pos} · ${fantasyPrice(p.price)}</span></span><span class="fb-form num">${p.form.toFixed(1)}</span></li>`).join('')}</ul>
            <div class="fb-leagues">${leagues.slice(0, 2).map((l) => `<a href="#/fantasy/league/${l.id}" class="fb-league"><span><b>${esc(l.name)}</b><span class="xs">Fantasy Pass · ${l.participants.toLocaleString()} managers</span></span><span class="fb-prize">${money(l.prizeValue || 0)}</span></a>`).join('')}</div>
          </div>
        </div>
      </section>

      <section class="section matchday-band">
        <div class="container">
          <div class="sec-head"><div><h2 class="h2" style="color:#fff">Live & next up</h2><p style="color:rgba(255,255,255,.65)">Egyptian league, CAF, the Champions League and Europe’s big five — in Cairo time.</p></div>
            <a class="btn btn-sm home-sched-btn" href="#/matches">${icon('calendar', 15)} Full schedule</a></div>
          <div class="scroller home-matches" style="--col:clamp(250px, 26vw, 300px)">${spotlight.map((m) => `<a class="home-match ${m.status === 'live' ? 'is-live' : ''}" href="#/matches${m.date === SA.core.utils.isoDate(SA.core.utils.today()) ? '' : '?date=' + m.date}" style="--ca:${m.homeT.a};--cb:${m.awayT.a}">
              <span class="hm-comp xs"><i class="cat-dot" style="background:${m.comp.color}"></i>${esc(m.comp.name)}</span>
              <span class="hm-teams"><span>${SA.ui.sports.crest(m.homeT, 40)}<b>${esc(m.homeT.short)}</b></span>
                <span class="hm-mid">${m.status === 'live' ? `<b class="num">${m.hs} – ${m.as}</b><em><span class="live-dot"></span>${m.label}</em>` : `<b class="num">${m.label}</b><em>${SA.core.utils.relDay(m.date)}</em>`}</span>
                <span>${SA.ui.sports.crest(m.awayT, 40)}<b>${esc(m.awayT.short)}</b></span></span>
              <span class="hm-names xs">${esc(m.homeT.name)} vs ${esc(m.awayT.name)}</span></a>`).join('')}</div>
        </div>
      </section>

      <section class="container section">
        <div class="sec-head"><div><h2 class="h2">Football news</h2><p>The latest on Al Ahly, Zamalek, the Pharaohs and the whole Egyptian game.</p></div><a class="btn btn-ghost btn-sm" href="#/news">All news ${icon('arrowRight', 16)}</a></div>
        <div class="home-news">${SA.ui.sports.newsCard(newsList[0], { size: 'lg' })}<div class="home-news-side">${newsList.slice(1, 4).map((s) => SA.ui.sports.newsCard(s, { size: 'sm' })).join('')}</div></div>
      </section>

      <section class="container section-tight">
        <div class="promo-grid">${S.content.banners().map((b, i) => `<a class="promo promo-${'abc'[i]} reveal" ${i ? `style="--d:${i * 80}ms"` : ''} href="${esc(b.link || '#/')}"><img src="${media.photo(b.image, 900, 700)}" alt="" loading="lazy" /><div>${b.badge ? `<span class="badge ${i ? 'badge-glass' : 'badge-blaze'}">${esc(b.badge)}</span>` : ''}<h3 class="${i ? 'h3' : 'h2'}">${esc(b.title)}</h3>${!i && b.cta ? `<span class="link" style="color:#fff">${esc(b.cta)} ${icon('arrowRight', 16)}</span>` : ''}</div></a>`).join('')}</div>
      </section>

      ${SA.config.storeEnabled ? '' : '<!-- store paused -->'}<section class="container section" ${SA.config.storeEnabled ? '' : 'hidden'}>
        <div class="sec-head"><div><h2 class="h2">Popular in the shop</h2><p>Gear players in your area are buying this week.</p></div><a class="btn btn-ghost btn-sm" href="#/shop">All products ${icon('arrowRight', 16)}</a></div>
        <div class="product-grid">${products.filter((p) => !p.service).slice(0, 8).map((p) => cards.product(p)).join('')}</div>
      </section>

      <section class="container section-tight">
        <div class="stats-strip reveal">
          <div><b class="num" data-countup="420">0</b><span>partner pitches</span></div>
          <div><b class="num" data-countup="18500">0</b><span>games filled last month</span></div>
          <div><b class="num" data-countup="184320">0</b><span>fantasy managers</span></div>
          <div><b class="num" data-countup="98.4" data-decimals="1">0</b><span>% bookings confirmed instantly</span></div>
        </div>
      </section>`;
    },
    mount(el) {
      const offs = [];
      offs.push(SA.core.motion.tilt(el.querySelector('[data-tilt]'), { max: 6 }));
      offs.push(SA.ui.c.tickCountdowns(el));
      // Rotating last word of the headline
      const words = [...el.querySelectorAll('.rotator > span')];
      let wi = 0;
      if (SA.core.motion.enabled() && words.length) {
        const t = setInterval(() => {
          const cur = words[wi]; wi = (wi + 1) % words.length; const nxt = words[wi];
          cur.classList.remove('is-on'); cur.classList.add('is-out');
          nxt.classList.remove('is-out'); nxt.classList.add('is-on');
          setTimeout(() => cur.classList.remove('is-out'), 800);
        }, 2600);
        offs.push(() => clearInterval(t));
      }
      // "How it works" steps light up in sequence once visible
      const strip = el.querySelector('[data-lightup]');
      if (strip) {
        const o = new IntersectionObserver(([e]) => { if (e.isIntersecting) { strip.classList.add('is-lit'); o.disconnect(); } }, { threshold: 0.4 });
        o.observe(strip); offs.push(() => o.disconnect());
      }
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
        { sel: '.pitch3d-anchor', x: 0.5, at: 'center', y: 0.5 },
        { sel: '.ticker', x: 0.86 },
        { sel: '.service-grid', x: 0.14 },
        { sel: '.scroller', x: 0.5 },
        { sel: '.flow-strip', x: 0.22 },
        { sel: '.match-grid', x: 0.5 },
        { sel: '.fantasy-band', x: 0.3 },
        { sel: '.home-matches', x: 0.72 },
        { sel: '.home-news', x: 0.2 },
        { sel: '.promo-grid', x: 0.76 },
        { sel: '.product-grid', x: 0.16 },
        { sel: '.stats-strip', x: 0.82 },
      ]));
      return () => offs.forEach((f) => f && f());
    },
  };
})(window.SA);
