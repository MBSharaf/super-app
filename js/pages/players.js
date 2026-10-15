(function (SA) {
  const { esc, money, fmtHour, relDay, fmtRange, fmtDate, plural } = SA.core.utils;
  const { icon, cards } = SA.ui;
  const c = SA.ui.c;
  const media = SA.data.media;
  const S = SA.services;

  const state = { when: '', level: '', gender: '', gk: false, sort: 'soon' };

  const filters = () => `<div class="filter-row">
    <div class="chips" role="group" aria-label="When">${[['', 'Any day'], ['today', 'Today'], ['tomorrow', 'Tomorrow'], ['week', 'Next 7 days']].map(([v, l]) => `<button class="chip" data-f="when" data-v="${v}" aria-pressed="${state.when === v}">${l}</button>`).join('')}</div>
    <div class="chips" role="group" aria-label="Level">${SA.data.matches.levels.map((l) => `<button class="chip" data-f="level" data-v="${l}" aria-pressed="${state.level === l}">${l}</button>`).join('')}</div>
    <div class="chips" role="group" aria-label="More filters">
      ${['Mixed', 'Women'].map((g) => `<button class="chip" data-f="gender" data-v="${g}" aria-pressed="${state.gender === g}">${g}</button>`).join('')}
      <button class="chip" data-f="gk" data-v="1" aria-pressed="${state.gk}">${icon('glove', 14)} Keeper wanted</button>
    </div></div>`;

  /** Join flow modal (also used from cards anywhere in the app). */
  const openJoin = async (id) => {
    const m = await S.matches.get(id);
    if (m.joined || m.isMine || S.games.myRequest(id)) return SA.core.router.go(`/players/${id}`);
    const rec = S.games.record(S.games.meAsPlayer());
    const hs = S.games.hostSettings(m);
    SA.ui.overlay.open({
      title: m.spotsLeft ? 'Ask to join this game' : 'Join the waitlist', subtitle: `${esc(m.pitch.name)} · ${relDay(m.date)}, ${fmtRange(m.hour, m.duration)}`,
      body: `<div class="join-summary">
          <div class="media join-thumb"><img src="${media.photo(m.pitch.image, 300, 220)}" alt="" /></div>
          <div class="grow"><div class="row wrap" style="--gap:6px"><span class="badge badge-info">${m.level}</span><span class="badge">${m.type}</span><span class="badge">${m.gender}</span></div>
            <div style="margin-top:10px">${c.spots(m.filled, m.total)}</div>
            <p class="small soft" style="margin-top:6px">${m.spotsLeft} of ${m.total} spots left · hosted by ${esc(m.hostName)}</p></div>
        </div>
        ${m.notes ? `<blockquote class="host-note">“${esc(m.notes)}”</blockquote>` : ''}
        <form id="join-form" class="stack" style="--gap:14px;margin-top:16px">
          <div class="form-grid"><div class="field"><label for="jn-pos">Your position</label><select class="select" id="jn-pos" name="position">${S.games.POSITIONS.map((p) => `<option ${p === (m.needsGK ? 'Goalkeeper' : S.user.me().position) ? 'selected' : ''}>${p}</option>`).join('')}</select></div>
            <div class="field"><label for="jn-msg">Message to the host <span class="muted">(optional)</span></label><input class="input" id="jn-msg" name="message" maxlength="200" placeholder="e.g. I can be there 15 minutes early" /></div></div>
          <div class="notice ${hs.autoAccept ? 'turf' : ''}">${icon(hs.autoAccept ? 'bolt' : 'shield', 18)}<span>${hs.autoAccept ? '<b>Instant:</b> this host accepts automatically — you’re in as soon as you pay.' : `<b>${esc(m.hostName)} decides.</b> Your share is held by Super App and comes back in full if you’re not accepted.`} The host sees your record: ${rec.games} games${rec.attendance != null ? `, ${Math.round(rec.attendance * 100)}% attendance` : ''}${rec.rating ? `, ${rec.rating.toFixed(1)}★` : ''}.</span></div>
          ${SA.ui.payment.render({ amount: m.pricePerPlayer, allowCash: false, name: 'joinpay' })}
          <label class="row small" style="--gap:10px;align-items:flex-start"><input type="checkbox" required name="agree" style="margin-top:3px" /> <span>I’ll arrive 10 minutes early and tell the host if I can’t make it.</span></label>
        </form>`,
      footer: `<div class="grow"><b class="num">${money(m.pricePerPlayer)}</b><span class="xs muted" style="display:block">your share of the pitch</span></div><button class="btn btn-blaze" form="join-form" type="submit">${m.spotsLeft ? "Pay &amp; ask to join" : "Pay &amp; join waitlist"}</button>`,
      onMount(el, close) {
        const form = el.querySelector('#join-form');
        SA.ui.payment.bind(form, { name: 'joinpay' });
        form.addEventListener('submit', async (e) => {
          e.preventDefault();
          if (!form.agree.checked) { SA.ui.overlay.toast('Tick the box to confirm you’ll show up on time.', { type: 'error' }); return; }
          const payment = SA.ui.payment.read(form, { name: 'joinpay' });
          try {
            const r = await SA.ui.payment.process(() => S.games.request(id, { position: form.position.value, message: form.message.value, payment }), { root: form, label: 'Sending your request', methodHint: payment.method });
            close();
            if (r?.status === 'accepted') { SA.core.motion.confetti(); SA.ui.overlay.toast(`You’re in! See you ${relDay(m.date).toLowerCase()} at ${fmtHour(m.hour)}.`, { action: 'View game', onAction: () => SA.core.router.go(`/players/${id}`) }); }
            else SA.ui.overlay.toast(r?.status === 'waitlist' ? 'You’re on the waitlist — we’ll tell you if a spot opens' : 'Request sent — the host will accept or decline', { type: 'info', action: 'View game', onAction: () => SA.core.router.go(`/players/${id}`) });
            SA.app.rerender();
          } catch { /* shown by process() */ }
        });
      },
    });
  };

  SA.pages.players = {
    title: 'Find players — Super App',
    openJoin,
    async render() {
      const mine = await S.matches.mine();
      const upcoming = S.bookings.upcomingSync().filter((b) => !b.publishedMatchId);
      return `
      ${c.pageHead({ crumbs: [['Home', '#/'], ['Find players']], title: 'Find players', text: 'Join a game that’s short on players, or post your own booking and fill the empty spots.', actions: c.cta('Post your match', { href: '#/players/new', ic: 'plus' }) })}
      <div class="container">
        <div class="flow-strip compact">
          ${[['pitch', 'Book a pitch'], ['users', 'Missing players'], ['share', 'Post the match'], ['search', 'Players discover it'], ['check', 'They join']].map(([ic, t], i) => `<div class="flow-step"><span class="flow-n">${icon(ic, 16)}</span><span>${t}</span></div>`).join(`<span class="flow-arrow">${icon('arrowRight', 16)}</span>`)}
        </div>
        <div class="players-layout">
          <div>
            <div class="filters-slot">${filters()}</div>
            <div class="row-between" style="margin:16px 0 12px"><p class="small muted results-count" aria-live="polite"></p>
              <label class="sort-select">${icon('sort', 16)}<span class="sr-only">Sort</span><select class="select" id="match-sort">${[['soon', 'Starting soonest'], ['distance', 'Nearest'], ['spots', 'Fewest spots left']].map(([v, l]) => `<option value="${v}" ${state.sort === v ? 'selected' : ''}>${l}</option>`).join('')}</select></label></div>
            <div class="match-list stack" style="--gap:12px">${c.skeletonCards(3, 140)}</div>
          </div>
          <aside class="players-side stack" style="--gap:16px">
            <div class="panel panel-pad host-cta">
              <h2 class="h4">Booked a pitch but short on players?</h2>
              <p class="small soft" style="margin-top:6px">Post it here. Players near the pitch get notified and can join and pay their share.</p>
              ${upcoming.length ? `<ul class="mini-bookings">${upcoming.slice(0, 3).map((b) => `<li><a href="#/players/new?booking=${b.id}"><span class="grow"><b class="small">${esc(b.pitchName)}</b><span class="xs muted" style="display:block">${relDay(b.date)} · ${fmtHour(b.hour)}</span></span><span class="link link-blaze small">Post ${icon('arrowRight', 14)}</span></a></li>`).join('')}</ul>`
                : `<a class="btn btn-primary btn-block" style="margin-top:14px" href="#/pitches">${S.bookings.upcomingSync().length ? "Book another pitch" : "Book a pitch first"}</a>`}
            </div>
            <div class="panel panel-pad">
              <h2 class="h4">Your matches</h2>
              ${mine.joined.length || mine.hosting.length ? `<ul class="mini-bookings">${[...mine.hosting, ...mine.joined].map((m) => `<li><a href="#/players/${m.id}"><span class="grow"><b class="small">${esc(m.pitch.name)}</b><span class="xs muted" style="display:block">${relDay(m.date)} · ${fmtHour(m.hour)} · ${m.filled}/${m.total}</span></span><span class="badge ${m.isMine ? 'badge-ink' : 'badge-turf'}">${m.isMine ? 'Host' : 'Joined'}</span></a></li>`).join('')}</ul>`
                : `<p class="small muted" style="margin-top:6px">Matches you join or host will show up here.</p>`}
            </div>
          </aside>
        </div>
      </div>`;
    },
    mount(el) {
      const list = el.querySelector('.match-list');
      const load = async () => {
        const items = await S.matches.list(state);
        el.querySelector('.results-count').textContent = `${plural(items.length, 'open match', 'open matches')}`;
        list.innerHTML = items.length ? items.map((m) => cards.match(m)).join('') : c.empty({ icon: 'users', title: 'No open matches for these filters', text: 'Try another day or level — or post your own match and let players come to you.', action: '<a class="btn btn-primary" href="#/players/new">Post your match</a>' });
        SA.core.motion.enhance(list);
      };
      el.addEventListener('click', (e) => {
        const f = e.target.closest('[data-f]');
        if (!f) return;
        const k = f.dataset.f;
        if (k === 'gk') state.gk = !state.gk;
        else state[k] = state[k] === f.dataset.v ? '' : f.dataset.v;
        el.querySelector('.filters-slot').innerHTML = filters();
        load();
      });
      el.querySelector('#match-sort').addEventListener('change', (e) => { state.sort = e.target.value; load(); });
      load();
    },
  };
})(window.SA);
