/**
 * Rich cards for Super App content inside community posts: free slots (live: "Book now" until someone books it),
 * games looking for players (ask to join), and any Super App link people paste — pitch, product or design, seller store,
 * tournament, community post. Outside links are blocked by the community filter, so every card here is ours.
 *   SA.ui.linkCards.slot(card) · SA.ui.linkCards.game(card) · SA.ui.linkCards.fromText(text)
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
    /** Cards for Super App links written in a post or comment */
    fromText(text) {
      return (S.community?.linksIn(text) || []).map((l) => {
        try {
          if (l.type === 'slot') { const q = new URLSearchParams(l.extra); return q.get('date') && q.get('hour') ? cards.slot({ pitchId: l.id, date: q.get('date'), hour: +q.get('hour'), duration: +q.get('dur') || 1 }) : ''; }
          if (l.type === 'game') return cards.game({ matchId: l.id });
          if (l.type === 'pitch') { const p = S.pitches.getSync(l.id); return p ? shell(l.hash, media.photo(p.images[0], 360, 240), `<span class="badge">${icon('pitch', 11)} Pitch</span><b>${esc(p.name)}</b><span class="xs muted">${esc(p.area)}, ${esc(p.city)} · ${esc(p.format)}</span><span class="lk-when">${icon('star', 13)} ${p.rating || '—'} · from <b class="num">${money(p.price)}</b>/h</span>`) : ''; }
          if (l.type === 'product') { const p = S.shop.getSync(l.id); return p ? shell(l.hash, media.u(p.images[0], 300, 300), `<span class="badge">${icon(p.pod ? 'shirt' : 'bag', 11)} ${p.pod ? 'Design' : 'Product'}</span><b>${esc(p.name)}</b><span class="xs muted">${esc(p.brand || '')}</span><span class="lk-when"><b class="num">${money(p.price)}</b></span>`) : ''; }
          if (l.type === 'store') { const s = S.sellers.byHandle(l.id); return s && s.status === 'active' ? shell(l.hash, s.cover ? media.photo(s.cover, 360, 240) : '', `<span class="badge">${icon('bag', 11)} Store</span><b>${esc(s.name)}</b><span class="xs muted">@${esc(s.handle)} · ${esc(S.sellers.levelOf(s).name)} seller</span>`) : ''; }
          if (l.type === 'tournament') { const t = S.tournaments.getSync(l.id); return t ? shell(l.hash, '', `<span class="badge">${icon('trophy', 11)} Tournament</span><b>${esc(t.name)}</b><span class="xs muted">${esc(t.format)} · ${t.teams.length}/${t.maxTeams || '—'} teams · entry ${money(t.fee || 0)}</span>`) : ''; }
          if (l.type === 'post' || l.type === 'community') { const c = S.community.get(l.id); return shell(l.hash, media.photo(c.cover, 360, 240), `<span class="badge">${icon('users', 11)} Community</span><b>${esc(c.name)}</b><span class="xs muted">${c.members.toLocaleString()} members${l.type === 'post' ? ' · open the post' : ''}</span>`); }
        } catch { return ''; }
        return '';
      }).join('');
    },
    /** Links in a text made clickable (Super App links only) */
    linkify(html) { return html.replace(/((?:https?:\/\/[^\s<]+|#\/[^\s<]+))/g, (u) => (S.community?.isInternal(u.replace(/&amp;/g, '&')) ? `<a class="link" href="${S.community.hashOf(u.replace(/&amp;/g, '&'))}">${u.length > 48 ? `${u.slice(0, 46)}…` : u}</a>` : u)); },
  };
  SA.ui.linkCards = cards;
})(window.SA);
