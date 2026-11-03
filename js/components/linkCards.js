/**
 * Rich cards for Super App content: free slots posted by venues (live: "Book now" until someone books it) and games
 * looking for players (ask to join).
 *   SA.ui.linkCards.slot(card) · SA.ui.linkCards.game(card)
 */
(function (SA) {
  const { esc, money, relDay, fmtHour, fmtRange } = SA.core.utils;
  const { icon } = SA.ui;
  const media = SA.data.media;
  const S = SA.services;

  const shell = (href, img, body, extra = '') => `<a class="lk-card" href="${href}">${img ? `<span class="lk-img"><img src="${img}" alt="" loading="lazy" /></span>` : ''}<span class="lk-body">${body}</span>${extra}</a>`;

  /** Live state of a posted free slot */
  const slotState = (c) => {
    const p = S.pitches.getSync(c.pitchId);
    if (!p) return { p, state: 'gone' };
    const start = SA.core.utils.parseISO(c.date).getTime() + c.hour * 36e5;
    if (start < Date.now()) return { p, state: 'expired' };
    const s = (S.pitches.slotsSync(c.pitchId, c.date) || []).find((x) => x.hour === c.hour);
    return { p, state: s?.status === 'available' ? 'free' : 'booked', price: S.pitches.priceAt(p, c.date, c.hour) };
  };

  const cards = {
    slotState,
    slot(c) {
      const { p, state, price } = slotState(c);
      if (!p) return '<div class="lk-card is-off"><span class="lk-body small muted">This pitch is no longer on Super App.</span></div>';
      const tag = state === 'free' ? `<span class="badge badge-turf">${icon('check', 11, 2.6)} Free now</span>` : state === 'booked' ? '<span class="badge badge-danger">Booked</span>' : '<span class="badge">Gone</span>';
      const body = `<span class="row wrap" style="--gap:6px">${tag}${c.pct ? `<span class="badge badge-blaze">${Math.round(c.pct * 100)}% off</span>` : ''}<span class="badge">${esc(p.format)}</span></span>
        <b>${esc(p.name)}</b><span class="xs muted">${esc(p.area)}, ${esc(p.city)}</span>
        <span class="lk-when">${icon('calendar', 14)} <b>${relDay(c.date)}</b> · ${fmtRange(c.hour, c.duration || 1)}${state === 'free' ? ` · <b class="num">${money(price)}</b>` : ''}</span>`;
      const btn = state === 'free' ? `<span class="btn btn-blaze btn-sm lk-cta">${icon('bolt', 14)} Book now</span>` : '';
      return shell(state === 'free' ? `#/book/${p.id}?date=${c.date}&hour=${c.hour}&dur=${c.duration || 1}` : `#/pitches/${p.id}`, media.photo(p.images[0], 360, 240), body, btn).replace('class="lk-card"', `class="lk-card lk-slot is-${state}"`);
    },
    game(c) {
      const m = S.matches.allSync().find((x) => x.id === c.matchId);
      if (!m) return '<div class="lk-card is-off"><span class="lk-body small muted">This game is no longer listed — it filled up or already happened.</span></div>';
      const r = S.games?.myRequest(m.id);
      const status = m.isMine ? '<span class="badge badge-info">Your game</span>' : m.joined ? '<span class="badge badge-turf">You’re in</span>' : r?.status === 'pending' ? '<span class="badge badge-gold">Request sent</span>' : r?.status === 'waitlist' ? '<span class="badge">Waitlist</span>' : m.spotsLeft ? `<span class="badge badge-blaze">${m.spotsLeft} spot${m.spotsLeft > 1 ? 's' : ''} left</span>` : '<span class="badge">Full</span>';
      const body = `<span class="row wrap" style="--gap:6px">${status}<span class="badge">${esc(m.level)}</span><span class="badge">${esc(m.type)}</span>${m.needsGK ? `<span class="badge badge-gold">${icon('glove', 11)} Keeper needed</span>` : ''}</span>
        <b>${esc(m.pitch.name)}</b><span class="xs muted">Hosted by ${esc(m.hostName)} · ${esc(m.pitch.area)}</span>
        <span class="lk-when">${icon('calendar', 14)} <b>${relDay(m.date)}</b> · ${fmtHour(m.hour)} · <b class="num">${money(m.pricePerPlayer)}</b> per player</span>`;
      const cta = !m.isMine && !m.joined && !r && m.spotsLeft ? `<span class="btn btn-primary btn-sm lk-cta">${icon('users', 14)} Ask to join</span>` : '';
      return shell(`#/players/${m.id}`, media.photo(m.pitch.image, 360, 240), body, cta).replace('class="lk-card"', 'class="lk-card lk-game"');
    },
  };
  SA.ui.linkCards = cards;
})(window.SA);
